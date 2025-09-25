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

    // === Dependencies ===
    private CheatConsole _cheatConsole;
    public CameraController CameraController;
    public Inventory Inventory;
    public TargetIndicator TargetIndicator;

    // === Controlled Character ===
    public Ally ControlledAlly { get; private set; }
    public Vector3Int TilemapPosition => ControlledAlly.TilemapPosition;
    public Stats FinalStats => ControlledAlly.FinalStats;
    public Stats DisplayedStats => ControlledAlly.DisplayedStats;
    public Vitals Vitals => ControlledAlly.Vitals;
    public Vitals DisplayedVitals => ControlledAlly.DisplayedVitals;

    // === team stuff ====
    public int Floor;
    public int Gold;

    // === Control Mode ===
    public PlayerControlMode CurrentControlMode { get; set; }

    private void Start()
    {
        TargetIndicator.gameObject.SetActive(false);
        _cheatConsole = FindFirstObjectByType<CheatConsole>();
    }

    private void Update()
    {
        if (ShouldBlockInput()) return;

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
        if (_cheatConsole.ScreenObject.activeSelf)
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

        Vector2 move = PlayerInputHandler.Instance.moveInput;

        if (move.sqrMagnitude >= 0.01f)
        {
            // Normalize the input so diagonal directions are consistent
            move.Normalize();

            if (move.x < -0.5f && move.y > 0.5f)
                ControlledAlly.SetFacing(Facing.UpLeft);
            else if (move.x > 0.5f && move.y > 0.5f)
                ControlledAlly.SetFacing(Facing.UpRight);
            else if (move.x < -0.5f && move.y < -0.5f)
                ControlledAlly.SetFacing(Facing.DownLeft);
            else if (move.x > 0.5f && move.y < -0.5f)
                ControlledAlly.SetFacing(Facing.DownRight);
            else if (move.y > 0.5f)
                ControlledAlly.SetFacing(Facing.Up);
            else if (move.x < -0.5f)
                ControlledAlly.SetFacing(Facing.Left);
            else if (move.y < -0.5f)
                ControlledAlly.SetFacing(Facing.Down);
            else if (move.x > 0.5f)
                ControlledAlly.SetFacing(Facing.Right);
        }

        if (!PlayerInputHandler.Instance.holdPosition)
        {
            TargetIndicator.gameObject.SetActive(false);
            if (holdTime > repeatTime)
            {
                holdTime = 0f;
                var offset = Dungeon.GetFacingOffset(ControlledAlly.CurrentFacing);
                if (Game.Instance.CurrentDungeon.CanWalkTo(newMapPosition, newMapPosition + offset))
                {
                    newMapPosition += offset;

                    var destinationChar = Game.Instance.AllCharacters.FirstOrDefault(x => x.TilemapPosition == newMapPosition);

                    if (destinationChar == null)
                    {
                        ControlledAlly.SetAction(new MovementAction(ControlledAlly, originalPosition, newMapPosition));
                        return;
                    }
                    else if (destinationChar.Team == ControlledAlly.Team &&
                        destinationChar.CanMove())
                    {
                        ControlledAlly.SetAction(new SwapAllyPositionAction(ControlledAlly, destinationChar));
                        return;
                    }

                    //else you can't move
                }
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
            var offset = Dungeon.GetFacingOffset(ControlledAlly.CurrentFacing);
            var targetAlly = Game.Instance.Allies.FirstOrDefault(x => x.TilemapPosition == ControlledAlly.TilemapPosition + offset);
            if (targetAlly != null)
			{
                FindObjectOfType<MenuManager>().OpenAllyMenu(targetAlly);
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
                newMapPosition += offset;
                ControlledAlly.SetAction(new AttackAction(ControlledAlly, originalPosition, newMapPosition));
            }
            return;
        }
        
        if (PlayerInputHandler.Instance.interactPressed)
        {
            if (ControlledAlly.currentInteractable != null)
            {
                ControlledAlly.SetAction(new InteractAction(ControlledAlly.currentInteractable));
                return;
            }
        }
        
        if (PlayerInputHandler.Instance.swapAllyPressed)
        {
            TakeControlNextAlly();
        }
    }

    public List<GameAction> MoveSideEffects(Character character)
    {
        bool hungerTick = character == ControlledAlly;

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
        ControlledAlly.IsWaitingForPlayerInput = true;
        Vitals.ActionsPerTurnLeft = FinalStats.ActionsPerTurnMax;
        Vitals.AttacksPerTurnLeft = FinalStats.AttacksPerTurnMax;

        //SyncDisplayedStats();

        //update minimap
        if (Game.Instance.CurrentDungeon != null)
        {
            var visibleTiles = Game.Instance.CurrentDungeon.GetVisionBounds(ControlledAlly, this.TilemapPosition);
            Minimap minimap = FindFirstObjectByType<Minimap>();
            minimap.UpdateVision(visibleTiles);
            minimap.UpdateMinimap(visibleTiles);
        }

        if (ControlledAlly.currentInteractable is Stairs stairs &&
            ControlledAlly.MovedThisTurn)
        {
            var target = stairs;
            MenuManager.Instance.ShowYesNoDialog(
                () => ControlledAlly.SetAction(new InteractAction(target)),
                () => { });
        }
    }

    public void TakeControl(Ally newAlly)
    {
        var oldAlly = ControlledAlly;
        if (oldAlly != null)
        {
            oldAlly.IsWaitingForPlayerInput = false;
            oldAlly.SetToCPU();
        }
        if (newAlly != null)
        {
            ControlledAlly = newAlly;
            newAlly.IsWaitingForPlayerInput = true;
            CameraController.SetFollowTarget(newAlly.CirlcleRenderer.transform);
            newAlly.SetToPlayer();
        }
    }

    public void TakeControlNextAlly()
    {
        var allies = Game.Instance.Allies
            .Where(x => x.Vitals.HP > 0)
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

    internal bool CanOpenMenu()
    {
        return !ControlledAlly.StatusEffects.Any(x => x.PreventsMenu());
    }
}

public enum PlayerControlMode
{
    FollowAlly,
    TargetSelecting,
}