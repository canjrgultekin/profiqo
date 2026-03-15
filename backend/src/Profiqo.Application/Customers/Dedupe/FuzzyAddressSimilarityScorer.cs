// Path: backend/src/Profiqo.Application/Customers/Dedupe/FuzzyAddressSimilarityScorer.cs
using System.Globalization;
using System.Text;

namespace Profiqo.Application.Customers.Dedupe;

public sealed class FuzzyAddressSimilarityScorer : ICustomerSimilarityScorer
{
    public Task<double> ScoreAsync(CustomerDuplicateCandidateDto a, CustomerDuplicateCandidateDto b, CancellationToken ct)
    {
        var nameOk = NormalizeName(a.FirstName, a.LastName) == NormalizeName(b.FirstName, b.LastName);
        if (!nameOk) return Task.FromResult(0d);

        var pa = NormalizePhone(a.ShippingAddress?.Phone ?? a.BillingAddress?.Phone);
        var pb = NormalizePhone(b.ShippingAddress?.Phone ?? b.BillingAddress?.Phone);
        var phoneMatch = !string.IsNullOrWhiteSpace(pa) && pa == pb;

        var sa = NormalizeAddress(a.ShippingAddress) ?? NormalizeAddress(a.BillingAddress) ?? "";
        var sb = NormalizeAddress(b.ShippingAddress) ?? NormalizeAddress(b.BillingAddress) ?? "";

        if (string.IsNullOrWhiteSpace(sa) || string.IsNullOrWhiteSpace(sb))
            return Task.FromResult(phoneMatch ? 0.95d : 0.2d); // name matches but no address info

        var jw = JaroWinkler(sa, sb);

        var bonus = 0d;
        bonus += EqBonus(a, b, x => x.City, 0.08);
        bonus += EqBonus(a, b, x => x.District, 0.06);
        bonus += EqBonus(a, b, x => x.PostalCode, 0.05);
        bonus += EqBonus(a, b, x => x.Country, 0.03);

        if (phoneMatch) bonus += 0.25;

        var score = Math.Min(1.0, jw + bonus);

        // guardrail: if city differs strongly, clamp
        var ca = NormalizeToken(a.ShippingAddress?.City ?? a.BillingAddress?.City);
        var cb = NormalizeToken(b.ShippingAddress?.City ?? b.BillingAddress?.City);
        if (!phoneMatch && !string.IsNullOrWhiteSpace(ca) && !string.IsNullOrWhiteSpace(cb) && ca != cb && score > 0.85)
            score = 0.85;

        return Task.FromResult(score);
    }

    private static double EqBonus(CustomerDuplicateCandidateDto a, CustomerDuplicateCandidateDto b, Func<AddressSnapshotDto, string?> selector, double w)
    {
        var av = NormalizeToken(selector(a.ShippingAddress ?? a.BillingAddress ?? new AddressSnapshotDto(null, null, null, null, null, null, null)));
        var bv = NormalizeToken(selector(b.ShippingAddress ?? b.BillingAddress ?? new AddressSnapshotDto(null, null, null, null, null, null, null)));
        if (!string.IsNullOrWhiteSpace(av) && av == bv) return w;
        return 0d;
    }

    private static string NormalizeName(string? first, string? last)
        => NormalizeToken($"{first ?? ""} {last ?? ""}");

    private static string? NormalizeAddress(AddressSnapshotDto? a)
    {
        if (a is null) return null;

        var sb = new StringBuilder();
        sb.Append(a.Country).Append(' ')
          .Append(a.City).Append(' ')
          .Append(a.District).Append(' ')
          .Append(a.PostalCode).Append(' ')
          .Append(a.AddressLine1).Append(' ')
          .Append(a.AddressLine2);

        return NormalizeToken(sb.ToString());
    }

    private static string NormalizeToken(string? s)
    {
        s ??= "";
        s = s.Trim().ToLowerInvariant();

        // Turkish char fold
        s = s.Replace('ı', 'i')
             .Replace('ş', 's')
             .Replace('ğ', 'g')
             .Replace('ü', 'u')
             .Replace('ö', 'o')
             .Replace('ç', 'c');

        // remove punctuation
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
                sb.Append(ch);
        }

        // collapse spaces
        var t = sb.ToString();
        while (t.Contains("  ", StringComparison.Ordinal)) t = t.Replace("  ", " ", StringComparison.Ordinal);
        return t.Trim();
    }

    private static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "";

        // keep digits only
        var digits = new System.Text.StringBuilder(phone.Length);
        foreach (var ch in phone)
        {
            if (char.IsDigit(ch)) digits.Append(ch);
        }

        var d = digits.ToString();
        if (d.Length == 0) return "";

        // TR heuristics
        // 0532... (11 digits with leading 0) -> +90...
        if (d.Length == 11 && d.StartsWith("0", StringComparison.Ordinal))
            d = d.Substring(1);

        if (d.Length == 10)
            return "+90" + d;

        if (d.StartsWith("90", StringComparison.Ordinal))
            return "+" + d;

        // fallback
        return d.StartsWith("+", StringComparison.Ordinal) ? d : "+" + d;
    }


    // Jaro-Winkler
    private static double JaroWinkler(string s1, string s2)
    {
        if (s1 == s2) return 1.0;
        if (s1.Length == 0 || s2.Length == 0) return 0.0;

        var matchDistance = Math.Max(s1.Length, s2.Length) / 2 - 1;

        var s1Matches = new bool[s1.Length];
        var s2Matches = new bool[s2.Length];

        var matches = 0;
        for (var i = 0; i < s1.Length; i++)
        {
            var start = Math.Max(0, i - matchDistance);
            var end = Math.Min(i + matchDistance + 1, s2.Length);

            for (var j = start; j < end; j++)
            {
                if (s2Matches[j]) continue;
                if (s1[i] != s2[j]) continue;
                s1Matches[i] = true;
                s2Matches[j] = true;
                matches++;
                break;
            }
        }

        if (matches == 0) return 0.0;

        var t = 0;
        var k = 0;
        for (var i = 0; i < s1.Length; i++)
        {
            if (!s1Matches[i]) continue;
            while (!s2Matches[k]) k++;
            if (s1[i] != s2[k]) t++;
            k++;
        }

        var transpositions = t / 2.0;

        var m = matches;
        var jaro = (m / (double)s1.Length + m / (double)s2.Length + (m - transpositions) / m) / 3.0;

        // Winkler prefix
        var prefix = 0;
        for (var i = 0; i < Math.Min(4, Math.Min(s1.Length, s2.Length)); i++)
        {
            if (s1[i] == s2[i]) prefix++;
            else break;
        }

        return jaro + prefix * 0.1 * (1 - jaro);
    }
}
