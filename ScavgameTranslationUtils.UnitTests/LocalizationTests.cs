using ScavgameTranslationUtils.Models;

namespace ScavgameTranslationUtils.UnitTests;

public class LocalizationTests
{
    public static IEnumerable<object[]> ValidPaths => ((string[])[
        // EN.json keys
        "main:bandage",
        "buildings:drillpod",
        "moodles:key",
        "other:pain1",
        "character:0:eatGood:0",
        "notes:0:0:text",
        "pdaNotes:0:text",
        // Non-EN.json keys that might still be valid according to the schema
        "character:0:notpresentinlocale:0",
    ]).Select(x => new object[] { x }).ToArray();
    
    public static IEnumerable<object[]> InvalidPaths => ((string[])[
        // Intermediary nodes
        "main",
        "buildings",
        "moodles",
        "other",
        "character",
        "character:0",
        "character:0:eatGood",
        "notes",
        "notes:0",
        "notes:0:0",
        "pdaNotes",
        "pdaNotes:0",
        // Paths that are invalid in one way or another
        "notavalidfield",
        "character:notanindex:eatGood:0",
        "character:-1:eatGood:0",
        "character:0:eatGood:notanindex",
        "character:0:eatGood:-1",
        "notes:notanindex:0:text",
        "notes:-1:0:text",
        "notes:0:notanindex:text",
        "notes:0:-1:text",
        "notes:0:0:notavalidfield",
        "pdaNotes:notanindex:text",
        "pdaNotes:-1:text",
        "pdaNotes:0:notavalidfield",
        // Weird stuff
        "this:probably:does:not:exist",
        "",
        "      "
    ]).Select(x => new object[] { x }).ToArray();
    
    [Theory(DisplayName = "Test setting keys by paths for supported paths")]
    [MemberData(nameof(ValidPaths))]
    public void TestKnownPaths(string path)
    {
        const string expectedValue = "testvalue";
        
        var localization = new Localization();
        var previousValue = localization.GetTextByPath(path);
        localization.SetTextByPath(path, expectedValue);
        var currentValue = localization.GetTextByPath(path);
        var keys = localization.GetPaths();

        Assert.Null(previousValue);
        Assert.Equal(expectedValue, currentValue);
        Assert.Contains(path, keys);
    }
    
    [Theory(DisplayName = "Test setting keys by paths for unsupported paths")]
    [MemberData(nameof(InvalidPaths))]
    public void TestUnknownPaths(string path)
    {
        const string expectedValue = "testvalue";
        
        var localization = new Localization();
        var previousValue = localization.GetTextByPath(path);
        localization.SetTextByPath(path, expectedValue);
        var currentValue = localization.GetTextByPath(path);
        var keys = localization.GetPaths();

        Assert.Null(previousValue);
        Assert.Null(currentValue);
        Assert.DoesNotContain(path, keys);
    }
}
