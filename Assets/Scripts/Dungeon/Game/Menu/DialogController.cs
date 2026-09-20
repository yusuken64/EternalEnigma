using System;
using System.Collections.Generic;
using UnityEngine;

namespace JuicyChickenGames.Menu
{
    // Shared by dungeon and town. A dialog has exactly one owner and one close path.
    public sealed class DialogController
    {
        public readonly Stack<Dialog> Stack = new();
        public Dialog Current => Stack.Count > 0 ? Stack.Peek() : null;
        public bool Opened => Stack.Count > 0;
        public Action LateAction;
        private readonly Action closing;

        public DialogController(Action closing = null) { this.closing = closing; }

        public void Open(Dialog dialog)
        {
            if (Stack.Contains(dialog)) return;
            if (dialog.Owner != null && dialog.Owner != this)
                throw new InvalidOperationException("Dialog is already owned by another controller.");
            Current?.SaveSelection();
            dialog.Owner = this;
            dialog.gameObject.SetActive(true);
            Stack.Push(dialog);
            Common.Instance.MenuInputHandler.ClearInputThisFrame();
            Common.Instance.MenuInputHandler.SwitchToUIInput();
            MenuUIInputModule.Active?.PushDialog(dialog, dialog.transform, back: () => Close(dialog));
            LateAction = () => { if (Current == dialog) dialog.SetFirstSelect(); };
        }

        public void Close(Dialog dialog)
        {
            if (Current != dialog) return;
            closing?.Invoke();
            Stack.Pop();
            Release(dialog);
            if (!Opened) Finish();
            else LateAction = () => Current?.RestoreSelect();
        }

        private static void Release(Dialog dialog)
        {
            dialog.gameObject.SetActive(false);
            MenuUIInputModule.Active?.PopDialog(dialog);
            var callback = dialog.CloseAction;
            dialog.CloseAction = null;
            dialog.Owner = null;
            callback?.Invoke();
        }

        public void CloseAll()
        {
            while (Stack.Count > 0) Release(Stack.Pop());
            Finish();
        }

        private void Finish()
        {
            LateAction = null;
            Common.Instance.MenuInputHandler.SwitchToPlayerInput();
            Common.Instance.MenuInputHandler.ClearInputThisFrame();
        }

        public void Tick()
        {
            var action = LateAction;
            LateAction = null;
            action?.Invoke();
        }
    }
}
