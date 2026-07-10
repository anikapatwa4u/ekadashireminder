using EkadashiReminder.Models;
using EkadashiReminder.Services;
using EkadashiReminder.ViewModels;
using System.Collections.ObjectModel;

namespace EkadashiReminder.Pages;

public partial class RemindersPage : ContentPage
{
    private readonly CustomReminderService _reminderService;
    private readonly AddReminderViewModel _addReminderViewModel;

    public ObservableCollection<CustomReminder> Reminders { get; } = [];

    public RemindersPage(CustomReminderService reminderService, AddReminderViewModel addReminderViewModel)
    {
        InitializeComponent();
        _reminderService = reminderService;
        _addReminderViewModel = addReminderViewModel;
        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadReminders();
    }

    private void LoadReminders()
    {
        Reminders.Clear();
        var all = _reminderService.GetAll().OrderBy(r => r.Date);
        foreach (var r in all)
            Reminders.Add(r);
    }

    private async void OnAddReminderClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(AddReminderPage));
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is Guid id)
        {
            bool confirmed = await DisplayAlert(
                "Delete Reminder",
                "Are you sure you want to delete this reminder?",
                "Delete", "Cancel");

            if (confirmed)
            {
                _reminderService.Delete(id);
                LoadReminders();
            }
        }
    }
}
