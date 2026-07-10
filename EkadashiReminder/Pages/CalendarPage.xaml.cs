using EkadashiReminder.ViewModels;
using EkadashiReminder.Services;

namespace EkadashiReminder.Pages;

public partial class CalendarPage : ContentPage
{
    private readonly CalendarViewModel _viewModel;
    private bool _allDatesVisible;
    private bool _isInitialized;

    public CalendarPage(CalendarViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = _viewModel;
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_isInitialized)
        {
            _isInitialized = true;
            // Fire-and-forget: ask permission and load data without blocking the UI thread.
            _ = InitializeOnFirstAppearAsync();
        }
    }

    private async Task InitializeOnFirstAppearAsync()
    {
        // Request permission without blocking page rendering.
        await NotificationService.RequestPermissionAsync();
        await _viewModel.InitializeAsync();
    }

    private void OnToggleAllDatesClicked(object sender, EventArgs e)
    {
        _allDatesVisible = !_allDatesVisible;
        AllUpcomingView.IsVisible = _allDatesVisible;
        ToggleAllBtn.Text = _allDatesVisible ? "Hide ?" : "Show ?";
    }
}
