using EkadashiReminder.Services;
using Xunit;

namespace EkadashiReminder.Tests;

/// <summary>
/// Unit tests for <see cref="ICalParserCore"/>. These cover the core reliability
/// concerns of the app: correct identification of Ekadashi fasting days, correct
/// date parsing, RFC 5545 line-folding, break-fast (Paaran) linkage, and robustness
/// against malformed input.
/// </summary>
public class ICalParserCoreTests
{
    private const string SampleCalendar = """
        BEGIN:VCALENDAR
        BEGIN:VEVENT
        UID:20260114-3172-VAISNAVACALENDAR.INFO
        DTSTART;VALUE=DATE:20260114
        SUMMARY:Fasting for Sat-tila Ekadasi
        DESCRIPTION:
        END:VEVENT
        BEGIN:VEVENT
        UID:20260115-51985-VAISNAVACALENDAR.INFO
        DTSTART;VALUE=DATE:20260115
        SUMMARY:Break fast 06:45 (sunrise) - 10:34 (1/3 of daylight) LT
        END:VEVENT
        BEGIN:VEVENT
        UID:20260103-54420-VAISNAVACALENDAR.INFO
        DTSTART;VALUE=DATE:20260103
        SUMMARY:Sri Krsna Pusya Abhiseka
        END:VEVENT
        END:VCALENDAR
        """;

    // ---------------------------------------------------------------------------
    // 1. Ekadashi identification
    // ---------------------------------------------------------------------------
    [Fact]
    public void Parse_ReturnsOnlyEkadashiFast_AsFastEvents()
    {
        var events = ICalParserCore.Parse(SampleCalendar);
        var fasts = events.Where(e => e.IsEkadashiFast).ToList();

        Assert.Single(fasts);
        Assert.Equal("Sat-tila Ekadashi", fasts[0].Name);
    }

    [Theory]
    [InlineData("Fasting for Sat-tila Ekadasi", true)]
    [InlineData("Fasting for Pandava Nirjala Ekadashi", true)]
    [InlineData("Sri Krsna Pusya Abhiseka", false)]
    [InlineData("Break fast 06:45 - 10:34 LT", false)]
    public void IsEkadashiFastingEvent_MatchesBothSpellings(string summary, bool expected)
    {
        Assert.Equal(expected, ICalParserCore.IsEkadashiFastingEvent(summary));
    }

    // ---------------------------------------------------------------------------
    // 2. Name cleaning / normalisation
    // ---------------------------------------------------------------------------
    [Fact]
    public void CleanName_StripsFastingPrefix_AndNormalisesSpelling()
    {
        Assert.Equal("Sat-tila Ekadashi", ICalParserCore.CleanName("Fasting for Sat-tila Ekadasi"));
    }

    [Fact]
    public void CleanName_LeavesAlreadyCleanNameUnchanged()
    {
        Assert.Equal("Pandava Nirjala Ekadashi", ICalParserCore.CleanName("Pandava Nirjala Ekadashi"));
    }

    // ---------------------------------------------------------------------------
    // 3. Date parsing robustness (a core reliability concern)
    // ---------------------------------------------------------------------------
    [Theory]
    [InlineData("20260114", 2026, 1, 14)]
    [InlineData("20260115T060000", 2026, 1, 15)]   // date-time form
    [InlineData("20271231", 2027, 12, 31)]
    public void ParseIcalDate_ParsesValidDates(string value, int y, int m, int d)
    {
        var result = ICalParserCore.ParseIcalDate(value);
        Assert.NotNull(result);
        Assert.Equal(new DateOnly(y, m, d), result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("2026")]        // too short
    [InlineData("20261301")]    // month 13
    [InlineData("20260132")]    // day 32
    [InlineData("notadate")]
    public void ParseIcalDate_RejectsInvalidDates(string value)
    {
        Assert.Null(ICalParserCore.ParseIcalDate(value));
    }

    // ---------------------------------------------------------------------------
    // 4. Break-fast (Paaran) window extraction and linkage
    // ---------------------------------------------------------------------------
    [Fact]
    public void ExtractBreakFastWindow_StripsPrefixAndLtSuffix()
    {
        var window = ICalParserCore.ExtractBreakFastWindow(
            "Break fast 06:45 (sunrise) - 10:34 (1/3 of daylight) LT");
        Assert.Equal("06:45 (sunrise) - 10:34 (1/3 of daylight)", window);
    }

    [Fact]
    public void Parse_LinksBreakFastWindow_ToPrecedingEkadashi()
    {
        var events = ICalParserCore.Parse(SampleCalendar);
        var fast = Assert.Single(events.Where(e => e.IsEkadashiFast));

        // Ekadashi is on the 14th; break-fast is on the 15th and should be linked.
        Assert.NotNull(fast.BreakFastWindow);
        Assert.Contains("06:45", fast.BreakFastWindow);
        Assert.Contains("10:34", fast.BreakFastWindow);
    }

    // ---------------------------------------------------------------------------
    // 5. RFC 5545 line-folding (the reliability bug that was fixed)
    // ---------------------------------------------------------------------------
    [Fact]
    public void Unfold_JoinsContinuationLines()
    {
        // Per RFC 5545 the CRLF + single leading whitespace of a folded line is
        // removed entirely, so the original unbroken text is reconstructed.
        var folded = "SUMMARY:Fasting for Pandava Nirjala\n Ekadasi";
        var unfolded = ICalParserCore.Unfold(folded);

        Assert.Single(unfolded);
        Assert.Equal("SUMMARY:Fasting for Pandava NirjalaEkadasi", unfolded[0]);
    }

    [Fact]
    public void Parse_HandlesFoldedSummary_WithoutLosingText()
    {
        var calendar = """
            BEGIN:VCALENDAR
            BEGIN:VEVENT
            DTSTART;VALUE=DATE:20260601
            SUMMARY:Fasting for Pandava Nirjala
             Ekadasi
            END:VEVENT
            END:VCALENDAR
            """;

        // The fold point falls mid-word, so unfolding reconstructs "NirjalaEkadasi"
        // (no lost characters). The key reliability guarantee is that NO text is dropped.
        var events = ICalParserCore.Parse(calendar);
        var fast = Assert.Single(events);
        Assert.Equal("Pandava NirjalaEkadashi", fast.Name);
    }

    // ---------------------------------------------------------------------------
    // 6. Ordering and overall structure
    // ---------------------------------------------------------------------------
    [Fact]
    public void Parse_ReturnsEventsSortedByDate()
    {
        var calendar = """
            BEGIN:VCALENDAR
            BEGIN:VEVENT
            DTSTART;VALUE=DATE:20260601
            SUMMARY:Fasting for Yogini Ekadasi
            END:VEVENT
            BEGIN:VEVENT
            DTSTART;VALUE=DATE:20260114
            SUMMARY:Fasting for Sat-tila Ekadasi
            END:VEVENT
            END:VCALENDAR
            """;

        var events = ICalParserCore.Parse(calendar);
        Assert.Equal(2, events.Count);
        Assert.True(events[0].Date < events[1].Date);
    }

    // ---------------------------------------------------------------------------
    // 7. Malformed / empty input robustness
    // ---------------------------------------------------------------------------
    [Fact]
    public void Parse_EmptyInput_ReturnsEmptyList()
    {
        Assert.Empty(ICalParserCore.Parse(string.Empty));
    }

    [Fact]
    public void Parse_EventWithoutDate_IsSkipped()
    {
        var calendar = """
            BEGIN:VCALENDAR
            BEGIN:VEVENT
            SUMMARY:Fasting for Broken Ekadasi
            END:VEVENT
            END:VCALENDAR
            """;

        Assert.Empty(ICalParserCore.Parse(calendar));
    }

    [Fact]
    public void Parse_IgnoresNonEkadashiAndNonBreakFastEvents()
    {
        var events = ICalParserCore.Parse(SampleCalendar);
        Assert.DoesNotContain(events, e => e.Name.Contains("Pusya"));
    }

    // ---------------------------------------------------------------------------
    // 8. Text unescaping
    // ---------------------------------------------------------------------------
    [Fact]
    public void Parse_UnescapesCommaInDescription()
    {
        var calendar = """
            BEGIN:VCALENDAR
            BEGIN:VEVENT
            DTSTART;VALUE=DATE:20260601
            SUMMARY:Fasting for Yogini Ekadasi
            DESCRIPTION:Fast today\, feast tomorrow
            END:VEVENT
            END:VCALENDAR
            """;

        var fast = Assert.Single(ICalParserCore.Parse(calendar));
        Assert.Equal("Fast today, feast tomorrow", fast.Description);
    }

    // ---------------------------------------------------------------------------
    // 9. "Next Ekadashi" selection — regression tests for the bug where the wrong
    //    date (e.g. July 25 instead of July 24) was shown on first open due to a
    //    race between two concurrent location loads. The selection logic itself must
    //    be deterministic: always the earliest upcoming Ekadashi for the given data.
    // ---------------------------------------------------------------------------
    private static ParsedIcalEvent Fast(int year, int month, int day, string name = "Ekadashi")
        => new(name, new DateOnly(year, month, day), string.Empty, true, false, null);

    [Fact]
    public void GetNextUpcoming_ReturnsEarliestFutureEkadashi()
    {
        var events = new[]
        {
            Fast(2026, 7, 24, "Kamika Ekadashi"),
            Fast(2026, 7, 25, "Some Other Ekadashi"),
            Fast(2026, 8, 8,  "Pavitropana Ekadashi"),
        };

        var next = ICalParserCore.GetNextUpcoming(events, new DateOnly(2026, 7, 20));

        Assert.NotNull(next);
        Assert.Equal(new DateOnly(2026, 7, 24), next!.Date);
        Assert.Equal("Kamika Ekadashi", next.Name);
    }

    [Fact]
    public void GetNextUpcoming_IsOrderIndependent()
    {
        // Regardless of the input order (which is what the concurrency race scrambled),
        // the earliest upcoming date must always be selected.
        var ordered = new[] { Fast(2026, 7, 24), Fast(2026, 7, 25), Fast(2026, 8, 8) };
        var shuffled = new[] { Fast(2026, 8, 8), Fast(2026, 7, 25), Fast(2026, 7, 24) };

        var today = new DateOnly(2026, 7, 20);
        var a = ICalParserCore.GetNextUpcoming(ordered, today);
        var b = ICalParserCore.GetNextUpcoming(shuffled, today);

        Assert.Equal(a!.Date, b!.Date);
        Assert.Equal(new DateOnly(2026, 7, 24), a.Date);
    }

    [Fact]
    public void GetUpcoming_ExcludesPastDates()
    {
        var events = new[]
        {
            Fast(2026, 7, 10),  // past
            Fast(2026, 7, 24),  // future
            Fast(2026, 8, 8),   // future
        };

        var upcoming = ICalParserCore.GetUpcoming(events, new DateOnly(2026, 7, 20));

        Assert.Equal(2, upcoming.Count);
        Assert.DoesNotContain(upcoming, e => e.Date == new DateOnly(2026, 7, 10));
    }

    [Fact]
    public void GetUpcoming_IncludesTodayItself()
    {
        var events = new[] { Fast(2026, 7, 24) };
        var upcoming = ICalParserCore.GetUpcoming(events, new DateOnly(2026, 7, 24));
        Assert.Single(upcoming);
    }

    [Fact]
    public void GetNextUpcoming_ReturnsNull_WhenNoFutureEvents()
    {
        var events = new[] { Fast(2026, 1, 1) };
        Assert.Null(ICalParserCore.GetNextUpcoming(events, new DateOnly(2026, 12, 31)));
    }
}
