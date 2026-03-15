// Path: backend/src/Profiqo.Application/Abstractions/Integrations/Hepsiburada/HepsiburadaOptions.cs
namespace Profiqo.Application.Abstractions.Integrations.Hepsiburada;

public sealed class HepsiburadaOptions
{
    /// <summary>
    /// Canlı ortam: https://oms-external.hepsiburada.com
    /// Test (SIT): https://oms-external-sit.hepsiburada.com
    /// </summary>
    public string BaseUrl { get; init; } = "https://oms-external.hepsiburada.com";

    /// <summary>Ödemesi tamamlanmış siparişleri çekerken kullanılacak default limit (max 50).</summary>
    public int DefaultLimit { get; init; } = 50;

    /// <summary>Ödemesi tamamlanmış sipariş limit üst sınırı.</summary>
    public int LimitMax { get; init; } = 50;

    /// <summary>Paket endpoint'i için limit (HB max 10).</summary>
    public int PackageLimitMax { get; init; } = 10;

    /// <summary>Tek bir sync koşusunda max kaç sayfa çekilecek.</summary>
    public int DefaultMaxPages { get; init; } = 100;

    /// <summary>Geriye dönük kaç gün backfill yapılacak. HB sipariş API'sı son 1 ay sınırı var.</summary>
    public int BackfillDays { get; init; } = 30;
}