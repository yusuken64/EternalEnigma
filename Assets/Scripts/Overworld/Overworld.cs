using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Overworld : MonoBehaviour
{
    public WalkableMap WalkableMap;

    public GameObject EntrancePrefab;

    public Vector3Int EntrancePosition;

    // Start is called before the first frame update
    void Start()
    {
        EntrancePosition = WalkableMap.RandomEntrancePosition().Coord;
        //var position = new Vector3Int(0,0);
        var worldPosition = WalkableMap.CellToWorld(EntrancePosition);
        var entrance = Instantiate(EntrancePrefab, this.transform);
        entrance.transform.position = worldPosition;
    }
}
