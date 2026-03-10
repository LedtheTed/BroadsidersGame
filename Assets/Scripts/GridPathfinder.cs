using System.Collections.Generic;
using UnityEngine;

public static class GridPathfinder{
    // min-heap priority queue for A*
    private class MinHeap{
        private readonly List<(int node, int fScore)> data = new();
        public int Count => data.Count;

        public void Push(int node, int fScore){
            data.Add((node, fScore));
            int i = data.Count - 1;
            while (i > 0){
                int p = (i - 1) / 2;
                if (data[p].fScore <= data[i].fScore) break;
                (data[p], data[i]) = (data[i], data[p]);
                i = p;
            }
        }

        public int Pop(){
            int res = data[0].node;
            data[0] = data[^1];
            data.RemoveAt(data.Count - 1);

            int i = 0;
            while (true){
                int l = i * 2 + 1;
                int r = i * 2 + 2;
                if (l >= data.Count) break;

                int best = (r < data.Count && data[r].fScore < data[l].fScore) ? r : l;
                if (data[i].fScore <= data[best].fScore) break;

                (data[i], data[best]) = (data[best], data[i]);
                i = best;
            }

            return res;
        }
    }

    // return list of world-space waypoints
    public static List<Vector3> FindPath(NavGrid grid, Vector3 startWorld, Vector3 goalWorld, bool smooth, LayerMask obstacleMask, float agentRadius){
        Vector2Int s = grid.WorldToCell(startWorld);
        Vector2Int g = grid.WorldToCell(goalWorld);

        // if start/goal are in blocked cells nudge to nearest
        if (!grid.IsWalkable(s.x, s.y)) s = FindNearestWalkable(grid, s);
        if (!grid.IsWalkable(g.x, g.y)) g = FindNearestWalkable(grid, g);

        int w = Mathf.RoundToInt( (grid.CellSize > 0f) ? (1f) : 1f );

        int start = Encode(grid, s.x, s.y);
        int goal = Encode(grid, g.x, g.y);

        var open = new MinHeap();
        var cameFrom = new Dictionary<int, int>(2048);
        var gScore = new Dictionary<int, int>(2048);

        gScore[start] = 0;
        open.Push(start, Heuristic(s, g));

        // 8-neighbor movement
        int[] dx = { 1, -1, 0, 0,  1, 1, -1, -1 };
        int[] dz = { 0, 0, 1, -1, 1, -1, 1, -1 };
        int[] cost = { 10, 10, 10, 10, 14, 14, 14, 14 };

        int safety = 0;
        while (open.Count > 0 && safety++ < 200000){
            int current = open.Pop();
            if (current == goal)
                return Reconstruct(grid, cameFrom, current, smooth, obstacleMask, agentRadius);

            Decode(current, out int cx, out int cz);

            int curG = gScore[current];

            for (int i = 0; i < dx.Length; i++){
                int nx = cx + dx[i];
                int nz = cz + dz[i];

                if (!grid.IsWalkable(nx, nz)) continue;

                // prevent cutting corners through diagonals
                if (i >= 4){
                    if (!grid.IsWalkable(cx, nz) || !grid.IsWalkable(nx, cz))
                        continue;
                }

                int neighbor = Encode(grid, nx, nz);
                int tentativeG = curG + cost[i];

                if (!gScore.TryGetValue(neighbor, out int oldG) || tentativeG < oldG){
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;

                    int f = tentativeG + Heuristic(new Vector2Int(nx, nz), g);
                    open.Push(neighbor, f);
                }
            }
        }
        return new List<Vector3> { goalWorld };
    }

    private static int Heuristic(Vector2Int a, Vector2Int b){
        // Manhattan * 10 (grid units)
        return (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y)) * 10;
    }

    private static int Encode(NavGrid grid, int x, int z) => (z << 16) ^ (x & 0xFFFF);
    private static void Decode(int code, out int x, out int z){
        z = code >> 16;
        x = (short)(code & 0xFFFF);
    }

    private static List<Vector3> Reconstruct(NavGrid grid, Dictionary<int, int> cameFrom, int current, bool smooth, LayerMask obstacleMask, float agentRadius){
        var cells = new List<int>();
        cells.Add(current);
        while (cameFrom.TryGetValue(current, out int prev)){
            current = prev;
            cells.Add(current);
        }
        cells.Reverse();

        var pts = new List<Vector3>(cells.Count);
        for (int i = 0; i < cells.Count; i++){
            Decode(cells[i], out int x, out int z);
            pts.Add(grid.CellToWorld(x, z));
        }

        if (!smooth || pts.Count <= 2){
            return pts;
        }

        return SmoothPath(pts, obstacleMask, agentRadius);
    }

    private static List<Vector3> SmoothPath(List<Vector3> pts, LayerMask obstacleMask, float agentRadius){
        // keep farthest visible waypoint using SphereCast for clearance
        var result = new List<Vector3>();
        int i = 0;
        result.Add(pts[0]);

        while (i < pts.Count - 1){
            int best = i + 1;
            for (int j = pts.Count - 1; j > i; j--){
                if (HasLineOfSight(pts[i], pts[j], obstacleMask, agentRadius)){
                    best = j;
                    break;
                }
            }

            result.Add(pts[best]);
            i = best;
        }

        return result;
    }

    private static bool HasLineOfSight(Vector3 a, Vector3 b, LayerMask obstacleMask, float agentRadius){
        Vector3 dir = b - a;
        dir.y = 0f;
        float dist = dir.magnitude;
        if (dist < 0.01f) return true;
        dir /= dist;

        // sphereCast so we respect ship clearance
        Vector3 origin = new Vector3(a.x, a.y, a.z);
        return !Physics.SphereCast(origin, agentRadius, dir, out _, dist, obstacleMask);
    }

    private static Vector2Int FindNearestWalkable(NavGrid grid, Vector2Int start){
        // small BFS ring search
        for (int r = 0; r < 20; r++){
            for (int dz = -r; dz <= r; dz++){
                for (int dx = -r; dx <= r; dx++){
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r) continue;
                    int x = start.x + dx;
                    int z = start.y + dz;
                    if (grid.IsWalkable(x, z))
                        return new Vector2Int(x, z);
                }
            }
        }
        return start;
    }
}