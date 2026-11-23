namespace JuicyChickenGames.Menu
{
    using UnityEngine;
    using UnityEngine.InputSystem;

    [DefaultExecutionOrder(-100)] // Consumers must see this frame's press/release, including the end of a walk tween.
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
        private bool holdingPosition;
        public bool holdPosition { get => holdingPosition; private set { if (holdingPosition == value) return; holdingPosition = value; HudRefresh.Request(HudChange.Dungeon); } }
        public bool fastWalkHeld { get; private set; }
        public bool optionsPressed { get; private set; }

        private InputAction moveAction;
        private Vector2 pressedMove;
        private int movePressFrame = -1;
        private bool moveEngaged;
        private InputAction moveUpLeftAction, moveUpRightAction, moveDownLeftAction, moveDownRightAction;
        private InputAction lookAction;
        private InputAction attackAction;
        //private InputAction interactAction;
        private InputAction waitAction;
        private InputAction holdPositionAction;
        private InputAction fastWalkAction;
        private InputAction menuAction;
        private InputAction skillsAction;
        private InputAction mapAction;
        private InputAction swapAllyAction;
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
            fastWalkAction = PlayerInput.actions["FastWalk"];
            menuAction = PlayerInput.actions["Menu"];
            skillsAction = PlayerInput.actions["Skills"];
            mapAction = PlayerInput.actions["Map"];
            swapAllyAction = PlayerInput.actions["SwapAlly"];
            optionsAction = PlayerInput.actions["Options"];

            PlayerInput.SwitchCurrentActionMap("Player");
        }

        private void Update()
        {
            if (ControlBindings.SuppressInput)
            {
                moveInput = lookInput = Vector2.zero;
                isMoving = attackPressed = waitPressed = menuPressed = skillsPressed = mapPressed = swapAllyPressed = holdPosition = fastWalkHeld = optionsPressed = false;
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
            fastWalkHeld = fastWalkAction.IsPressed();
            swapAllyPressed = swapAllyAction.WasPressedThisFrame();
            optionsPressed = optionsAction.WasPressedThisFrame();


        }

        private Vector2 ReadMovement()
        {
            bool upLeft = moveUpLeftAction.IsPressed(), upRight = moveUpRightAction.IsPressed();
            bool downLeft = moveDownLeftAction.IsPressed(), downRight = moveDownRightAction.IsPressed();
            if (!upLeft && !upRight && !downLeft && !downRight)
            {
                var value = moveAction.ReadValue<Vector2>();
                return movePressFrame == Time.frameCount && value.sqrMagnitude <= .1f ? pressedMove : value;
            }

            // Dedicated directions take priority while held; opposite components cancel.
            return new Vector2((upRight ? 1 : 0) + (downRight ? 1 : 0) - (upLeft ? 1 : 0) - (downLeft ? 1 : 0),
                (upLeft ? 1 : 0) + (upRight ? 1 : 0) - (downLeft ? 1 : 0) - (downRight ? 1 : 0)).normalized;
        }

        private void CaptureMove(InputAction.CallbackContext context)
        {
            var value = context.ReadValue<Vector2>();
            bool engaged = value.sqrMagnitude > .1f;
            if (engaged && !moveEngaged) { pressedMove = value; movePressFrame = Time.frameCount; }
            moveEngaged = engaged;
        }

        void OnEnable()
        {
            if (moveAction == null) Initialize();
            moveAction.performed += CaptureMove;
            moveAction.canceled += CaptureMove;
        }

        void OnDisable()
        {
            if (moveAction == null) return;
            moveAction.performed -= CaptureMove;
            moveAction.canceled -= CaptureMove;
            moveEngaged = false;
            movePressFrame = -1;
        }
    }
}
