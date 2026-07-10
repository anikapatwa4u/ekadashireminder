using EkadashiReminder.Models;

namespace EkadashiReminder.Services;

public class ICalParser
{
    // The real calendar files use "Ekadasi" (one h). We match both spellings for safety.
    private static bool IsEkadashiFastingEvent(string summary)
        => summary.Contains("Ekadasi", StringComparison.OrdinalIgnoreCase)
        || summary.Contains("Ekadashi", StringComparison.OrdinalIgnoreCase);

    // Strip "Fasting for " prefix and trailing break-fast / daylight info for a clean name.
    private static string CleanName(string summary)
    {
        var name = summary;
        if (name.StartsWith("Fasting for ", StringComparison.OrdinalIgnoreCase))
            name = name["Fasting for ".Length..].Trim();
        // Normalise "Ekadasi" ? "Ekadashi" for consistency
        name = name.Replace("Ekadasi", "Ekadashi", StringComparison.OrdinalIgnoreCase);
        return name;
    }

    public List<EkadashiEvent> Parse(string icalContent, string location)
    {
        var events      = new List<EkadashiEvent>();
        // Key = date of the "Break fast" event (Ekadashi date + 1), Value = parsed time window
        var breakFasts  = new Dictionary<DateOnly, string>();

        var lines = icalContent.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

        bool inEvent = false;
        string summary = string.Empty;
        string description = string.Empty;
        DateOnly date = default;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            if (line == "BEGIN:VEVENT")
            {
                inEvent = true;
                summary = string.Empty;
                description = string.Empty;
                date = default;
                continue;
            }

            if (line == "END:VEVENT")
            {
                inEvent = false;
                if (date != default && !string.IsNullOrWhiteSpace(summary))
                {
                    if (IsEkadashiFastingEvent(summary))
                    {
                        events.Add(new EkadashiEvent
                        {
                            Name        = CleanName(summary),
                            Date        = date,
                            Location    = location,
                            Description = description,
                            IsCustom    = false
                        });
                    }
                    else if (summary.StartsWith("Break fast", StringComparison.OrdinalIgnoreCase))
                    {
                        // Strip "Break fast " prefix and trailing " LT" to get the time window.
                        var timeWindow = summary["Break fast".Length..].Trim();
                        if (timeWindow.EndsWith(" LT", StringComparison.OrdinalIgnoreCase))
                            timeWindow = timeWindow[..^3].Trim();
                        breakFasts[date] = timeWindow;
                    }
                }
                continue;
            }

            if (!inEvent) continue;

            if (line.StartsWith("SUMMARY:"))
                summary = line["SUMMARY:".Length..].Trim();
            else if (line.StartsWith("DESCRIPTION:"))
                description = line["DESCRIPTION:".Length..].Trim();
            else if (line.StartsWith("DTSTART;VALUE=DATE:") || line.StartsWith("DTSTART:"))
            {
                var dateStr = line[(line.IndexOf(':') + 1)..].Trim();
                if (dateStr.Length >= 8
                    && int.TryParse(dateStr[..4], out int year)
                    && int.TryParse(dateStr[4..6], out int month)
                    && int.TryParse(dateStr[6..8], out int day))
                {
                    date = new DateOnly(year, month, day);
                }
            }
        }

        // Link each Ekadashi event to its break-fast window (stored on date + 1).
        foreach (var ev in events)
        {
            var breakFastDate = ev.Date.AddDays(1);
            if (breakFasts.TryGetValue(breakFastDate, out var window))
                ev.BreakFastTime = window;
        }

        return events.OrderBy(e => e.Date).ToList();
    }
}
