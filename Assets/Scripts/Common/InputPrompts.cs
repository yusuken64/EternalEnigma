public static class InputPrompts
{
    public static string Pick(string keyboard, string pad) => ControlDeviceState.Gamepad ? pad : keyboard;
    public static string Confirm => ControlBindings.Prompt("UI/Submit", "Enter / Space", "A");
    public static string Back => ControlBindings.Prompt("UI/Cancel", "Escape", "B");
    public static string Move => ControlBindings.Prompt("Player/Move", "WASD / arrows", "Left stick / D-pad");
    public static string Navigate => ControlBindings.Prompt("UI/Navigate", "Arrow keys", "D-pad");
    public static string Interact => ControlBindings.Prompt("UI/Submit", "Enter", "A");
    public static string Attack => ControlBindings.Prompt("Player/Attack", "Space", "A");
    public static string InteractionButton => ControlBindings.PrimaryPrompt("Player/Attack");
    public static string NextHero => ControlBindings.Prompt("UI/InspectNextHero", "Tab", "RB");
    public static string PreviousHero => ControlBindings.Prompt("UI/InspectPreviousHero", "Shift+Tab", "LT");
    public static string ToggleFullControl => ControlBindings.Prompt("Player/ToggleFullControl", "F", "RS");
    public static string Inventory => ControlBindings.Prompt("Player/Menu", "Q", "X");
    public static string Skills => ControlBindings.Prompt("Player/Skills", "R", "B");
    public static string History => ControlBindings.Prompt("UI/History", "H", "");
    public static string Party => Inventory;
    public static string Equipment => ControlDeviceState.Gamepad ? Inventory + ", then right" : ControlBindings.Prompt("UI/OpenEquipment", "I", "");
    public static string Stats => ControlDeviceState.Gamepad ? Skills + ", then right" : ControlBindings.Prompt("UI/OpenStats", "T", "");
    public static string Scroll => Pick("Up / Down", "Left stick / D-pad up / down");
    public static string Actions => Pick("Right", "Left stick / D-pad right");
    public static string PresentationKey => ControlBindings.Prompt("UI/Presentation", "F11", "View");
    public static string Presentation => "Press " + PresentationKey + " for 3D view";
    public static string MenuHint => $"Use {Move} to select. Press {Confirm} to confirm. {Presentation}.";
    public static string DescriptionHint => $"Use {Scroll} to scroll. Press {Actions} for actions.";

    public static string Format(string template) => GameplayText.Format(template);

    internal static string Resolve(string template)
    {
        if (template == null) return null;
        return template.Replace("{Confirm}", Confirm).Replace("{Back}", Back)
            .Replace("{Move}", Move).Replace("{Navigate}", Navigate).Replace("{Interact}", Interact).Replace("{Attack}", Attack)
            .Replace("{ToggleFullControl}", ToggleFullControl)
            .Replace("{Inventory}", Inventory).Replace("{Skills}", Skills)
            .Replace("{Party}", Party).Replace("{Equipment}", Equipment).Replace("{Stats}", Stats)
            .Replace("{Scroll}", Scroll).Replace("{Actions}", Actions).Replace("{Presentation}", Presentation);
    }
}
