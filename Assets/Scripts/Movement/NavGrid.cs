using System.Collections.Generic;
using UnityEngine;

public class NavGrid : MonoBehaviour
{
    [Header("Grid Bounds (XZ)")]
    [SerializeField] private Vector3 center = Vector3.zero;
    [SerializeField] private Vector2 size = new Vector2(300f, 300f); // x,z extents
    [SerializeField] private float cellSize = 4f;

    [Header("Walkability")]
    [SerializeField] private LayerMask obstacleMask;    // layer we say is an obstacle
    [SerializeField] private float agentRadius = 1.5f;  // ship radius for clearance
    [SerializeField] private float ySample = 0f;        // height to sample obstacles at (water plane y)

    [Header("Debug")]
    [SerializeField] private bool drawFilledGrid = true;

    private int width, height;
    private bool[] walkable; // width*height

    public float CellSize => cellSize;

    private void Awake(){
        Bake();
    }

    [ContextMenu("Bake Grid")]
    public void Bake(){
        width = Mathf.Max(1, Mathf.RoundToInt(size.x / cellSize));
        height = Mathf.Max(1, Mathf.RoundToInt(size.y / cellSize));
        walkable = new bool[width * height];

        for (int z = 0; z < height; z++){
            for (int x = 0; x < width; x++){
                Vector3 wp = CellToWorld(x, z);
                // check clearance using a sphere at sample height
                Vector3 p = new Vector3(wp.x, ySample, wp.z);
                bool blocked = Physics.CheckSphere(p, agentRadius, obstacleMask);
                walkable[z * width + x] = !blocked;
            }
        }
    }

    public bool InBounds(int x, int z) => x >= 0 && z >= 0 && x < width && z < height;

    public bool IsWalkable(int x, int z){
        if (!InBounds(x, z)) return false;
        return walkable[z * width + x];
    }

    public Vector2Int WorldToCell(Vector3 world){
        float minX = center.x - size.x * 0.5f;
        float minZ = center.z - size.y * 0.5f;

        int x = Mathf.FloorToInt((world.x - minX) / cellSize);
        int z = Mathf.FloorToInt((world.z - minZ) / cellSize);
        return new Vector2Int(x, z);
    }

    public Vector3 CellToWorld(int x, int z){
        float minX = center.x - size.x * 0.5f;
        float minZ = center.z - size.y * 0.5f;

        float wx = minX + (x + 0.5f) * cellSize;
        float wz = minZ + (z + 0.5f) * cellSize;
        return new Vector3(wx, center.y, wz);
    }

    private void OnDrawGizmosSelected(){
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(center, new Vector3(size.x, 1f, size.y));
    }

    private void OnDrawGizmos(){
        if (!Application.isPlaying) return;      // only draw during play mode
        if (!drawFilledGrid) return;
        if (walkable == null || walkable.Length == 0) return;

        for (int z = 0; z < height; z++){
            for (int x = 0; x < width; x++){
                int index = z * width + x;
                bool isWalkable = walkable[index];

                Vector3 worldPos = CellToWorld(x, z);

                Gizmos.color = isWalkable
                    ? new Color(0f, 1f, 0f, 0.15f)   // green = walkable
                    : new Color(1f, 0f, 0f, 0.30f);  // red = blocked

                Gizmos.DrawCube(
                    new Vector3(worldPos.x, worldPos.y + 0.05f, worldPos.z),
                    new Vector3(cellSize * 0.95f, 0.05f, cellSize * 0.95f)
                );
            }
        }
    }
}