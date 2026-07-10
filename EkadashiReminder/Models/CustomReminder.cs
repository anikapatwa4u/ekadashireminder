namespace EkadashiReminder.Models;

public class CustomReminder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string? Notes { get; set; }
}
