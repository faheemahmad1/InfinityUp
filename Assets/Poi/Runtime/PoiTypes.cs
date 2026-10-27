using System;

namespace Poi
{
    public enum PoiType : byte
    {
        Landmark = 0,
        Resource = 1,
        Hazard = 2,
        Settlement = 3,
        Objective = 4
    }

    public enum PoiPriority : byte
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    public enum MarkerLod : byte
    {
        None = 0,
        Icon = 1,
        IconAndLabel = 2
    }

    public enum IconKey : byte
    {
        Landmark = 0,
        Resource = 1,
        Hazard = 2,
        Settlement = 3,
        Objective = 4,
        Fallback = 5
    }

    public enum PoiChangeKind : byte
    {
        Name = 0,
        Type = 1,
        Priority = 2,
        Icon = 3
    }

    public static class PoiEnums
    {
        public const int TypeCount = 5;
        public const int PriorityCount = 4;
        public const int IconCount = 6;
        public const int AllTypesMask = (1 << TypeCount) - 1;

        public static int TypeBit(PoiType type)
        {
            return 1 << (int)type;
        }

        public static bool TryParseType(string value, out PoiType type)
        {
            return Enum.TryParse(value, true, out type) && Enum.IsDefined(typeof(PoiType), type);
        }

        public static bool TryParsePriority(string value, out PoiPriority priority)
        {
            return Enum.TryParse(value, true, out priority) && Enum.IsDefined(typeof(PoiPriority), priority);
        }

        public static bool TryParseIcon(string value, out IconKey icon)
        {
            return Enum.TryParse(value, true, out icon) && Enum.IsDefined(typeof(IconKey), icon);
        }
    }

    [Serializable]
    public struct PoiSeedEntry
    {
        public int id;
        public string name;
        public PoiType type;
        public float x;
        public float z;
        public IconKey icon;
        public PoiPriority priority;
    }

    public readonly struct PoiSnapshot
    {
        public readonly int id;
        public readonly string name;
        public readonly PoiType type;
        public readonly PoiPriority priority;
        public readonly IconKey icon;
        public readonly float x;
        public readonly float z;
        public readonly bool alive;

        public PoiSnapshot(int id, string name, PoiType type, PoiPriority priority, IconKey icon, float x, float z, bool alive)
        {
            this.id = id;
            this.name = name;
            this.type = type;
            this.priority = priority;
            this.icon = icon;
            this.x = x;
            this.z = z;
            this.alive = alive;
        }
    }

    public struct PoiQuery
    {
        public float minX;
        public float minZ;
        public float maxX;
        public float maxZ;
        public float playerX;
        public float playerZ;
        public float closeRange;
        public float displayRange;
        public int budget;
        public PoiPriority minPriority;
        public int typeMask;
    }

    public struct PoiHit
    {
        public int slot;
        public int id;
        public string name;
        public PoiType type;
        public PoiPriority priority;
        public IconKey icon;
        public MarkerLod lod;
        public float x;
        public float z;
        public float distanceSq;
    }
}
