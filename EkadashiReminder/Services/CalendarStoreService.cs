using Microsoft.Maui.Graphics;
using Plugin.Maui.CalendarStore;

namespace EkadashiReminder.Services;

/// <summary>
/// Adds and removes Ekadashi events in the device's native calendar app.
/// All events are placed in a dedicated "Ekadashi Reminder" calendar.
/// </summary>
public class CalendarStoreService
{
    private const string CalendarName = "Ekadashi Reminder";
    private const string CalendarIdKey = "ekadashi_calendar_id";

    // ?? Public API ??????????????????????????????????????????????????????????????

    /// <summary>
    /// Adds an all-day event to the native calendar for the given date.
    /// Returns the new calendar event ID, or null if permission was denied / not available.
    /// </summary>
    public async Task<string?> AddEventAsync(string title, DateOnly date, string? description = null, string? location = null)
    {
        try
        {
            var calendarId = await GetOrCreateCalendarIdAsync();
            if (calendarId is null)
                return null;

            var start = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var end   = start.AddDays(1);

            var eventId = await CalendarStore.Default.CreateAllDayEvent(
                calendarId,
                title,
                description ?? string.Empty,
                location ?? string.Empty,
                start,
                end);

            return eventId;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CalendarStoreService] AddEventAsync failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>Removes an event from the native calendar by its event ID.</summary>
    public async Task RemoveEventAsync(string calendarEventId)
    {
        try
        {
            await CalendarStore.Default.DeleteEvent(calendarEventId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CalendarStoreService] RemoveEventAsync failed: {ex.Message}");
        }
    }

    // ?? Helpers ?????????????????????????????????????????????????????????????????

    private async Task<string?> GetOrCreateCalendarIdAsync()
    {
        // Return cached ID if still valid
        var cached = Preferences.Default.Get(CalendarIdKey, string.Empty);
        if (!string.IsNullOrEmpty(cached) && await CalendarExistsAsync(cached))
            return cached;

        // Try to find an existing calendar with our name
        var calendars = await CalendarStore.Default.GetCalendars();
        var existing  = calendars.FirstOrDefault(c => c.Name == CalendarName);
        if (existing is not null)
        {
            Preferences.Default.Set(CalendarIdKey, existing.Id);
            return existing.Id;
        }

        // Create a new one with saffron colour
        var newId = await CalendarStore.Default.CreateCalendar(CalendarName, Color.FromArgb("#FF7722"));
        if (!string.IsNullOrEmpty(newId))
            Preferences.Default.Set(CalendarIdKey, newId);

        return string.IsNullOrEmpty(newId) ? null : newId;
    }

    private static async Task<bool> CalendarExistsAsync(string calendarId)
    {
        try
        {
            var calendars = await CalendarStore.Default.GetCalendars();
            return calendars.Any(c => c.Id == calendarId);
        }
        catch
        {
            return false;
        }
    }
}
