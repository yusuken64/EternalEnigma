using System;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "TreasureTile", menuName = "Game/Tile/TreasureTile")]
public class TreasureTile : InteractableTile
{
	public Sprite ClosedSprite;
	public Sprite OpenSprite;

	public bool Opened;
	private Vector3Int position;
	private ITilemap tilemap;

	public Action OpenedAction { get; internal set; }

	public override bool StartUp(Vector3Int position, ITilemap tilemap, GameObject go)
	{
		Opened = false;
		this.position = position;
		this.tilemap = tilemap;
		return base.StartUp(position, tilemap, go);
	}

	public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
	{
		base.GetTileData(position, tilemap, ref tileData);

		UpdateSprite();
	}

	private void UpdateSprite()
	{
		var sprite = Opened ? OpenSprite : ClosedSprite;
		this.sprite = sprite;
	}

	internal override string GetInteractionText()
	{
		return !Opened ? "Open Chest" : "";
	}

	internal override void DoInteraction()
	{
		if (!Opened)
		{
			this.Opened = true;
			OpenedAction?.Invoke();
			UpdateSprite();
			RefreshTile(position, tilemap);
			this.Opened = true; //the opened is set to false on refresh
		}
	}

	internal TreasureTile CloneTile()
	{
		var ret = new TreasureTile()
		{
			ClosedSprite = ClosedSprite,
			OpenSprite = OpenSprite,
			Opened = Opened
		};

		ret.UpdateSprite();

		return ret;
	}
}
