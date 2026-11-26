namespace TallyEnergyBilling.Core;

public sealed class TelemetryDataEnablementService
{
    private readonly TimeSpan _expectedInterval;
    private readonly double _maxAllowableValue;

    public TelemetryDataEnablementService(
        TimeSpan? expectedInterval = null,
        double maxAllowableValue = 100_000.0)
    {
        _expectedInterval = expectedInterval ?? TimeSpan.FromMinutes(15);
        _maxAllowableValue = maxAllowableValue;
    }

    public TelemetryEnablementSummary IngestAndAggregate(TelemetryIngestionBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentException.ThrowIfNullOrWhiteSpace(batch.DeviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(batch.SiteId);

        var validReadings = new List<TelemetryReading>();
        var quarantine = new List<TelemetryQuarantineRecord>();
        var seenSequences = new HashSet<long>();
        var seenReadingKeys = new HashSet<(DateTimeOffset, TelemetryMetricType)>();

        foreach (var reading in batch.Readings.OrderBy(r => r.SequenceNumber))
        {
            // Validation 1: Future timestamp check (allowing max 5 min clock drift)
            if (reading.Timestamp > batch.ReceivedAt.AddMinutes(5))
            {
                quarantine.Add(new TelemetryQuarantineRecord(
                    batch.BatchId,
                    reading.ReadingId,
                    "Future timestamp exceeds allowable clock drift",
                    DateTimeOffset.UtcNow,
                    reading));
                continue;
            }

            // Validation 2: Out of bounds / negative values for non-RSSI metrics
            if (reading.MetricType != TelemetryMetricType.RadioSignalRssiDbm && reading.Value < 0)
            {
                quarantine.Add(new TelemetryQuarantineRecord(
                    batch.BatchId,
                    reading.ReadingId,
                    "Negative telemetry value rejected for metric",
                    DateTimeOffset.UtcNow,
                    reading));
                continue;
            }

            if (Math.Abs(reading.Value) > _maxAllowableValue)
            {
                quarantine.Add(new TelemetryQuarantineRecord(
                    batch.BatchId,
                    reading.ReadingId,
                    "Value exceeds maximum plausible telemetry threshold",
                    DateTimeOffset.UtcNow,
                    reading));
                continue;
            }

            // Validation 3: Duplicate sequence number or exact timestamp collision
            var dedupKey = (reading.Timestamp, reading.MetricType);
            if (!seenSequences.Add(reading.SequenceNumber) || !seenReadingKeys.Add(dedupKey))
            {
                quarantine.Add(new TelemetryQuarantineRecord(
                    batch.BatchId,
                    reading.ReadingId,
                    "Duplicate sequence number or timestamp collision",
                    DateTimeOffset.UtcNow,
                    reading));
                continue;
            }

            validReadings.Add(reading);
        }

        // Window & Bucket Aggregation
        var buckets = new List<TelemetryTimeBucket>();
        int gapsDetected = 0;

        if (validReadings.Count > 0)
        {
            var groupedByMetric = validReadings.GroupBy(r => r.MetricType);

            foreach (var metricGroup in groupedByMetric)
            {
                var sortedReadings = metricGroup.OrderBy(r => r.Timestamp).ToList();

                // Detect interval gaps
                for (int i = 1; i < sortedReadings.Count; i++)
                {
                    var delta = sortedReadings[i].Timestamp - sortedReadings[i - 1].Timestamp;
                    if (delta > _expectedInterval * 1.5)
                    {
                        gapsDetected++;
                    }
                }

                // Bucket into aligned windows
                var bucketed = sortedReadings.GroupBy(r => AlignToBucketStart(r.Timestamp, _expectedInterval));

                foreach (var b in bucketed)
                {
                    var readingsInBucket = b.ToList();
                    var bucketStart = b.Key;
                    var bucketEnd = bucketStart + _expectedInterval;

                    buckets.Add(new TelemetryTimeBucket(
                        batch.DeviceId,
                        batch.SiteId,
                        bucketStart,
                        bucketEnd,
                        metricGroup.Key,
                        readingsInBucket.Count,
                        readingsInBucket.Min(r => r.Value),
                        readingsInBucket.Max(r => r.Value),
                        readingsInBucket.Average(r => r.Value),
                        readingsInBucket.Last().Value));
                }
            }
        }

        var minTime = validReadings.Count > 0 ? validReadings.Min(r => r.Timestamp) : batch.ReceivedAt;
        var maxTime = validReadings.Count > 0 ? validReadings.Max(r => r.Timestamp) : batch.ReceivedAt;
        int totalExpected = Math.Max(1, validReadings.Count + gapsDetected);
        double completeness = Math.Min(1.0, (double)validReadings.Count / totalExpected);

        return new TelemetryEnablementSummary(
            batch.DeviceId,
            batch.SiteId,
            minTime,
            maxTime,
            batch.Readings.Count,
            validReadings.Count,
            quarantine.Count,
            gapsDetected,
            completeness,
            buckets,
            quarantine);
    }

    private static DateTimeOffset AlignToBucketStart(DateTimeOffset time, TimeSpan interval)
    {
        long ticks = time.Ticks / interval.Ticks * interval.Ticks;
        return new DateTimeOffset(ticks, time.Offset);
    }
}
