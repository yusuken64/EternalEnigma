using UnityEngine;

public enum DungeonAnimationMode { Normal, AnimateAlliedActions, AnimateControlledHeroActions, NoAnimations }

public static class DungeonPreferences
{
    public static bool? FullControlOverride;
    public static DungeonAnimationMode? AnimationOverride;
    public static bool FullControl
    {
        get => !AutoplayRunner.BlocksPlayerInput &&
            (FullControlOverride ?? GamePreferences.GetInt("Dungeon.FullControl", 0) != 0);
        set { GamePreferences.SetInt("Dungeon.FullControl", value ? 1 : 0); GamePreferences.Save(); }
    }
    public static DungeonAnimationMode AnimationMode
    {
        get => AutoplayRunner.SuppressesAnimations ? DungeonAnimationMode.NoAnimations :
            AutoplayRunner.PlaybackAnimationMode ?? AnimationOverride ?? SavedAnimationMode;
        set { GamePreferences.SetInt("Dungeon.AnimationModeV2", (int)value); GamePreferences.Save(); }
    }
    private static DungeonAnimationMode SavedAnimationMode
    {
        get
        {
            if (GamePreferences.HasKey("Dungeon.AnimationModeV2"))
                return (DungeonAnimationMode)Mathf.Clamp(GamePreferences.GetInt("Dungeon.AnimationModeV2"), 0, 3);
            int old = Mathf.Clamp(GamePreferences.GetInt("Dungeon.AnimationMode", 0), 0, 3);
            var migrated = old == 0 ? DungeonAnimationMode.Normal :
                old == 3 ? DungeonAnimationMode.NoAnimations : DungeonAnimationMode.AnimateControlledHeroActions;
            GamePreferences.SetInt("Dungeon.AnimationModeV2", (int)migrated);
            GamePreferences.Save();
            return migrated;
        }
    }
    public static string SpeedLabel => AnimationMode switch
    {
        DungeonAnimationMode.AnimateAlliedActions => "Animate allied actions",
        DungeonAnimationMode.AnimateControlledHeroActions => "Animate only controlled hero actions",
        DungeonAnimationMode.NoAnimations => "No animations",
        _ => "Normal"
    };
}
