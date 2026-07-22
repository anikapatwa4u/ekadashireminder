namespace EkadashiReminder.Services;

/// <summary>
/// A lightweight, framework-independent representation of a parsed iCal event.
/// Kept free of any .NET MAUI dependencies so the parsing logic can be unit tested
/// in a plain <c>net10.0</c> test project.
/// </summary>
public sealed record ParsedIcalEvent(
    string Name,
    DateOnly Date,
    string Description,
    bool IsEkadashiFast,
    bool IsBreakFast,
    string? BreakFastWindow);

/// <summary>
/// Pure iCal (RFC 5545) parsing for Vaishnava calendar files. This class intentionally
/// has <b>no</b> dependency on MAUI so it is fully unit-testable.
///
/// Reliability notes / fixes handled here:
///  * RFC 5545 line folding: continuation lines (starting with a space or tab) are
///    unfolded before processing. The previous implementation trimmed every line which
///    silently corrupted folded SUMMARY / DESCRIPTION values.
///  * Robust date parsing for both <c>DTSTART;VALUE=DATE:20260101</c> and
///    <c>DTSTART;TZID=...:20260101T060000</c> forms.
///  * Break-fast (Paaran) windows are linked to the Ekadashi event on the previous day.
/// </summary>
public static class ICalParserCore
{
    public static bool IsEkadashiFastingEvent(string summary)
        => summary.Contains("Ekadasi", StringComparison.OrdinalIgnoreCase)
        || summary.Contains("Ekadashi", StringComparison.OrdinalIgnoreCase);

    public static bool IsBreakFastEvent(string summary)
        => summary.StartsWith("Break fast", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Strips the "Fasting for " prefix and normalises the "Ekadasi" spelling to
    /// "Ekadashi" for a clean, user-facing display name.
    /// </summary>
    public static string CleanName(string summary)
    {
        var name = summary;
        if (name.StartsWith("Fasting for ", StringComparison.OrdinalIgnoreCase))
            name = name["Fasting for ".Length..].Trim();
        name = name.Replace("Ekadasi", "Ekadashi", StringComparison.OrdinalIgnoreCase);
        return name;
    }

    /// <summary>
    /// Extracts the time window from a "Break fast ..." summary, dropping the trailing
    /// " LT" (local time) suffix. Returns an empty string when nothing follows.
    /// </summary>
    public static string ExtractBreakFastWindow(string summary)
    {
        if (!IsBreakFastEvent(summary)) return string.Empty;
        var window = summary["Break fast".Length..].Trim();
        if (window.EndsWith(" LT", StringComparison.OrdinalIgnoreCase))
            window = window[..^3].Trim();
        return window;
    }

    /// <summary>
    /// Parses a DTSTART value (the part after the last colon) into a <see cref="DateOnly"/>.
    /// Handles both plain dates (<c>20260101</c>) and date-times (<c>20260101T060000</c>).
    /// Returns null when the value cannot be parsed.
    /// </summary>
    public static DateOnly? ParseIcalDate(string value)
    {
        var v = value.Trim();
        if (v.Length < 8) return null;
        if (int.TryParse(v.AsSpan(0, 4), out int year)
            && int.TryParse(v.AsSpan(4, 2), out int month)
            && int.TryParse(v.AsSpan(6, 2), out int day)
            && month is >= 1 and <= 12
            && day is >= 1 and <= 31)
        {
            try { return new DateOnly(year, month, day); }
            catch (ArgumentOutOfRangeException) { return null; }
        }
        return null;
    }

    /// <summary>
    /// Unfolds RFC 5545 folded lines: any line beginning with a space or tab is a
    /// continuation of the previous line and must be appended to it (with the leading
    /// whitespace removed).
    /// </summary>
    public static IReadOnlyList<string> Unfold(string icalContent)
    {
        var normalized = icalContent.Replace("\r\n", "\n").Replace("\r", "\n");
        var rawLines = normalized.Split('\n');
        var result = new List<string>(rawLines.Length);

        foreach (var raw in rawLines)
        {
            if (raw.Length > 0 && (raw[0] == ' ' || raw[0] == '\t') && result.Count > 0)
            {
                // Continuation of the previous logical line.
                result[^1] += raw[1..];
            }
            else
            {
                result.Add(raw);
            }
        }
        return result;
    }

    /// <summary>
    /// Parses the full iCal content and returns every relevant event (Ekadashi fasting
    /// events and break-fast markers), with break-fast windows already linked onto the
    /// matching Ekadashi event.
    /// </summary>
    public static List<ParsedIcalEvent> Parse(string icalContent)
    {
        var events = new List<ParsedIcalEvent>();
        // Key = date of the "Break fast" event (Ekadashi date + 1) -> parsed time window.
        var breakFasts = new Dictionary<DateOnly, string>();

        bool inEvent = false;
        string summary = string.Empty;
        string description = string.Empty;
        DateOnly? date = null;

        foreach (var logicalLine in Unfold(icalContent))
        {
            // Only trim trailing whitespace; leading whitespace was already handled by Unfold.
            var line = logicalLine.TrimEnd();

            if (line == "BEGIN:VEVENT")
            {
                inEvent = true;
                summary = string.Empty;
                description = string.Empty;
                date = null;
                continue;
            }

            if (line == "END:VEVENT")
            {
                inEvent = false;
                if (date is { } d && !string.IsNullOrWhiteSpace(summary))
                {
                    if (IsEkadashiFastingEvent(summary))
                    {
                        events.Add(new ParsedIcalEvent(
                            Name: CleanName(summary),
                            Date: d,
                            Description: description,
                            IsEkadashiFast: true,
                            IsBreakFast: false,
                            BreakFastWindow: null));
                    }
                    else if (IsBreakFastEvent(summary))
                    {
                        breakFasts[d] = ExtractBreakFastWindow(summary);
                    }
                }
                continue;
            }

            if (!inEvent) continue;

            if (line.StartsWith("SUMMARY:", StringComparison.Ordinal))
                summary = Unescape(line["SUMMARY:".Length..]);
            else if (line.StartsWith("DESCRIPTION:", StringComparison.Ordinal))
                description = Unescape(line["DESCRIPTION:".Length..]);
            else if (line.StartsWith("DTSTART", StringComparison.Ordinal))
            {
                var colon = line.IndexOf(':');
                if (colon >= 0)
                    date = ParseIcalDate(line[(colon + 1)..]);
            }
        }

        // Link each Ekadashi event to its break-fast window (stored on date + 1).
        var linked = new List<ParsedIcalEvent>(events.Count);
        foreach (var ev in events)
        {
            var breakFastDate = ev.Date.AddDays(1);
            linked.Add(breakFasts.TryGetValue(breakFastDate, out var window) && !string.IsNullOrEmpty(window)
                ? ev with { BreakFastWindow = window }
                : ev);
        }

        return linked.OrderBy(e => e.Date).ToList();
    }

    /// <summary>
    /// Returns the upcoming Ekadashi fasting events (on or after <paramref name="today"/>),
    /// sorted by date. This is the pure logic behind the "Next Ekadashi" featured card.
    /// Kept deterministic and side-effect-free so the correct next date is always chosen
    /// regardless of load ordering.
    /// </summary>
    public static IReadOnlyList<ParsedIcalEvent> GetUpcoming(
        IEnumerable<ParsedIcalEvent> events, DateOnly today)
        => events
            .Where(e => e.IsEkadashiFast && e.Date >= today)
            .OrderBy(e => e.Date)
            .ToList();

    /// <summary>Returns the single next upcoming Ekadashi fasting event, or null when none.</summary>
    public static ParsedIcalEvent? GetNextUpcoming(
        IEnumerable<ParsedIcalEvent> events, DateOnly today)
        => GetUpcoming(events, today).FirstOrDefault();

    /// <summary>Unescapes the small set of RFC 5545 text escapes that appear in these files.</summary>
    private static string Unescape(string value)
        => value
            .Replace("\\n", "\n")
            .Replace("\\N", "\n")
            .Replace("\\,", ",")
            .Replace("\\;", ";")
            .Replace("\\\\", "\\")
            .Trim();
}
