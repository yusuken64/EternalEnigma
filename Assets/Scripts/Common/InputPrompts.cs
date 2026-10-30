public static class InputPrompts
{
    public static string Pick(string keyboard, string pad) => ControlDeviceState.Gamepad ? pad : keyboard;
    public static string Confirm => ControlBindings.Prompt("UI/Submit", "Enter / Space", "A / Cross");
    public static string Back => ControlBindings.Prompt("UI/Cancel", "Escape", "B / Circle");
    public static string Move => ControlBindings.Prompt("Player/Move", "WASD / arrows", "Left stick / D-pad");
    public static string Interact => ControlBindings.Prompt("UI/Submit", "Enter", "A / Cross");
    public static string ToggleFullControl => ControlBindings.Prompt("Player/ToggleFullControl", "F", "Right stick press");
    public static string Inventory => ControlBindings.Prompt("Player/Menu", "Q", "X / Square");
    public static string Skills => ControlBindings.Prompt("Player/Skills", "R", "B / Circle");
    public static string Party => Inventory;
    public static string Equipment => ControlDeviceState.Gamepad ? Inventory + ", then right" : ControlBindings.Prompt("UI/OpenEquipment", "I", "");
    public static string Stats => ControlDeviceState.Gamepad ? Skills + ", then right" : ControlBindings.Prompt("UI/OpenStats", "T", "");
    public static string Scroll => Pick("Up / Down", "Left stick / D-pad up / down");
    public static string Actions => Pick("Right", "Left stick / D-pad right");
    public static string PresentationKey => ControlBindings.Prompt("UI/Presentation", "F11", "View / Share");
    public static string Presentation => "Press " + PresentationKey + " for 3D view";
    public static string MenuHint => $"Use {Move} to select. Press {Confirm} to confirm. {Presentation}.";
    public static string DescriptionHint => $"Use {Scroll} to scroll. Press {Actions} for actions.";

    public static string Format(string template) => GameplayText.Format(template);

    internal static string Resolve(string template)
    {
        if (template == null) return null;
        return template.Replace("{Confirm}", Confirm).Replace("{Back}", Back)
            .Replace("{Move}", Move).Replace("{Interact}", Interact)
            .Replace("{ToggleFullControl}", ToggleFullControl)
            .Replace("{Inventory}", Inventory).Replace("{Skills}", Skills)
            .Replace("{Party}", Party).Replace("{Equipment}", Equipment).Replace("{Stats}", Stats)
            .Replace("{Scroll}", Scroll).Replace("{Actions}", Actions).Replace("{Presentation}", Presentation);
    }
}
