namespace TallyEnergyBilling.Core;

public sealed class EvChargingTariff
{
    public string TariffId { get; set; } = "ev-public-standard";
    public long ActivationFeeCents { get; set; } = 50;
    public decimal EnergyRateCentsPerKwh { get; set; } = 38.5m;
    public long IdleFeeCentsPerMinute { get; set; } = 12;
    public int IdleGracePeriodMinutes { get; set; } = 15;
    public long? MaxIdleFeeCents { get; set; } = 2500;
    public int VatRateBps { get; set; } = 2100;
}

public sealed class EvChargingSession
{
    public string SessionId { get; set; } = string.Empty;
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public decimal EnergyDeliveredKwh { get; set; }
    public int ChargingDurationMinutes { get; set; }
    public int IdleDurationMinutes { get; set; }
}

public sealed class EvSettlementLine
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public long AmountCents { get; set; }
}

public sealed class EvSessionSettlement
{
    public string SessionId { get; set; } = string.Empty;
    public decimal EnergyDeliveredKwh { get; set; }
    public int TotalDurationMinutes { get; set; }
    public int BillableIdleMinutes { get; set; }
    public long ActivationCents { get; set; }
    public long EnergyCents { get; set; }
    public long IdleCents { get; set; }
    public long SubtotalCents { get; set; }
    public long VatCents { get; set; }
    public long TotalCents { get; set; }
    public List<EvSettlementLine> Lines { get; set; } = new();
}
