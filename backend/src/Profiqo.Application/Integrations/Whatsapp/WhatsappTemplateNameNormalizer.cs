using System.Text;

namespace Profiqo.Application.Integrations.Whatsapp;

internal static class WhatsappTemplateNameNormalizer
{
    public static string Normalize(string input)
    {
        var s = (input ?? string.Empty).Trim().ToLowerInvariant();
        if (s.Length == 0) throw new ArgumentException("Template name required.", nameof(input));

        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (ch >= 'a' && ch <= 'z') { sb.Append(ch); continue; }
            if (ch >= '0' && ch <= '9') { sb.Append(ch); continue; }
            if (ch == '_' || ch == ' ' || ch == '-' || ch == '.') { sb.Append('_'); continue; }
        }

        var raw = sb.ToString();
        while (raw.Contains("__", StringComparison.Ordinal)) raw = raw.Replace("__", "_", StringComparison.Ordinal);
        raw = raw.Trim('_');

        if (raw.Length == 0) throw new ArgumentException("Template name invalid after normalization.", nameof(input));

        if (!(raw[0] >= 'a' && raw[0] <= 'z'))
            raw = "t_" + raw;

        if (raw.Length > 512) raw = raw[..512];
        return raw;
    }
}