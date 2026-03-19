using UnityEngine;

public class Chunk
{
    public Vector2Int coord;
    public GameObject rootObject;

    public GameObject terrainObject;

    public Chunk(Vector2Int coord)
    {
        this.coord = coord;
    }
}