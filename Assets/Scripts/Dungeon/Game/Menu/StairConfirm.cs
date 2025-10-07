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
