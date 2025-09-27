namespace JuicyChickenGames.Menu
{
    using UnityEngine;
    using UnityEngine.InputSystem;

    public class PlayerInputHandler : SingletonMonoBehaviour<PlayerInputHandler>
    {
        public PlayerInput PlayerInput;

        public Vector2 moveInput { get; private set; }
        public Vector2 lookInput { get; private set; }
        public bool isMoving { get; private set; }
        public bool attackPressed { get; private set; }
        //public bool interactPressed { get; private set; }
        public bool waitPressed { get; private set; }
        public bool menuPressed { get; private set; }
        public bool skillsPressed { get; private set; }
        public bool mapPressed { get; private set; }
        public bool swapAllyPressed { get; private set; }
        public bool holdPosition { get; private set; }

        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction attackAction;
        //private InputAction interactAction;
        //private InputAction waitAction;
        private InputAction holdPositionAction;
        private InputAction menuAction;
        private InputAction skillsAction;
        private InputAction mapAction;
        private InputAction swapAllyAction;

        protected override void Initialize()
        {
            base.Initialize();
            moveAction = PlayerInput.actions["Move"];
            lookAction = PlayerInput.actions["Look"];
            attackAction = PlayerInput.actions["Attack"];
            //interactAction = PlayerInput.actions["Use"];
            //waitAction = PlayerInput.actions["Wait"];
            holdPositionAction = PlayerInput.actions["HoldPosition"];
            menuAction = PlayerInput.actions["Menu"];
            skillsAction = PlayerInput.actions["Skills"];
            mapAction = PlayerInput.actions["Map"];
            swapAllyAction = PlayerInput.actions["SwapAlly"];

            PlayerInput.SwitchCurrentActionMap("Player");
        }

        private void Update()
        {
            moveInput = moveAction.ReadValue<Vector2>();
            isMoving = moveInput.sqrMagnitude > 0.1f;

            lookInput = lookAction.ReadValue<Vector2>();

            attackPressed = attackAction.WasPressedThisFrame();
            //interactPressed = interactAction.WasPressedThisFrame();
            menuPressed = menuAction.WasPressedThisFrame();
            skillsPressed = skillsAction.WasPressedThisFrame();
            mapPressed = mapAction.WasPressedThisFrame();
            //waitPressed = waitAction.WasPressedThisFrame();
            holdPosition = holdPositionAction.IsPressed();
            swapAllyPressed = swapAllyAction.WasPressedThisFrame();

            if (PlayerInput.currentActionMap.name == "Player")
            {
                MenuInputHandler.Instance.MenuOpenClosedInput = menuPressed;
            }
            if (PlayerInput.currentActionMap.name == "Player")
            {
                MenuInputHandler.Instance.OpenSkillMenuInput = skillsPressed;
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