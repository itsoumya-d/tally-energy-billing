namespace TallyEnergyBilling.Core;

public enum TelemetryMetricType
{
    ActivePowerKw,
    ReactivePowerKvar,
    VoltageVolts,
    CurrentAmps,
    BatterySocPercent,
    RadioSignalRssiDbm
}

public sealed record TelemetryReading(
    string ReadingId,
    DateTimeOffset Timestamp,
    TelemetryMetricType MetricType,
    double Value,
    string Unit,
    long SequenceNumber
);

public sealed record TelemetryIngestionBatch(
    string BatchId,
    string DeviceId,
    string SiteId,
    DateTimeOffset ReceivedAt,
    List<TelemetryReading> Readings
);

public sealed record TelemetryQuarantineRecord(
    string BatchId,
    string ReadingId,
    string Reason,
    DateTimeOffset QuarantinedAt,
    TelemetryReading OriginalReading
);

public sealed record TelemetryTimeBucket(
    string DeviceId,
    string SiteId,
    DateTimeOffset BucketStart,
    DateTimeOffset BucketEnd,
    TelemetryMetricType MetricType,
    int ReadingCount,
    double MinValue,
    double MaxValue,
    double AverageValue,
    double LatestValue
);

public sealed record TelemetryEnablementSummary(
    string DeviceId,
    string SiteId,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    int TotalReceived,
    int TotalPromoted,
    int TotalQuarantined,
    int GapsDetected,
    double CompletenessRatio,
    List<TelemetryTimeBucket> Buckets,
    List<TelemetryQuarantineRecord> Quarantine
);
