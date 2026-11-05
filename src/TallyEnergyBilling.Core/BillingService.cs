using System.Globalization;

namespace TallyEnergyBilling.Core;

public sealed record BandRate(string Name, decimal RateCentsPerKwh);

public sealed class BillingService
{
    private static readonly TimeZoneInfo Auckland = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland");

    public BandRate RateFor(Tariff tariff, DateTimeOffset at)
    {
        var local = TimeZoneInfo.ConvertTime(at, Auckland);
        if (local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            var offPeak = tariff.Bands.FirstOrDefault(b => b.Name == "Off-peak")
                ?? throw new BillingException("Tariff has no off-peak band.");
            return new BandRate(offPeak.Name, offPeak.RateCentsPerKwh);
        }

        var minute = local.Hour * 60 + local.Minute;
        foreach (var band in tariff.Bands.Where(b => b.DayType is DayType.All or DayType.Weekday))
        {
            var matches = band.StartMinute <= band.EndMinute
                ? minute >= band.StartMinute && minute < band.EndMinute
                : minute >= band.StartMinute || minute < band.EndMinute;
            if (matches) return new BandRate(band.Name, band.RateCentsPerKwh);
        }

        throw new BillingException($"No tariff band covers {local:HH:mm} on {local.DayOfWeek}.");
    }

    public EnergyBill Calculate(Tariff tariff, BillingPeriod period)
    {
        if (period.End < period.Start) throw new BillingException("Period end must be on or after start.");
        if (period.ActiveDays <= 0) throw new BillingException("Active days must be positive.");
        var daysInMonth = DateTime.DaysInMonth(period.Start.Year, period.Start.Month);
        if (period.ActiveDays > daysInMonth) throw new BillingException("Active days exceed the month.");
        if (period.MaxDemandKw < 0) throw new BillingException("Max demand cannot be negative.");

        var bandKwh = new Dictionary<string, decimal>();
        var bandRates = new Dictionary<string, decimal>();
        decimal exportKwh = 0;

        foreach (var interval in period.Intervals)
        {
            if (interval.End <= interval.Start) throw new BillingException("Interval end must be after start.");
            if (interval.Kwh < 0) throw new BillingException("Interval kWh cannot be negative.");
            if (interval.ExportKwh < 0) throw new BillingException("Interval export cannot be negative.");

            var startDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(interval.Start, Auckland).DateTime);
            if (startDate < period.Start || startDate > period.End)
                throw new BillingException("Interval falls outside the billing period.");

            var band = RateFor(tariff, interval.Start);
            bandKwh[band.Name] = bandKwh.GetValueOrDefault(band.Name) + interval.Kwh;
            bandRates[band.Name] = band.RateCentsPerKwh;
            exportKwh += interval.ExportKwh;
        }

        long usageCents = 0;
        var lines = new List<BillLine>();
        foreach (var (name, kwh) in bandKwh.OrderBy(x => x.Key))
        {
            var amount = Round(kwh * bandRates[name]);
            usageCents += amount;
            lines.Add(new BillLine
            {
                Description = $"{name} usage",
                Quantity = kwh,
                Unit = "kWh",
                AmountCents = amount
            });
        }

        var supplyCents = Round(tariff.DailySupplyCents * period.ActiveDays);
        lines.Add(new BillLine
        {
            Description = "Daily supply charge",
            Quantity = period.ActiveDays,
            Unit = "days",
            AmountCents = supplyCents
        });

        var demandCents = Round(tariff.DemandCentsPerKw * period.MaxDemandKw);
        if (demandCents > 0)
        {
            lines.Add(new BillLine
            {
                Description = "Demand charge",
                Quantity = period.MaxDemandKw,
                Unit = "kW",
                AmountCents = demandCents
            });
        }

        var exportCreditCents = Round(tariff.ExportCentsPerKwh * exportKwh);
        if (exportCreditCents > 0)
        {
            lines.Add(new BillLine
            {
                Description = "Solar export credit",
                Quantity = exportKwh,
                Unit = "kWh",
                AmountCents = -exportCreditCents
            });
        }

        var subtotal = usageCents + supplyCents + demandCents - exportCreditCents;
        var gst = Round(subtotal * tariff.GstRateBps / 10000m);
        lines.Add(new BillLine
        {
            Description = "GST",
            Quantity = tariff.GstRateBps / 100m,
            Unit = "%",
            AmountCents = gst
        });

        return new EnergyBill
        {
            PeriodStart = period.Start,
            PeriodEnd = period.End,
            ActiveDays = period.ActiveDays,
            UsageCents = usageCents,
            SupplyCents = supplyCents,
            DemandCents = demandCents,
            ExportCreditCents = exportCreditCents,
            SubtotalCents = subtotal,
            GstCents = gst,
            TotalCents = subtotal + gst,
            BandKwh = bandKwh,
            Lines = lines
        };
    }

    private static long Round(decimal value) =>
        (long)decimal.Round(value, 0, MidpointRounding.AwayFromZero);
}
