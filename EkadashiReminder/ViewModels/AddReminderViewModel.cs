using System.ComponentModel;
using System.Runtime.CompilerServices;
using EkadashiReminder.Models;
using EkadashiReminder.Services;

namespace EkadashiReminder.ViewModels;

public class AddReminderViewModel : INotifyPropertyChanged
{
    private readonly CustomReminderService _reminderService;

    private string _name = string.Empty;
    private DateTime _date = DateTime.Today.AddDays(1);
    private string _notes = string.Empty;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanSave)); }
    }

    public DateTime Date
    {
        get => _date;
        set { _date = value; OnPropertyChanged(); }
    }

    public string Notes
    {
        get => _notes;
        set { _notes = value; OnPropertyChanged(); }
    }

    public bool CanSave => !string.IsNullOrWhiteSpace(Name);

    public Guid? EditingId { get; private set; }

    public AddReminderViewModel(CustomReminderService reminderService)
    {
        _reminderService = reminderService;
    }

    public void LoadForEdit(CustomReminder reminder)
    {
        EditingId = reminder.Id;
        Name = reminder.Name;
        Date = reminder.Date.ToDateTime(TimeOnly.MinValue);
        Notes = reminder.Notes ?? string.Empty;
    }

    public void Save()
    {
        var reminder = new CustomReminder
        {
            Id = EditingId ?? Guid.NewGuid(),
            Name = Name.Trim(),
            Date = DateOnly.FromDateTime(Date),
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim()
        };

        _reminderService.Save(reminder);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
