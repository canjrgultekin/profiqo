// Path: backend/src/Profiqo.Application/Abstractions/Integrations/Trendyol/TrendyolOptions.cs
namespace Profiqo.Application.Abstractions.Integrations.Trendyol;

public sealed class TrendyolOptions
{
    public string BaseUrl { get; init; } = "https://apigw.trendyol.com";
    public string IntegrationPrefix { get; init; } = "/integration";

    public int PageSizeMax { get; init; } = 200;
    public int DefaultPageSize { get; init; } = 200;
    public int DefaultMaxPages { get; init; } = 20;

    // Trendyol max 3 ay geçmiş data: biz 90 gün alıyoruz
    public int BackfillDays { get; init; } = 90;

    public string OrderByField { get; init; } = "CreatedDate";
}