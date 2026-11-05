namespace TallyEnergyBilling.Core;

public enum DayType
{
    All,
    Weekday
}

public sealed class TariffBand
{
    public string Name { get; set; } = string.Empty;
    public decimal RateCentsPerKwh { get; set; }
    public int StartMinute { get; set; }
    public int EndMinute { get; set; }
    public DayType DayType { get; set; }
}

public sealed class Tariff
{
    public string Id { get; set; } = "nz-tou-residential";
    public string Name { get; set; } = "NZ Residential Time-of-Use";
    public long DailySupplyCents { get; set; } = 180;
    public long DemandCentsPerKw { get; set; } = 1250;
    public long ExportCentsPerKwh { get; set; } = 8;
    public int GstRateBps { get; set; } = 1500;
    public List<TariffBand> Bands { get; set; } = new();

    public static Tariff Default => new()
    {
        Bands =
        {
            new TariffBand { Name = "Peak", RateCentsPerKwh = 32m, StartMinute = 7 * 60, EndMinute = 11 * 60, DayType = DayType.Weekday },
            new TariffBand { Name = "Shoulder", RateCentsPerKwh = 22m, StartMinute = 11 * 60, EndMinute = 17 * 60, DayType = DayType.Weekday },
            new TariffBand { Name = "Peak", RateCentsPerKwh = 32m, StartMinute = 17 * 60, EndMinute = 21 * 60, DayType = DayType.Weekday },
            new TariffBand { Name = "Shoulder", RateCentsPerKwh = 22m, StartMinute = 21 * 60, EndMinute = 23 * 60, DayType = DayType.Weekday },
            new TariffBand { Name = "Off-peak", RateCentsPerKwh = 12m, StartMinute = 23 * 60, EndMinute = 7 * 60, DayType = DayType.All },
        }
    };
}

public sealed class UsageInterval
{
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public decimal Kwh { get; set; }
    public decimal ExportKwh { get; set; }
}

public sealed class BillingPeriod
{
    public DateOnly Start { get; set; }
    public DateOnly End { get; set; }
    public int ActiveDays { get; set; }
    public decimal MaxDemandKw { get; set; }
    public List<UsageInterval> Intervals { get; set; } = new();
}

public sealed class BillLine
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public long AmountCents { get; set; }
}

public sealed class EnergyBill
{
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public int ActiveDays { get; set; }
    public long UsageCents { get; set; }
    public long SupplyCents { get; set; }
    public long DemandCents { get; set; }
    public long ExportCreditCents { get; set; }
    public long SubtotalCents { get; set; }
    public long GstCents { get; set; }
    public long TotalCents { get; set; }
    public Dictionary<string, decimal> BandKwh { get; set; } = new();
    public List<BillLine> Lines { get; set; } = new();
}

public sealed class BillingException(string message) : Exception(message);
