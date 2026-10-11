using ScavgameTranslationUtils.Models;

namespace ScavgameTranslationUtils.UnitTests;

public class LocalizationTests
{
    private Localization _locale = new Localization();
    
    const string Sample = "testvalue";
    
    [Theory(DisplayName = "Sets keys for valid paths")]
    [InlineData("main:bandage")]
    [InlineData("buildings:drillpod")]
    [InlineData("moodles:key")]
    [InlineData("other:pain1")]
    [InlineData("character:0:eatGood:0")]
    [InlineData("notes:0:0:text")]
    [InlineData("pdaNotes:0:text")]
    public void TestValidPaths(string path)
    {
        var previousValue = _locale.GetTextByPath(path);

        Assert.True(_locale.SetTextByPath(path, Sample));
        Assert.Null(previousValue);
        Assert.Equal(Sample, _locale.GetTextByPath(path));
        Assert.Contains(path, _locale.GetPaths());
    }

    [Theory(DisplayName = "Does not set keys for invalid paths")]
    [InlineData("character:-1:eatGood:0")]
    [InlineData("character:0:eatGood:-1")]
    public void TestInvalidPaths(string path)
    {
        var previousValue = _locale.GetTextByPath(path);
        
        Assert.False(_locale.SetTextByPath(path, Sample));
        Assert.Null(previousValue);
        Assert.Null(_locale.GetTextByPath(path));
        Assert.DoesNotContain(path, _locale.GetPaths());
    }

    [Fact(DisplayName = "Deletes keys for existing texts")]
    public void TestDeleteExistingText()
    {
        Assert.True(_locale.SetTextByPath("character:0:eatGood:0", Sample));
        Assert.True(_locale.SetTextByPath("notes:0:0:Item1", Sample));
        
        Assert.Equal(2, _locale.GetPaths().Count);
        
        Assert.True(_locale.DeleteTextByPath("character:0:eatGood:0", Sample));
        Assert.True(_locale.DeleteTextByPath("notes:0:0:Item1", Sample));
        
        Assert.Empty(_locale.GetPaths());
        Assert.Null(_locale.GetTextByPath("character:0:eatGood:0"));
        Assert.Null(_locale.GetTextByPath("notes:0:0:Item1"));
    }

    [Fact(DisplayName = "Does not delete keys for nonexisting paths")]
    public void TestDeleteNonexistingText()
    {
        Assert.False(_locale.DeleteTextByPath("character:0:eatGood:0", Sample));
        Assert.False(_locale.DeleteTextByPath("notes:0:0:Item1", Sample));
    }

    [Fact(DisplayName = "Does not delete keys for intermediary nodes")]
    public void TestDeleteIntermediaryNodes()
    {
        Assert.True(_locale.SetTextByPath("character:0:eatGood:0", Sample));
        Assert.True(_locale.SetTextByPath("notes:0:0:Item1", Sample));
        
        Assert.Equal(2, _locale.GetPaths().Count);
        
        Assert.False(_locale.DeleteTextByPath("character:0:eatGood", Sample));
        Assert.False(_locale.DeleteTextByPath("notes:0:0", Sample));
        
        Assert.Equal(2, _locale.GetPaths().Count);
    }

    [Fact(DisplayName = "Deletes keys for arrays only if they are the highest index in the array")]
    public void TestDeleteLowerArrayIndices()
    {
        Assert.True(_locale.SetTextByPath("character:0:eatGood:0", Sample));
        Assert.True(_locale.SetTextByPath("character:0:eatGood:1", Sample));
        Assert.True(_locale.SetTextByPath("character:0:eatGood:2", Sample));
        Assert.True(_locale.SetTextByPath("character:0:eatGood:3", Sample));
        
        Assert.Equal(4, _locale.GetPaths().Count);
        
        Assert.False(_locale.DeleteTextByPath("character:0:eatGood:0", Sample));
        Assert.False(_locale.DeleteTextByPath("character:0:eatGood:1", Sample));
        Assert.False(_locale.DeleteTextByPath("character:0:eatGood:2", Sample));
        
        Assert.Equal(4, _locale.GetPaths().Count);
        
        Assert.True(_locale.DeleteTextByPath("character:0:eatGood:3", Sample));
        Assert.True(_locale.DeleteTextByPath("character:0:eatGood:2", Sample));
        Assert.True(_locale.DeleteTextByPath("character:0:eatGood:1", Sample));
        Assert.True(_locale.DeleteTextByPath("character:0:eatGood:0", Sample));
        
        Assert.Empty(_locale.GetPaths());
    }

    [Fact(DisplayName = "Sets keys where a string property is used for a part that is an array, when forcing conform")]
    public void TestInvalidArrayIndexWithForceConform()
    {
        Assert.True(_locale.SetTextByPath("character:0:eatGood:0", Sample));
        Assert.True(_locale.SetTextByPath("notes:0:0:Item1", Sample));
        Assert.True(_locale.SetTextByPath("character:eatBad", Sample));
        Assert.True(_locale.SetTextByPath("character:0:eatGood:good", Sample));
        Assert.True(_locale.SetTextByPath("notes:test", Sample));
        Assert.True(_locale.SetTextByPath("notes:0:test", Sample));
    }

    [Fact(DisplayName = "Does not set keys where a string property is used for a part that is an array, when not set to force conform")]
    public void TestInvalidArrayIndexWithoutForceConform()
    {
        Assert.True(_locale.SetTextByPath("character:0:eatGood:0", Sample, false));
        Assert.True(_locale.SetTextByPath("notes:0:0:Item1", Sample, false));
        Assert.False(_locale.SetTextByPath("character:eatBad", Sample, false));
        Assert.False(_locale.SetTextByPath("character:0:eatGood:good", Sample, false));
        Assert.False(_locale.SetTextByPath("notes:test", Sample, false));
        Assert.False(_locale.SetTextByPath("notes:0:test", Sample, false));
    }

    [Fact(DisplayName = "Does not set keys if the full path already points to an object or array")]
    public void TestSettingStringOnObjectOrArray()
    {
        Assert.True(_locale.SetTextByPath("character:0:eatGood:0", Sample));
        Assert.False(_locale.SetTextByPath("character:0:eatGood", Sample));
        Assert.False(_locale.SetTextByPath("character:0", Sample));
        Assert.False(_locale.SetTextByPath("character", Sample));
    }

    [Theory(DisplayName = "Sets lower array indices with strings if a higher index is set with a string")]
    [InlineData("character", 3)]
    [InlineData("character", 1)]
    [InlineData("character:3", 7)]
    [InlineData("character", 999)]
    public void TestArrayBackfill(string basePath, int index)
    {
        Assert.True(_locale.SetTextByPath($"{basePath}:{index}", Sample));
        
        Assert.All(Enumerable.Range(0, index), backfilledIndex =>
        {
            Assert.Equal("", _locale.GetTextByPath($"{basePath}:{backfilledIndex}"));
        });
        
        Assert.Equal(Sample, _locale.GetTextByPath($"{basePath}:{index}"));
    }
}