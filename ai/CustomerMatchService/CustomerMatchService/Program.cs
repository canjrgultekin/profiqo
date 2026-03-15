using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Couchbase;
using Couchbase.KeyValue;
using Couchbase.Query;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var opt = AppOptions.FromEnv();

builder.Services.AddSingleton(opt);
builder.Services.AddSingleton<CouchbaseProvider>();

builder.Services.AddHttpClient<OpenAiClient>(http =>
{
    http.BaseAddress = new Uri(opt.OpenAi.BaseUrl);
    http.Timeout = TimeSpan.FromSeconds(opt.OpenAi.TimeoutSeconds);
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", opt.OpenAi.ApiKey);
});

builder.Services.AddHttpClient<FtsClient>(http =>
{
    http.BaseAddress = new Uri(opt.Couchbase.FtsBaseUrl.TrimEnd('/') + "/");
    http.Timeout = TimeSpan.FromSeconds(opt.Couchbase.FtsTimeoutSeconds);

    var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{opt.Couchbase.Username}:{opt.Couchbase.Password}"));
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
});

builder.Services.AddSingleton<CustomerMatchService>();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/healthz", () => Results.Ok(new { ok = true }));

app.MapPost("/v1/customers/match", async (MatchRequest req, CustomerMatchService svc, CancellationToken ct) =>
{
    if (req.Customers is null || req.Customers.Count == 0)
        return Results.BadRequest(new { error = "customers_empty" });

    if (req.Customers.Count > 100)
        return Results.BadRequest(new { error = "customers_too_many", max = 100 });

    var resp = await svc.MatchAsync(req.Customers, ct);
    return resp.Errors is { Count: > 0 } ? Results.BadRequest(resp) : Results.Ok(resp);
});

await app.RunAsync();


// ======================
// Service
// ======================
sealed class CustomerMatchService
{
    private readonly AppOptions _opt;
    private readonly CouchbaseProvider _cb;
    private readonly OpenAiClient _openAi;
    private readonly FtsClient _fts;

    public CustomerMatchService(AppOptions opt, CouchbaseProvider cb, OpenAiClient openAi, FtsClient fts)
    {
        _opt = opt;
        _cb = cb;
        _openAi = openAi;
        _fts = fts;
    }

    public async Task<MatchResponse> MatchAsync(List<CustomerInput> inputs, CancellationToken ct)
    {
        var errors = Validate(inputs);
        if (errors.Count > 0) return new MatchResponse { Errors = errors };

        var ctxs = inputs.Select((c, i) => new InputCtx(i, c, NormalizedInput.From(c))).ToList();

        // 1) Exact match (N1QL)
        var exactByIndex = await ExactMatchAsync(ctxs, ct);

        // 2) Vector match for those without exact
        var needVector = ctxs.Where(c => !exactByIndex.ContainsKey(c.Index)).ToList();
        var vectorByIndex = new Dictionary<int, List<Candidate>>();

        if (needVector.Count > 0)
        {
            var queryTexts = needVector.Select(BuildQueryText).ToArray();
            Console.WriteLine($"[VectorMatch] queryTexts=[{string.Join(" | ", queryTexts)}]");

            var queryVectors = await _openAi.CreateEmbeddingsAsync(queryTexts, _opt.OpenAi.EmbeddingModel, _opt.OpenAi.EmbeddingDims, ct);
            Console.WriteLine($"[VectorMatch] got {queryVectors.Length} embeddings, dims={queryVectors[0].Length}");

            for (var i = 0; i < needVector.Count; i++)
            {
                var qvec = VectorCodec.NormalizeL2(queryVectors[i]);
                var qB64 = VectorCodec.ToBase64LittleEndianFloat32(qvec);

                var docIds = await _fts.KnnSearchDocIdsAsync(
                    _opt.Couchbase.FtsIndexName,
                    field: "embedding",
                    vectorBase64: qB64,
                    k: _opt.Match.VectorCandidatePool,
                    ct: ct);

                Console.WriteLine($"[VectorMatch] FTS returned {docIds.Count} docIds");
                if (docIds.Count > 0)
                    Console.WriteLine($"[VectorMatch] first docIds: {string.Join(", ", docIds.Take(5))}");

                if (docIds.Count == 0)
                {
                    vectorByIndex[needVector[i].Index] = new List<Candidate>();
                    continue;
                }

                var cands = await FetchCandidatesByIdsAsync(docIds, ct);
                Console.WriteLine($"[VectorMatch] fetched {cands.Count} candidates from KV");

                foreach (var cand in cands)
                {
                    if (string.IsNullOrWhiteSpace(cand.EmbeddingBase64))
                    {
                        Console.WriteLine($"[VectorMatch] {cand.DocId} has no embedding, skip");
                        continue;
                    }

                    float[] dv;
                    try { dv = VectorCodec.DecodeBase64ToFloat32(cand.EmbeddingBase64); }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[VectorMatch] {cand.DocId} embedding decode failed: {ex.Message}");
                        continue;
                    }

                    if (dv.Length != _opt.OpenAi.EmbeddingDims)
                    {
                        Console.WriteLine($"[VectorMatch] {cand.DocId} dims mismatch: got {dv.Length} expected {_opt.OpenAi.EmbeddingDims}");
                        continue;
                    }

                    dv = VectorCodec.NormalizeL2(dv);
                    var sim = VectorCodec.CosineSimilarity(qvec, dv);
                    cand.Score = sim;
                    Console.WriteLine($"[VectorMatch] {cand.DocId} similarity={sim:F4}");
                }

                // Re-rank: composite scoring
                var inputCtx = needVector[i];
                foreach (var cand in cands.Where(x => x.Score is not null))
                {
                    var vectorSim = cand.Score!.Value;
                    var composite = CompositeScorer.Score(inputCtx.N, cand, vectorSim);
                    Console.WriteLine($"[Rerank] {cand.DocId} name={cand.Name?.Full} vectorSim={vectorSim:F4} composite={composite:F4}");
                    cand.Score = composite;
                }

                var ranked = cands
                    .Where(x => x.Score is not null)
                    .Where(x => x.Score!.Value >= _opt.Match.MinScore)
                    .OrderByDescending(x => x.Score)
                    .ToList();

                Console.WriteLine($"[VectorMatch] ranked after MinScore({_opt.Match.MinScore}) filter: {ranked.Count}");
                vectorByIndex[needVector[i].Index] = ranked;
             
            }
        }

        var results = new List<MatchResult>(inputs.Count);

        foreach (var ctx in ctxs)
        {
            if (exactByIndex.TryGetValue(ctx.Index, out var exactList) && exactList.Count > 0)
            {
                var trimmed = ApplyTopKRule(exactList);
                results.Add(BuildMatched(ctx, trimmed, method: "exact"));
                continue;
            }

            if (vectorByIndex.TryGetValue(ctx.Index, out var vecList) && vecList.Count > 0)
            {
                var trimmed = ApplyTopKRule(vecList);
                if (trimmed.Count == 0)
                {
                    results.Add(BuildNoMatch(ctx, "below_threshold"));
                    continue;
                }

                results.Add(BuildMatched(ctx, trimmed, method: "vector"));
                continue;
            }

            results.Add(BuildNoMatch(ctx, "no_match"));
        }

        return new MatchResponse { Results = results };
    }

    private List<ApiError> Validate(List<CustomerInput> inputs)
    {
        var errs = new List<ApiError>();

        for (var i = 0; i < inputs.Count; i++)
        {
            var c = inputs[i];

            var phoneAny = PhoneVariants(c.Phone).Any();
            var email = NormalizedInput.NormalizeEmail(c.Email);

            var hasPhoneOrEmail = phoneAny || email is not null;
            var hasName = !string.IsNullOrWhiteSpace(c.FullName) ||
                          (!string.IsNullOrWhiteSpace(c.FirstName) && !string.IsNullOrWhiteSpace(c.LastName));

            if (!hasPhoneOrEmail && !hasName)
            {
                errs.Add(new ApiError
                {
                    Index = i,
                    Code = "validation_failed",
                    Message = "phone ve email boşsa fullName veya (firstName+lastName) zorunlu"
                });
            }
        }

        return errs;
    }

    private async Task<Dictionary<int, List<Candidate>>> ExactMatchAsync(List<InputCtx> ctxs, CancellationToken ct)
    {
        var phones = ctxs
            .SelectMany(x => PhoneVariants(x.Input.Phone))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var emails = ctxs
            .Select(x => NormalizedInput.NormalizeEmail(x.Input.Email))
            .Where(x => x is not null)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var dict = new Dictionary<int, List<Candidate>>();
        if (phones.Length == 0 && emails.Length == 0)
            return dict;

        var cluster = await _cb.GetClusterAsync(ct);

        // DEBUG: parametreleri logla
        Console.WriteLine($"[ExactMatch] phones=[{string.Join(",", phones)}] emails=[{string.Join(",", emails)}]");

        var statement = $@"
SELECT META().id AS docId,
       c.mergeKey AS mergeKey,
       c.provider AS provider,
       c.customerNumber AS customerNumber,
       c.name AS name,
       c.birthdate AS birthdate,
       c.contact AS contact
FROM `{_opt.Couchbase.Bucket}`.`{_opt.Couchbase.Scope}`.`{_opt.Couchbase.Collection}` AS c
WHERE c.type = ""customer""
  AND (
    c.contact.phone IN $phones
    OR c.contact.email IN $emails
  )
LIMIT 50;
";

        Console.WriteLine($"[ExactMatch] statement={statement}");

        // CLUSTER-LEVEL query (scope-level değil)
        var res = await cluster.QueryAsync<QueryRowDto>(statement, opts =>
        {
            opts.Parameter("phones", phones);
            opts.Parameter("emails", emails);
            opts.Timeout(TimeSpan.FromSeconds(_opt.Couchbase.QueryTimeoutSeconds));
        });

        var rows = await res.Rows.ToListAsync(ct);
        Console.WriteLine($"[ExactMatch] rows.Count={rows.Count}");

        var candidates = new List<Candidate>(rows.Count);
        foreach (var row in rows)
        {
            try
            {
                Console.WriteLine($"[ExactMatch] row docId={row.DocId} phone={row.Contact?.Phone}");
                candidates.Add(Candidate.FromQueryDto(row));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ExactMatch] row parse error: {ex.Message}");
            }
        }

        Console.WriteLine($"[ExactMatch] parsed candidates={candidates.Count}");

        foreach (var ctx in ctxs)
        {
            var n = NormalizedInput.From(ctx.Input);

            var ranked = new List<Candidate>();
            foreach (var cand in candidates)
            {
                var s = ExactScore(n, cand);
                Console.WriteLine($"[ExactMatch] input={ctx.Input.Phone} vs cand={cand.Contact?.Phone} score={s}");
                if (s >= _opt.Match.MinScore)
                {
                    cand.Score = s;
                    ranked.Add(cand);
                }
            }

            if (ranked.Count == 0) continue;

            ranked = ranked
                .OrderByDescending(x => x.Score)
                .ToList();

            dict[ctx.Index] = Dedup(ranked);
        }

        return dict;
    }
    private async Task<List<Candidate>> FetchCandidatesByIdsAsync(List<string> docIds, CancellationToken ct)
    {
        var cluster = await _cb.GetClusterAsync(ct);
        var bucket = await cluster.BucketAsync(_opt.Couchbase.Bucket);
        var scope = await bucket.ScopeAsync(_opt.Couchbase.Scope);
        var coll = await scope.CollectionAsync(_opt.Couchbase.Collection);

        var throttler = new SemaphoreSlim(32, 32);

        // FTS ID'leri scope.collection prefix'li gelebilir, strip et
        var prefix = $"{_opt.Couchbase.Scope}.{_opt.Couchbase.Collection}.";
        var cleanIds = docIds.Select(id => id.StartsWith(prefix, StringComparison.Ordinal) ? id[prefix.Length..] : id).ToList();

        Console.WriteLine($"[KVFetch] raw first={docIds[0]}");
        Console.WriteLine($"[KVFetch] clean first={cleanIds[0]}");

        var tasks = cleanIds.Select(async id =>
        {
            await throttler.WaitAsync(ct);
            try
            {
                var get = await coll.GetAsync(id, opts => opts.Timeout(TimeSpan.FromSeconds(5)));
                var dto = get.ContentAs<QueryRowDto>();

                var cand = Candidate.FromQueryDto(dto!);
                cand.DocId = id; // KV'den gelen DTO'da docId yok, biz set ediyoruz
                return cand;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KVFetch] FAILED id={id} error={ex.GetType().Name}: {ex.Message}");
                return null;
            }
            finally
            {
                throttler.Release();
            }
        });

        var res = await Task.WhenAll(tasks);
        return res.Where(x => x is not null).Cast<Candidate>().ToList();
    }
    private static List<Candidate> Dedup(List<Candidate> items)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var outp = new List<Candidate>();
        foreach (var c in items)
        {
            var key = c.DocId ?? $"{c.MergeKey ?? ""}";
            if (seen.Add(key)) outp.Add(c);
        }
        return outp;
    }

    private List<Candidate> ApplyTopKRule(List<Candidate> ranked)
    {
        if (ranked.Count == 0) return ranked;

        var top1 = ranked[0].Score ?? 0;
        var take = top1 >= _opt.Match.Top1OnlyScore ? 1
            : top1 >= _opt.Match.Top3Score ? Math.Min(3, ranked.Count)
            : Math.Min(_opt.Match.MaxTopK, ranked.Count);

        return ranked.Take(take).ToList();
    }

    private MatchResult BuildMatched(InputCtx ctx, List<Candidate> ranked, string method)
    {
        var top = ranked[0];
        var enriched = Enrich(ctx.Input, top);

        return new MatchResult
        {
            Index = ctx.Index,
            Status = "matched",
            Method = method,
            TopScore = top.Score ?? 0,
            Returned = ranked.Count,
            Enriched = enriched,
            Matches = ranked.Select(c => new MatchItem
            {
                DocId = c.DocId,
                Score = c.Score ?? 0,
                MergeKey = c.MergeKey,
                Provider = c.Provider,
                CustomerNumber = c.CustomerNumber,
                Name = c.Name,
                Birthdate = c.Birthdate,
                Contact = c.Contact
            }).ToList()
        };
    }

    private static MatchResult BuildNoMatch(InputCtx ctx, string reason) => new()
    {
        Index = ctx.Index,
        Status = "no_match",
        Method = reason,
        TopScore = 0,
        Returned = 0,
        Enriched = ctx.Input,
        Matches = new List<MatchItem>()
    };

    private static CustomerInput Enrich(CustomerInput input, Candidate top)
    {
        var outp = input with { };

        if (!PhoneVariants(outp.Phone).Any() && !string.IsNullOrWhiteSpace(top.Contact?.Phone))
            outp = outp with { Phone = top.Contact!.Phone };

        if (NormalizedInput.NormalizeEmail(outp.Email) is null && !string.IsNullOrWhiteSpace(top.Contact?.Email))
            outp = outp with { Email = top.Contact!.Email };

        if (string.IsNullOrWhiteSpace(outp.FullName) && !string.IsNullOrWhiteSpace(top.Name?.Full))
            outp = outp with { FullName = top.Name!.Full };

        if (string.IsNullOrWhiteSpace(outp.FirstName) && !string.IsNullOrWhiteSpace(top.Name?.First))
            outp = outp with { FirstName = top.Name!.First };

        if (string.IsNullOrWhiteSpace(outp.LastName) && !string.IsNullOrWhiteSpace(top.Name?.Last))
            outp = outp with { LastName = top.Name!.Last };

        if (outp.Birthdate is null && !string.IsNullOrWhiteSpace(top.Birthdate) && DateTime.TryParse(top.Birthdate, out var dt))
            outp = outp with { Birthdate = dt.Date };

        return outp;
    }

    private static double ExactScore(NormalizedInput n, Candidate c)
    {
        var cPhone10 = NormalizedInput.NormalizePhone(c.Contact?.Phone);
        var cPhoneRaw = DigitsOnly(c.Contact?.Phone);

        var inPhone10 = n.Phone;
        var inPhoneRaw = DigitsOnly(n.RawPhone);

        var inEmail = n.Email;
        var cEmail = NormalizedInput.NormalizeEmail(c.Contact?.Email);

        var phoneMatch = (inPhoneRaw is not null && cPhoneRaw is not null && inPhoneRaw == cPhoneRaw)
                         || (inPhone10 is not null && cPhone10 is not null && inPhone10 == cPhone10);

        var emailMatch = inEmail is not null && cEmail is not null && inEmail == cEmail;

        if (phoneMatch)
        {
            if (emailMatch) return 1.00;
            return 0.98;
        }

        if (emailMatch) return 0.96;

        return 0.0;
    }

    private static string BuildQueryText(InputCtx ctx)
    {
        var n = ctx.N;
        var full = n.FullName ?? $"{n.FirstName} {n.LastName}".Trim();
        var emailDomain = n.Email is null ? "" : (n.Email.Split('@').LastOrDefault() ?? "");
        var bd = n.Birthdate is null ? "" : n.Birthdate.Value.ToString("yyyy-MM-dd");

        return $"name:{full} birthdate:{bd} hasPhone:{(PhoneVariants(n.RawPhone).Any() ? "1" : "0")} hasEmail:{(n.Email is null ? "0" : "1")} emailDomain:{emailDomain}".Trim();
    }

    private sealed record InputCtx(int Index, CustomerInput Input, NormalizedInput N);

    private static string? DigitsOnly(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var d = new string(phone.Where(char.IsDigit).ToArray());
        return d.Length == 0 ? null : d;
    }

    private static IEnumerable<string> PhoneVariants(string? phone)
    {
        var digits = DigitsOnly(phone);
        if (digits is null) yield break;

        yield return digits;

        var d = digits;
        if (d.Length == 11 && d.StartsWith("0")) d = d[1..];
        if (d.Length == 12 && d.StartsWith("90")) d = d[2..];
        if (d.Length > 10) d = d[^10..];

        yield return d;
    }
}

// ======================
// DTOs
// ======================
sealed record MatchRequest([property: JsonPropertyName("customers")] List<CustomerInput> Customers);

sealed record CustomerInput(
    [property: JsonPropertyName("firstName")] string? FirstName,
    [property: JsonPropertyName("lastName")] string? LastName,
    [property: JsonPropertyName("fullName")] string? FullName,
    [property: JsonPropertyName("birthdate")] DateTime? Birthdate,
    [property: JsonPropertyName("phone")] string? Phone,
    [property: JsonPropertyName("email")] string? Email
);

sealed class MatchResponse
{
    [JsonPropertyName("results")] public List<MatchResult> Results { get; set; } = new();
    [JsonPropertyName("errors")] public List<ApiError>? Errors { get; set; }
}

sealed class ApiError
{
    [JsonPropertyName("index")] public int Index { get; set; }
    [JsonPropertyName("code")] public string Code { get; set; } = default!;
    [JsonPropertyName("message")] public string Message { get; set; } = default!;
}

sealed class MatchResult
{
    [JsonPropertyName("index")] public int Index { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = default!;
    [JsonPropertyName("method")] public string Method { get; set; } = default!;
    [JsonPropertyName("topScore")] public double TopScore { get; set; }
    [JsonPropertyName("returned")] public int Returned { get; set; }
    [JsonPropertyName("enriched")] public CustomerInput Enriched { get; set; } = default!;
    [JsonPropertyName("matches")] public List<MatchItem> Matches { get; set; } = new();
}

sealed class MatchItem
{
    [JsonPropertyName("docId")] public string? DocId { get; set; }
    [JsonPropertyName("score")] public double Score { get; set; }
    [JsonPropertyName("mergeKey")] public string? MergeKey { get; set; }
    [JsonPropertyName("provider")] public JsonElement? Provider { get; set; }
    [JsonPropertyName("customerNumber")] public string? CustomerNumber { get; set; }
    [JsonPropertyName("name")] public NameInfo? Name { get; set; }
    [JsonPropertyName("birthdate")] public string? Birthdate { get; set; }
    [JsonPropertyName("contact")] public ContactInfo? Contact { get; set; }
}

sealed class NameInfo
{
    [JsonPropertyName("first")] public string? First { get; set; }
    [JsonPropertyName("last")] public string? Last { get; set; }
    [JsonPropertyName("full")] public string? Full { get; set; }
}

sealed class ContactInfo
{
    [JsonPropertyName("phone")] public string? Phone { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
}

sealed class Candidate
{
    public string? DocId { get; set; }
    public string? MergeKey { get; set; }
    public JsonElement? Provider { get; set; }
    public string? CustomerNumber { get; set; }
    public NameInfo? Name { get; set; }
    public string? Birthdate { get; set; }
    public ContactInfo? Contact { get; set; }
    public string? EmbeddingBase64 { get; set; }
    public double? Score { get; set; }
    public static Candidate FromQueryDto(QueryRowDto dto)
    {
        return new Candidate
        {
            DocId = dto.DocId,
            MergeKey = dto.MergeKey,
            Provider = dto.Provider is not null ? JsonSerializer.Deserialize<JsonElement>(dto.Provider.ToString()) : null,
            CustomerNumber = dto.CustomerNumber,
            Name = dto.Name is not null ? new NameInfo { First = dto.Name.First, Last = dto.Name.Last, Full = dto.Name.Full } : null,
            Birthdate = dto.Birthdate,
            Contact = dto.Contact is not null ? new ContactInfo { Phone = dto.Contact.Phone, Email = dto.Contact.Email } : null,
            EmbeddingBase64 = dto.Embedding
        };
    }
    public static Candidate FromQueryRow(JsonElement row)
    {
        if (row.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"Query row is not an object. ValueKind={row.ValueKind}");

        return new Candidate
        {
            DocId = row.TryGetProperty("docId", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null,
            MergeKey = row.TryGetProperty("mergeKey", out var mk) && mk.ValueKind == JsonValueKind.String ? mk.GetString() : null,
            Provider = row.TryGetProperty("provider", out var p) ? p : (JsonElement?)null,
            CustomerNumber = row.TryGetProperty("customerNumber", out var cn) && cn.ValueKind == JsonValueKind.String ? cn.GetString() : null,
            Name = row.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.Object ? JsonSerializer.Deserialize<NameInfo>(n) : null,
            Birthdate = row.TryGetProperty("birthdate", out var bd) && bd.ValueKind == JsonValueKind.String ? bd.GetString() : null,
            Contact = row.TryGetProperty("contact", out var c) && c.ValueKind == JsonValueKind.Object ? JsonSerializer.Deserialize<ContactInfo>(c) : null,
            EmbeddingBase64 = row.TryGetProperty("embedding", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null
        };
    }

    public static Candidate FromDoc(string docId, JsonElement doc)
    {
        if (doc.ValueKind != JsonValueKind.Object)
            return new Candidate { DocId = docId };

        return new Candidate
        {
            DocId = docId,
            MergeKey = doc.TryGetProperty("mergeKey", out var mk) && mk.ValueKind == JsonValueKind.String ? mk.GetString() : null,
            Provider = doc.TryGetProperty("provider", out var p) ? p : (JsonElement?)null,
            CustomerNumber = doc.TryGetProperty("customerNumber", out var cn) && cn.ValueKind == JsonValueKind.String ? cn.GetString() : null,
            Name = doc.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.Object ? JsonSerializer.Deserialize<NameInfo>(n) : null,
            Birthdate = doc.TryGetProperty("birthdate", out var bd) && bd.ValueKind == JsonValueKind.String ? bd.GetString() : null,
            Contact = doc.TryGetProperty("contact", out var c) && c.ValueKind == JsonValueKind.Object ? JsonSerializer.Deserialize<ContactInfo>(c) : null,
            EmbeddingBase64 = doc.TryGetProperty("embedding", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null
        };
    }
}

sealed class NormalizedInput
{
    public string? Phone { get; init; }        // local-10 normalized
    public string? RawPhone { get; init; }     // original
    public string? Email { get; init; }
    public string? FullName { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public DateTime? Birthdate { get; init; }

    public static NormalizedInput From(CustomerInput c) => new()
    {
        Phone = NormalizePhone(c.Phone),
        RawPhone = c.Phone,
        Email = NormalizeEmail(c.Email),
        FullName = string.IsNullOrWhiteSpace(c.FullName) ? null : c.FullName.Trim(),
        FirstName = string.IsNullOrWhiteSpace(c.FirstName) ? null : c.FirstName.Trim(),
        LastName = string.IsNullOrWhiteSpace(c.LastName) ? null : c.LastName.Trim(),
        Birthdate = c.Birthdate
    };

    public static string? NormalizeEmail(string? email)
        => string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    public static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return null;

        if (digits.Length == 11 && digits.StartsWith("0")) digits = digits[1..];
        if (digits.Length == 12 && digits.StartsWith("90")) digits = digits[2..];
        if (digits.Length > 10) digits = digits[^10..];

        return digits;
    }
}

// ======================
// Couchbase + OpenAI + FTS Clients
// ======================
sealed class CouchbaseProvider
{
    private readonly AppOptions _opt;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private ICluster? _cluster;

    public CouchbaseProvider(AppOptions opt) => _opt = opt;

    public async Task<ICluster> GetClusterAsync(CancellationToken ct)
    {
        if (_cluster is not null) return _cluster;

        await _lock.WaitAsync(ct);
        try
        {
            if (_cluster is not null) return _cluster;

            _cluster = await Cluster.ConnectAsync(_opt.Couchbase.ConnectionString,
                new ClusterOptions
                {
                    KvTimeout = TimeSpan.FromSeconds(10),
                    KvConnectTimeout = TimeSpan.FromSeconds(30)
                }.WithCredentials(_opt.Couchbase.Username, _opt.Couchbase.Password));

            return _cluster;
        }
        finally
        {
            _lock.Release();
        }
    }
}

sealed class OpenAiClient
{
    private readonly HttpClient _http;
    public OpenAiClient(HttpClient http) => _http = http;

    public async Task<float[][]> CreateEmbeddingsAsync(string[] inputs, string model, int dims, CancellationToken ct)
    {
        const int maxAttempts = 6;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var payload = JsonSerializer.Serialize(new { model, input = inputs, dimensions = dims, encoding_format = "float" });
                using var resp = await _http.PostAsync("/v1/embeddings", new StringContent(payload, Encoding.UTF8, "application/json"), ct);

                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    var data = doc.RootElement.GetProperty("data");

                    var res = new float[data.GetArrayLength()][];
                    for (var i = 0; i < res.Length; i++)
                    {
                        var emb = data[i].GetProperty("embedding");
                        var vec = new float[emb.GetArrayLength()];
                        for (var j = 0; j < vec.Length; j++) vec[j] = emb[j].GetSingle();
                        res[i] = vec;
                    }
                    return res;
                }

                if (attempt < maxAttempts && (resp.StatusCode == HttpStatusCode.TooManyRequests || (int)resp.StatusCode >= 500))
                {
                    await Task.Delay(250 * attempt + Random.Shared.Next(0, 200), ct);
                    continue;
                }

                var body = await resp.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"OpenAI embeddings failed status={(int)resp.StatusCode}, body={body}");
            }
            catch (OperationCanceledException) { throw; }
            catch when (attempt < maxAttempts)
            {
                await Task.Delay(250 * attempt + Random.Shared.Next(0, 200), ct);
            }
        }

        throw new InvalidOperationException("OpenAI embeddings failed after retries");
    }
}

sealed class FtsClient
{
    private readonly HttpClient _http;
    public FtsClient(HttpClient http) => _http = http;

    public async Task<List<string>> KnnSearchDocIdsAsync(string indexName, string field, string vectorBase64, int k, CancellationToken ct)
    {
        var payload = new
        {
            query = new { match_none = new { } },
            knn = new[]
            {
                new Dictionary<string, object?>
                {
                    ["field"] = field,
                    ["k"] = k,
                    ["vector_base64"] = vectorBase64
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);

        using var resp = await _http.PostAsync($"api/index/{indexName}/query",
            new StringContent(json, Encoding.UTF8, "application/json"), ct);

        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"FTS query failed status={(int)resp.StatusCode} body={body}");
        }

        var resJson = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(resJson);

        var hits = doc.RootElement.GetProperty("hits");
        var ids = new List<string>(hits.GetArrayLength());

        foreach (var h in hits.EnumerateArray())
        {
            if (h.TryGetProperty("id", out var idEl))
            {
                var id = idEl.GetString();
                if (!string.IsNullOrWhiteSpace(id)) ids.Add(id);
            }
        }

        return ids;
    }
}

static class VectorCodec
{
    public static float[] NormalizeL2(float[] v)
    {
        double sum = 0;
        for (var i = 0; i < v.Length; i++) sum += (double)v[i] * v[i];
        var norm = Math.Sqrt(sum);
        if (norm <= 0) return v;

        var outV = new float[v.Length];
        for (var i = 0; i < v.Length; i++) outV[i] = (float)(v[i] / norm);
        return outV;
    }

    public static double CosineSimilarity(float[] aNorm, float[] bNorm)
    {
        double dot = 0;
        for (var i = 0; i < aNorm.Length; i++) dot += (double)aNorm[i] * bNorm[i];
        if (dot < 0) dot = 0;
        if (dot > 1) dot = 1;
        return dot;
    }

    public static string ToBase64LittleEndianFloat32(float[] v)
    {
        var bytes = new byte[v.Length * 4];
        for (var i = 0; i < v.Length; i++)
        {
            var bits = BitConverter.SingleToInt32Bits(v[i]);
            bytes[i * 4 + 0] = (byte)(bits & 0xFF);
            bytes[i * 4 + 1] = (byte)((bits >> 8) & 0xFF);
            bytes[i * 4 + 2] = (byte)((bits >> 16) & 0xFF);
            bytes[i * 4 + 3] = (byte)((bits >> 24) & 0xFF);
        }
        return Convert.ToBase64String(bytes);
    }

    public static float[] DecodeBase64ToFloat32(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        if (bytes.Length % 4 != 0) return Array.Empty<float>();

        var len = bytes.Length / 4;
        var v = new float[len];

        for (var i = 0; i < len; i++)
        {
            var b0 = bytes[i * 4 + 0];
            var b1 = bytes[i * 4 + 1];
            var b2 = bytes[i * 4 + 2];
            var b3 = bytes[i * 4 + 3];

            var bits = b0 | (b1 << 8) | (b2 << 16) | (b3 << 24);
            v[i] = BitConverter.Int32BitsToSingle(bits);
        }

        return v;
    }
}

// ======================
// Options
// ======================
sealed class AppOptions
{
    public required CouchbaseOptions Couchbase { get; init; }
    public required OpenAiOptions OpenAi { get; init; }
    public required MatchOptions Match { get; init; }

    public static AppOptions FromEnv()
    {
        var cb = new CouchbaseOptions
        {
            ConnectionString = EnvReq("CB_CONN"),
            Username = EnvReq("CB_USER"),
            Password = EnvReq("CB_PASS"),
            Bucket = EnvReq("CB_BUCKET"),
            Scope = EnvOpt("CB_SCOPE") ?? "crm",
            Collection = EnvOpt("CB_COLLECTION") ?? "customers_unified",
            FtsBaseUrl = EnvReq("CB_FTS_BASEURL"),
            FtsIndexName = EnvOpt("CB_FTS_INDEX") ?? "crm_customers.crm.customers-fts-vector",
            FtsTimeoutSeconds = EnvInt("CB_FTS_TIMEOUT_SECONDS", 10),
            QueryTimeoutSeconds = EnvInt("CB_QUERY_TIMEOUT_SECONDS", 60)
        };

        var oa = new OpenAiOptions
        {
            ApiKey = EnvReq("OPENAI_API_KEY"),
            BaseUrl = EnvOpt("OPENAI_BASE_URL") ?? "https://api.openai.com",
            EmbeddingModel = EnvOpt("OPENAI_EMBEDDING_MODEL") ?? "text-embedding-3-small",
            EmbeddingDims = EnvInt("OPENAI_EMBEDDING_DIMS", 512),
            TimeoutSeconds = EnvInt("OPENAI_TIMEOUT_SECONDS", 20)
        };

        var m = new MatchOptions
        {
            MinScore = EnvDouble("MATCH_MIN_SCORE", 0.70),
            Top1OnlyScore = EnvDouble("MATCH_TOP1_ONLY_SCORE", 0.95),
            Top3Score = EnvDouble("MATCH_TOP3_SCORE", 0.85),
            MaxTopK = EnvInt("MATCH_MAX_TOPK", 5),
            VectorCandidatePool = EnvInt("VECTOR_CANDIDATE_POOL", 20)
        };

        return new AppOptions { Couchbase = cb, OpenAi = oa, Match = m };
    }

    private static string EnvReq(string k) => Environment.GetEnvironmentVariable(k) ?? throw new InvalidOperationException($"Missing env var: {k}");
    private static string? EnvOpt(string k) => Environment.GetEnvironmentVariable(k);
    private static int EnvInt(string k, int d)
        => int.TryParse(EnvOpt(k), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : d;
    private static double EnvDouble(string k, double d)
        => double.TryParse(EnvOpt(k), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : d;
}

sealed class CouchbaseOptions
{
    public int QueryTimeoutSeconds { get; set; } = 60;

    public string ConnectionString { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string Bucket { get; set; } = default!;
    public string Scope { get; set; } = default!;
    public string Collection { get; set; } = default!;

    public string FtsBaseUrl { get; set; } = default!;
    public string FtsIndexName { get; set; } = default!;
    public int FtsTimeoutSeconds { get; set; }
}

sealed class OpenAiOptions
{
    public string ApiKey { get; set; } = default!;
    public string BaseUrl { get; set; } = default!;
    public string EmbeddingModel { get; set; } = default!;
    public int EmbeddingDims { get; set; }
    public int TimeoutSeconds { get; set; }
}

sealed class MatchOptions
{
    public double MinScore { get; set; }
    public double Top1OnlyScore { get; set; }
    public double Top3Score { get; set; }
    public int MaxTopK { get; set; }
    public int VectorCandidatePool { get; set; }
}
sealed class QueryRowDto
{
    [Newtonsoft.Json.JsonProperty("docId")] public string? DocId { get; set; }
    [Newtonsoft.Json.JsonProperty("mergeKey")] public string? MergeKey { get; set; }
    [Newtonsoft.Json.JsonProperty("provider")] public Newtonsoft.Json.Linq.JToken? Provider { get; set; }
    [Newtonsoft.Json.JsonProperty("customerNumber")] public string? CustomerNumber { get; set; }
    [Newtonsoft.Json.JsonProperty("name")] public QueryNameDto? Name { get; set; }
    [Newtonsoft.Json.JsonProperty("birthdate")] public string? Birthdate { get; set; }
    [Newtonsoft.Json.JsonProperty("contact")] public QueryContactDto? Contact { get; set; }
    [Newtonsoft.Json.JsonProperty("embedding")] public string? Embedding { get; set; }
}

sealed class QueryNameDto
{
    [Newtonsoft.Json.JsonProperty("first")] public string? First { get; set; }
    [Newtonsoft.Json.JsonProperty("last")] public string? Last { get; set; }
    [Newtonsoft.Json.JsonProperty("full")] public string? Full { get; set; }
}

sealed class QueryContactDto
{
    [Newtonsoft.Json.JsonProperty("phone")] public string? Phone { get; set; }
    [Newtonsoft.Json.JsonProperty("email")] public string? Email { get; set; }
}
static class JaroWinkler
{
    public static double Similarity(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return 0.0;

        a = a.Trim().ToLowerInvariant();
        b = b.Trim().ToLowerInvariant();

        if (a == b) return 1.0;

        var jaro = JaroSimilarity(a, b);

        // Winkler prefix bonus (max 4 chars)
        var prefixLen = 0;
        var maxPrefix = Math.Min(4, Math.Min(a.Length, b.Length));
        for (var i = 0; i < maxPrefix; i++)
        {
            if (a[i] == b[i]) prefixLen++;
            else break;
        }

        const double scalingFactor = 0.1;
        return jaro + (prefixLen * scalingFactor * (1.0 - jaro));
    }

    private static double JaroSimilarity(string a, string b)
    {
        if (a.Length == 0 && b.Length == 0) return 1.0;
        if (a.Length == 0 || b.Length == 0) return 0.0;

        var matchWindow = Math.Max(0, Math.Max(a.Length, b.Length) / 2 - 1);

        var aMatched = new bool[a.Length];
        var bMatched = new bool[b.Length];

        var matches = 0;
        var transpositions = 0;

        for (var i = 0; i < a.Length; i++)
        {
            var start = Math.Max(0, i - matchWindow);
            var end = Math.Min(i + matchWindow + 1, b.Length);

            for (var j = start; j < end; j++)
            {
                if (bMatched[j] || a[i] != b[j]) continue;
                aMatched[i] = true;
                bMatched[j] = true;
                matches++;
                break;
            }
        }

        if (matches == 0) return 0.0;

        var k = 0;
        for (var i = 0; i < a.Length; i++)
        {
            if (!aMatched[i]) continue;
            while (!bMatched[k]) k++;
            if (a[i] != b[k]) transpositions++;
            k++;
        }

        return ((double)matches / a.Length
              + (double)matches / b.Length
              + (matches - transpositions / 2.0) / matches) / 3.0;
    }
}
static class CompositeScorer
{
    public static double Score(NormalizedInput input, Candidate cand, double vectorSim)
    {
        // Ağırlıklar: input'ta hangi field'lar varsa onlara göre dinamik
        var components = new List<(double score, double weight)>();

        // Vector similarity her zaman dahil
        components.Add((vectorSim, 0.25));

        // Name similarity
        var inputFull = input.FullName ?? $"{input.FirstName} {input.LastName}".Trim();
        var candFull = cand.Name?.Full ?? $"{cand.Name?.First} {cand.Name?.Last}".Trim();

        if (!string.IsNullOrWhiteSpace(inputFull))
        {
            var nameSim = JaroWinkler.Similarity(inputFull, candFull);

            // First+Last ayrı ayrı da dene, en yüksek skoru al
            if (input.FirstName is not null && cand.Name?.First is not null)
            {
                var firstSim = JaroWinkler.Similarity(input.FirstName, cand.Name.First);
                var lastSim = JaroWinkler.Similarity(input.LastName, cand.Name.Last);
                var partialSim = (firstSim * 0.5) + (lastSim * 0.5);
                nameSim = Math.Max(nameSim, partialSim);
            }

            components.Add((nameSim, 0.45));
        }

        // Phone match
        var inPhone = NormalizedInput.NormalizePhone(input.RawPhone);
        var candPhone = NormalizedInput.NormalizePhone(cand.Contact?.Phone);
        if (inPhone is not null)
        {
            var phoneSim = (inPhone == candPhone) ? 1.0 : 0.0;
            components.Add((phoneSim, 0.15));
        }

        // Email match
        if (input.Email is not null)
        {
            var candEmail = NormalizedInput.NormalizeEmail(cand.Contact?.Email);
            var emailSim = (input.Email == candEmail) ? 1.0 : 0.0;
            components.Add((emailSim, 0.10));
        }

        // Birthdate match
        if (input.Birthdate is not null && !string.IsNullOrWhiteSpace(cand.Birthdate))
        {
            if (DateTime.TryParse(cand.Birthdate, out var candBd))
            {
                var bdSim = (input.Birthdate.Value.Date == candBd.Date) ? 1.0 : 0.0;
                components.Add((bdSim, 0.05));
            }
        }

        // Ağırlıkları normalize et (input'ta olmayan field'ların ağırlığını dağıt)
        var totalWeight = components.Sum(c => c.weight);
        if (totalWeight <= 0) return vectorSim;

        var composite = components.Sum(c => c.score * (c.weight / totalWeight));

        return Math.Max(0, Math.Min(1, composite));
    }
}