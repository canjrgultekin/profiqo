namespace Profiqo.Application.Abstractions.Integrations.Trendyol;

public sealed class TrendyolOptions
{
    public string BaseUrl { get; init; } = "https://api.trendyol.com/sapigw";
    public string DefaultStatus { get; init; } = "Created";

    public int DefaultPageSize { get; init; } = 50;
    public int DefaultMaxPages { get; init; } = 20;

    // incremental backfill window for first sync
    public int InitialBackfillDays { get; init; } = 30;
}