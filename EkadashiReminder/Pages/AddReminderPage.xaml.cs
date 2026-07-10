using EkadashiReminder.ViewModels;

namespace EkadashiReminder.Pages;

public partial class AddReminderPage : ContentPage
{
    private readonly AddReminderViewModel _viewModel;

    public AddReminderPage(AddReminderViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (!_viewModel.CanSave)
            return;

        _viewModel.Save();
        await Shell.Current.GoToAsync("..");
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
