using System;
using System.Collections.Generic;

namespace Poi
{
    public interface IPoiStore
    {
        int AliveCount { get; }
        int Capacity { get; }
        bool TryGet(int id, out PoiSnapshot snapshot);
        PoiSnapshot GetBySlot(int slot);
    }

    /// <summary>
    /// Slot array plus a free list. Dead slots drop their name so the string can be collected.
    /// Slot indices stay stable; the alive list is a separate index array.
    /// </summary>
    public sealed class PoiStore : IPoiStore
    {
        public struct Slot
        {
            public int id;
            public string name;
            public PoiType type;
            public PoiPriority priority;
            public IconKey icon;
            public float x;
            public float z;
            public int cell;
            public int prev;
            public int next;
            public int moverIndex;
            public bool alive;
        }

        public Slot[] Slots;
        public int[] AliveSlots;
        public int AliveCount { get; private set; }
        public int Capacity => Slots.Length;

        readonly Dictionary<int, int> idToSlot;
        readonly int[] freeNext;
        readonly int[] aliveIndex;
        int freeHead;
        int nextGeneratedId;

        public PoiStore(int capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            Slots = new Slot[capacity];
            AliveSlots = new int[capacity];
            aliveIndex = new int[capacity];
            freeNext = new int[capacity];
            idToSlot = new Dictionary<int, int>(capacity);
            freeHead = 0;
            for (int i = 0; i < capacity; i++)
            {
                freeNext[i] = i + 1;
                Slots[i].prev = -1;
                Slots[i].next = -1;
                Slots[i].cell = -1;
                Slots[i].moverIndex = -1;
            }

            freeNext[capacity - 1] = -1;
            nextGeneratedId = 1;
        }

        public bool TryGet(int id, out PoiSnapshot snapshot)
        {
            if (!idToSlot.TryGetValue(id, out int slot))
            {
                snapshot = default;
                return false;
            }

            snapshot = GetBySlot(slot);
            return snapshot.alive;
        }

        public PoiSnapshot GetBySlot(int slot)
        {
            Slot s = Slots[slot];
            return new PoiSnapshot(s.id, s.name, s.type, s.priority, s.icon, s.x, s.z, s.alive);
        }

        public int Allocate(int id, string name, PoiType type, PoiPriority priority, IconKey icon, float x, float z)
        {
            if (freeHead < 0)
            {
                throw new InvalidOperationException("POI store is full.");
            }

            if (id <= 0)
            {
                id = nextGeneratedId++;
            }
            else if (id >= nextGeneratedId)
            {
                nextGeneratedId = id + 1;
            }

            if (idToSlot.ContainsKey(id))
            {
                throw new InvalidOperationException("Duplicate POI id " + id);
            }

            int slot = freeHead;
            freeHead = freeNext[slot];
            Slots[slot].id = id;
            Slots[slot].name = name ?? string.Empty;
            Slots[slot].type = type;
            Slots[slot].priority = priority;
            Slots[slot].icon = icon;
            Slots[slot].x = x;
            Slots[slot].z = z;
            Slots[slot].prev = -1;
            Slots[slot].next = -1;
            Slots[slot].cell = -1;
            Slots[slot].moverIndex = -1;
            Slots[slot].alive = true;
            aliveIndex[slot] = AliveCount;
            AliveSlots[AliveCount++] = slot;
            idToSlot.Add(id, slot);
            return slot;
        }

        public int SlotOf(int id)
        {
            return idToSlot[id];
        }

        public bool Contains(int id)
        {
            return idToSlot.ContainsKey(id);
        }

        public void Release(int slot)
        {
            if (!Slots[slot].alive)
            {
                return;
            }

            idToSlot.Remove(Slots[slot].id);
            Slots[slot].alive = false;
            Slots[slot].name = null;
            Slots[slot].prev = -1;
            Slots[slot].next = -1;
            Slots[slot].cell = -1;
            Slots[slot].moverIndex = -1;
            RemoveAlive(slot);
            freeNext[slot] = freeHead;
            freeHead = slot;
        }

        void RemoveAlive(int slot)
        {
            int index = aliveIndex[slot];
            int last = AliveCount - 1;
            int moved = AliveSlots[last];
            AliveSlots[index] = moved;
            aliveIndex[moved] = index;
            AliveCount = last;
        }
    }
}
