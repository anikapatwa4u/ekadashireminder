using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EkadashiReminder.Models;

namespace EkadashiReminder.Services;

public class EkadashiDataService
{
    private readonly ICalParser _parser = new();

    // Cache parsed events per location key so repeated selections are instant.
    private readonly Dictionary<string, List<EkadashiEvent>> _cache = [];

    // ---------------------------------------------------------------------------
    // Location catalogue � built from the 215 cities in the downloaded iCal files.
    // File naming pattern: "ical/{City} [{Country}]-a{year}-ICS.ics"
    // ---------------------------------------------------------------------------
    private static readonly Lazy<IReadOnlyList<LocationRegion>> _allLocations
        = new(() => BuildLocations());
    private static readonly Lazy<IReadOnlyList<string>> _allCountries
        = new(() => _allLocations.Value.Select(l => l.Country).Distinct().OrderBy(c => c).ToList<string>());

    public static IReadOnlyList<LocationRegion> AllLocations => _allLocations.Value;
    public static IReadOnlyList<string> AllCountries => _allCountries.Value;

    private static List<LocationRegion> BuildLocations()
    {
        // Canonical list of all 215 cities as "City [Country]" pairs.
        // The years available are 2026 and 2027 (downloaded into Resources/Raw/ical/).
        // 2025 data comes from the legacy hand-crafted files in Resources/Raw/ for the
        // three broad regions; for real cities we rely on 2026 + 2027 only.
        string[] cities =
        [
            "Aberdeen [United Kingdom]",
            "Abu Dhabi [United Arab Emirates]",
            "Accra [Ghana]",
            "Adelaide [Australia]",
            "Ahmadabad [India]",
            "Almaty [Kazakhstan]",
            "Amsterdam [Netherlands]",
            "Anchorage [United States of America]",
            "Antananarivo [Madagascar]",
            "Athens [Greece]",
            "Atlanta [United States of America]",
            "Auckland [New Zealand]",
            "Austin [United States of America]",
            "Baghdad [Iraq]",
            "Baltimore [United States of America]",
            "Bangalore [India]",
            "Bangkok [Thailand]",
            "Barcelona [Spain]",
            "Basel [Switzerland]",
            "Belfast [United Kingdom]",
            "Belgrade [Serbia]",
            "Berkeley [United States of America]",
            "Berlin [Germany]",
            "Bern [Switzerland]",
            "Bogota [Colombia]",
            "Boston [United States of America]",
            "Bratislava [Slovakia]",
            "Brihuega [Spain]",
            "Brisbane [Australia]",
            "Brussels [Belgium]",
            "Bucharest [Romania]",
            "Budapest [Hungary]",
            "Buenos Aires [Argentina]",
            "Calgary [Canada]",
            "Cape Town [South Africa]",
            "Caracas [Venezuela]",
            "Cardiff [United Kingdom]",
            "Charleston [United States of America]",
            "Chicago [United States of America]",
            "Colombo [Sri Lanka]",
            "Columbus [United States of America]",
            "Copenhagen [Denmark]",
            "Dallas [United States of America]",
            "Denver [United States of America]",
            "Detroit [United States of America]",
            "Dhaka [Bangladesh]",
            "Dnipropetrovsk [Ukraine]",
            "Dubai [United Arab Emirates]",
            "Dublin [Ireland]",
            "Durban [South Africa]",
            "Durham [United States of America]",
            "Ekaterinburg [Russia]",
            "Frankfurt [Germany]",
            "Gainesville [United States of America]",
            "Gayaji [India]",
            "Goteborg [Sweden]",
            "Guadalajara [Spain]",
            "Guatemala [Guatemala]",
            "Halifax [Canada]",
            "Hamburg [Germany]",
            "Hartford [United States of America]",
            "Helsinki [Finland]",
            "Hillsborough [United States of America]",
            "Honolulu [United States of America]",
            "Houston [United States of America]",
            "Hyderabad [India]",
            "Istanbul [Turkey]",
            "Jakarta [Indonesia]",
            "Johannesburg [South Africa]",
            "Kampala [Uganda]",
            "Kansas City [United States of America]",
            "Kathmandu [Nepal]",
            "Kiev [Ukraine]",
            "Knoxville [United States of America]",
            "Kolkata [India]",
            "Kuala Lumpur [Malaysia]",
            "Lagos [Nigeria]",
            "Leicester [United Kingdom]",
            "Liepaja [Latvia]",
            "Lima [Peru]",
            "Lisbon [Portugal]",
            "Ljubljana [Slovenia]",
            "London [United Kingdom]",
            "Los Angeles [United States of America]",
            "Lusaka [Zambia]",
            "Madrid [Spain]",
            "Malaga [Spain]",
            "Manchester [United Kingdom]",
            "Manila [Philippines]",
            "Melbourne [Australia]",
            "Memphis [United States of America]",
            "Mexico City [Mexico]",
            "Miami [United States of America]",
            "Milan [Italy]",
            "Minneapolis [Canada]",
            "Minsk [Belarus]",
            "Montreal [Canada]",
            "Moscow [Russia]",
            "Mumbai [India]",
            "Munich [Germany]",
            "Nairobi [Kenya]",
            "New Delhi [India]",
            "New Orleans [United States of America]",
            "New York City [United States of America]",
            "Nieuw Nickerie [Suriname]",
            "Novosibirsk [Russia]",
            "Oklahoma City [United States of America]",
            "Orlando [United States of America]",
            "Oslo [Norway]",
            "Ottawa [Canada]",
            "Paris [France]",
            "Perth [Australia]",
            "Philadelphia [United States of America]",
            "Phoenix [United States of America]",
            "Picayune [United States of America]",
            "Pittsburgh [United States of America]",
            "Portland [United States of America]",
            "Prague [Czech Republic]",
            "Presov [Slovakia]",
            "Provo [United States of America]",
            "Puerto Rico [United States of America]",
            "Pune [India]",
            "Riga [Latvia]",
            "Rome [Italy]",
            "Saint Louis [United States of America]",
            "Saint Paul [United States of America]",
            "San Diego [United States of America]",
            "San Francisco [United States of America]",
            "San Jose [United States of America]",
            "Santiago [Chile]",
            "Sao Paulo [Brazil]",
            "Seattle [United States of America]",
            "Secunderabad [India]",
            "Singapore [Singapore]",
            "Skopje [Macedonia]",
            "Smolensk [Russia]",
            "Sofia [Bulgaria]",
            "Stockholm [Sweden]",
            "Sundsvall [Sweden]",
            "Suva [Fiji]",
            "Sydney [Australia]",
            "T ai-chung-shih [Taiwan]",
            "Taipei [Taiwan]",
            "Tallassee [United States of America]",
            "Tashkent [Uzbekistan]",
            "Tbilisi [Georgia]",
            "Tehran [Iran]",
            "Tel Aviv-Yafo [Israel]",
            "Tenerife [Spain]",
            "Thiruvananthapuram [India]",
            "Timisoara [Romania]",
            "Tokyo [Japan]",
            "Tongaat [South Africa]",
            "Toronto [Canada]",
            "Towaco [United States of America]",
            "Trinidad [Trinidad and Tobago]",
            "Tucson [United States of America]",
            "Udhampur [India]",
            "Utrecht [Netherlands]",
            "Vancouver [Canada]",
            "Vienna [Austria]",
            "Vilnius [Lithuania]",
            "Vladivostok [Russia]",
            "Vrindavan [India]",
            "Walla Walla [United States of America]",
            "Warsaw [Poland]",
            "Washington [United States of America]",
            "Wellington [New Zealand]",
            "Wilmington [United States of America]",
            "Winnipeg [Canada]",
            "Yerevan [Armenia]",
            "Zagreb [Croatia]",
            "Zurich [Switzerland]",
        ];

        var result = new List<LocationRegion>(cities.Length);
        foreach (var entry in cities)
        {
            // entry = "Mumbai [India]"
            var bracket = entry.IndexOf('[');
            if (bracket < 1) continue;
            var city    = entry[..(bracket - 1)].Trim();
            var country = entry[(bracket + 1)..entry.IndexOf(']')].Trim();

            result.Add(new LocationRegion
            {
                Key          = entry,   // "Mumbai [India]"
                City         = city,
                Country      = country,
                IcalFileNames = [
                    $"ical/{entry}-a2026-ICS.ics",
                    $"ical/{entry}-a2027-ICS.ics",
                ]
            });
        }

        return result.OrderBy(l => l.Country).ThenBy(l => l.City).ToList();
    }

    // ---------------------------------------------------------------------------
    // Per-country subset helper used by the ViewModel for the two-level picker.
    // ---------------------------------------------------------------------------
    public static IReadOnlyList<LocationRegion> GetLocationsForCountry(string country)
        => AllLocations.Where(l => l.Country == country).OrderBy(l => l.City).ToList();

    // ---------------------------------------------------------------------------
    // Attempt to guess the best default location from the device time zone.
    // ---------------------------------------------------------------------------
    public static LocationRegion? GuessLocation()
    {
        var tzId = TimeZoneInfo.Local.Id;

        // Map well-known TZ identifiers to city keys
        var guesses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["India Standard Time"]          = "Mumbai [India]",
            ["Asia/Kolkata"]                 = "Mumbai [India]",
            ["Asia/Calcutta"]                = "Mumbai [India]",
            ["Eastern Standard Time"]        = "New York City [United States of America]",
            ["America/New_York"]             = "New York City [United States of America]",
            ["Central Standard Time"]        = "Chicago [United States of America]",
            ["America/Chicago"]              = "Chicago [United States of America]",
            ["Mountain Standard Time"]       = "Denver [United States of America]",
            ["America/Denver"]               = "Denver [United States of America]",
            ["Pacific Standard Time"]        = "Los Angeles [United States of America]",
            ["America/Los_Angeles"]          = "Los Angeles [United States of America]",
            ["GMT Standard Time"]            = "London [United Kingdom]",
            ["Europe/London"]                = "London [United Kingdom]",
            ["W. Europe Standard Time"]      = "Berlin [Germany]",
            ["Central Europe Standard Time"] = "Berlin [Germany]",
            ["Europe/Berlin"]                = "Berlin [Germany]",
            ["Russia Time Zone 3"]           = "Moscow [Russia]",
            ["Europe/Moscow"]                = "Moscow [Russia]",
            ["AUS Eastern Standard Time"]    = "Sydney [Australia]",
            ["Australia/Sydney"]             = "Sydney [Australia]",
            ["Tokyo Standard Time"]          = "Tokyo [Japan]",
            ["Asia/Tokyo"]                   = "Tokyo [Japan]",
            ["Singapore Standard Time"]      = "Singapore [Singapore]",
            ["Asia/Singapore"]               = "Singapore [Singapore]",
            ["Canada Central Standard Time"] = "Toronto [Canada]",
            ["America/Toronto"]              = "Toronto [Canada]",
        };

        if (guesses.TryGetValue(tzId, out var key))
            return AllLocations.FirstOrDefault(l => l.Key == key);

        return null;
    }

    // ---------------------------------------------------------------------------
    // Load Ekadashi events for a given location across all available years.
    // Results are cached so switching back is instant.
    // ---------------------------------------------------------------------------
    public async Task<List<EkadashiEvent>> GetEventsForLocationAsync(string locationKey)
    {
        if (_cache.TryGetValue(locationKey, out var cached))
            return cached;

        var location = AllLocations.FirstOrDefault(l => l.Key == locationKey);
        if (location is null) return [];

        // Read all file contents on a background thread so the main thread stays free.
        var fileContents = await Task.Run(async () =>
        {
            var results = new List<string>();
            foreach (var fileName in location.IcalFileNames)
            {
                try
                {
                    using var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
                    using var reader = new StreamReader(stream);
                    results.Add(await reader.ReadToEndAsync());
                }
                catch
                {
                    // File for this year may not exist � silently skip.
                }
            }
            return results;
        });

        var all = new List<EkadashiEvent>();
        foreach (var content in fileContents)
            all.AddRange(_parser.Parse(content, location.DisplayName));

        var result = all.OrderBy(e => e.Date).DistinctBy(e => e.Date).ToList();
        _cache[locationKey] = result;
        return result;
    }

    // Keep legacy overload so NotificationService compiles without changes.
    public async Task<List<EkadashiEvent>> GetEventsForRegionAsync(string regionKey)
        => await GetEventsForLocationAsync(regionKey);
}
