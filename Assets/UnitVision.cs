using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitVision : MonoBehaviour
{

    [SerializeField] private float visionRange = 60f;

    private ChunkManager chunkManager;
    private Vector2Int lastChunk;

    void Start()
    {
        chunkManager = GameMaster.Instance.chunkManager;
        lastChunk = chunkManager.GetChunkCoord(transform.position);

        Reveal();
    }

    void Update()
    {
        Vector2Int currentChunk = chunkManager.GetChunkCoord(transform.position);

        // Only update when crossing chunk boundaries
        if (currentChunk != lastChunk)
        {
            lastChunk = currentChunk;
            Reveal();
        }
    }

    void Reveal()
    {
        chunkManager.GenerateChunksInRadius(transform.position, visionRange);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;

        Gizmos.DrawWireSphere(transform.position, visionRange);
    }

}