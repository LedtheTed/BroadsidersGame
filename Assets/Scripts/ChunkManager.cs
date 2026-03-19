using UnityEngine;
using System.Collections.Generic;

public class ChunkManager : MonoBehaviour
{
    [SerializeField] private GameObject oceanPrefab;
    [SerializeField] private float chunkSize = 50f;


    private Dictionary<Vector2Int, Chunk> chunks = new();

    void Start()
    {
        float detected = GetPrefabSize(oceanPrefab);

        if (Mathf.Abs(detected - chunkSize) > 0.1f)
        {
            Debug.LogWarning($"Chunk size ({chunkSize}) != prefab size ({detected})");
        }

        GenerateTestChunks();
    }

    float GetPrefabSize(GameObject prefab)
    {
        Renderer r = prefab.GetComponentInChildren<Renderer>();
        if (r == null)
        {
            Debug.LogError("Prefab has no Renderer!");
            return 1f;
        }

        return r.bounds.size.x; // assuming square (x == z)
    }

    void GenerateTestChunks()
    {
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                CreateChunk(new Vector2Int(x, y));
            }
        }
    }

    public Vector2Int GetChunkCoord(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt(worldPos.x / chunkSize);
        int y = Mathf.FloorToInt(worldPos.z / chunkSize);
        return new Vector2Int(x, y);
    }

    void CreateChunk(Vector2Int coord)
    {
        if (chunks.ContainsKey(coord))
            return;

        Chunk chunk = new Chunk(coord);

        // Create GameObject
        GameObject chunkGO = new GameObject($"Chunk_{coord.x}_{coord.y}");
        chunkGO.transform.parent = this.transform;

        // Position it in world
        Vector3 worldPos = new Vector3(
            coord.x * chunkSize,
            0,
            coord.y * chunkSize
        );
        chunkGO.transform.position = worldPos;

        GameObject ocean = Instantiate(oceanPrefab, chunkGO.transform);

        ocean.transform.localPosition = Vector3.zero;
        ocean.transform.localRotation = Quaternion.identity;

        chunk.terrainObject = ocean;

        chunks.Add(coord, chunk);
    }

    public void GenerateChunksInRadius(Vector3 worldPos, float radius)
    {
        Vector2Int center = GetChunkCoord(worldPos);

        int chunkRadius = Mathf.CeilToInt(radius / chunkSize);

        for (int x = -chunkRadius; x <= chunkRadius; x++)
        {
            for (int y = -chunkRadius; y <= chunkRadius; y++)
            {
                Vector2Int coord = new Vector2Int(center.x + x, center.y + y);

                // Optional: circular check instead of square
                float dist = new Vector2(x, y).magnitude;
                if (dist > chunkRadius) continue;

                CreateChunk(coord);
            }
        }
    }

    void OnDrawGizmos()
    {
        if (chunks == null) return;

        Gizmos.color = Color.red;

        foreach (var chunk in chunks.Values)
        {
            if (chunk == null || chunk.rootObject == null) continue;

            Vector3 pos = chunk.rootObject.transform.position;

            Vector3 size = new Vector3(chunkSize, 0.1f, chunkSize);

            Gizmos.DrawWireCube(
                pos + new Vector3(chunkSize / 2f, 0, chunkSize / 2f),
                size
            );
        }
    }

}