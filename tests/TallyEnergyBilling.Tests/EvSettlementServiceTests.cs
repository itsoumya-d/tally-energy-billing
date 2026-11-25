using TallyEnergyBilling.Core;

namespace TallyEnergyBilling.Tests;

public sealed class EvSettlementServiceTests
{
    private readonly EvSettlementService _service = new();
    private readonly EvChargingTariff _defaultTariff = new()
    {
        ActivationFeeCents = 50,
        EnergyRateCentsPerKwh = 40m,
        IdleFeeCentsPerMinute = 10,
        IdleGracePeriodMinutes = 15,
        MaxIdleFeeCents = 2000,
        VatRateBps = 2100
    };

    [Fact]
    public void Settle_ActiveChargingOnly_CalculatesEnergyAndVatCorrectly()
    {
        var start = DateTimeOffset.UtcNow.AddMinutes(-60);
        var session = new EvChargingSession
        {
            SessionId = "sess-001",
            StartTime = start,
            EndTime = start.AddMinutes(60),
            EnergyDeliveredKwh = 25.0m,
            ChargingDurationMinutes = 60,
            IdleDurationMinutes = 0
        };

        var res = _service.Settle(_defaultTariff, session);

        Assert.Equal("sess-001", res.SessionId);
        Assert.Equal(50, res.ActivationCents);
        Assert.Equal(1000, res.EnergyCents); // 25 kWh * 40c = 1000c
        Assert.Equal(0, res.IdleCents);
        Assert.Equal(0, res.BillableIdleMinutes);
        Assert.Equal(1050, res.SubtotalCents);
        Assert.Equal(221, res.VatCents); // 1050 * 0.21 = 220.5 -> 221c
        Assert.Equal(1271, res.TotalCents);
    }

    [Fact]
    public void Settle_IdleWithinGracePeriod_NoIdleFeeCharged()
    {
        var start = DateTimeOffset.UtcNow.AddMinutes(-45);
        var session = new EvChargingSession
        {
            SessionId = "sess-002",
            StartTime = start,
            EndTime = start.AddMinutes(45),
            EnergyDeliveredKwh = 10.0m,
            ChargingDurationMinutes = 35,
            IdleDurationMinutes = 10 // <= 15m grace period
        };

        var res = _service.Settle(_defaultTariff, session);

        Assert.Equal(0, res.BillableIdleMinutes);
        Assert.Equal(0, res.IdleCents);
        Assert.Equal(450, res.SubtotalCents); // 50 activation + 400 energy
    }

    [Fact]
    public void Settle_IdleExceedingGracePeriod_BillsOnlyOverage()
    {
        var start = DateTimeOffset.UtcNow.AddMinutes(-70);
        var session = new EvChargingSession
        {
            SessionId = "sess-003",
            StartTime = start,
            EndTime = start.AddMinutes(70),
            EnergyDeliveredKwh = 15.0m,
            ChargingDurationMinutes = 40,
            IdleDurationMinutes = 30 // 30 - 15 = 15 billable mins
        };

        var res = _service.Settle(_defaultTariff, session);

        Assert.Equal(15, res.BillableIdleMinutes);
        Assert.Equal(150, res.IdleCents); // 15 mins * 10c = 150c
        Assert.Equal(800, res.SubtotalCents); // 50 + 600 + 150
    }

    [Fact]
    public void Settle_IdleCappedAtMaximumLimit_EnforcesCap()
    {
        var start = DateTimeOffset.UtcNow.AddMinutes(-300);
        var session = new EvChargingSession
        {
            SessionId = "sess-004",
            StartTime = start,
            EndTime = start.AddMinutes(300),
            EnergyDeliveredKwh = 20.0m,
            ChargingDurationMinutes = 60,
            IdleDurationMinutes = 240 // 240 - 15 = 225 mins * 10c = 2250c -> capped at 2000c
        };

        var res = _service.Settle(_defaultTariff, session);

        Assert.Equal(225, res.BillableIdleMinutes);
        Assert.Equal(2000, res.IdleCents);
        Assert.Equal(2850, res.SubtotalCents); // 50 + 800 + 2000
    }

    [Fact]
    public void Settle_ReversedTimestamps_ThrowsBillingException()
    {
        var now = DateTimeOffset.UtcNow;
        var session = new EvChargingSession
        {
            StartTime = now,
            EndTime = now.AddMinutes(-10),
            EnergyDeliveredKwh = 5.0m
        };

        Assert.Throws<BillingException>(() => _service.Settle(_defaultTariff, session));
    }

    [Fact]
    public void Settle_NegativeEnergy_ThrowsBillingException()
    {
        var now = DateTimeOffset.UtcNow;
        var session = new EvChargingSession
        {
            StartTime = now,
            EndTime = now.AddMinutes(30),
            EnergyDeliveredKwh = -2.5m
        };

        Assert.Throws<BillingException>(() => _service.Settle(_defaultTariff, session));
    }
}
