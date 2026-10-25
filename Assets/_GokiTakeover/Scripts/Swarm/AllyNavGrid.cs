using System.Collections.Generic;
using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// Floor grid for allies, baked once in the Editor from the kitchen colliders. Allies do not use the
    /// resident's NavMesh (that one deliberately excludes the space under furniture) and need no physics:
    /// A* on this small grid replaces both. Sticky traps are walkable but expensive, so paths avoid them.
    /// </summary>
    public sealed class AllyNavGrid : ScriptableObject
    {
        public const byte Blocked = 0, Open = 1, Sticky = 2;
        [SerializeField] private Vector3 origin;
        [SerializeField] private float cellSize = 0.25f;
        [SerializeField] private int width, depth;
        [SerializeField] private byte[] cells;
        private float[] cost;
        private byte[] baked;   // runtime overlays (placed traps) are undone from this copy
        private int[] parent, heap, stamp, heapIndex;
        private int search;
        private static readonly int[] StepX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] StepZ = { 0, 0, 1, -1, 1, -1, 1, -1 };

        public int Width => width;
        public int Depth => depth;
        public float CellSize => cellSize;
        public int CellCount => cells == null ? 0 : cells.Length;

        public void Bake(Vector3 gridOrigin, float size, int x, int z, byte[] data)
        { origin = gridOrigin; cellSize = size; width = x; depth = z; cells = data; cost = null; }

        /// <summary>Marks open cells inside area as sticky (traps placed during play). Use on a runtime copy.</summary>
        public void AddStickyArea(Bounds area)
        {
            if (baked == null) baked = (byte[])cells.Clone();
            var min = ToCell(area.min); var max = ToCell(area.max);
            for (int z = Mathf.Max(0, min.y); z <= Mathf.Min(depth - 1, max.y); z++)
            for (int x = Mathf.Max(0, min.x); x <= Mathf.Min(width - 1, max.x); x++)
                if (cells[z * width + x] == Open) cells[z * width + x] = Sticky;
        }
        public void ClearOverlays() { if (baked != null) System.Array.Copy(baked, cells, cells.Length); }

        public bool InGrid(int x, int z) => x >= 0 && z >= 0 && x < width && z < depth;
        public Vector2Int ToCell(Vector3 world) =>
            new Vector2Int(Mathf.FloorToInt((world.x - origin.x) / cellSize), Mathf.FloorToInt((world.z - origin.z) / cellSize));
        public Vector3 ToWorld(int x, int z) => new Vector3(origin.x + (x + 0.5f) * cellSize, 0, origin.z + (z + 0.5f) * cellSize);
        public byte Get(int x, int z) => InGrid(x, z) ? cells[z * width + x] : Blocked;
        public bool Walkable(Vector3 world) { var c = ToCell(world); return Get(c.x, c.y) != Blocked; }
        public bool IsSticky(Vector3 world) { var c = ToCell(world); return Get(c.x, c.y) == Sticky; }

        public bool NearestWalkable(Vector3 world, float maxDistance, out Vector3 result)
        {
            var c = ToCell(world);
            int reach = Mathf.CeilToInt(maxDistance / cellSize);
            float best = float.MaxValue; result = world;
            for (int r = 0; r <= reach; r++)
            {
                for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r || Get(c.x + dx, c.y + dz) != Open) continue;
                    Vector3 p = ToWorld(c.x + dx, c.y + dz);
                    float d = (p - new Vector3(world.x, 0, world.z)).sqrMagnitude;
                    if (d < best) { best = d; result = p; }
                }
                if (best < float.MaxValue) return true;
            }
            return false;
        }

        /// <summary>A* from start to goal (8-neighbour, no corner cutting), then shortened by line of sight.</summary>
        public bool FindPath(Vector3 start, Vector3 goal, List<Vector3> path)
        {
            path.Clear();
            if (cells == null || cells.Length == 0) return false;
            if (!Walkable(start) && !NearestWalkable(start, 1f, out start)) return false;
            if (Get(ToCell(goal).x, ToCell(goal).y) != Open && !NearestWalkable(goal, 1.5f, out goal)) return false;
            Prepare();
            var s = ToCell(start); var g = ToCell(goal);
            int startIndex = s.y * width + s.x, goalIndex = g.y * width + g.x;
            search++;
            int count = 0;
            cost[startIndex] = 0; parent[startIndex] = -1; stamp[startIndex] = search;
            Push(startIndex, Heuristic(startIndex, goalIndex), ref count);
            bool found = false;
            while (count > 0)
            {
                int current = Pop(ref count);
                if (current == goalIndex) { found = true; break; }
                int cx = current % width, cz = current / width;
                for (int i = 0; i < 8; i++)
                {
                    int nx = cx + StepX[i], nz = cz + StepZ[i];
                    byte kind = Get(nx, nz);
                    if (kind == Blocked) continue;
                    if (i >= 4 && (Get(cx + StepX[i], cz) == Blocked || Get(cx, cz + StepZ[i]) == Blocked)) continue;
                    int next = nz * width + nx;
                    float step = (i >= 4 ? 1.4142f : 1f) * (kind == Sticky ? 8f : 1f);
                    float g2 = cost[current] + step;
                    if (stamp[next] == search && g2 >= cost[next]) continue;
                    bool queued = stamp[next] == search && heapIndex[next] >= 0;
                    stamp[next] = search; cost[next] = g2; parent[next] = current;
                    float f = g2 + Heuristic(next, goalIndex);
                    if (queued) { score[heapIndex[next]] = f; SiftUp(heapIndex[next]); }
                    else Push(next, f, ref count);
                }
                heapIndex[current] = -2;
            }
            if (!found) return false;
            for (int i = goalIndex; i >= 0; i = parent[i]) path.Add(ToWorld(i % width, i / width));
            path.Reverse();
            path[path.Count - 1] = new Vector3(goal.x, 0, goal.z);
            Shorten(path);
            return true;
        }

        public bool ClearLine(Vector3 a, Vector3 b)
        {
            Vector3 delta = b - a; delta.y = 0;
            int steps = Mathf.CeilToInt(delta.magnitude / (cellSize * 0.4f));
            for (int i = 1; i <= steps; i++)
            {
                var c = ToCell(a + delta * (i / (float)steps));
                if (Get(c.x, c.y) != Open) return false;
            }
            return true;
        }

        private void Shorten(List<Vector3> path)
        {
            int write = 1;
            for (int read = 1; read < path.Count; read++)
            {
                bool last = read == path.Count - 1;
                if (!last && ClearLine(path[write - 1], path[read + 1])) continue;
                path[write++] = path[read];
            }
            path.RemoveRange(write, path.Count - write);
            if (path.Count > 1) path.RemoveAt(0);
        }

        private float[] score;
        private void Prepare()
        {
            int n = cells.Length;
            if (cost != null && cost.Length == n) return;
            cost = new float[n]; parent = new int[n]; heap = new int[n]; stamp = new int[n]; heapIndex = new int[n]; score = new float[n];
            search = 0;
        }
        private float Heuristic(int a, int b)
        {
            int dx = Mathf.Abs(a % width - b % width), dz = Mathf.Abs(a / width - b / width);
            return Mathf.Max(dx, dz) + 0.4142f * Mathf.Min(dx, dz);
        }
        // Binary heap keyed by score[slot]; heapIndex maps a cell to its slot.
        private void Push(int cell, float f, ref int count)
        {
            heap[count] = cell; score[count] = f; heapIndex[cell] = count; count++;
            SiftUp(count - 1);
        }
        private int Pop(ref int count)
        {
            int top = heap[0];
            count--;
            if (count > 0) { Move(count, 0); SiftDown(0, count); }
            heapIndex[top] = -1;
            return top;
        }
        private void SiftUp(int i)
        {
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (score[p] <= score[i]) break;
                Swap(i, p); i = p;
            }
        }
        private void SiftDown(int i, int count)
        {
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, m = i;
                if (l < count && score[l] < score[m]) m = l;
                if (r < count && score[r] < score[m]) m = r;
                if (m == i) return;
                Swap(i, m); i = m;
            }
        }
        private void Move(int from, int to) { heap[to] = heap[from]; score[to] = score[from]; heapIndex[heap[to]] = to; }
        private void Swap(int a, int b)
        {
            int c = heap[a]; heap[a] = heap[b]; heap[b] = c;
            float f = score[a]; score[a] = score[b]; score[b] = f;
            heapIndex[heap[a]] = a; heapIndex[heap[b]] = b;
        }
    }
}
