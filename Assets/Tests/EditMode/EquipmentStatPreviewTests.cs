using System.Linq;
using NUnit.Framework;

public class EquipmentStatPreviewTests
{
    [Test]
    public void CoreColumnsKeepAbsoluteTotalsAndUnchangedStats()
    {
        var before=new Stats { Strength=5,Defense=8,MagicPower=3 };
        var after=new Stats(before) { Strength=11 };
        var preview=new EquipmentStatPreview(before,after,false);
        Assert.That(preview.Caption,Is.EqualTo("Equip preview"));
        Assert.That(preview.Values.Select(value=>value.Label),Is.EqualTo(new[]{"Attack","Defense","Magic"}));
        Assert.That(preview.Values[0].BeforeText,Is.EqualTo("5"));
        Assert.That(preview.Values[0].AfterText,Is.EqualTo("11"));
        Assert.That(preview.Values[0].DeltaText,Is.EqualTo("+6"));
        Assert.That(preview.Values[1].AfterText,Is.EqualTo("8"));
        Assert.That(preview.Values[1].DeltaText,Is.EqualTo("—"));
        Assert.That(new EquipmentStatPreview(after,before,true).Values[0].DeltaText,Is.EqualTo("−6"));
    }

    [Test]
    public void MixedChangesIncludeSecondaryStatsAndPercentTotals()
    {
        var before=new Stats {Strength=11,Defense=8,HPMax=50,HitBonus=.05f,DropRate=1f,HPRegenAcccumlateThreshold=10};
        var after=new Stats(before) {Strength=5,Defense=12,HPMax=60,HitBonus=.1f,DropRate=1.25f,HPRegenAcccumlateThreshold=8};
        var values=new EquipmentStatPreview(before,after,false).Values.ToDictionary(value=>value.Label);
        Assert.That(values["Attack"].Direction,Is.EqualTo(-1));
        Assert.That(values["Defense"].Direction,Is.EqualTo(1));
        Assert.That(values["HP"].AfterText,Is.EqualTo("60"));
        Assert.That(values["Hit"].BeforeText,Is.EqualTo("85%"));
        Assert.That(values["Hit"].AfterText,Is.EqualTo("90%"));
        Assert.That(values["Hit"].DeltaText,Is.EqualTo("+5%"));
        Assert.That(values["Loot"].DeltaText,Is.EqualTo("+0.25×"));
        Assert.That(values["HP recovery"].Direction,Is.EqualTo(-1),"Color represents numeric direction, including recovery intervals.");
        Assert.That(values.ContainsKey("Mana"),Is.False);
    }
}
