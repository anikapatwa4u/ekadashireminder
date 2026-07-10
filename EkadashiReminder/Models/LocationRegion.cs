namespace EkadashiReminder.Models;

public class LocationRegion
{
    /// <summary>Unique key, e.g. "Mumbai [India]"</summary>
    public string Key { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;

    /// <summary>ICS filenames (without path prefix) for each available year, e.g. ["ical/Mumbai [India]-a2026-ICS.ics"]</summary>
    public string[] IcalFileNames { get; set; } = [];

    public string DisplayName => $"{City}, {Country}";

    public override string ToString() => DisplayName;
}
