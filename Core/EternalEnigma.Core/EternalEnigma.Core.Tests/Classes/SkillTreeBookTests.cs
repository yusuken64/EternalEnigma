using EternalEnigma.ConsoleExplorer;
using EternalEnigma.Core.Classes;
using Xunit;

namespace EternalEnigma.Core.Tests.Classes;

public sealed class SkillTreeBookTests : IDisposable
{
    private const string Warrior = @"skilltree 1
class warrior
name Warrior

tier 1
  mastery   | Novice Training
  skill     | Strike | 5
  skill     | Guard | 3
tier 2
  mastery   | Adept Training
tier 3
  mastery   | Master Training
";

    private readonly string dir = Path.Combine(Path.GetTempPath(), "skilltree-" + Guid.NewGuid().ToString("N"));

    public SkillTreeBookTests()
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "warrior.skilltree"), Warrior);
    }

    public void Dispose() => Directory.Delete(dir, true);

    private SkillTreeBook Open() => SkillTreeBook.Open(dir);

    [Fact]
    public void OpensADirectoryAndReportsBadFilesWithoutFailing()
    {
        File.WriteAllText(Path.Combine(dir, "bad.skilltree"), "nonsense");

        var book = Open();

        Assert.Single(book.Files);
        Assert.Contains("bad.skilltree", book.Message);
    }

    [Fact]
    public void SelectionEditsFollowTheSkill()
    {
        var book = Open();
        book.MoveSelection(1);
        Assert.Equal("Strike", book.SelectedEntry!.SkillId);

        book.AdjustRank(-2);
        Assert.Equal(3, book.Current!.Document.Find("Strike")!.MaxRank);

        book.ShiftTier(1);
        Assert.Equal(2, book.Current.Document.Find("Strike")!.Tier);
        Assert.Equal("Strike", book.SelectedEntry!.SkillId);

        book.CycleKind();
        Assert.Equal(SkillKind.Mastery, book.Current.Document.Find("Strike")!.Kind);
        Assert.Equal(1, book.Current.Document.Find("Strike")!.MaxRank);
    }

    [Fact]
    public void InvalidEditsReportAMessageAndChangeNothing()
    {
        var book = Open();
        book.ShiftTier(-1); // Novice Training is already tier 1
        Assert.False(book.Current!.Dirty);
        Assert.Contains("no tier 0", book.Message);

        book.AdjustRank(1); // masteries have a single rank
        Assert.False(book.Current.Dirty);
        Assert.False(book.Current.CanUndo);
    }

    [Fact]
    public void AddRemoveRenameAndUndo()
    {
        var book = Open();
        book.MoveSelection(1);

        book.Add("Cleave");
        Assert.Equal(1, book.Current!.Document.Find("Cleave")!.Tier);
        Assert.Equal("Cleave", book.SelectedEntry!.SkillId);

        book.Rename("Great Cleave");
        Assert.Null(book.Current.Document.Find("Cleave"));
        Assert.NotNull(book.Current.Document.Find("Great Cleave"));

        book.Remove();
        Assert.Null(book.Current.Document.Find("Great Cleave"));

        book.Undo();
        Assert.NotNull(book.Current.Document.Find("Great Cleave"));
        book.Undo();
        book.Undo();
        Assert.False(book.Current.Dirty);
    }

    [Fact]
    public void RenameKeepsPositionAndRefusesDuplicates()
    {
        var book = Open();
        book.MoveSelection(1);

        book.Rename("Guard");
        Assert.Contains("already in the tree", book.Message);
        Assert.NotNull(book.Current!.Document.Find("Strike"));

        book.Rename("Slash");
        Assert.Equal(new[] { "Novice Training", "Slash", "Guard" },
            book.Current.Document.InTier(1).Select(e => e.SkillId).ToArray());
    }

    [Fact]
    public void SaveWritesTheFileAndClearsTheModifiedFlag()
    {
        var book = Open();
        book.MoveSelection(1);
        book.Remove();
        Assert.True(book.Current!.Dirty);

        book.Save();

        Assert.False(book.Current.Dirty);
        var reread = SkillTreeFormat.Parse(File.ReadAllText(Path.Combine(dir, "warrior.skilltree")));
        Assert.Null(reread.Find("Strike"));
    }

    [Fact]
    public void ExportThenImportCarriesATreeBetweenBooks()
    {
        var book = Open();
        book.MoveSelection(1);
        book.SwitchTree(0);
        book.Add("Whirlwind");
        string exported = Path.Combine(dir, "mine");
        book.Export(exported);

        var other = new SkillTreeBook(dir);
        other.Import(exported + SkillTreeFormat.Extension);

        Assert.Single(other.Files);
        Assert.NotNull(other.Current!.Document.Find("Whirlwind"));
        Assert.Null(other.Current.Path);
        other.Save();
        Assert.True(File.Exists(Path.Combine(dir, "warrior.skilltree")));
    }

    [Fact]
    public void ImportReplacesAnOpenTreeOfTheSameClassAndCanBeUndone()
    {
        var book = Open();
        string edited = Path.Combine(dir, "edited.skilltree");
        File.WriteAllText(edited, Warrior.Replace("Guard | 3", "Guard | 4").Replace("Strike | 5", "Strike | 2"));

        book.Import(edited);

        Assert.Single(book.Files);
        Assert.Equal(2, book.Current!.Document.Find("Strike")!.MaxRank);
        Assert.True(book.Current.Dirty);

        book.Undo();
        Assert.Equal(5, book.Current.Document.Find("Strike")!.MaxRank);
    }

    [Fact]
    public void BadImportLeavesTheBookAlone()
    {
        var book = Open();
        string bad = Path.Combine(dir, "bad.txt");
        File.WriteAllText(bad, "nope");

        book.Import(bad);

        Assert.Contains("Import failed", book.Message);
        Assert.Single(book.Files);
        Assert.False(book.AnyDirty);
    }

    [Fact]
    public void LeavingWithUnsavedChangesNeedsConfirmation()
    {
        var book = Open();
        Assert.True(book.TryLeave());

        book.MoveSelection(1);
        book.Remove();

        Assert.False(book.TryLeave());
        Assert.True(book.TryLeave());
    }

    [Fact]
    public void RenderShowsTiersSelectionAndProblems()
    {
        var book = Open();
        book.MoveSelection(1);

        var text = string.Join("\n", SkillTreeScreen.Render(book));

        Assert.Contains("Tier 2  (unlocks at level 10)", text);
        Assert.Contains("> skill     Strike", text);
        Assert.Contains("ranks 1-5  lv 1-13  15 pt", text);
        Assert.Contains("Valid:", text);

        book.Current!.Document.Remove("Adept Training");
        Assert.Contains("missing tier 2 mastery", string.Join("\n", SkillTreeScreen.Render(book)));
    }

    [Fact]
    public void RenderScrollsToKeepTheSelectionVisible()
    {
        var book = Open();
        book.MoveSelection(2);

        var lines = SkillTreeScreen.Render(book, 6);

        Assert.True(lines.Count <= 6);
        Assert.Contains(lines, l => l.Contains("> skill     Guard"));
        Assert.StartsWith("SKILL TREE", lines[0]);
    }

    [Fact]
    public void KeysDriveTheBook()
    {
        var book = Open();
        string? Prompt(string _) => "Slash";

        SkillTreeScreen.Handle(book, new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false), Prompt);
        SkillTreeScreen.Handle(book, new ConsoleKeyInfo('\0', ConsoleKey.N, false, false, false), Prompt);
        Assert.NotNull(book.Current!.Document.Find("Slash"));

        Assert.True(SkillTreeScreen.Handle(book, new ConsoleKeyInfo('\0', ConsoleKey.Escape, false, false, false), Prompt));
        Assert.False(SkillTreeScreen.Handle(book, new ConsoleKeyInfo('\0', ConsoleKey.Escape, false, false, false), Prompt));
    }
}
