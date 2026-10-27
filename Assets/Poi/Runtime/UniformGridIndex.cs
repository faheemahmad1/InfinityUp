using System;

namespace Poi
{
    public interface IPoiIndex
    {
        int CellCountX { get; }
        int CellCountZ { get; }
        float CellSize { get; }
        void Link(int slot);
        void Unlink(int slot);
        void RelinkPosition(int slot);
        void RelinkPriority(int slot, PoiPriority priority);
    }

    /// <summary>
    /// Uniform grid. Each cell has one intrusive doubly linked list per priority.
    /// Add, remove, and move touch only the slot being changed.
    /// </summary>
    public sealed class UniformGridIndex : IPoiIndex
    {
        public int CellCountX { get; }
        public int CellCountZ { get; }
        public float CellSize { get; }
        public float OriginX { get; }
        public float OriginZ { get; }
        public float WorldSize { get; }

        readonly PoiStore store;
        readonly int[] heads;

        public UniformGridIndex(PoiStore store, float worldSize, float cellSize)
        {
            if (store == null)
            {
                throw new ArgumentNullException(nameof(store));
            }

            if (worldSize <= 0f || cellSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize));
            }

            this.store = store;
            WorldSize = worldSize;
            CellSize = cellSize;
            OriginX = -worldSize * 0.5f;
            OriginZ = -worldSize * 0.5f;
            CellCountX = Math.Max(1, (int)Math.Ceiling(worldSize / cellSize));
            CellCountZ = CellCountX;
            heads = new int[CellCountX * CellCountZ * PoiEnums.PriorityCount];
            for (int i = 0; i < heads.Length; i++)
            {
                heads[i] = -1;
            }
        }

        public int CellOf(float x, float z)
        {
            int cx = (int)Math.Floor((x - OriginX) / CellSize);
            int cz = (int)Math.Floor((z - OriginZ) / CellSize);
            if (cx < 0) cx = 0;
            if (cz < 0) cz = 0;
            if (cx >= CellCountX) cx = CellCountX - 1;
            if (cz >= CellCountZ) cz = CellCountZ - 1;
            return cz * CellCountX + cx;
        }

        public void Link(int slot)
        {
            ref PoiStore.Slot s = ref store.Slots[slot];
            s.cell = CellOf(s.x, s.z);
            int headIndex = HeadIndex(s.cell, s.priority);
            int head = heads[headIndex];
            s.prev = -1;
            s.next = head;
            if (head >= 0)
            {
                store.Slots[head].prev = slot;
            }

            heads[headIndex] = slot;
        }

        public void Unlink(int slot)
        {
            ref PoiStore.Slot s = ref store.Slots[slot];
            if (s.cell < 0)
            {
                return;
            }

            int headIndex = HeadIndex(s.cell, s.priority);
            if (s.prev >= 0)
            {
                store.Slots[s.prev].next = s.next;
            }
            else
            {
                heads[headIndex] = s.next;
            }

            if (s.next >= 0)
            {
                store.Slots[s.next].prev = s.prev;
            }

            s.prev = -1;
            s.next = -1;
            s.cell = -1;
        }

        public void RelinkPosition(int slot)
        {
            int nextCell = CellOf(store.Slots[slot].x, store.Slots[slot].z);
            if (nextCell == store.Slots[slot].cell)
            {
                return;
            }

            Unlink(slot);
            Link(slot);
        }

        public void RelinkPriority(int slot, PoiPriority priority)
        {
            Unlink(slot);
            store.Slots[slot].priority = priority;
            Link(slot);
        }

        public int Head(int cell, PoiPriority priority)
        {
            return heads[HeadIndex(cell, priority)];
        }

        public void CellRange(float minX, float minZ, float maxX, float maxZ, out int x0, out int z0, out int x1, out int z1)
        {
            x0 = (int)Math.Floor((minX - OriginX) / CellSize);
            z0 = (int)Math.Floor((minZ - OriginZ) / CellSize);
            x1 = (int)Math.Floor((maxX - OriginX) / CellSize);
            z1 = (int)Math.Floor((maxZ - OriginZ) / CellSize);
            if (x0 < 0) x0 = 0;
            if (z0 < 0) z0 = 0;
            if (x1 >= CellCountX) x1 = CellCountX - 1;
            if (z1 >= CellCountZ) z1 = CellCountZ - 1;
        }

        int HeadIndex(int cell, PoiPriority priority)
        {
            return cell * PoiEnums.PriorityCount + (int)priority;
        }
    }
}
