using System.Collections.Generic;

// Shared legacy tile-grid data remains available after removing the unused generator.
public class BSPNode
{
	public string label;
	public BSPRect space;
	public BSPRect room;
	public BSPNode leftChild;
	public BSPNode rightChild;
	internal bool splitHorizontal;

	public List<BSPPosition> RoadPositions = new();

	public List<BSPNode> Connections = new();

	internal BSPPosition RoomCenterPos()
	{
		return new BSPPosition(room.X + (room.Width / 2),
			room.Y + (room.Height / 2));
	}

	public static List<BSPNode> Flatten(BSPNode node)
	{
		if (node == null)
		{
			return new();
		}

		List<BSPNode> result = new();
		result.AddRange(Flatten(node.leftChild));
		result.Add(node);
		result.AddRange(Flatten(node.rightChild));

		return result;
	}
}

public class BSPPosition
{
	public int X;
	public int Y;

	public BSPPosition(int x, int y)
	{
		X = x;
		Y = y;
	}
}

public class BSPRect
{
    public int X;
    public int Y;
    public int Width;
    public int Height;

	public BSPRect(int x, int y, int width, int height)
	{
		this.X = x;
		this.Y = y;
		this.Width = width;
		this.Height = height;
	}
}
