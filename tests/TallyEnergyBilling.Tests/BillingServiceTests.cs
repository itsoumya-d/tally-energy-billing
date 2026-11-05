using TallyEnergyBilling.Core;

namespace TallyEnergyBilling.Tests;

public sealed class BillingServiceTests
{
    private static readonly TimeSpan Nzdt = TimeSpan.FromHours(13);
    private readonly BillingService _service = new();
    private readonly Tariff _tariff = Tariff.Default;

    private static DateTimeOffset At(int day, int hour, int minute = 0) =>
        new DateTimeOffset(2026, 11, 1, 0, 0, 0, Nzdt)
            .AddDays(day - 1)
            .AddHours(hour)
            .AddMinutes(minute);

    private static BillingPeriod Period(params UsageInterval[] intervals) => new()
    {
        Start = new DateOnly(2026, 11, 1),
        End = new DateOnly(2026, 11, 30),
        ActiveDays = 30,
        MaxDemandKw = 6.5m,
        Intervals = intervals.ToList()
    };

    private static UsageInterval Interval(int day, int hour, int minute, decimal kwh, decimal export = 0) => new()
    {
        Start = At(day, hour, minute),
        End = At(day, hour, minute + 30),
        Kwh = kwh,
        ExportKwh = export
    };

    private static BillingPeriod SamplePeriod() => Period(
        Interval(2, 8, 0, 2m),
        Interval(2, 12, 0, 3m),
        Interval(2, 23, 30, 1.5m, 4m));

    [Fact]
    public void Peak_rate_applies_on_a_weekday_morning()
    {
        var rate = _service.RateFor(_tariff, At(2, 8));
        Assert.Equal("Peak", rate.Name);
        Assert.Equal(32m, rate.RateCentsPerKwh);
    }

    [Fact]
    public void Shoulder_rate_applies_at_midday()
    {
        var rate = _service.RateFor(_tariff, At(2, 12));
        Assert.Equal("Shoulder", rate.Name);
        Assert.Equal(22m, rate.RateCentsPerKwh);
    }

    [Fact]
    public void Off_peak_rate_applies_late_at_night()
    {
        var rate = _service.RateFor(_tariff, At(2, 23, 30));
        Assert.Equal("Off-peak", rate.Name);
        Assert.Equal(12m, rate.RateCentsPerKwh);
    }

    [Fact]
    public void Weekend_uses_off_peak_all_day()
    {
        var rate = _service.RateFor(_tariff, At(7, 12));
        Assert.Equal("Off-peak", rate.Name);
        Assert.Equal(12m, rate.RateCentsPerKwh);
    }

    [Fact]
    public void Peak_boundary_at_seven_is_peak()
    {
        Assert.Equal("Peak", _service.RateFor(_tariff, At(2, 7)).Name);
    }

    [Fact]
    public void Shoulder_boundary_at_nine_pm_is_shoulder()
    {
        Assert.Equal("Shoulder", _service.RateFor(_tariff, At(2, 21)).Name);
    }

    [Fact]
    public void Off_peak_boundary_at_eleven_pm_is_off_peak()
    {
        Assert.Equal("Off-peak", _service.RateFor(_tariff, At(2, 23)).Name);
    }

    [Fact]
    public void Calculates_usage_across_bands()
    {
        var bill = _service.Calculate(_tariff, SamplePeriod());
        Assert.Equal(148, bill.UsageCents);
        Assert.Equal(2m, bill.BandKwh["Peak"]);
        Assert.Equal(3m, bill.BandKwh["Shoulder"]);
        Assert.Equal(1.5m, bill.BandKwh["Off-peak"]);
    }

    [Fact]
    public void Calculates_daily_supply_charge()
    {
        var bill = _service.Calculate(_tariff, SamplePeriod());
        Assert.Equal(5400, bill.SupplyCents);
    }

    [Fact]
    public void Calculates_demand_charge()
    {
        var bill = _service.Calculate(_tariff, SamplePeriod());
        Assert.Equal(8125, bill.DemandCents);
    }

    [Fact]
    public void Calculates_export_credit()
    {
        var bill = _service.Calculate(_tariff, SamplePeriod());
        Assert.Equal(32, bill.ExportCreditCents);
    }

    [Fact]
    public void Calculates_subtotal_gst_and_total()
    {
        var bill = _service.Calculate(_tariff, SamplePeriod());
        Assert.Equal(13641, bill.SubtotalCents);
        Assert.Equal(2046, bill.GstCents);
        Assert.Equal(15687, bill.TotalCents);
    }

    [Fact]
    public void Prorates_supply_for_partial_month()
    {
        var period = Period(Interval(2, 8, 0, 2m));
        period.ActiveDays = 15;
        var bill = _service.Calculate(_tariff, period);
        Assert.Equal(2700, bill.SupplyCents);
    }

    [Fact]
    public void Empty_usage_still_bills_supply_and_demand()
    {
        var bill = _service.Calculate(_tariff, Period());
        Assert.Equal(0, bill.UsageCents);
        Assert.Equal(5400, bill.SupplyCents);
        Assert.Equal(8125, bill.DemandCents);
        Assert.Equal(13525, bill.SubtotalCents);
    }

    [Fact]
    public void Emits_line_items_for_each_component()
    {
        var bill = _service.Calculate(_tariff, SamplePeriod());
        Assert.Equal(7, bill.Lines.Count);
        Assert.Contains(bill.Lines, line => line.Description == "Peak usage");
        Assert.Contains(bill.Lines, line => line.Description == "Solar export credit" && line.AmountCents < 0);
        Assert.Contains(bill.Lines, line => line.Description == "GST");
    }

    [Fact]
    public void Rejects_reversed_period()
    {
        var period = Period();
        period.Start = new DateOnly(2026, 11, 30);
        period.End = new DateOnly(2026, 11, 1);
        Assert.Throws<BillingException>(() => _service.Calculate(_tariff, period));
    }

    [Fact]
    public void Rejects_zero_active_days()
    {
        var period = Period();
        period.ActiveDays = 0;
        Assert.Throws<BillingException>(() => _service.Calculate(_tariff, period));
    }

    [Fact]
    public void Rejects_active_days_beyond_the_month()
    {
        var period = Period();
        period.ActiveDays = 31;
        Assert.Throws<BillingException>(() => _service.Calculate(_tariff, period));
    }

    [Fact]
    public void Rejects_negative_usage_or_export()
    {
        Assert.Throws<BillingException>(() => _service.Calculate(_tariff, Period(Interval(2, 8, 0, -1m))));
        Assert.Throws<BillingException>(() => _service.Calculate(_tariff, Period(Interval(2, 8, 0, 1m, -1m))));
    }

    [Fact]
    public void Rejects_negative_demand()
    {
        var period = Period();
        period.MaxDemandKw = -1m;
        Assert.Throws<BillingException>(() => _service.Calculate(_tariff, period));
    }

    [Fact]
    public void Rejects_intervals_outside_the_period()
    {
        var interval = new UsageInterval
        {
            Start = new DateTimeOffset(2026, 10, 31, 12, 0, 0, Nzdt),
            End = new DateTimeOffset(2026, 10, 31, 12, 30, 0, Nzdt),
            Kwh = 1m
        };
        Assert.Throws<BillingException>(() => _service.Calculate(_tariff, Period(interval)));
    }

    [Fact]
    public void Rejects_interval_that_ends_before_it_starts()
    {
        var interval = new UsageInterval
        {
            Start = At(2, 12, 30),
            End = At(2, 12),
            Kwh = 1m
        };
        Assert.Throws<BillingException>(() => _service.Calculate(_tariff, Period(interval)));
    }
}
