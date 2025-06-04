namespace JuicyChickenGames.Menu
{
    using UnityEngine;
    using UnityEngine.InputSystem;

    public class PlayerInputHandler : SingletonMonoBehaviour<PlayerInputHandler>
    {
        public PlayerInput PlayerInput;

        public Vector2 moveInput { get; private set; }
        public bool isMoving { get; private set; }
        public bool attackPressed { get; private set; }
        public bool interactPressed { get; private set; }
        public bool waitPressed { get; private set; }
        public bool menuPressed { get; private set; }
        public bool holdPosition { get; private set; }

        private InputAction moveAction;
        private InputAction attackAction;
        private InputAction interactAction;
        //private InputAction waitAction;
        private InputAction holdPositionAction;
        private InputAction menuAction;

        protected override void Initialize()
        {
            base.Initialize();
            moveAction = PlayerInput.actions["Move"];
            attackAction = PlayerInput.actions["Attack"];
            interactAction = PlayerInput.actions["Use"];
            //waitAction = PlayerInput.actions["Wait"];
            holdPositionAction = PlayerInput.actions["HoldPosition"];
            menuAction = PlayerInput.actions["Menu"];

            PlayerInput.SwitchCurrentActionMap("Player");
        }

        private void Update()
        {
            moveInput = moveAction.ReadValue<Vector2>();
            isMoving = moveInput.sqrMagnitude > 0.1f;

            attackPressed = attackAction.WasPressedThisFrame();
            interactPressed = interactAction.WasPressedThisFrame();
            menuPressed = menuAction.WasPressedThisFrame();
            //waitPressed = waitAction.WasPressedThisFrame();
            holdPosition = holdPositionAction.IsPressed();

            if (PlayerInput.currentActionMap.name == "Player")
            {
                MenuInputHandler.Instance.MenuOpenClosedInput = menuPressed;
            }
        }

        void OnEnable()
        {
            PlayerInput.onControlsChanged += OnControlsChanged;
        }

        void OnDisable()
        {
            PlayerInput.onControlsChanged -= OnControlsChanged;
        }

        private void OnControlsChanged(PlayerInput obj)
        {
            Debug.Log("Control scheme changed to: " + obj.currentControlScheme);
        }

    }
}