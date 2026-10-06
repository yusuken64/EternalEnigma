public static class InputPrompts
{
    public static string Pick(string keyboard, string pad) => ControlDeviceState.Gamepad ? pad : keyboard;
    public static string Confirm => Pick("Enter / Space", "A / Cross");
    public static string Back => Pick("Escape", "B / Circle");
    public static string Move => Pick("WASD / arrows", "Left stick / D-pad");
    public static string Interact => Pick("Enter", "A / Cross");
    public static string ToggleFullControl => Pick("F", "Right stick (press)");
    public static string Inventory => Pick("Q", "X / Square");
    public static string Skills => Pick("R", "LB / L1");
    public static string Party => Pick("P", "B / Circle");

    public static string Format(string template)
    {
        if (template == null) return null;
        return template.Replace("{Confirm}", Confirm).Replace("{Back}", Back)
            .Replace("{Move}", Move).Replace("{Interact}", Interact)
            .Replace("{ToggleFullControl}", ToggleFullControl)
            .Replace("{Inventory}", Inventory).Replace("{Skills}", Skills)
            .Replace("{Party}", Party);
    }
}
