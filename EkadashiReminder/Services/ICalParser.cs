using EkadashiReminder.Models;

namespace EkadashiReminder.Services;

public class ICalParser
{
    /// <summary>
    /// Parses iCal content into <see cref="EkadashiEvent"/>s for the given location.
    /// The heavy lifting is delegated to the framework-independent
    /// <see cref="ICalParserCore"/> so the logic can be unit tested without MAUI.
    /// </summary>
    public List<EkadashiEvent> Parse(string icalContent, string location)
    {
        return ICalParserCore.Parse(icalContent)
            .Where(e => e.IsEkadashiFast)
            .Select(e => new EkadashiEvent
            {
                Name          = e.Name,
                Date          = e.Date,
                Location      = location,
                Description   = e.Description,
                IsCustom      = false,
                BreakFastTime = e.BreakFastWindow
            })
            .OrderBy(e => e.Date)
            .ToList();
    }
}

