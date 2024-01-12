using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class OverworldPlayer : MonoBehaviour
{
	public Overworld Overworld;
	private float holdTime = 0f;
	private float repeatTime = 0.1f;
	public Camera Camera;
	public Vector3 CameraOffset = new Vector3(1, -7.23999977f, -11.0200005f);

    public Animator HeroAnimator;

	public Vector3Int TilemapPosition;

	public GameObject VisualParent;
	public Facing CurrentFacing;
	private bool _busy;
	public WalkableMap WalkableMap;

	// Start is called before the first frame update
	void Start()
    {
        HeroAnimator.Play("");

		var startPosition = WalkableMap.RandomStartPlayerPosition().Coord;
		var worldPosition = WalkableMap.CellToWorld(startPosition);
		this.transform.position = worldPosition;
		this.TilemapPosition = startPosition;
	}

    private void LateUpdate()
    {
        Camera.transform.position = this.transform.position + CameraOffset;
    }

    // Update is called once per frame
    void Update()
	{
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
			SceneManager.LoadScene("DungeonScene");
			yield break;
		}
		if (this.TilemapPosition == Overworld.StatuePosition)
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
		_busy = false;
		holdTime = 0f;
		yield return null;
	}

	internal void SetFacing(Facing facing)
	{
		CurrentFacing = facing;
		var multiplier = 0;
		switch (facing)
		{
			case Facing.Up:
				multiplier = 0;
				break;
			case Facing.Down:
				multiplier = 4;
				break;
			case Facing.Left:
				multiplier = 6;
				break;
			case Facing.Right:
				multiplier = 2;
				break;
			case Facing.UpLeft:
				multiplier = 7;
				break;
			case Facing.UpRight:
				multiplier = 1;
				break;
			case Facing.DownLeft:
				multiplier = 5;
				break;
			case Facing.DownRight:
				multiplier = 3;
				break;
		}
		Vector3 desiredRotation = new Vector3(0, 0, -45 * multiplier);
		VisualParent.transform.eulerAngles = desiredRotation;
	}
}
