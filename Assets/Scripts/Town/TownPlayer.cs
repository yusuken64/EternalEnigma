using JuicyChickenGames.Menu;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class TownPlayer : MonoBehaviour
{
	public TownAlly ControllingTownAlly;
	private float holdTime = 0f;

	public CameraController CameraController;

	private bool _busy;
	private bool _menuBusy;
	public bool IsBusy => _busy;
	public WalkableMap WalkableMap;

	public int Gold;
	public List<InventoryItem> Inventory = new();

	public TextMeshProUGUI UIText;

	public List<TownAlly> RecruitedAllies;
	public List<Vector3Int> WalkPositionHistory;
	private TownMenuManager townMenuManager;
	private bool initialied = false;
	private int allyIndex;

	public bool ControllerHeld { get; internal set; }

	public void Initialize()
	{
		townMenuManager = FindFirstObjectByType<TownMenuManager>();
		Common.Instance.MenuInputHandler.SwitchToPlayerInput();
		initialied = true;
		CycleAlly();
	}

	internal void RecordWalkPosition()
	{
		WalkPositionHistory.Add(ControllingTownAlly.TilemapPosition);
		if (WalkPositionHistory.Count > 5)
		{
			WalkPositionHistory.RemoveAt(0);
		}
	}

	public Vector3Int GetNthFromLastPosition(int n)
	{
		if (WalkPositionHistory == null || WalkPositionHistory.Count == 0)
			return Vector3Int.zero;

		// Clamp to valid range
		n = Mathf.Clamp(n, 0, WalkPositionHistory.Count - 1);

		int index = WalkPositionHistory.Count - n - 1;

		return WalkPositionHistory[Mathf.Clamp(index, 0, WalkPositionHistory.Count - 1)];
	}

	// Update is called once per frame
	void Update()
	{
		if (AutoplayRunner.Active != null) { UpdateUI(); return; }
		if (Common.Instance.Travel.IsTransitioning || MenuUIInputModule.Active?.InputConsumed == true || Common.Instance.GlobalSettings.IsOpen) return;
		UpdateUI();
		if (!initialied) { return; }
		if (ControllerHeld)
		{
			holdTime += Time.deltaTime;
		}

		if (townMenuManager.DialogStack.Count > 0)
		{
			return;
		}
		if (!_busy && !_menuBusy)
		{
			DeterminePlayerAction();
		}
	}

	private void UpdateUI()
	{
		UIText.text = $@"{Gold}";
	}

	private void DeterminePlayerAction()
	{
		var inputHandler = PlayerInputHandler.Instance;

		if (inputHandler == null || _busy)
			return;

		var moveInput = inputHandler.moveInput;

		Facing? newFacing = null;

        bool moving = moveInput.magnitude > 0.1f;
        if (moving)
		{
			// Normalize to handle diagonal directions nicely
			var normInput = moveInput.normalized;

			// Determine primary direction by thresholds for diagonals
			if (normInput.y > 0.5f)
			{
				if (normInput.x < -0.5f)
					newFacing = Facing.UpLeft;
				else if (normInput.x > 0.5f)
					newFacing = Facing.UpRight;
				else
					newFacing = Facing.Up;
			}
			else if (normInput.y < -0.5f)
			{
				if (normInput.x < -0.5f)
					newFacing = Facing.DownLeft;
				else if (normInput.x > 0.5f)
					newFacing = Facing.DownRight;
				else
					newFacing = Facing.Down;
			}
			else
			{
				// Y near zero, horizontal only
				if (normInput.x < 0)
					newFacing = Facing.Left;
				else
					newFacing = Facing.Right;
			}
		}

		if (newFacing.HasValue)
		{
			ControllingTownAlly.SetFacing(newFacing.Value);
		}

		if (moving)
		{
			var offset = Dungeon.GetFacingOffset(ControllingTownAlly.CurrentFacing);
			var originalPosition = ControllingTownAlly.TilemapPosition;
			var newMapPosition = ControllingTownAlly.TilemapPosition + offset;

			if (!PlayerInputHandler.Instance.holdPosition)
			{
				if (WalkableMap.CanWalkTo(originalPosition, newMapPosition) &&
					!FindFirstObjectByType<Town>().ShopVendors.Any(v => v.TilemapPosition == newMapPosition))
				{
					SetAction(new TownMovement(this, originalPosition, newMapPosition));
					holdTime = 0f;
					return;
				}
			}
		}

		if (inputHandler.swapAllyPressed)
		{
			CycleAlly();
		}

		if (inputHandler.attackPressed)
		{
			//determine ally at attackposition;
			var offset = Dungeon.GetFacingOffset(ControllingTownAlly.CurrentFacing);
			var originalPosition = ControllingTownAlly.TilemapPosition;
			var targetMapPosition = originalPosition + offset;

			var targetingAlly = RecruitedAllies.FirstOrDefault(x => x.TilemapPosition == targetMapPosition);
			if (targetingAlly != null)
			{
				_menuBusy = true;
				var townMenu = FindFirstObjectByType<TownMenu>();
				townMenu.AllyRecruitDialog.Show(targetingAlly, AllyRecruitDialogMode.Talk);
				townMenu.AllyRecruitDialog.CloseAction = () =>
				{
					//Do nothing
					StartCoroutine(Wait(() =>
					{
						_menuBusy = false;
					}));
				};
				townMenuManager.Open(townMenu.AllyRecruitDialog);
			}
			else
			{
				var vendor = FindFirstObjectByType<Town>().ShopVendors.FirstOrDefault(x => x.TilemapPosition == targetMapPosition);
				if (vendor != null)
				{
					_menuBusy = true;
					var townMenu = FindFirstObjectByType<TownMenu>();
					var dialog = townMenu.OpenBuilding(vendor.Building, this, null);
					dialog.CloseAction = () => StartCoroutine(Wait(() => { _menuBusy = false; }));
				}
			}
		}

		if (inputHandler.optionsPressed)
		{
			if (townMenuManager.DialogStack.Count == 0)
			{
				Common.Instance.GlobalSettings.ShowDialog();
			}
		}
	}

	private IEnumerator Wait(Action action)
	{
		yield return null;
		action?.Invoke();
	}

    internal void EnsureControlledAlly()
    {
        if (!RecruitedAllies.Contains(ControllingTownAlly)) CycleAlly();
    }

	private void CycleAlly()
	{
		if (RecruitedAllies == null || RecruitedAllies.Count == 0)
		{
			return;
		}

		allyIndex = (allyIndex + 1) % RecruitedAllies.Count;
		var oldAlly = ControllingTownAlly;
		var newAlly = RecruitedAllies[allyIndex];

		oldAlly?.SetToCPU();
		newAlly.SetToPlayer();

		ControllingTownAlly = newAlly;
		CameraController.SetFollowTarget(newAlly.CirlcleRenderer.transform);
	}

	internal void SetAction(TownAction townAction)
	{
		if (townAction == null) { return; }
		//There is no town turns?
		//just immediately execute
		townAction.ExecuteImmediate();
		StartCoroutine(DoTownActionRoutine(townAction));
	}

	private IEnumerator DoTownActionRoutine(TownAction townAction)
	{
		_busy = true;
		yield return StartCoroutine(townAction.ExecuteRoutine());

		TownAction reverse = null;
		if (townAction is TownMovement townMovement)
		{
			reverse = townMovement.GetReverse();
		}

		var town = FindFirstObjectByType<Town>();
		if (Common.Instance.CampaignContext != null && ControllingTownAlly.TilemapPosition == new Vector3Int(10, 0, 0))
        { Common.Instance.Travel.ExitTown(town); _busy = false; yield break; }
		var overlappingBuilding = town.TownBuildings.FirstOrDefault(x =>
			x.TilemapPosition == this.ControllingTownAlly.TilemapPosition && !x.HasInterior);
		if (overlappingBuilding != null)
		{
			overlappingBuilding.Interact(this, reverse);
		}
		else if (town.TownAllies.Any(x => x.TilemapPosition == this.ControllingTownAlly.TilemapPosition))
		{
			var ally = town.TownAllies.First(x => x.TilemapPosition == this.ControllingTownAlly.TilemapPosition);
			var townMenu = FindFirstObjectByType<TownMenu>();
			townMenuManager.Open(townMenu.AllyRecruitDialog);
			townMenu.AllyRecruitDialog.Show(ally, AllyRecruitDialogMode.Recruit);
			townMenu.AllyRecruitDialog.CloseAction = () =>
			{
				SetAction(reverse);
			};
		}
		_busy = false;
		holdTime = 0f;
		yield return null;
	}
}
