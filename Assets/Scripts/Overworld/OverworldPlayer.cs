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

	// Start is called before the first frame update
	public void Initialize()
	{
		var startPosition = WalkableMap.RandomStartPlayerPosition().Coord;
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
		if (Input.GetKey(KeyCode.W) ||
			Input.GetKey(KeyCode.A) ||
			Input.GetKey(KeyCode.S) ||
			Input.GetKey(KeyCode.D))
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
		var originalPosition = new Vector3Int(TilemapPosition.x, TilemapPosition.y);
		var newMapPosition = new Vector3Int(TilemapPosition.x, TilemapPosition.y);

		if (Input.GetKey(KeyCode.W) && Input.GetKey(KeyCode.A))
		{
			SetFacing(Facing.UpLeft);
		}
		else if (Input.GetKey(KeyCode.W) && Input.GetKey(KeyCode.D))
		{
			SetFacing(Facing.UpRight);
		}
		else if (Input.GetKey(KeyCode.S) && Input.GetKey(KeyCode.A))
		{
			SetFacing(Facing.DownLeft);
		}
		else if (Input.GetKey(KeyCode.S) && Input.GetKey(KeyCode.D))
		{
			SetFacing(Facing.DownRight);
		}
		else if (Input.GetKey(KeyCode.W))
		{
			SetFacing(Facing.Up);
		}
		else if (Input.GetKey(KeyCode.A))
		{
			SetFacing(Facing.Left);
		}
		else if (Input.GetKey(KeyCode.S))
		{
			SetFacing(Facing.Down);
		}
		else if (Input.GetKey(KeyCode.D))
		{
			SetFacing(Facing.Right);
		}

		if (!Input.GetKey(KeyCode.LeftShift))
		{
			if (holdTime > repeatTime)
			{
				holdTime = 0f;
				var offset = TileWorldDungeon.GetFacingOffset(CurrentFacing);
				if (WalkableMap.CanWalkTo(newMapPosition, newMapPosition + offset))
				{
					newMapPosition += offset;
					SetAction(new OverworldMovement(this, originalPosition, newMapPosition));
					return;
				}
			}
		}

		if ((Input.GetKeyDown(KeyCode.W) ||
			Input.GetKeyDown(KeyCode.A) ||
			Input.GetKeyDown(KeyCode.S) ||
			Input.GetKeyDown(KeyCode.D)) &&
			!Input.GetKey(KeyCode.LeftShift))

		{
			holdTime = 0f;
			var offset = Dungeon.GetFacingOffset(CurrentFacing);
			if (WalkableMap.CanWalkTo(newMapPosition, newMapPosition + offset))
			{
				newMapPosition += offset;
				SetAction(new OverworldMovement(this, originalPosition, newMapPosition));
				return;
			}
		}

		if (Input.GetKeyDown(KeyCode.Space))
		{
			//var offset = Dungeon.GetFacingOffset(CurrentFacing);
			//newMapPosition += offset;
			//SetAction(new AttackAction(this, originalPosition, newMapPosition));
			//return;
		}
		else if (Input.GetKeyDown(KeyCode.E))
		{
			//if (currentInteractable != null)
			//{
			//	SetAction(new InteractAction(currentInteractable));
			//	return;
			//}
		}
		if (Input.GetKeyDown(KeyCode.Z))
		{
			////pass turn
			//SetAction(new WaitAction());
			//return;
		}
		if (Input.GetKeyDown(KeyCode.Tab))
		{
			_busy = true;
			var overworldMenu = FindObjectOfType<OverworldMenu>();
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
		//There is no overworld turns?
		//just immediately execute
		overworldAction.ExecuteImmediate();
		StartCoroutine(DoOverworldActionRoutine(overworldAction));
	}

	private IEnumerator DoOverworldActionRoutine(OverworldAction overworldAction)
	{
		_busy = true;
		yield return StartCoroutine(overworldAction.ExecuteRoutine());

		if (this.TilemapPosition == Overworld.EntrancePosition)
		{
			Overworld.WriteSaveData();
			SceneManager.LoadScene("DungeonScene");
			yield break;
		}
		else if(this.TilemapPosition == Overworld.StatuePosition)
		{
			var overworldMenu = FindObjectOfType<OverworldMenu>();
			OverworldMenuManager.Open(overworldMenu.StatueDialog);
			overworldMenu.StatueDialog.Show(); //this should be done to all dialogs in Open()
			overworldMenu.StatueDialog.CloseAction = () =>
			{
				_busy = false;
			};
			yield break;
		}
		else if(this.TilemapPosition == Overworld.ShopPosition)
		{
			var overworldMenu = FindObjectOfType<OverworldMenu>();
			OverworldMenuManager.Open(overworldMenu.ShopDialog);
			overworldMenu.ShopDialog.Show(); //this should be done to all dialogs in Open()
			overworldMenu.ShopDialog.CloseAction = () =>
			{
				_busy = false;
			};
			yield break;
		}
		else if (this.TilemapPosition == Overworld.BallistaPosition)
		{
			var overworldMenu = FindObjectOfType<OverworldMenu>();
			OverworldMenuManager.Open(overworldMenu.BallistaDialog);
			overworldMenu.BallistaDialog.Show(); //this should be done to all dialogs in Open()
			overworldMenu.BallistaDialog.CloseAction = () =>
			{
				_busy = false;
			};
			yield break;
		}
		else if (Overworld.OverworldAllies.Any(x => x.TilemapPosition == this.TilemapPosition))
		{
			var ally = Overworld.OverworldAllies.First(x => x.TilemapPosition == this.TilemapPosition);
			var overworldMenu = FindObjectOfType<OverworldMenu>();
			OverworldMenuManager.Open(overworldMenu.AllyRecruitDialog);
			overworldMenu.AllyRecruitDialog.Show(ally); //this should be done to all dialogs in Open()
			overworldMenu.AllyRecruitDialog.CloseAction = () =>
			{
				_busy = false;
			};
			yield break;
		}
		_busy = false;
		holdTime = 0f;
		yield return null;
	}
}
