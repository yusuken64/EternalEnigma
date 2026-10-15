public static class InputPrompts
{
    public static string Pick(string keyboard, string pad) => ControlDeviceState.Gamepad ? pad : keyboard;
    public static string Confirm => Pick("Enter / Space", "A / Cross");
    public static string Back => Pick("Escape", "B / Circle");
    public static string Move => Pick("WASD / arrows", "Left stick / D-pad");
    public static string Interact => Pick("Enter", "A / Cross");
    public static string ToggleFullControl => Pick("F", "Right stick press");
    public static string Inventory => Pick("Q", "X / Square");
    public static string Skills => Pick("R", "LB / L1");
    public static string Party => Inventory;
    public static string Equipment => Pick("I", "X / Square, then right");
    public static string Stats => Pick("T", "LB / L1, then right");
    public static string Scroll => Pick("Up / Down", "Left stick / D-pad up / down");
    public static string Actions => Pick("Right", "Left stick / D-pad right");
    public static string Presentation => Pick("Press F11 for 3D view", "Select 3D view in the menu");
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
