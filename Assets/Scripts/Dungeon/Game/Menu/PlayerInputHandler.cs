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
        public bool planPressed { get; private set; }
        public bool holdPosition { get; private set; }
        public bool optionsPressed { get; private set; }

        private InputAction moveAction;
        private InputAction moveUpLeftAction, moveUpRightAction, moveDownLeftAction, moveDownRightAction;
        private InputAction lookAction;
        private InputAction attackAction;
        //private InputAction interactAction;
        private InputAction waitAction;
        private InputAction holdPositionAction;
        private InputAction menuAction;
        private InputAction skillsAction;
        private InputAction mapAction;
        private InputAction swapAllyAction;
        private InputAction planAction;
        private InputAction optionsAction;

        protected override void Initialize()
        {
            base.Initialize();
            moveAction = PlayerInput.actions["Move"];
            moveUpLeftAction = PlayerInput.actions["MoveUpLeft"];
            moveUpRightAction = PlayerInput.actions["MoveUpRight"];
            moveDownLeftAction = PlayerInput.actions["MoveDownLeft"];
            moveDownRightAction = PlayerInput.actions["MoveDownRight"];
            lookAction = PlayerInput.actions["Look"];
            attackAction = PlayerInput.actions["Attack"];
            //interactAction = PlayerInput.actions["Use"];
            waitAction = PlayerInput.actions["Wait"];
            holdPositionAction = PlayerInput.actions["HoldPosition"];
            menuAction = PlayerInput.actions["Menu"];
            skillsAction = PlayerInput.actions["Skills"];
            mapAction = PlayerInput.actions["Map"];
            swapAllyAction = PlayerInput.actions["SwapAlly"];
            planAction = PlayerInput.actions["Plan"];
            optionsAction = PlayerInput.actions["Options"];

            PlayerInput.SwitchCurrentActionMap("Player");
        }

        private void Update()
        {
            if (ControlBindings.SuppressInput)
            {
                moveInput = lookInput = Vector2.zero;
                isMoving = attackPressed = waitPressed = menuPressed = skillsPressed = mapPressed = swapAllyPressed = planPressed = holdPosition = optionsPressed = false;
                return;
            }
            moveInput = ReadMovement();
            isMoving = moveInput.sqrMagnitude > 0.1f;

            lookInput = lookAction.ReadValue<Vector2>();

            attackPressed = attackAction.WasPressedThisFrame();
            //interactPressed = interactAction.WasPressedThisFrame();
            menuPressed = menuAction.WasPressedThisFrame();
            skillsPressed = skillsAction.WasPressedThisFrame();
            mapPressed = mapAction.WasPressedThisFrame();
            waitPressed = waitAction.WasPressedThisFrame();
            holdPosition = holdPositionAction.IsPressed();
            swapAllyPressed = swapAllyAction.WasPressedThisFrame();
            planPressed = planAction.WasPressedThisFrame();
            optionsPressed = optionsAction.WasPressedThisFrame();


        }

        private Vector2 ReadMovement()
        {
            bool upLeft = moveUpLeftAction.IsPressed(), upRight = moveUpRightAction.IsPressed();
            bool downLeft = moveDownLeftAction.IsPressed(), downRight = moveDownRightAction.IsPressed();
            if (!upLeft && !upRight && !downLeft && !downRight) return moveAction.ReadValue<Vector2>();

            // Dedicated directions take priority while held; opposite components cancel.
            return new Vector2((upRight ? 1 : 0) + (downRight ? 1 : 0) - (upLeft ? 1 : 0) - (downLeft ? 1 : 0),
                (upLeft ? 1 : 0) + (upRight ? 1 : 0) - (downLeft ? 1 : 0) - (downRight ? 1 : 0)).normalized;
        }

        void OnEnable()
        {
            if (moveAction == null) Initialize();
        }
    }
}
