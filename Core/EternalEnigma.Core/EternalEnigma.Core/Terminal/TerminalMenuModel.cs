namespace EternalEnigma.Core.Terminal;

public sealed class TerminalMenuOption
{
    public string Id { get; }
    public string Label { get; }
    public bool Enabled { get; }
    public string DisabledReason { get; }
    public TerminalMenuOption(string id, string label, bool enabled = true, string? disabledReason = null)
    { Id = string.IsNullOrEmpty(id) ? throw new ArgumentException("An option needs a stable ID.", nameof(id)) : id;
      Label = label ?? string.Empty; Enabled = enabled; DisabledReason = disabledReason ?? string.Empty; }
}

/// <summary>Pure navigation state. The caller owns activation and any game effects.</summary>
public sealed class TerminalMenuModel
{
    private readonly Stack<MenuPage> history = new();
    private MenuPage current;
    private long lastConfirmedInput = long.MinValue;
    public string Title => current.Title;
    public string? SelectedId => current.SelectedId;
    public IReadOnlyList<TerminalMenuOption> Options => current.Options;
    public int PageSize { get; }
    public int Page => current.Offset / PageSize;
    public int PageCount => Math.Max(1, (current.Options.Count + PageSize - 1) / PageSize);
    public IEnumerable<TerminalMenuOption> VisibleOptions => current.Options.Skip(current.Offset).Take(PageSize);
    public bool CanCancel => history.Count > 0;
    public TerminalMenuModel(string title, IEnumerable<TerminalMenuOption> options, int pageSize = 8)
    { PageSize = Math.Max(1, pageSize); current = new MenuPage(title, options); }
    public void Replace(string title, IEnumerable<TerminalMenuOption> options)
    {
        var selected = current.SelectedId;
        current = new MenuPage(title, options, selected);
        KeepSelectedVisible();
    }
    public void Push(string title, IEnumerable<TerminalMenuOption> options)
    { history.Push(current); current = new MenuPage(title, options); }
    public bool Cancel()
    { if (history.Count == 0) return false; current = history.Pop(); return true; }
    public void Move(int direction)
    {
        if (current.Options.Count == 0 || direction == 0) return;
        int index = current.Options.ToList().FindIndex(o => o.Id == current.SelectedId);
        index = Math.Clamp(index + Math.Sign(direction), 0, current.Options.Count - 1);
        current.SelectedId = current.Options[index].Id;
        KeepSelectedVisible();
    }
    public void Select(string id)
    { if (current.Options.Any(o => o.Id == id)) { current.SelectedId = id; KeepSelectedVisible(); } }
    public string? Confirm()
    {
        var option = current.Options.FirstOrDefault(o => o.Id == current.SelectedId);
        return option?.Enabled == true ? option.Id : null;
    }
    public string? Confirm(long inputSequence)
    {
        if (lastConfirmedInput == inputSequence) return null;
        lastConfirmedInput = inputSequence;
        return Confirm();
    }
    private void KeepSelectedVisible()
    {
        int index = current.Options.ToList().FindIndex(o => o.Id == current.SelectedId);
        current.Offset = index < 0 ? 0 : index / PageSize * PageSize;
    }
    private sealed class MenuPage
    {
        public string Title { get; }
        public IReadOnlyList<TerminalMenuOption> Options { get; }
        public string? SelectedId { get; set; }
        public int Offset { get; set; }
        public MenuPage(string title, IEnumerable<TerminalMenuOption> options, string? selected = null)
        {
            Title = title ?? string.Empty;
            var list = (options ?? Array.Empty<TerminalMenuOption>()).ToArray();
            if (list.Select(o => o.Id).Distinct(StringComparer.Ordinal).Count() != list.Length) throw new ArgumentException("Option IDs must be unique.", nameof(options));
            Options = Array.AsReadOnly(list);
            SelectedId = list.Any(o => o.Id == selected) ? selected : list.FirstOrDefault(o => o.Enabled)?.Id ?? list.FirstOrDefault()?.Id;
        }
    }
}
