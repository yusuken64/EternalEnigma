using JuicyChickenGames.Menu;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StairConfirm : Dialog
{
    public TextMeshProUGUI PromptText;
    public Button YesButton;
    public Button NoButton;
    private Action _yesAction;
    private Action _noAction;

    public void Setup(string prompt, Action yesAction, Action noAction)
    {
        PromptText.text = prompt;
        _yesAction = yesAction;
        _noAction = noAction;
        
        SetNavigation();
    }

    private void SetNavigation()
    {
        Navigation yesNav = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnLeft = NoButton,
        };
        YesButton.navigation = yesNav;

        Navigation noNav = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnRight = YesButton,
        };
        NoButton.navigation = noNav;
    }

    public void YesClicked()
    {
        _yesAction?.Invoke();
        MenuManager.Instance.CloseAllMenus();
    }

    public void NoClicked()
    {
        _noAction?.Invoke();
        MenuManager.Instance.CloseAllMenus();
    }

    internal override void SetFirstSelect()
    {
        NoButton.Select();
    }
}
