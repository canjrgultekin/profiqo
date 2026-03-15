namespace Profiqo.Whatsapp.Automation.Worker;

internal static class TimeZones
{
    public static readonly TimeZoneInfo Istanbul = Find("Europe/Istanbul", "Turkey Standard Time");

    private static TimeZoneInfo Find(params string[] ids)
    {
        foreach (var id in ids)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch { }
        }
        return TimeZoneInfo.Utc;
    }
}