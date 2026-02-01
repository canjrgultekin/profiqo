// Path: backend/src/Profiqo.Infrastructure/Integrations/Ikas/IkasGraphqlClient.cs
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

using Profiqo.Application.Abstractions.Integrations.Ikas;
using Profiqo.Application.Common.Exceptions;

namespace Profiqo.Infrastructure.Integrations.Ikas;

public sealed class IkasOptions
{
    public string GraphqlEndpoint { get; init; } = "https://api.myikas.com/api/admin/graphql";
    public int DefaultPageSize { get; init; } = 50;
    public int DefaultMaxPages { get; init; } = 20;
}

internal sealed class IkasGraphqlClient : IIkasGraphqlClient
{
    private readonly HttpClient _http;
    private readonly IkasOptions _opts;

    public IkasGraphqlClient(HttpClient http, IOptions<IkasOptions> opts)
    {
        _http = http;
        _opts = opts.Value;
    }

    public async Task<string> MeAsync(string accessToken, CancellationToken ct)
    {
        const string op = "me";
        var payload = new
        {
            operationName = op,
            query = @"query me { me { id } }",
            variables = new { }
        };

        using var doc = await PostAsync(accessToken, op, payload, ct);
        var id = doc.RootElement.GetProperty("data").GetProperty("me").GetProperty("id").GetString();
        return id ?? throw new InvalidOperationException("Ikas me.id missing.");
    }

    public Task<JsonDocument> ListCustomersAsync(string accessToken, int page, int limit, CancellationToken ct)
    {
        const string op = "listCustomer";

        var payload = new
        {
            operationName = op,
            query = @"
query listCustomer(
  $pagination: PaginationInput,
  $search: String,
  $sort: String
) {
  listCustomer(
    pagination: $pagination,
    search: $search,
    sort: $sort
  ) {
    count
    page
    limit
    hasNext
    data {
      id
      email
      firstName
      lastName
      phone
      updatedAt
      createdAt
      orderCount
      totalOrderPrice
    }
  }
}",
            variables = new
            {
                pagination = new { page, limit },
                search = (string?)null,
                sort = "-updatedAt"
            }
        };

        return PostAsync(accessToken, op, payload, ct);
    }

    // ✅ Updated query: shippingAddress included, updatedAt filter included
    // Signature stays the same to avoid breaking callers; orderedAtGteMs param is now used as updatedAt.gte
    public Task<JsonDocument> ListOrdersAsync(string accessToken, int page, int limit, long? orderedAtGteMs, CancellationToken ct)
    {
        const string op = "listOrder";

        var payload = new
        {
            operationName = op,
            query = @"
query listOrder(
  $pagination: PaginationInput,
  $search: String,
  $sort: String,
  $status: OrderStatusEnumFilterInput,
  $updatedAt: DateFilterInput
) {
  listOrder(
    pagination: $pagination,
    search: $search,
    sort: $sort,
    status: $status,
    updatedAt: $updatedAt
  ) {
    count
    page
    limit
    hasNext
    data {
      id
      orderNumber
      orderedAt
      updatedAt
      status
      currencyCode
      totalPrice
      totalFinalPrice

      salesChannelId
      salesChannel { id name type }

      customer { id email firstName lastName phone }

      orderLineItems {
        id
        quantity
        price
        finalPrice
        currencyCode
        updatedAt
        status
        deleted
        variant {
          id
          name
          sku
          productId
          slug
          brand {
            name
          }
          categories {
            name
          }
          barcodeList
          hsCode
        }
        discountPrice
      }
      shippingAddress {
        addressLine1
        addressLine2
        city {
          name
          code
        }
        country {
          name
          code
        }
        district {
          name
          code
        }
        identityNumber
        phone
        postalCode
        region {
          name
        }
        state {
          name
          code
        }
        taxNumber
        taxOffice
        company
        firstName
        lastName
      }
      couponCode
      billingAddress {
        addressLine1
        addressLine2
        city {
          code
          name
        }
        company
        country {
          code
          name
        }
        district {
          code
          name
        }
        firstName
        lastName
        phone
        identityNumber
        postalCode
        region {
          name
        }
        state {
          code
          name
        }
        taxNumber
        taxOffice
      }
    }
  }
}",
            variables = new
            {
                pagination = new { page, limit },
                search = (string?)null,
                sort = "-updatedAt",
                status = new
                {
                    @in = new[]
                    {
                        "CANCELLED",
                        "CREATED",
                        "PARTIALLY_CANCELLED",
                        "PARTIALLY_REFUNDED",
                        "REFUNDED",
                        "REFUND_REJECTED",
                        "REFUND_REQUESTED"
                    }
                },
                updatedAt = orderedAtGteMs.HasValue ? new { gte = orderedAtGteMs.Value } : null
            }
        };

        return PostAsync(accessToken, op, payload, ct);
    }

    public Task<JsonDocument> ListAbandonedCheckoutsAsync(string accessToken, int page, int limit, long? lastActivityGteMs, CancellationToken ct)
    {
        const string op = "listAbandonedCheckouts";

        var payload = new
        {
            operationName = op,
            query = @"
query listAbandonedCheckouts (
  $customerId: StringFilterInput,
  $id: StringFilterInput,
  $input: ListAbandonedCartInput!,
  $lastActivityDate: DateFilterInput,
  $mailSendDate: DateFilterInput,
  $pagination: PaginationInput,
  $sort: String
) {
  listAbandonedCheckouts (
    customerId: $customerId,
    id: $id,
    input: $input,
    lastActivityDate: $lastActivityDate,
    mailSendDate: $mailSendDate,
    pagination: $pagination,
    sort: $sort
  ) {
    count
    page
    limit
    hasNext
    data {
      id
      orderNumber
      status
      recoveryStatus
      recoverEmailStatus
      updatedAt
      customer { email firstName lastName phone }
      cart { id lastActivityDate currencyCode totalPrice updatedAt itemCount }
    }
  }
}",
            variables = new
            {
                customerId = (object?)null,
                id = (object?)null,
                input = new { },
                lastActivityDate = lastActivityGteMs.HasValue ? new { gte = lastActivityGteMs.Value } : null,
                mailSendDate = (object?)null,
                pagination = new { page, limit },
                sort = "-lastActivityDate"
            }
        };

        return PostAsync(accessToken, op, payload, ct);
    }

    private async Task<JsonDocument> PostAsync(string accessToken, string op, object body, CancellationToken ct)
    {
        var url = QueryHelpers.AddQueryString(_opts.GraphqlEndpoint, "op", op);

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var json = JsonSerializer.Serialize(body);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        var text = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Ikas GraphQL HTTP {(int)res.StatusCode}: {text}");

        var doc = JsonDocument.Parse(text);

        if (doc.RootElement.TryGetProperty("errors", out var errors) &&
            errors.ValueKind == JsonValueKind.Array &&
            errors.GetArrayLength() > 0)
        {
            var first = errors[0];
            var msg = first.TryGetProperty("message", out var m) ? m.GetString() : null;

            if (string.Equals(msg, "LOGIN_REQUIRED", StringComparison.OrdinalIgnoreCase))
                throw new ExternalServiceAuthException("ikas", "Ikas token invalid veya yetkisiz.");

            throw new InvalidOperationException($"Ikas GraphQL errors: {errors}");
        }

        return doc;
    }
}
