using EkadashiReminder.Models;
using Plugin.LocalNotification;
using Plugin.LocalNotification.AndroidOption;

namespace EkadashiReminder.Services;

public class NotificationService
{
    private const string ChannelId = "ekadashi_reminders";
    private readonly CalendarStoreService _calendarStore;

    public NotificationService(CalendarStoreService calendarStore)
    {
        _calendarStore = calendarStore;
    }

    /// <summary>Schedules day-before notifications for custom reminders only.</summary>
    public async Task ScheduleAllNotificationsAsync(
        IEnumerable<EkadashiEvent> ekadashiEvents,
        IEnumerable<CustomReminder> customReminders)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        // Custom reminder: notify one day before at 08:00
        foreach (var reminder in customReminders.Where(r => r.Date >= today))
        {
            var dayBefore = reminder.Date.AddDays(-1);
            if (dayBefore >= today)
            {
                await ScheduleAsync(
                    reminder.Id.GetHashCode() & 0x7FFFFFFF,
                    $"Reminder Tomorrow: {reminder.Name}",
                    $"Your custom reminder '{reminder.Name}' is tomorrow ({reminder.Date:MMMM d}).",
                    dayBefore.ToDateTime(new TimeOnly(8, 0)));
            }
        }
    }

    private static DateOnly? FindWeekendBefore(DateOnly date)
    {
        var check = date.AddDays(-1);
        for (int i = 0; i < 7; i++)
        {
            if (check.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                return check;
            check = check.AddDays(-1);
        }
        return null;
    }

    private static async Task ScheduleAsync(int id, string title, string body, DateTime notifyAt)
    {
        if (notifyAt <= DateTime.Now)
            return;

        var request = new NotificationRequest
        {
            NotificationId = id,
            Title = title,
            Description = body,
            Schedule = new NotificationRequestSchedule
            {
                NotifyTime = notifyAt,
                RepeatType = NotificationRepeat.No
            },
            Android = new AndroidOptions
            {
                ChannelId = ChannelId
            }
        };

        await LocalNotificationCenter.Current.Show(request);
    }

    /// <summary>
    /// Schedules the day-before and weekend-before notifications for a single Ekadashi event.
    /// Uses the event's stable NotificationId as a base; weekend notification uses id+1.
    /// </summary>
    public async Task ScheduleForEventAsync(EkadashiEvent ev)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var dayBefore = ev.Date.AddDays(-1);
        if (dayBefore >= today)
        {
            await ScheduleAsync(
                ev.NotificationId,
                $"Ekadashi Tomorrow: {ev.Name}",
                $"Tomorrow ({ev.Date:MMMM d}) is {ev.Name}. Prepare for fasting.",
                dayBefore.ToDateTime(new TimeOnly(8, 0)));
        }

        var weekendBefore = FindWeekendBefore(ev.Date);
        if (weekendBefore.HasValue && weekendBefore.Value >= today && weekendBefore.Value != dayBefore)
        {
            await ScheduleAsync(
                ev.NotificationId + 1,
                $"Upcoming Ekadashi: {ev.Name}",
                $"{ev.Name} is on {ev.Date:MMMM d}. Plan your celebration!",
                weekendBefore.Value.ToDateTime(new TimeOnly(9, 0)));
        }

        // Add to device calendar if not already added
        if (ev.CalendarEventId is null)
        {
            var calEventId = await _calendarStore.AddEventAsync(
                $"?? {ev.Name}",
                ev.Date,
                ev.Description ?? "Ekadashi fasting day",
                ev.Location);
            ev.CalendarEventId = calEventId;
        }
    }

    /// <summary>Cancels both notification slots for a single event and removes it from the device calendar.</summary>
    public async Task CancelForEventAsync(EkadashiEvent ev)
    {
        LocalNotificationCenter.Current.Cancel([ev.NotificationId, ev.NotificationId + 1]);

        if (ev.CalendarEventId is not null)
        {
            await _calendarStore.RemoveEventAsync(ev.CalendarEventId);
            ev.CalendarEventId = null;
        }
    }

    /// <summary>Returns the set of notification IDs that are currently pending.</summary>
    public static async Task<HashSet<int>> GetScheduledIdsAsync()
    {
        var pending = await LocalNotificationCenter.Current.GetPendingNotificationList();
        return pending.Select(n => n.NotificationId).ToHashSet();
    }

    public static async Task<bool> RequestPermissionAsync()
    {
        return await LocalNotificationCenter.Current.RequestNotificationPermission();
    }
}
