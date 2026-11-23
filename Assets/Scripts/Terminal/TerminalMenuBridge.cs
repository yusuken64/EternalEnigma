using System;
using System.Collections.Generic;
using EternalEnigma.Core.Terminal;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>One menu input owner; retained uGUI remains the clickable production view.</summary>
[DisallowMultipleComponent]
public sealed class TerminalMenuBridge : MonoBehaviour
{
    private MonoBehaviour owner;
    private Transform root;
    private MainMenu mainMenu;
    private TerminalMenuSession session;
    private readonly TerminalMenuStyle style = new();
    private float nextStyle;
    public TerminalMenuModel Model => session?.Model;

    public static bool Process(MenuUIInputModule module, MonoBehaviour owner, Transform root, Action back)
    {
        var bridge = module.GetComponent<TerminalMenuBridge>();
        if (!TerminalMode.Effective)
        {
            if (bridge != null) bridge.Release();
            return false;
        }
        if (bridge == null) bridge = module.gameObject.AddComponent<TerminalMenuBridge>();
        return bridge.ProcessInput(module, owner, root);
    }

    private bool ProcessInput(MenuUIInputModule module, MonoBehaviour currentOwner, Transform currentRoot)
    {
        if (currentOwner == null)
        {
            if (mainMenu == null) mainMenu = FindFirstObjectByType<MainMenu>();
            if (mainMenu != null && mainMenu.NavigationHandler.gameObject.activeInHierarchy)
            {
                currentOwner = mainMenu;
                // The authored buttons live on a sibling Canvas, outside the
                // MainMenu controller hierarchy. Bind their actual focus root.
                currentRoot = mainMenu.StartButton.transform.parent;
            }
        }
        if (currentOwner == null || currentRoot == null) { Release(); return false; }
        if (owner != currentOwner || root != currentRoot)
        {
            Release();
            owner = currentOwner; root = currentRoot;
            session = new TerminalMenuSession(root, () => owner != null && root != null &&
                root.gameObject.activeInHierarchy && (!module.HasDialog ? owner == mainMenu : module.OwnsFocus(owner)),
                () => AdditionalControls(module));
        }
        RefreshStyle();

        var current = EventSystem.current?.currentSelectedGameObject;
        var selected = current != null ? current.GetComponent<Selectable>() : null;
        session.Select(selected);
        if (module.InputConsumed) { session.Refresh(); return false; }
        if (module.UI.Cancel.WasPressedThisFrame() || module.UI.Options.WasPressedThisFrame())
        { session.Refresh(); return false; }
        // Native editors, dropdown popups and read-only scrolling keep their own controls.
        if (selected is TMP_InputField || selected is InputField || selected is TrainerPreviewScroll ||
            selected is TMP_Dropdown || selected is Dropdown)
        { session.Refresh(); return false; }
        if (module.UI.Submit.WasPressedThisFrame())
        {
            bool validSelection = selected != null && module.Allows(selected.gameObject) && session.Selected == selected;
            if (validSelection) session.Confirm(Time.frameCount);
            else module.RestoreFocus();
            module.ConsumeInput();
            // The parent module suppresses native navigation once input is consumed,
            // but must still process pointer edges this frame (including held navigation).
            return false;
        }
        session.Refresh();
        var move = module.NavigationStep;
        // Horizontal input remains available to party tabs, sliders and native widgets.
        if (move.y == 0) return false;
        if (selected == null || !module.Allows(selected.gameObject)) module.RestoreFocus();
        else
        {
            var next = move.y > 0 ? selected.FindSelectableOnUp() : selected.FindSelectableOnDown();
            if (next != null && module.Allows(next.gameObject) && MenuUIInputModule.IsUsable(next.gameObject) && session.Select(next))
                next.Select();
            else if (selected.navigation.mode == Navigation.Mode.Automatic)
            {
                session.Model.Move(move.y > 0 ? -1 : 1);
                if (session.Selected != null && MenuUIInputModule.IsUsable(session.Selected.gameObject)) session.Selected.Select();
            }
        }

        module.ConsumeInput();
        return false;
    }

    private void LateUpdate()
    {
        if (!TerminalMode.Effective || owner == null || root == null || !root.gameObject.activeInHierarchy) Release();
        // Rebinding suspends menu input, but its newly enabled prompt still needs
        // the same styling as the settings hierarchy that owns it.
        else if (ControlBindings.IsCapturing) RefreshStyle();
    }

    private void RefreshStyle()
    {
        if (Time.unscaledTime < nextStyle) return;
        var screen = Common.InstanceOrNull?.GetComponentInChildren<TerminalScreen>(true);
        // MainMenu has its own reversible title/button treatment.
        if (owner != mainMenu && screen != null) style.Refresh(root, screen.FontAsset, opaqueBackdrop: true);
        nextStyle = Time.unscaledTime + .1f;
    }

    private IEnumerable<Selectable> AdditionalControls(MenuUIInputModule module)
    {
        // Party tabs and Options belong to the party focus scope but are retained
        // in the scene HUD, outside the dialog's transform hierarchy.
        if (owner is not PartyMenu party || party.HudTabs == null) yield break;
        foreach (var control in party.HudTabs.GetComponentsInChildren<Selectable>())
            if (module.Allows(control.gameObject)) yield return control;
        var options = party.HudTabs.OptionsButton;
        if (options != null && module.Allows(options.gameObject)) yield return options;
    }
    private void Release()
    {
        style.Restore();
        owner = null; root = null; session = null; nextStyle = 0;
    }
    private void OnDisable() => Release();
}
