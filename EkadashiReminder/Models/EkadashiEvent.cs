using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EkadashiReminder.Models;

public class EkadashiEvent : INotifyPropertyChanged
{
    public string Name { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string Location { get; set; } = string.Empty;
    public bool IsCustom { get; set; }
    public string? Description { get; set; }

    /// <summary>
    /// Raw break-fast window from the ICS, e.g. "08:37 (sunrise) - 11:04 (1/3 of daylight)".
    /// Null when not available (custom reminders, legacy files).
    /// </summary>
    public string? BreakFastTime { get; set; }

    /// <summary>True when break-fast time data is available to show in the UI.</summary>
    public bool HasBreakFastTime => !string.IsNullOrEmpty(BreakFastTime);

    /// <summary>Formatted label shown in the card, e.g. "?? Break fast: 08:37 – 11:04".</summary>
    public string BreakFastLabel
    {
        get
        {
            if (string.IsNullOrEmpty(BreakFastTime)) return string.Empty;
            // Input: "08:37 (sunrise) - 11:04 (1/3 of daylight) LT"
            // Output: "08:37 – 11:04"
            var parts = BreakFastTime.Split('-');
            if (parts.Length >= 2)
            {
                var from = parts[0].Trim().Split(' ')[0]; // "08:37"
                var to   = parts[1].Trim().Split(' ')[0]; // "11:04"
                return $"Break fast (Paaran): {from} \u2013 {to}";
            }
            return $"Break fast (Paaran): {BreakFastTime}";
        }
    }

    /// <summary>Stable integer ID derived from the date, used to track scheduled notifications.</summary>
    public int NotificationId => Date.DayNumber;

    /// <summary>
    /// Native calendar event ID assigned by the device calendar, persisted in Preferences.
    /// Key is based on NotificationId so it survives app restarts.
    /// </summary>
    public string? CalendarEventId
    {
        get => Preferences.Default.Get($"cal_event_{NotificationId}", string.Empty) is { Length: > 0 } v ? v : null;
        set
        {
            if (value is null)
                Preferences.Default.Remove($"cal_event_{NotificationId}");
            else
                Preferences.Default.Set($"cal_event_{NotificationId}", value);
        }
    }

    private bool _reminderEnabled;
    public bool ReminderEnabled
    {
        get => _reminderEnabled;
        set
        {
            if (_reminderEnabled == value) return;
            _reminderEnabled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ReminderIcon));
        }
    }

    public string ReminderIcon => _reminderEnabled ? "??" : "??";

    public int DaysUntil => Date.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber;

    public bool IsUpcoming => DaysUntil >= 0;

    public string DaysUntilLabel => DaysUntil switch
    {
        0 => "Today",
        1 => "Tomorrow",
        < 0 => $"{Math.Abs(DaysUntil)} days ago",
        _ => $"In {DaysUntil} days"
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
