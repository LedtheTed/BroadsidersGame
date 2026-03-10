using UnityEngine;
using UnityEngine.InputSystem;

public class GroupMoveController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SelectionManager selection;
    [SerializeField] private Camera cam;

    [Header("Raycast")]
    [SerializeField] private LayerMask waterMask;
    [SerializeField] private float maxRayDistance = 5000f;

    [Header("Multi-Group Movement")]
    [SerializeField] private float splitClusterDistance = 40f;   // if selected ships are farther than this, they become separate path groups
    [SerializeField] private float slotSpacing = 5f;             // spacing between final slots around the destination
    [SerializeField] private float groupRingSpacing = 12f;       // how far apart groups land around the destination

    [Header("Grid")]
    [SerializeField] private NavGrid navGrid;
    [SerializeField] private LayerMask staticObstacleMask; // for smoothing line-of-sight
    [SerializeField] private float agentRadius = 1.5f;
    [SerializeField] private bool smoothPath = true;

    [Header("Independent Pathing")]
    [SerializeField] private float independentPathDistance = 50f;  // distance threshold for individual pathing

    private void Awake(){
        if (selection == null) selection = FindFirstObjectByType<SelectionManager>();
        if (cam == null) cam = Camera.main;
        if (navGrid == null) navGrid = FindFirstObjectByType<NavGrid>();
    }

    private void Update(){
        if (cam == null || selection == null) return;
        if (Mouse.current == null) return;
        if (!Mouse.current.rightButton.wasPressedThisFrame) return;
        if (selection.Selected.Count == 0) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = cam.ScreenPointToRay(mousePos);

        if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, waterMask)) return;

        IssueGroupMove(hit.point);
    }

    private void IssueGroupMove(Vector3 destination){
        var units = selection.Selected;
        if (units.Count == 0) return;
        if (navGrid == null) navGrid = FindFirstObjectByType<NavGrid>();

        // Always hard-reset previous move state so multiple move commands work.
        for (int i = 0; i < units.Count; i++){
            var m = units[i]?.Motor;
            if (m == null) continue;
            m.ClearDestination();
        }

        // Split selection into spatial clusters so far-apart packs can path independently.
        var groups = BuildClusters(units, splitClusterDistance);
        if (groups.Count == 0) return;

        // One A* per cluster. Each cluster lands on a different ring around the destination.
        for (int gi = 0; gi < groups.Count; gi++){
            var group = groups[gi];
            if (group.Count == 0) continue;

            Vector3 groupLandingCenter = ComputeGroupLandingCenter(destination, gi);

            // Build final slot positions (formation) for this group.
            var slots = BuildSlots(group.Count, groupLandingCenter, slotSpacing);

            // Choose a per-group start anchor to run A* from (closest ship to group centroid)
            var startUnit = PickGroupStartUnit(group);
            if (startUnit == null) continue;

            var basePath = GridPathfinder.FindPath(
                navGrid,
                startUnit.transform.position,
                groupLandingCenter,
                smoothPath,
                staticObstacleMask,
                agentRadius
            );

            // Assign each ship its OWN path: base path + ship-specific final slot.
            for (int i = 0; i < group.Count; i++){
                var u = group[i];
                if (u == null) continue;
                var m = u.Motor;
                if (m == null) continue;

                // Check if ship should use independent path
                bool shouldUseIndependentPath = false;
                
                // Check distance to start unit
                float distToStart = Vector3.Distance(u.transform.position, startUnit.transform.position);
                if (distToStart > independentPathDistance){
                    shouldUseIndependentPath = true;
                }
                
                // Check if ship can reach the group path's starting line segment
                if (!shouldUseIndependentPath && basePath.Count > 1){
                    Vector3 shipPos = u.transform.position;
                    Vector3 lineStart = basePath[0];
                    Vector3 lineEnd = basePath[1];
                    
                    // Try to find path to the line segment
                    Vector3 closestPointOnLine = GetClosestPointOnLineSegment(shipPos, lineStart, lineEnd);
                    var pathToLine = GridPathfinder.FindPath(navGrid, shipPos, closestPointOnLine, smoothPath, staticObstacleMask, agentRadius);
                    
                    // If path is blocked or too long, use independent path
                    if (pathToLine == null || pathToLine.Count <= 1 || Vector3.Distance(shipPos, pathToLine[pathToLine.Count - 1]) > 1f){
                        shouldUseIndependentPath = true;
                    }
                }
                
                System.Collections.Generic.List<Vector3> shipPath;
                if (shouldUseIndependentPath){
                    // Generate individual path from ship to its slot
                    shipPath = GridPathfinder.FindPath(navGrid, u.transform.position, slots[i], smoothPath, staticObstacleMask, agentRadius);
                } else {
                    // Use group path with ship-specific final slot
                    shipPath = CopyPathWithFinalSlot(basePath, slots[i]);
                }
                
                m.SetPath(shipPath);
            }
        }
    }

    private System.Collections.Generic.List<System.Collections.Generic.List<SelectableUnit>> BuildClusters(
        System.Collections.Generic.IReadOnlyList<SelectableUnit> units,
        float maxLinkDistance
    ){
        var groups = new System.Collections.Generic.List<System.Collections.Generic.List<SelectableUnit>>();
        if (units == null || units.Count == 0) return groups;

        float maxLinkSqr = maxLinkDistance * maxLinkDistance;
        var visited = new System.Collections.Generic.HashSet<SelectableUnit>();

        for (int i = 0; i < units.Count; i++){
            var seed = units[i];
            if (seed == null || visited.Contains(seed)) continue;

            var group = new System.Collections.Generic.List<SelectableUnit>();
            var queue = new System.Collections.Generic.Queue<SelectableUnit>();
            queue.Enqueue(seed);
            visited.Add(seed);

            while (queue.Count > 0){
                var cur = queue.Dequeue();
                if (cur == null) continue;
                group.Add(cur);

                Vector3 cp = cur.transform.position;
                for (int j = 0; j < units.Count; j++){
                    var other = units[j];
                    if (other == null || visited.Contains(other)) continue;
                    if ((other.transform.position - cp).sqrMagnitude <= maxLinkSqr){
                        visited.Add(other);
                        queue.Enqueue(other);
                    }
                }
            }

            groups.Add(group);
        }

        return groups;
    }

    private SelectableUnit PickGroupStartUnit(System.Collections.Generic.List<SelectableUnit> group){
        if (group == null || group.Count == 0) return null;

        Vector3 c = Vector3.zero;
        int n = 0;
        for (int i = 0; i < group.Count; i++){
            if (group[i] == null) continue;
            c += group[i].transform.position;
            n++;
        }
        if (n <= 0) return group[0];
        c /= n;

        SelectableUnit bestU = group[0];
        float best = float.MaxValue;
        for (int i = 0; i < group.Count; i++){
            var u = group[i];
            if (u == null) continue;
            float d = (u.transform.position - c).sqrMagnitude;
            if (d < best){ best = d; bestU = u; }
        }
        return bestU;
    }

    private Vector3 ComputeGroupLandingCenter(Vector3 destination, int groupIndex){
        if (groupIndex <= 0) return destination;

        int ring = 1 + (groupIndex - 1) / 6;
        int posInRing = (groupIndex - 1) % 6;
        float angle = (posInRing / 6f) * Mathf.PI * 2f;
        float radius = ring * groupRingSpacing;

        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
        return destination + offset;
    }

    private System.Collections.Generic.List<Vector3> BuildSlots(int count, Vector3 center, float spacing){
        var slots = new System.Collections.Generic.List<Vector3>(count);
        if (count <= 0) return slots;

        int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
        int rows = Mathf.CeilToInt(count / (float)cols);
        float halfW = (cols - 1) * 0.5f;
        float halfH = (rows - 1) * 0.5f;

        for (int i = 0; i < count; i++){
            int x = i % cols;
            int z = i / cols;
            float ox = (x - halfW) * spacing;
            float oz = (z - halfH) * spacing;
            slots.Add(center + new Vector3(ox, 0f, oz));
        }

        return slots;
    }

    private System.Collections.Generic.List<Vector3> CopyPathWithFinalSlot(System.Collections.Generic.List<Vector3> basePath, Vector3 slot){
        var p = new System.Collections.Generic.List<Vector3>();

        if (basePath != null && basePath.Count > 0){
            p.AddRange(basePath);
            p[p.Count - 1] = slot; // ship-specific final landing point
        } else {
            p.Add(slot);
        }

        return p;
    }

    private Vector3 GetClosestPointOnLineSegment(Vector3 point, Vector3 lineStart, Vector3 lineEnd){
        Vector3 lineDir = lineEnd - lineStart;
        float lineLengthSq = lineDir.sqrMagnitude;
        if (lineLengthSq < 0.0001f) return lineStart; // degenerate line
        
        float t = Vector3.Dot(point - lineStart, lineDir) / lineLengthSq;
        t = Mathf.Clamp01(t); // clamp to segment bounds
        
        return lineStart + lineDir * t;
    }
}