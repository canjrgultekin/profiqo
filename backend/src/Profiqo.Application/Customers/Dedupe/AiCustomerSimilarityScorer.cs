// Path: backend/src/Profiqo.Application/Customers/Dedupe/AiCustomerSimilarityScorer.cs
using System.Net.Http.Json;

using Microsoft.Extensions.Options;

namespace Profiqo.Application.Customers.Dedupe;

public sealed class AiScoringOptions
{
    public string? Endpoint { get; init; } // example: http://localhost:8000/identity/score
    public int TimeoutSeconds { get; init; } = 8;
}

public sealed class AiCustomerSimilarityScorer : ICustomerSimilarityScorer
{
    private readonly HttpClient _http;
    private readonly AiScoringOptions _opts;

    public AiCustomerSimilarityScorer(HttpClient http, IOptions<AiScoringOptions> opts)
    {
        _http = http;
        _opts = opts.Value;
        _http.Timeout = TimeSpan.FromSeconds(_opts.TimeoutSeconds);
    }

    public async Task<double> ScoreAsync(CustomerDuplicateCandidateDto a, CustomerDuplicateCandidateDto b, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opts.Endpoint))
            return 0d;

        var req = new
        {
            a,
            b
        };

        using var res = await _http.PostAsJsonAsync(_opts.Endpoint, req, cancellationToken: ct);
        if (!res.IsSuccessStatusCode) return 0d;

        var payload = await res.Content.ReadFromJsonAsync<AiScoreResponse>(cancellationToken: ct);
        if (payload is null) return 0d;

        return payload.Score is < 0 ? 0 : payload.Score is > 1 ? 1 : payload.Score;
    }

    private sealed record AiScoreResponse(double Score);
}