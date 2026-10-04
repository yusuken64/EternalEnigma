using UnityEngine;
using System.Collections.Generic;
using JuicyChickenGames.Menu;
using System.Linq;
using System;

public class PlayerController : MonoBehaviour
{
    // === Input Timing ===
    private float holdTime = 0f;
    private float menuCooldown = 0f;
    private float repeatTime = 0.1f;
    private const float DiagonalReleaseGrace = 0.12f;
    private float lastDiagonalInputTime = float.NegativeInfinity;

    private static bool IsDiagonal(Facing facing) =>
        facing == Facing.UpLeft || facing == Facing.UpRight || facing == Facing.DownLeft || facing == Facing.DownRight;

    // === Dependencies ===
    private CheatConsole _cheatConsole;
    public CameraController CameraController;
    public Inventory Inventory;
    public TargetIndicator TargetIndicator;

    // === Controlled Character ===
    public Ally ControlledAlly { get; private set; }
    public Ally PartyLeader { get; private set; }
    private bool releaseInput;
    private bool heldWalk;
    internal bool CanContinueHeldWalk => heldWalk && isActiveAndEnabled && !releaseInput &&
        CurrentControlMode == PlayerControlMode.FollowAlly && !ShouldBlockInput() &&
        MenuManager.Instance?.Opened != true && Common.Instance.Travel.IsTransitioning != true &&
        PlayerInputHandler.Instance != null && PlayerInputHandler.Instance.isMoving &&
        !PlayerInputHandler.Instance.holdPosition;

    internal void StopHeldWalk()
    {
        heldWalk = false;
        if (Game.Instance != null)
            foreach (var ally in Game.Instance.Allies) ally?.HeroAnimator?.StopWalkContinuation();
    }

    private void OnDisable() => StopHeldWalk();
    public void FocusCommand(Ally ally)
    {
        bool changedAlly = ControlledAlly != ally;
        if (changedAlly) StopHeldWalk();
        if (changedAlly && ControlledAlly != null)
        { ControlledAlly.IsWaitingForPlayerInput = false; ControlledAlly.SetToCPU(); }
        ControlledAlly = ally;
        if (ally == null) return;
        if (changedAlly)
        {
            ally.SetToPlayer();
            CameraController.SetFollowTarget(ally.CirlcleRenderer.transform);
            holdTime = 0; menuCooldown = 0; releaseInput = true;
        }
        TargetIndicator.gameObject.SetActive(false);
        CurrentControlMode = PlayerControlMode.FollowAlly;
    }
    public Vector3Int TilemapPosition => ControlledAlly.TilemapPosition;
    public Stats FinalStats => ControlledAlly.FinalStats;
    public Stats DisplayedStats => ControlledAlly.DisplayedStats;
    public Vitals Vitals => ControlledAlly.Vitals;
    public Vitals DisplayedVitals => ControlledAlly.DisplayedVitals;

    // === team stuff ====
    public int Floor;
    public int Gold;
    internal bool PendingRetreat;

    // === Control Mode ===
    public PlayerControlMode CurrentControlMode { get; set; }
    public Minimap Minimap;

    private void Start()
    {
        TargetIndicator.gameObject.SetActive(false);
        _cheatConsole = FindFirstObjectByType<CheatConsole>();
    }

    private void Update()
    {
        if (!CanContinueHeldWalk) StopHeldWalk();
        if (ShouldBlockInput()) return;

        if (releaseInput)
        {
            var input = PlayerInputHandler.Instance;
            if (input.isMoving || input.attackPressed || input.menuPressed || input.skillsPressed) return;
            releaseInput = false;
            return;
        }
        UpdateTimers();

        switch (CurrentControlMode)
        {
            case PlayerControlMode.FollowAlly:
                HandleMovementInput();
                break;
        }
    }

    private bool ShouldBlockInput()
    {
        if (ControlledAlly == null || Game.Instance == null || !Game.Instance.IsReady) return true;
        if (AutoplayRunner.BlocksPlayerInput) return true;
        if (MenuUIInputModule.Active?.InputConsumed == true || Common.Instance.GlobalSettings.IsOpen)
		{
            return true;
		}

        if (_cheatConsole != null && _cheatConsole.ScreenObject.activeSelf)
            return true;

        return false;
    }

    private void UpdateTimers()
    {
        menuCooldown += Time.deltaTime;

        if (PlayerInputHandler.Instance.isMoving)
        {
            holdTime += Time.deltaTime;
        }
        else
        {
            holdTime = 0;
        }
    }

    private void LateUpdate()
    {
        if (ControlledAlly == null) { return; }
        switch (CurrentControlMode)
        {
            case PlayerControlMode.FollowAlly:
                CameraController.SetFollowTarget(ControlledAlly.CirlcleRenderer.transform);
                break;
        }
    }

    private void HandleMovementInput()
    {
        if (ControlledAlly.IsWaitingForPlayerInput && menuCooldown > 0.2f)
        {
            DeterminePlayerAction();
        }
    }

    private void DeterminePlayerAction()
    {
        var originalPosition = new Vector3Int(ControlledAlly.TilemapPosition.x, ControlledAlly.TilemapPosition.y);
        var newMapPosition = new Vector3Int(ControlledAlly.TilemapPosition.x, ControlledAlly.TilemapPosition.y);

        if (PlayerInputHandler.Instance.waitPressed) { StopHeldWalk(); ControlledAlly.SetAction(new WaitAction()); return; }
        Vector2 move = PlayerInputHandler.Instance.moveInput;

        if (move.sqrMagnitude >= 0.01f)
        {
            // Normalize the input so diagonal directions are consistent
            move.Normalize();

            Facing? desired = null;
            if (move.x < -0.5f && move.y > 0.5f)
                desired = Facing.UpLeft;
            else if (move.x > 0.5f && move.y > 0.5f)
                desired = Facing.UpRight;
            else if (move.x < -0.5f && move.y < -0.5f)
                desired = Facing.DownLeft;
            else if (move.x > 0.5f && move.y < -0.5f)
                desired = Facing.DownRight;
            else if (move.y > 0.5f)
                desired = Facing.Up;
            else if (move.x < -0.5f)
                desired = Facing.Left;
            else if (move.y < -0.5f)
                desired = Facing.Down;
            else if (move.x > 0.5f)
                desired = Facing.Right;

            if (desired.HasValue)
            {
                bool diagonalDesired = IsDiagonal(desired.Value);
                // Keys are rarely released on the same frame; keep the diagonal briefly so
                // letting go of one key doesn't snap the facing to a cardinal direction.
                bool releasingDiagonal = !diagonalDesired && IsDiagonal(ControlledAlly.CurrentFacing) &&
                    Time.unscaledTime - lastDiagonalInputTime < DiagonalReleaseGrace;
                if (diagonalDesired) lastDiagonalInputTime = Time.unscaledTime;
                if (!releasingDiagonal) ControlledAlly.SetFacing(desired.Value);
            }
        }

        if (!PlayerInputHandler.Instance.holdPosition)
        {
            TargetIndicator.gameObject.SetActive(false);
            if (move.sqrMagnitude >= .01f && holdTime > repeatTime)
            {
                holdTime = 0f;
                var offset = Dungeon.GetFacingOffset(ControlledAlly.CurrentFacing);
                if (Game.Instance.CurrentDungeon.CanWalkTo(newMapPosition, newMapPosition + offset))
                {
                    newMapPosition += offset;

                    var destinationChar = Game.Instance.AllCharacters.FirstOrDefault(x => x.TilemapPosition == newMapPosition);

                    if (destinationChar == null)
                    {
                        heldWalk = true;
                        ControlledAlly.SetAction(new MovementAction(ControlledAlly, originalPosition, newMapPosition) { ManualWalkCommand = true });
                        return;
                    }
                    else if (destinationChar is Enemy mimic && EnemyBehavior.IsDisguised(mimic))
                    {
                        ControlledAlly.SetAction(new RevealMimicAction(mimic));
                        return;
                    }
                    else if (destinationChar.Team == ControlledAlly.Team &&
                        destinationChar.CanMove())
                    {
                        heldWalk = true;
                        ControlledAlly.SetAction(new SwapAllyPositionAction(ControlledAlly, destinationChar) { ManualWalkCommand = true });
                        return;
                    }

                    //else you can't move
                }
                StopHeldWalk();
            }
        }
		else
        {
            TargetIndicator.gameObject.SetActive(true);
            var offset = Dungeon.GetFacingOffset(ControlledAlly.CurrentFacing);
            newMapPosition += offset;
            TargetIndicator.SetTargetingPosition(newMapPosition);
        }

        if (PlayerInputHandler.Instance.attackPressed)
        {
            StopHeldWalk();
            var prop = Game.Instance.CurrentDungeon.PropAt(ControlledAlly.TilemapPosition + Dungeon.GetFacingOffset(ControlledAlly.CurrentFacing));
            if (prop != null && prop.Definition.Kind == EternalEnigma.Core.World.DungeonSceneryKind.Container && !PlayerInputHandler.Instance.holdPosition)
            { ControlledAlly.SetAction(new InteractAction(prop)); return; }
            if (ControlledAlly.currentInteractable != null && !PlayerInputHandler.Instance.holdPosition)
            {
                if (ControlledAlly.currentInteractable is Stairs stairs)
                    ShowStairPrompt(stairs);
                else
                    ControlledAlly.SetAction(new InteractAction(ControlledAlly.currentInteractable));
                return;
            }

            var offset = Dungeon.GetFacingOffset(ControlledAlly.CurrentFacing);
            var targetAlly = Game.Instance.Allies.FirstOrDefault(x => x.TilemapPosition == ControlledAlly.TilemapPosition + offset);
            if (targetAlly != null)
			{
                //FindObjectOfType<MenuManager>().OpenAllyMenu(targetAlly);
                return;
            }
            else if (ControlledAlly.IsRangedAttack(out GameObject projectilePrefab))
            {
                //ControlledAlly.Equipment.EquipedWeapon;
                var rangedAttackTargetPosition =
                    Game.Instance.CurrentDungeon.GetRangedAttackPosition(
                        ControlledAlly,
                        ControlledAlly.TilemapPosition,
                        ControlledAlly.CurrentFacing,
                        10,
                        Dungeon.StopArrow);

                Character rangedAttackTarget = Game.Instance.AllCharacters.FirstOrDefault(x => x.TilemapPosition == rangedAttackTargetPosition);

                ControlledAlly.SetAction(
                    new RangedAttackAction(
                        ControlledAlly,
                        rangedAttackTarget,
                        ControlledAlly.FinalStats.Strength,
                        projectilePrefab));
            }
            else
            {
                // Targeting has already offset its preview. Attacks always originate at
                // the actor's cell, so holding aim must not double their reach.
                ControlledAlly.SetAction(new AttackAction(ControlledAlly, originalPosition, originalPosition + offset));
            }
            return;
        }
        
        if (PlayerInputHandler.Instance.swapAllyPressed)
        {
            TakeControlNextAlly();
        }

        if (PlayerInputHandler.Instance.planPressed)
		{
            StopHeldWalk();
            MenuManager.Instance.OpenAllyMenu(ControlledAlly);
        }

        if (PlayerInputHandler.Instance.optionsPressed)
        {
            StopHeldWalk();
            Common.Instance.GlobalSettings.ShowDialog();
        }
    }

    public List<GameAction> MoveSideEffects(Character character)
    {
        bool hungerTick = character == PartyLeader;

        List<GameAction> turnSideEffects = new();

        turnSideEffects.Add(
            new ModifyStatAction(
            character,
            character,
            (stats, vitals) =>
            {
                if (hungerTick)
                {
                    vitals.HungerAccumulate++;
                }

                if (vitals.HP < stats.HPMax)
                {
                    vitals.HPRegenAcccumlate++;
                }

                if (vitals.SP < stats.SPMax)
                {
                    vitals.SPRegenAcccumlate++;
                }
            },
            false));

        if (character.Vitals.HungerAccumulate > character.FinalStats.HungerAccumulateThreshold)
        {
            turnSideEffects.Add(new ModifyStatAction(
                character,
                character,
                (stats, vitals) =>
                {
                    vitals.HungerAccumulate = 0;
                    vitals.Hunger--;
                },
                false));
        }

        if (character.Vitals.Hunger <= 0 &&
            hungerTick)
        {
            turnSideEffects.Add(new TakeDamageAction(character, character, 1, true));
        }

        if (character.Vitals.HPRegenAcccumlate > character.FinalStats.HPRegenAcccumlateThreshold &&
            character.Vitals.Hunger > 0)
        {
            turnSideEffects.Add(new ModifyStatAction(
                character,
                character,
                (stats, vitals) =>
                {
                    vitals.HPRegenAcccumlate = 0;
                    vitals.HP++;
                },
                false));
        }

        if (character.Vitals.SPRegenAcccumlate > character.FinalStats.SPRegenAcccumlateThreshold &&
            character.Vitals.Hunger > 0)
        {
            turnSideEffects.Add(new ModifyStatAction(
                character,
                character,
                (stats, vitals) =>
                {
                    vitals.SPRegenAcccumlate = 0;
                    vitals.SP++;
                },
                false));
        }
        return turnSideEffects;
    }

    public void StartTurn()
    {
        if (PendingRetreat)
        {
            PendingRetreat = false;
            GameOverScreen.Retreat(this);
            return;
        }

        Common.Instance.MenuInputHandler.SwitchToPlayerInput();

        ControlledAlly.IsWaitingForPlayerInput = true;
        Vitals.ActionsPerTurnLeft = FinalStats.ActionsPerTurnMax;
        Vitals.AttacksPerTurnLeft = FinalStats.AttacksPerTurnMax;

        //SyncDisplayedStats();

        //update minimap
        if (Game.Instance.CurrentDungeon != null)
        {
            Game.Instance.UpdateMiniMap();
        }

        if (ControlledAlly.currentInteractable is Stairs stairs &&
            ControlledAlly.MovedThisTurn)
        {
            ShowStairPrompt(stairs);
        }
    }

    private void ShowStairPrompt(Stairs stairs)
    {
        StopHeldWalk();
        if (!Game.Instance.CurrentDungeon.IsExitFloor)
        {
            MenuManager.Instance.ShowYesNoDialog(
                "Take Stairs?",
                () => ControlledAlly.SetAction(new InteractAction(stairs)),
                () => { });
        }
        else
        {
            MenuManager.Instance.ShowYesNoDialog(
                "Exit Dungeon?",
                () => GameOverScreen.GoBackToTown(true, this),
                () => { });
        }
    }

    public void TakeControl(Ally newAlly)
    {
        if (Game.Instance?.TurnManager?.IsProcessingTurn == true) return;
        StopHeldWalk();
        PartyLeader = newAlly;
        var oldAlly = ControlledAlly;
        if (oldAlly != null)
        {
            oldAlly.IsWaitingForPlayerInput = false;
            oldAlly.SetToCPU();
        }
        if (newAlly != null)
        {
            if (oldAlly != null && oldAlly != newAlly) GameMessages.Post($"Now controlling {GameMessages.Name(newAlly)}.");
            ControlledAlly = newAlly;
            newAlly.IsWaitingForPlayerInput = true;
            CameraController.SetFollowTarget(newAlly.CirlcleRenderer.transform);
            newAlly.SetToPlayer();
        }
    }

    public void TakeControlNextAlly()
    {
        if (Game.Instance.TurnManager.IsProcessingTurn || DungeonPreferences.FullControl) return;
        var allies = Game.Instance.Allies
            .Where(x => x != null && x.Vitals.HP > 0 && !PartyRules.IsSummon(x))
            .ToList();

        if (allies.Count == 0)
            return;

        var index = allies.IndexOf(ControlledAlly);
        int nextIndex;

        if (index == -1)
        {
            nextIndex = 0;
        }
        else
        {
            nextIndex = (index + 1) % allies.Count;
        }

        var nextAlly = allies[nextIndex];
        TakeControl(nextAlly);
    }

    internal void RestoreLeader()
    {
        if (!PartyRules.IsStanding(Game.Instance, PartyLeader)) PartyLeader = PartyRules.StandingMembers(Game.Instance).FirstOrDefault();
        if (PartyLeader != null) FocusCommand(PartyLeader);
    }

    internal bool CanOpenMenu()
    {
        return Game.Instance.IsReady && (!Game.Instance.TurnManager.IsProcessingTurn || Game.Instance.TurnManager.AwaitingCommand) &&
            ControlledAlly != null && ControlledAlly.Vitals.HP > 0 && ControlledAlly.IsWaitingForPlayerInput &&
            !ControlledAlly.StatusEffects.Any(x => !x.IsExpired() && x.PreventsMenu());
    }
}

public enum PlayerControlMode
{
    FollowAlly,
    TargetSelecting,
}
