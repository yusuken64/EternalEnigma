using JuicyChickenGames.Menu;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class OverworldPlayer : MonoBehaviour
{
	public OverworldAlly ControllingOverworldAlly;
	private float holdTime = 0f;
	private float repeatTime = 0.1f;

	public CameraController CameraController;

	private bool _busy;
	private bool _menuBusy;
	public WalkableMap WalkableMap;

	public int Gold;
	public List<InventoryItem> Inventory = new();

	public TextMeshProUGUI UIText;

	public List<OverworldAlly> RecruitedAllies;
	public List<Vector3Int> WalkPositionHistory;
	private bool initialied = false;
	private int allyIndex;

	public bool ControllerHeld { get; internal set; }

	public void Initialize()
	{
		initialied = true;
		CycleAlly();
	}

	internal void RecordWalkPosition()
	{
		WalkPositionHistory.Add(ControllingOverworldAlly.TilemapPosition);
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
		UpdateUI();
		if (!initialied) { return; }
		if (ControllerHeld)
		{
			holdTime += Time.deltaTime;
		}

		if (OverworldMenuManager.Instance.DialogStack.Count > 0)
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
			ControllingOverworldAlly.SetFacing(newFacing.Value);
		}

		if (moving)
		{
			var offset = Dungeon.GetFacingOffset(ControllingOverworldAlly.CurrentFacing);
			var originalPosition = ControllingOverworldAlly.TilemapPosition;
			var newMapPosition = ControllingOverworldAlly.TilemapPosition + offset;

			if (!PlayerInputHandler.Instance.holdPosition)
			{
				if (WalkableMap.CanWalkTo(originalPosition, newMapPosition))
				{
					SetAction(new OverworldMovement(this, originalPosition, newMapPosition));
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
			var offset = Dungeon.GetFacingOffset(ControllingOverworldAlly.CurrentFacing);
			var originalPosition = ControllingOverworldAlly.TilemapPosition;
			var targetMapPosition = originalPosition + offset;

			var targetingAlly = RecruitedAllies.FirstOrDefault(x => x.TilemapPosition == targetMapPosition);
			if (targetingAlly != null)
			{
				_menuBusy = true;
				var overworldMenu = FindFirstObjectByType<OverworldMenu>();
				overworldMenu.AllyRecruitDialog.Show(targetingAlly, AllyRecruitDialogMode.Talk);
				overworldMenu.AllyRecruitDialog.CloseAction = () =>
				{
					//Do nothing
					StartCoroutine(Wait(() =>
					{
						_menuBusy = false;
					}));
				};
				OverworldMenuManager.Open(overworldMenu.AllyRecruitDialog);
			}
		}

	}

	private IEnumerator Wait(Action? action)
	{
		yield return null;
		action?.Invoke();
	}

	private void CycleAlly()
	{
		if (RecruitedAllies == null || RecruitedAllies.Count == 0)
		{
			return;
		}

		allyIndex = (allyIndex + 1) % RecruitedAllies.Count;
		var oldAlly = ControllingOverworldAlly;
		var newAlly = RecruitedAllies[allyIndex];

		oldAlly?.SetToCPU();
		newAlly.SetToPlayer();

		ControllingOverworldAlly = newAlly;
		CameraController.SetFollowTarget(newAlly.CirlcleRenderer.transform);
	}

	internal void SetAction(OverworldAction overworldAction)
	{
		if (overworldAction == null) { return; }
		//There is no overworld turns?
		//just immediately execute
		overworldAction.ExecuteImmediate();
		StartCoroutine(DoOverworldActionRoutine(overworldAction));
	}

	private IEnumerator DoOverworldActionRoutine(OverworldAction overworldAction)
	{
		_busy = true;
		yield return StartCoroutine(overworldAction.ExecuteRoutine());

		OverworldAction reverse = null;
		if (overworldAction is OverworldMovement overworldMovement)
		{
			reverse = overworldMovement.GetReverse();
		}

		var overworld = FindObjectOfType<Overworld>();
		var overlappingBuilding = overworld.OverworldBuildings.FirstOrDefault(x => x.TilemapPosition == this.ControllingOverworldAlly.TilemapPosition);
		if (overlappingBuilding != null)
		{
			overlappingBuilding.Interact(this, reverse);
		}
		else if (overworld.OverworldAllies.Any(x => x.TilemapPosition == this.ControllingOverworldAlly.TilemapPosition))
		{
			var ally = overworld.OverworldAllies.First(x => x.TilemapPosition == this.ControllingOverworldAlly.TilemapPosition);
			var overworldMenu = FindFirstObjectByType<OverworldMenu>();
			OverworldMenuManager.Open(overworldMenu.AllyRecruitDialog);
			overworldMenu.AllyRecruitDialog.Show(ally, AllyRecruitDialogMode.Recruit);
			overworldMenu.AllyRecruitDialog.CloseAction = () =>
			{
				SetAction(reverse);
			};
		}
		_busy = false;
		holdTime = 0f;
		yield return null;
	}
}
