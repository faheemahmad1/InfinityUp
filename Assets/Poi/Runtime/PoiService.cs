using System;

namespace Poi
{
    public interface IPoiFeed
    {
        event Action<int> Added;
        event Action<int> Moved;
        event Action<int, PoiChangeKind> Changed;
        event Action<int> Removed;
    }

    public interface IPoiCommands
    {
        int Add(string name, PoiType type, PoiPriority priority, IconKey icon, float x, float z);
        int AddWithId(int id, string name, PoiType type, PoiPriority priority, IconKey icon, float x, float z);
        void Move(int id, float x, float z);
        void Change(int id, PoiChangeKind kind, string name, PoiType type, PoiPriority priority, IconKey icon);
        bool Remove(int id);
    }

    public sealed class PoiService : IPoiFeed, IPoiCommands
    {
        public const int MaxCapacity = 262144;

        public event Action<int> Added;
        public event Action<int> Moved;
        public event Action<int, PoiChangeKind> Changed;
        public event Action<int> Removed;

        public PoiStore Store { get; }
        public UniformGridIndex Index { get; }
        public PoiSelector Selector { get; }
        public float WorldSize { get; }
        public float CellSize { get; }

        public PoiService(float worldSize, float cellSize)
            : this(worldSize, cellSize, MaxCapacity)
        {
        }

        public PoiService(float worldSize, float cellSize, int capacity)
        {
            WorldSize = worldSize;
            CellSize = cellSize;
            Store = new PoiStore(capacity);
            Index = new UniformGridIndex(Store, worldSize, cellSize);
            Selector = new PoiSelector(Store, Index);
        }

        public int Add(string name, PoiType type, PoiPriority priority, IconKey icon, float x, float z)
        {
            return AddWithId(0, name, type, priority, icon, x, z);
        }

        public int AddWithId(int id, string name, PoiType type, PoiPriority priority, IconKey icon, float x, float z)
        {
            ClampToWorld(ref x, ref z);
            int slot = Store.Allocate(id, name, type, priority, icon, x, z);
            Index.Link(slot);
            int storedId = Store.Slots[slot].id;
            Added?.Invoke(storedId);
            return storedId;
        }

        public void Move(int id, float x, float z)
        {
            if (!Store.Contains(id))
            {
                return;
            }

            ClampToWorld(ref x, ref z);
            int slot = Store.SlotOf(id);
            Store.Slots[slot].x = x;
            Store.Slots[slot].z = z;
            Index.RelinkPosition(slot);
            Moved?.Invoke(id);
        }

        public void Change(int id, PoiChangeKind kind, string name, PoiType type, PoiPriority priority, IconKey icon)
        {
            if (!Store.Contains(id))
            {
                return;
            }

            int slot = Store.SlotOf(id);
            switch (kind)
            {
                case PoiChangeKind.Name:
                    Store.Slots[slot].name = name ?? string.Empty;
                    break;
                case PoiChangeKind.Type:
                    Store.Slots[slot].type = type;
                    break;
                case PoiChangeKind.Priority:
                    if (Store.Slots[slot].priority != priority)
                    {
                        Index.RelinkPriority(slot, priority);
                    }

                    break;
                case PoiChangeKind.Icon:
                    Store.Slots[slot].icon = icon;
                    break;
            }

            Changed?.Invoke(id, kind);
        }

        public bool Remove(int id)
        {
            if (!Store.Contains(id))
            {
                return false;
            }

            int slot = Store.SlotOf(id);
            Index.Unlink(slot);
            Store.Release(slot);
            Removed?.Invoke(id);
            return true;
        }

        public void LoadSeed(PoiSeedEntry[] entries)
        {
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                PoiSeedEntry entry = entries[i];
                if (entry.id > 0 && Store.Contains(entry.id))
                {
                    continue;
                }

                AddWithId(entry.id, entry.name, entry.type, entry.priority, entry.icon, entry.x, entry.z);
            }
        }

        void ClampToWorld(ref float x, ref float z)
        {
            float half = WorldSize * 0.5f;
            if (x < -half) x = -half;
            if (x > half) x = half;
            if (z < -half) z = -half;
            if (z > half) z = half;
        }
    }
}
