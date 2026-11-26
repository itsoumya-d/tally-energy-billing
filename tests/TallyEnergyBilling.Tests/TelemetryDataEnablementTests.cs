using Xunit;
using TallyEnergyBilling.Core;

namespace TallyEnergyBilling.Tests;

public sealed class TelemetryDataEnablementTests
{
    private readonly TelemetryDataEnablementService _service = new(TimeSpan.FromMinutes(15));

    [Fact]
    public void Ingest_ValidTelemetryBatch_CreatesBucketsAndSummary()
    {
        var now = new DateTimeOffset(2026, 11, 26, 8, 0, 0, TimeSpan.FromHours(13));
        var readings = new List<TelemetryReading>
        {
            new("r1", now.AddMinutes(2), TelemetryMetricType.ActivePowerKw, 12.5, "kW", 101),
            new("r2", now.AddMinutes(8), TelemetryMetricType.ActivePowerKw, 15.0, "kW", 102),
            new("r3", now.AddMinutes(12), TelemetryMetricType.ActivePowerKw, 18.2, "kW", 103),
        };

        var batch = new TelemetryIngestionBatch("batch_1", "dev_site_a", "site_south", now.AddMinutes(15), readings);
        var result = _service.IngestAndAggregate(batch);

        Assert.Equal("dev_site_a", result.DeviceId);
        Assert.Equal(3, result.TotalReceived);
        Assert.Equal(3, result.TotalPromoted);
        Assert.Equal(0, result.TotalQuarantined);
        Assert.Equal(0, result.GapsDetected);
        Assert.Equal(1.0, result.CompletenessRatio);
        Assert.Single(result.Buckets);

        var bucket = result.Buckets[0];
        Assert.Equal(3, bucket.ReadingCount);
        Assert.Equal(12.5, bucket.MinValue);
        Assert.Equal(18.2, bucket.MaxValue);
        Assert.Equal(18.2, bucket.LatestValue);
    }

    [Fact]
    public void Ingest_DuplicateSequenceNumber_QuarantinesCollidingEvent()
    {
        var now = new DateTimeOffset(2026, 11, 26, 9, 0, 0, TimeSpan.FromHours(13));
        var readings = new List<TelemetryReading>
        {
            new("r1", now.AddMinutes(1), TelemetryMetricType.VoltageVolts, 230.5, "V", 201),
            new("r2_dup", now.AddMinutes(3), TelemetryMetricType.VoltageVolts, 231.0, "V", 201), // duplicate seq
        };

        var batch = new TelemetryIngestionBatch("batch_dup", "dev_02", "site_01", now.AddMinutes(10), readings);
        var result = _service.IngestAndAggregate(batch);

        Assert.Equal(2, result.TotalReceived);
        Assert.Equal(1, result.TotalPromoted);
        Assert.Equal(1, result.TotalQuarantined);
        Assert.Single(result.Quarantine);
        Assert.Equal("r2_dup", result.Quarantine[0].ReadingId);
        Assert.Contains("Duplicate sequence number", result.Quarantine[0].Reason);
    }

    [Fact]
    public void Ingest_NegativePowerValue_QuarantinesEvent()
    {
        var now = new DateTimeOffset(2026, 11, 26, 10, 0, 0, TimeSpan.FromHours(13));
        var readings = new List<TelemetryReading>
        {
            new("r_valid", now.AddMinutes(1), TelemetryMetricType.CurrentAmps, 5.2, "A", 301),
            new("r_neg", now.AddMinutes(2), TelemetryMetricType.CurrentAmps, -1.5, "A", 302),
        };

        var batch = new TelemetryIngestionBatch("batch_neg", "dev_03", "site_01", now.AddMinutes(10), readings);
        var result = _service.IngestAndAggregate(batch);

        Assert.Equal(1, result.TotalPromoted);
        Assert.Equal(1, result.TotalQuarantined);
        Assert.Contains("Negative telemetry value", result.Quarantine[0].Reason);
    }

    [Fact]
    public void Ingest_FutureTimestampExceedingClockDrift_QuarantinesEvent()
    {
        var now = new DateTimeOffset(2026, 11, 26, 11, 0, 0, TimeSpan.FromHours(13));
        var readings = new List<TelemetryReading>
        {
            new("r_future", now.AddHours(2), TelemetryMetricType.ActivePowerKw, 10.0, "kW", 401),
        };

        var batch = new TelemetryIngestionBatch("batch_future", "dev_04", "site_01", now, readings);
        var result = _service.IngestAndAggregate(batch);

        Assert.Equal(0, result.TotalPromoted);
        Assert.Equal(1, result.TotalQuarantined);
        Assert.Contains("Future timestamp exceeds", result.Quarantine[0].Reason);
    }

    [Fact]
    public void Ingest_TelemetryGaps_DetectsGapsCorrectly()
    {
        var now = new DateTimeOffset(2026, 11, 26, 12, 0, 0, TimeSpan.FromHours(13));
        var readings = new List<TelemetryReading>
        {
            new("r1", now, TelemetryMetricType.ActivePowerKw, 10.0, "kW", 501),
            new("r2_delayed", now.AddMinutes(45), TelemetryMetricType.ActivePowerKw, 12.0, "kW", 502), // 45 min gap (> 15 min * 1.5)
        };

        var batch = new TelemetryIngestionBatch("batch_gap", "dev_05", "site_01", now.AddHours(1), readings);
        var result = _service.IngestAndAggregate(batch);

        Assert.Equal(2, result.TotalPromoted);
        Assert.Equal(1, result.GapsDetected);
        Assert.True(result.CompletenessRatio < 1.0);
    }

    [Fact]
    public void Ingest_RadioSignalRssi_AllowsNegativeValues()
    {
        var now = new DateTimeOffset(2026, 11, 26, 13, 0, 0, TimeSpan.FromHours(13));
        var readings = new List<TelemetryReading>
        {
            new("r_rssi", now, TelemetryMetricType.RadioSignalRssiDbm, -78.5, "dBm", 601),
        };

        var batch = new TelemetryIngestionBatch("batch_rssi", "dev_radio", "site_01", now.AddMinutes(5), readings);
        var result = _service.IngestAndAggregate(batch);

        Assert.Equal(1, result.TotalPromoted);
        Assert.Equal(0, result.TotalQuarantined);
        Assert.Single(result.Buckets);
        Assert.Equal(-78.5, result.Buckets[0].AverageValue);
    }
}
