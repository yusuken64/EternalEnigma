using System.Linq;
using EternalEnigma.Core.Progression;

/// <summary>Book chapters derive from saved campaign victories; reading never grants progress.</summary>
public static class LivingAtlas
{
    public const string Title = "The Unfinished Atlas";
    public const string Revelation = "The atlas was never predicting your journey. It was learning it. These towns and dungeons are its copies. Defeat the guardian inside each copied landmark to break its hold and open the road home.";

    public static string Read(CampaignContext context)
    {
        string text = context?.State.Finished == true
            ? "The last page shows your bedroom again. This time the impossible door is gone. Small figures stand beside the bed: the companions who brought you home. The ink has finally dried."
            : context?.Finale != null ? Revelation
            : context?.Completed.Contains("story-0") != true
            ? "An unsigned atlas lies open beside your bed. Its first illustration is this very room, down to the chest. But the drawing includes a second door where your wall is solid. The remaining pages are blank, except for the last: your hometown, with a mouth beneath its streets."
            : context.Completed.Count(id => id.StartsWith("story-")) >= 3
            ? "Your companions now appear in the margins. Their poses match moments you remember, including a wound dressed only yesterday. Beyond the last charted road, the book draws another version of home. Its streets curve like teeth. The atlas is making a copy."
            : "A page that was blank now depicts the Big Slime's chamber. A line of fresh ink follows the path you took through it. You did not draw this. Beneath the picture, a small door has appeared in the shape of the one beside your bed.";
        return "<b>" + Title + "</b>\n\n" + text;
    }

    public static string AfterClear(CampaignContext context, string id)
    {
        if (!id.StartsWith("story-")) return null;
        if (id == "story-0") return "<b>A page has changed</b>\nThe atlas in your pack falls open. Its blank page now shows the slime's chamber, with your footprints drawn in wet ink. Beside the picture is the impossible door from your bedroom.";
        return "<b>The atlas remembers</b>\n" + (context.Completed.Count(x => x.StartsWith("story-")) >= 3
            ? "Familiar companions fill the margins. Beyond your route, the atlas draws a second home: empty windows above a street of teeth. Something is copying the places you have visited."
            : "Another room appears on a blank page. The drawing includes a dropped item you left behind. This book knows more than the shape of the land.");
    }
}
