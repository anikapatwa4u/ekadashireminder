using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using EkadashiReminder.Models;
using EkadashiReminder.Services;

namespace EkadashiReminder.ViewModels;

public class CalendarViewModel : INotifyPropertyChanged
{
    private readonly EkadashiDataService _dataService;
    private readonly CustomReminderService _reminderService;
    private readonly NotificationService _notificationService;

    private bool _isBusy;
    private string _locationKey = string.Empty;
    private CancellationTokenSource _loadCts = new();

    // When true, property setters must NOT trigger event loads. Used during
    // InitializeAsync so setting Country/City to restore the saved location does not
    // kick off a spurious load for the alphabetically-first city, which could race
    // with (and overwrite) the correct load and show the wrong Ekadashi date.
    private bool _isInitializing;

    // ?? Commands ????????????????????????????????????????????????????????????????
    public ICommand RefreshCommand { get; }
    public ICommand ToggleReminderCommand { get; }

    // ?? Event lists ?????????????????????????????????????????????????????????????
    /// <summary>Next 4 upcoming Ekadashi dates shown in the featured cards.</summary>
    public ObservableCollection<EkadashiEvent> NextFourEvents { get; } = [];
    /// <summary>All upcoming events for the collapsible full-list section.</summary>
    public ObservableCollection<EkadashiEvent> UpcomingEvents { get; } = [];

    // ?? Location picker — two-level: Country ? City ??????????????????????????
    /// <summary>All distinct country names, alphabetically sorted.</summary>
    public IReadOnlyList<string> Countries => EkadashiDataService.AllCountries;

    private string? _selectedCountry;
    public string? SelectedCountry
    {
        get => _selectedCountry;
        set
        {
            if (_selectedCountry == value) return;
            _selectedCountry = value;
            OnPropertyChanged();
            RefreshCitiesForCountry();
        }
    }

    /// <summary>Cities available for the currently selected country.</summary>
    public ObservableCollection<LocationRegion> CitiesForCountry { get; } = [];

    private LocationRegion? _selectedLocation;
    public LocationRegion? SelectedLocation
    {
        get => _selectedLocation;
        set
        {
            if (_selectedLocation == value) return;
            _selectedLocation = value;
            OnPropertyChanged();
            if (value is not null && !_isInitializing)
            {
                _loadCts.Cancel();
                _loadCts = new CancellationTokenSource();
                _ = LoadEventsAsync(value.Key, _loadCts.Token);
            }
        }
    }

    // ?? Busy / next highlight ????????????????????????????????????????????????
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowContent));
        }
    }

    private bool _hasError;
    /// <summary>True when the last load failed; drives the error panel visibility.</summary>
    public bool HasError
    {
        get => _hasError;
        set
        {
            _hasError = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowContent));
        }
    }

    /// <summary>True only when not loading and no error — controls the events list visibility.</summary>
    public bool ShowContent => !_isBusy && !_hasError;

    private EkadashiEvent? _nextEkadashi;
    public EkadashiEvent? NextEkadashi
    {
        get => _nextEkadashi;
        set { _nextEkadashi = value; OnPropertyChanged(); }
    }

    // ?? Constructor ?????????????????????????????????????????????????????????????
    public CalendarViewModel(
        EkadashiDataService dataService,
        CustomReminderService reminderService,
        NotificationService notificationService)
    {
        _dataService = dataService;
        _reminderService = reminderService;
        _notificationService = notificationService;
        RefreshCommand = new Command(async () => await RefreshAsync());
        ToggleReminderCommand = new Command<EkadashiEvent>(async ev => await ToggleReminderAsync(ev));
    }

    // ?? Initialise ??????????????????????????????????????????????????????????????
    public async Task InitializeAsync()
    {
        // Warm up the lazy location catalogue on a background thread so the
        // main thread is never blocked by BuildLocations() or AllCountries LINQ.
        var (savedKey, location) = await Task.Run(() =>
        {
            var key = Preferences.Default.Get("selected_location", string.Empty);
            var guessed = EkadashiDataService.GuessLocation();

            LocationRegion? loc = null;
            if (!string.IsNullOrEmpty(key))
                loc = EkadashiDataService.AllLocations.FirstOrDefault(l => l.Key == key);
            loc ??= guessed ?? EkadashiDataService.AllLocations.First(l => l.Key == "Mumbai [India]");

            return (key, loc);
        });

        // Notify Countries picker now that AllCountries lazy value is ready.
        OnPropertyChanged(nameof(Countries));

        // Suppress load-on-set while we restore the saved location, so the
        // auto-select-first-city logic cannot start a competing load that races
        // with (and overwrites) the correct one.
        _isInitializing = true;
        try
        {
            // Set country first so cities list populates, then the exact saved city.
            _selectedCountry = location.Country;
            OnPropertyChanged(nameof(SelectedCountry));
            RefreshCitiesForCountry();

            _selectedLocation = location;
            OnPropertyChanged(nameof(SelectedLocation));
        }
        finally
        {
            _isInitializing = false;
        }

        // Now perform the single, authoritative load for the restored location.
        await LoadEventsAsync(location.Key, _loadCts.Token, scheduleReminders: true);
    }

    private void RefreshCitiesForCountry()
    {
        CitiesForCountry.Clear();
        if (_selectedCountry is null) return;
        foreach (var loc in EkadashiDataService.GetLocationsForCountry(_selectedCountry))
            CitiesForCountry.Add(loc);

        // Auto-select first city if current selection is not in this country
        if (_selectedLocation?.Country != _selectedCountry)
            SelectedLocation = CitiesForCountry.FirstOrDefault();
    }

    // ?? Load events ?????????????????????????????????????????????????????????????
    public Task LoadEventsAsync(string locationKey, CancellationToken ct = default, bool scheduleReminders = false)
        => LoadEventsCoreAsync(locationKey, ct, scheduleReminders);

    private async Task LoadEventsCoreAsync(string locationKey, CancellationToken ct, bool scheduleReminders)
    {
        IsBusy = true;
        HasError = false;
        try
        {
            _locationKey = locationKey;
            Preferences.Default.Set("selected_location", locationKey);

            // Parse .ics files on a background thread to keep the UI responsive.
            var ekadashiEvents = await Task.Run(
                () => _dataService.GetEventsForLocationAsync(locationKey), ct);

            ct.ThrowIfCancellationRequested();

            var customReminders = _reminderService.GetAll();

            var customAsEvents = customReminders.Select(r => new EkadashiEvent
            {
                Name = r.Name, Date = r.Date, Location = "Custom", IsCustom = true, Description = r.Notes
            });

            var today        = DateOnly.FromDateTime(DateTime.Today);
            var all          = ekadashiEvents.Concat(customAsEvents).OrderBy(e => e.Date).ToList();
            var scheduledIds = await NotificationService.GetScheduledIdsAsync();

            ct.ThrowIfCancellationRequested();

            foreach (var ev in all)
                ev.ReminderEnabled = scheduledIds.Contains(ev.NotificationId);

            UpcomingEvents.Clear();
            NextFourEvents.Clear();

            var upcoming = all.Where(e => e.Date >= today).ToList();
            foreach (var ev in upcoming)
                UpcomingEvents.Add(ev);

            foreach (var ev in upcoming.Take(4))
                NextFourEvents.Add(ev);

            NextEkadashi = NextFourEvents.FirstOrDefault();

            // Fire scheduling off in the background so the UI appears immediately.
            // Capture the lists needed before leaving the UI-thread context.
            if (scheduleReminders)
            {
                var upcomingSnapshot  = NextFourEvents.ToList();
                var eventsToSchedule  = NextFourEvents.Where(e => !e.ReminderEnabled).ToList();
                var remindersSnapshot = customReminders.ToList();

                // One-time reconciliation for users upgrading from a version that could
                // schedule reminders for the wrong location's Ekadashi date. Clears any
                // stale notifications and reschedules from the correct data. Runs once.
                const string ResyncKey = "reminders_resynced_v2";
                var needsResync = !Preferences.Default.Get(ResyncKey, false);

                _ = Task.Run(async () =>
                {
                    if (needsResync)
                    {
                        await _notificationService.ResyncAsync(upcomingSnapshot, remindersSnapshot);
                        foreach (var ev in upcomingSnapshot)
                            ev.ReminderEnabled = true;
                        Preferences.Default.Set(ResyncKey, true);
                        return;
                    }

                    foreach (var ev in eventsToSchedule)
                    {
                        await _notificationService.ScheduleForEventAsync(ev);
                        ev.ReminderEnabled = true;
                    }
                    await _notificationService.ScheduleAllNotificationsAsync([], remindersSnapshot);
                });
            }
        }
        catch (OperationCanceledException)
        {
            // A newer location was selected — discard this result silently.
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] LoadEvents failed: {ex.Message}");
            HasError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ToggleReminderAsync(EkadashiEvent ev)
    {
        if (ev.ReminderEnabled)
        {
            await _notificationService.CancelForEventAsync(ev);
            ev.ReminderEnabled = false;
        }
        else
        {
            await _notificationService.ScheduleForEventAsync(ev);
            ev.ReminderEnabled = true;
        }
    }

    public async Task RefreshAsync()
    {
        _loadCts.Cancel();
        _loadCts = new CancellationTokenSource();
        await LoadEventsAsync(_locationKey, _loadCts.Token, scheduleReminders: true);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
