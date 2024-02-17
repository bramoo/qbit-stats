class MetricConfig
{
    public required string Path { get; init; }
    public required int Port { get; init; }
    public required int MinRefreshSeconds { get; init; }
    public required string MeterName { get; init; }
}
