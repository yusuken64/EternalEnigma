using System;
using UnityEngine;

public class TargetIndicator : MonoBehaviour
{
    public GameObject SquareIndicator;
	internal void SetTargetingPosition(Vector3Int newMapPosition)
    {
        var worldPosition = Game.Instance.CurrentDungeon.CellCenterToWorld(newMapPosition);
        this.transform.position = worldPosition;
    }
}
