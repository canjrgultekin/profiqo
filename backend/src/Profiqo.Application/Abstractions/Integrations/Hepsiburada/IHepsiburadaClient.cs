// Path: backend/src/Profiqo.Application/Abstractions/Integrations/Hepsiburada/IHepsiburadaClient.cs
using System.Text.Json;

namespace Profiqo.Application.Abstractions.Integrations.Hepsiburada;

public interface IHepsiburadaClient
{
    /// <summary>
    /// Ödemesi tamamlanmış siparişleri listeler.
    /// GET /orders/merchantid/{merchantId}?offset={offset}&amp;limit={limit}&amp;begindate={begin}&amp;enddate={end}
    /// </summary>
    Task<JsonDocument> GetPaidOrdersAsync(
        string username,
        string password,
        string merchantId,
        int offset,
        int limit,
        string? beginDate,
        string? endDate,
        CancellationToken ct);

    /// <summary>
    /// Paketlenmiş siparişleri listeler (zengin müşteri/adres verisi).
    /// GET /packages/merchantid/{merchantId}?offset={offset}&amp;limit={limit}&amp;begindate={begin}&amp;enddate={end}
    /// </summary>
    Task<JsonDocument> GetPackagesAsync(
        string username,
        string password,
        string merchantId,
        int offset,
        int limit,
        string? beginDate,
        string? endDate,
        CancellationToken ct);

    /// <summary>
    /// Belirli bir sipariş numarasının detayını getirir.
    /// GET /orders/merchantid/{merchantId}/ordernumber/{orderNumber}
    /// </summary>
    Task<JsonDocument> GetOrderDetailAsync(
        string username,
        string password,
        string merchantId,
        string orderNumber,
        CancellationToken ct);
}