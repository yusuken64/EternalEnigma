using System;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Terminal;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Binds terminal navigation to live production controls. The controls still own their
/// transactions: TownServices, equipment transfers and turn commands are never copied here.
/// </summary>
internal sealed class TerminalMenuSession
{
    private readonly Transform root;
    private readonly Func<bool> ownsFocus;
    private readonly Dictionary<string, Selectable> controls = new();
    private readonly Dictionary<string, string> labels = new();
    private long lastInputSequence = long.MinValue;
    internal TerminalMenuModel Model { get; }
    internal Selectable Selected => Model.SelectedId != null && controls.TryGetValue(Model.SelectedId, out var value) ? value : null;

    internal TerminalMenuSession(Transform root, Func<bool> ownsFocus)
    {
        this.root = root;
        this.ownsFocus = ownsFocus;
        Model = new TerminalMenuModel(root != null ? root.name : "Menu", Array.Empty<TerminalMenuOption>());
        Refresh();
    }

    // IDs refer to the actual row, not its index. Rebuilding a list must not activate
    // the replacement that happens to occupy the old row's position.
    private static string Id(Selectable value) => value.GetInstanceID().ToString();
    internal static string Label(Selectable value)
    {
        var texts = value.GetComponentsInChildren<TMP_Text>().Where(t => t.isActiveAndEnabled);
        string label = string.Join(" ", texts.Select(t => t.text.Trim()).Where(t => t.Length > 0));
        if (label.Length == 0)
        {
            var legacy = value.GetComponentInChildren<Text>();
            label = legacy != null ? legacy.text : value.name;
        }
        if (value is Toggle toggle) label = (toggle.isOn ? "[x] " : "[ ] ") + label;
        if (value is Slider slider) label += $" {slider.value:0.##}";
        return label;
    }

    internal bool Refresh()
    {
        var selected = Model.SelectedId;
        labels.TryGetValue(selected ?? "", out var previousLabel);
        controls.Clear();
        labels.Clear();
        var options = new List<TerminalMenuOption>();
        if (root != null && root.gameObject.activeInHierarchy)
            foreach (var selectable in root.GetComponentsInChildren<Selectable>())
            {
                if (!selectable.isActiveAndEnabled) continue;
                string id = Id(selectable), label = Label(selectable);
                controls[id] = selectable;
                labels[id] = label;
                bool enabled = selectable.IsInteractable();
                options.Add(new TerminalMenuOption(id, label, enabled, enabled ? null : "Unavailable"));
            }
        Model.Replace(root != null ? root.name : "Menu", options);
        return selected != null && (!labels.TryGetValue(selected, out var currentLabel) || previousLabel != currentLabel);
    }

    internal bool Select(Selectable value)
    {
        if (value == null || !controls.ContainsKey(Id(value))) return false;
        Model.Select(Id(value));
        return true;
    }

    internal bool Confirm(long inputSequence)
    {
        if (lastInputSequence == inputSequence) return false;
        lastInputSequence = inputSequence;
        string selected = Model.SelectedId;
        if (root == null || !root.gameObject.activeInHierarchy || !ownsFocus()) return false;
        if (Refresh() || selected == null || Model.SelectedId != selected) return false;
        string id = Model.Confirm(inputSequence);
        if (id == null || !controls.TryGetValue(id, out var control) || !MenuUIInputModule.IsUsable(control.gameObject)) return false;
        // OnSubmit invokes the exact authored callback, including its current actor,
        // stock, bag membership, target and affordability validation.
        return ExecuteEvents.Execute(control.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
    }
}
