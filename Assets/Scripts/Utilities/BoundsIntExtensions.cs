using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class BoundsIntExtensions
{
	public static bool Overlaps2D(this BoundsInt bounds, BoundsInt other)
	{
		bool intersectsX = bounds.min.x < other.max.x && bounds.max.x > other.min.x;
		bool intersectsY = bounds.min.y < other.max.y && bounds.max.y > other.min.y;

		return intersectsX && intersectsY;
	}
	public static bool Overlaps2D(this BoundsInt bounds, Vector3Int other)
	{
		bool intersectsX = bounds.min.x < other.x && bounds.max.x > other.x;
		bool intersectsY = bounds.min.y < other.y && bounds.max.y > other.y;

		return intersectsX && intersectsY;
	}
}
