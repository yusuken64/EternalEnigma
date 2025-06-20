using JuicyChickenGames.Menu;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class OverworldPlayer : OverworldCharacter
{
	public Overworld Overworld;
	private float holdTime = 0f;
	private float repeatTime = 0.1f;
	public Camera Camera;
	public Vector3 CameraOffset = new Vector3(1, -7.23999977f, -11.0200005f);

	private bool _busy;
	public WalkableMap WalkableMap;

	public int Gold;
	public List<string> Inventory;

	public TextMeshProUGUI UIText;

	public List<OverworldAlly> RecruitedAllies;
	public List<Vector3Int> WalkPositionHistory;
	private bool initialied = false;

	public bool ControllerHeld { get; internal set; }

	public void Initialize()
	{
		//var startPosition = WalkableMap.RandomStartPlayerPosition().Coord;
		var startPosition = new Vector3Int(10, 4, 0);
		var worldPosition = WalkableMap.CellToWorld(startPosition);
		this.transform.position = worldPosition;
		this.TilemapPosition = startPosition;
		initialied = true;
	}

	internal void RecordWalkPosition()
	{
		WalkPositionHistory.Add(TilemapPosition);
		if (WalkPositionHistory.Count > 5)
		{
			WalkPositionHistory.RemoveAt(0);
		}
	}

	public Vector3Int GetNthFromLastPosition(int n)
	{
		if (WalkPositionHistory == null || n < 0 || n > WalkPositionHistory.Count)
		{
			n = 0;
		}

		// Calculate the index of the nth from the last position
		int index = WalkPositionHistory.Count - n - 2;

		return WalkPositionHistory[index];
	}

	private void LateUpdate()
    {
        Camera.transform.position = this.transform.position + CameraOffset;
    }

    // Update is called once per frame
    void Update()
	{
		if (!initialied) { return; }
		if (ControllerHeld)
		{
			holdTime += Time.deltaTime;
		}

		if (!_busy)
		{
			DeterminePlayerAction();
		}

		UpdateUI();
	}

	private void UpdateUI()
	{
		var inventoryString = string.Join(Environment.NewLine, Inventory);
		UIText.text = $@"{Gold}g
{inventoryString}";
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
			SetFacing(newFacing.Value);
		}

		if (moving)
		{
			var offset = Dungeon.GetFacingOffset(CurrentFacing);
			var originalPosition = TilemapPosition;
			var newMapPosition = TilemapPosition + offset;

			if (WalkableMap.CanWalkTo(originalPosition, newMapPosition))
			{
				SetAction(new OverworldMovement(this, originalPosition, newMapPosition));
				holdTime = 0f;
				return;
			}
		}

		if (Input.GetKeyDown(KeyCode.Tab))
		{
			_busy = true;
			var overworldMenu = FindFirstObjectByType<OverworldMenu>();
			OverworldMenuManager.Open(overworldMenu.OverworldHelpDialog);
			overworldMenu.OverworldHelpDialog.Show(); //this should be done to all dialogs in Open()
			overworldMenu.OverworldHelpDialog.CloseAction = () =>
			{
				_busy = false;
			};
			return;
		}
	}

	private void SetAction(OverworldAction overworldAction)
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

		if (this.TilemapPosition == Overworld.EntrancePosition)
		{
			Overworld.WriteSaveData();
			Common.Instance.ScreenTransition.DoTransition(() =>
			{
				SceneManager.LoadScene("DungeonScene");
			});
			yield break;
		}
		else if(this.TilemapPosition == Overworld.StatuePosition)
		{
			var overworldMenu = FindFirstObjectByType<OverworldMenu>();
			OverworldMenuManager.Open(overworldMenu.StatueDialog);
			overworldMenu.StatueDialog.Show(); //this should be done to all dialogs in Open()
			overworldMenu.StatueDialog.CloseAction = () =>
			{
				SetAction(reverse);
			};
			yield break;
		}
		else if(this.TilemapPosition == Overworld.ShopPosition)
		{
			var overworldMenu = FindFirstObjectByType<OverworldMenu>();
			OverworldMenuManager.Open(overworldMenu.ShopDialog);
			overworldMenu.ShopDialog.Show(); //this should be done to all dialogs in Open()
			overworldMenu.ShopDialog.CloseAction = () =>
			{
				SetAction(reverse);
			};
			yield break;
		}
		else if (this.TilemapPosition == Overworld.BallistaPosition)
		{
			var overworldMenu = FindFirstObjectByType<OverworldMenu>();
			OverworldMenuManager.Open(overworldMenu.BallistaDialog);
			overworldMenu.BallistaDialog.Show(); //this should be done to all dialogs in Open()
			overworldMenu.BallistaDialog.CloseAction = () =>
			{
				SetAction(reverse);
			};
			yield break;
		}
		else if (Overworld.OverworldAllies.Any(x => x.TilemapPosition == this.TilemapPosition))
		{
			var ally = Overworld.OverworldAllies.First(x => x.TilemapPosition == this.TilemapPosition);
			var overworldMenu = FindFirstObjectByType<OverworldMenu>();
			OverworldMenuManager.Open(overworldMenu.AllyRecruitDialog);
			overworldMenu.AllyRecruitDialog.Show(ally); //this should be done to all dialogs in Open()
			overworldMenu.AllyRecruitDialog.CloseAction = () =>
			{
				SetAction(reverse);
			};
			yield break;
		}
		_busy = false;
		holdTime = 0f;
		yield return null;
	}
}
