namespace TallyEnergyBilling.Core;

public sealed class EvSettlementService
{
    public EvSessionSettlement Settle(EvChargingTariff tariff, EvChargingSession session)
    {
        if (session.EndTime < session.StartTime)
            throw new BillingException("Session end time cannot be before start time.");
        if (session.EnergyDeliveredKwh < 0)
            throw new BillingException("Energy delivered cannot be negative.");
        if (session.ChargingDurationMinutes < 0)
            throw new BillingException("Charging duration cannot be negative.");
        if (session.IdleDurationMinutes < 0)
            throw new BillingException("Idle duration cannot be negative.");
        if (tariff.ActivationFeeCents < 0)
            throw new BillingException("Activation fee cannot be negative.");
        if (tariff.EnergyRateCentsPerKwh < 0)
            throw new BillingException("Energy rate cannot be negative.");
        if (tariff.IdleFeeCentsPerMinute < 0)
            throw new BillingException("Idle fee per minute cannot be negative.");
        if (tariff.IdleGracePeriodMinutes < 0)
            throw new BillingException("Idle grace period cannot be negative.");

        var billableIdleMinutes = Math.Max(0, session.IdleDurationMinutes - tariff.IdleGracePeriodMinutes);
        var rawIdleCents = Round(billableIdleMinutes * tariff.IdleFeeCentsPerMinute);
        var idleCents = tariff.MaxIdleFeeCents.HasValue
            ? Math.Min(rawIdleCents, tariff.MaxIdleFeeCents.Value)
            : rawIdleCents;

        var energyCents = Round(session.EnergyDeliveredKwh * tariff.EnergyRateCentsPerKwh);
        var activationCents = tariff.ActivationFeeCents;

        var lines = new List<EvSettlementLine>();
        if (activationCents > 0)
        {
            lines.Add(new EvSettlementLine
            {
                Description = "Session activation fee",
                Quantity = 1,
                Unit = "session",
                AmountCents = activationCents
            });
        }

        lines.Add(new EvSettlementLine
        {
            Description = $"Energy delivered ({tariff.EnergyRateCentsPerKwh:0.0}c/kWh)",
            Quantity = session.EnergyDeliveredKwh,
            Unit = "kWh",
            AmountCents = energyCents
        });

        if (billableIdleMinutes > 0 && idleCents > 0)
        {
            lines.Add(new EvSettlementLine
            {
                Description = $"Idle parking ({billableIdleMinutes} mins beyond {tariff.IdleGracePeriodMinutes}m grace)",
                Quantity = billableIdleMinutes,
                Unit = "minutes",
                AmountCents = idleCents
            });
        }

        var subtotal = activationCents + energyCents + idleCents;
        var vatCents = Round(subtotal * tariff.VatRateBps / 10000m);
        if (vatCents > 0)
        {
            lines.Add(new EvSettlementLine
            {
                Description = $"VAT ({tariff.VatRateBps / 100m:0.##}%)",
                Quantity = tariff.VatRateBps / 100m,
                Unit = "%",
                AmountCents = vatCents
            });
        }

        return new EvSessionSettlement
        {
            SessionId = session.SessionId,
            EnergyDeliveredKwh = session.EnergyDeliveredKwh,
            TotalDurationMinutes = session.ChargingDurationMinutes + session.IdleDurationMinutes,
            BillableIdleMinutes = billableIdleMinutes,
            ActivationCents = activationCents,
            EnergyCents = energyCents,
            IdleCents = idleCents,
            SubtotalCents = subtotal,
            VatCents = vatCents,
            TotalCents = subtotal + vatCents,
            Lines = lines
        };
    }

    private static long Round(decimal value) =>
        (long)decimal.Round(value, 0, MidpointRounding.AwayFromZero);
}
