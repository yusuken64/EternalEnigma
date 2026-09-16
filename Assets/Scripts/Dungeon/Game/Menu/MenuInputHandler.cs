using UnityEngine;
using UnityEngine.InputSystem;

namespace JuicyChickenGames.Menu
{
    // Sample the shared UI actions before menu managers consume their commands.
    [DefaultExecutionOrder(-100)]
    public class MenuInputHandler : MonoBehaviour
    {
        public bool MenuOpenClosedInput;
        public bool OpenSkillMenuInput;
        public bool SubmitMenuInput;
        public bool CancelMenuInput;
        public bool OptionInput;
        public PlayerInput PlayerInput;
        public InputAction NavigateActions;
        public InputAction MenuOpenCloseAction;
        public InputAction OpenSkillMenuAction;
        public InputAction SubmitAction;
        public InputAction CancelAction;
        public InputAction OpenOptions;
        public Vector2 MoveInput;
        public bool IsMoving;
        public bool IsBusy => PlayerInput != null && PlayerInput.currentActionMap?.name == "UI";

        private void Update()
        {
            var module = MenuUIInputModule.Active;
            ResetCommands();
            if (module == null || module.InputConsumed) return;
            var ui = module.UI;
            NavigateActions = ui.Navigate;
            MenuOpenCloseAction = ui.OpenMenu;
            OpenSkillMenuAction = ui.OpenSkillMenu;
            SubmitAction = ui.Submit;
            CancelAction = ui.Cancel;
            OpenOptions = ui.Options;
            MoveInput = NavigateActions.ReadValue<Vector2>();
            IsMoving = MoveInput.sqrMagnitude > 0.1f;
            MenuOpenClosedInput = MenuOpenCloseAction.WasPressedThisFrame();
            OpenSkillMenuInput = OpenSkillMenuAction.WasPressedThisFrame();
            SubmitMenuInput = SubmitAction.WasPressedThisFrame();
            CancelMenuInput = CancelAction.WasPressedThisFrame();
            OptionInput = OpenOptions.WasPressedThisFrame();
        }

        internal void SwitchToPlayerInput()
        {
            PlayerInput.SwitchCurrentActionMap("Player");
            ClearInputThisFrame();
        }

        internal void SwitchToUIInput()
        {
            PlayerInput.SwitchCurrentActionMap("UI");
            ClearInputThisFrame();
        }

        public void ClearInputThisFrame()
        {
            MenuUIInputModule.Active?.ConsumeInput();
            ResetCommands();
        }

        private void ResetCommands()
        {
            MenuOpenClosedInput = OpenSkillMenuInput = SubmitMenuInput = CancelMenuInput = OptionInput = false;
            MoveInput = Vector2.zero;
            IsMoving = false;
        }
    }
}
