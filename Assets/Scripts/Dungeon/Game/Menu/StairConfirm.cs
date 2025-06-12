using JuicyChickenGames.Menu;
using System;
using UnityEngine;
using UnityEngine.UI;

public class StairConfirm : Dialog
{
    public Button YesButton;
    public Button NoButton;
    private Action _yesAction;
    private Action _noAction;

    public void Setup(Action yesAction, Action noAction)
    {
        _yesAction = yesAction;
        _noAction = noAction;
    }

    public void YesClicked()
    {
        _yesAction?.Invoke();
        MenuManager.Instance.CloseMenu();
    }

    public void NoClicked()
    {
        _noAction?.Invoke();
        MenuManager.Instance.CloseMenu();
    }

    internal override void SetFirstSelect()
    {
        NoButton.Select();
    }
}
