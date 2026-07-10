using System.Text.Json;
using EkadashiReminder.Models;

namespace EkadashiReminder.Services;

public class CustomReminderService
{
    private const string StorageKey = "custom_reminders";

    public List<CustomReminder> GetAll()
    {
        var json = Preferences.Default.Get(StorageKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<List<CustomReminder>>(json) ?? [];
    }

    public void Save(CustomReminder reminder)
    {
        var all = GetAll();
        var existing = all.FirstOrDefault(r => r.Id == reminder.Id);
        if (existing is not null)
            all.Remove(existing);

        all.Add(reminder);
        Persist(all);
    }

    public void Delete(Guid id)
    {
        var all = GetAll();
        all.RemoveAll(r => r.Id == id);
        Persist(all);
    }

    private static void Persist(List<CustomReminder> reminders)
    {
        var json = JsonSerializer.Serialize(reminders);
        Preferences.Default.Set(StorageKey, json);
    }
}
