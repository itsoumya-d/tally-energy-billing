using TallyEnergyBilling.Api;
using TallyEnergyBilling.Core;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<BillingService>();

var app = builder.Build();

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (BillingException exception)
    {
        context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        await context.Response.WriteAsJsonAsync(new { error = exception.Message });
    }
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/tariff", () => Results.Ok(Tariff.Default));

app.MapPost("/api/bills", (BillRequest request, BillingService service) =>
{
    var period = new BillingPeriod
    {
        Start = request.PeriodStart,
        End = request.PeriodEnd,
        ActiveDays = request.ActiveDays,
        MaxDemandKw = request.MaxDemandKw,
        Intervals = request.Intervals.Select(x => new UsageInterval
        {
            Start = x.Start,
            End = x.End,
            Kwh = x.Kwh,
            ExportKwh = x.ExportKwh
        }).ToList()
    };
    return Results.Ok(service.Calculate(Tariff.Default, period));
});

app.MapGet("/api/sample-bill", (BillingService service) => Results.Ok(service.Calculate(Tariff.Default, SamplePeriod())));
app.MapGet("/", () => Results.Content(TallyDashboard.Page(), "text/html"));

app.Run();

static BillingPeriod SamplePeriod()
{
    var nzdt = TimeSpan.FromHours(13);
    DateTimeOffset At(int day, int hour, int minute = 0) =>
        new DateTimeOffset(2026, 11, 1, 0, 0, 0, nzdt).AddDays(day - 1).AddHours(hour).AddMinutes(minute);
    UsageInterval Interval(int day, int hour, int minute, decimal kwh, decimal export = 0) => new()
    {
        Start = At(day, hour, minute),
        End = At(day, hour, minute + 30),
        Kwh = kwh,
        ExportKwh = export
    };
    return new BillingPeriod
    {
        Start = new DateOnly(2026, 11, 1),
        End = new DateOnly(2026, 11, 30),
        ActiveDays = 30,
        MaxDemandKw = 6.5m,
        Intervals =
        {
            Interval(2, 8, 0, 2m),
            Interval(2, 12, 0, 3m),
            Interval(2, 23, 30, 1.5m, 4m)
        }
    };
}

public sealed record IntervalRequest(DateTimeOffset Start, DateTimeOffset End, decimal Kwh, decimal ExportKwh);
public sealed record BillRequest(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    int ActiveDays,
    decimal MaxDemandKw,
    List<IntervalRequest> Intervals);

public partial class Program;
