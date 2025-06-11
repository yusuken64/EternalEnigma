using System;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "DroppedItemTile", menuName = "Game/Tile/DroppedItemTile")]
public class DroppedItemTile : InteractableTile
{
	public Sprite ItemSprite;

	public bool Opened;
	private Vector3Int position;
	private ITilemap tilemap;

	public Action OpenedAction { get; internal set; }
	public ItemDefinition ItemDefinition { get; internal set; }

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
		var sprite = Opened ? null : ItemSprite;
		this.sprite = sprite;
	}

	internal override string GetInteractionText()
	{
		return !Opened ? $"Pick up {ItemDefinition.ItemName}" : "";
	}

	internal override void DoInteraction()
	{
		if (!Opened)
		{
			Game game = Game.Instance;
			var canAdd = game.PlayerController.Inventory.CanAdd();
			if (canAdd)
			{
				this.Opened = true;
				OpenedAction?.Invoke();
				RefreshTile(position, tilemap);
			}
			else
			{
				game.DoFloatingText("Inventory is full", Color.red, game.PlayerController.transform.position);
			}
		}
	}

	internal DroppedItemTile CloneTile()
	{
		var ret = new DroppedItemTile()
		{
			ItemSprite = ItemSprite,
			Opened = Opened
		};

		ret.UpdateSprite();

		return ret;
	}
}