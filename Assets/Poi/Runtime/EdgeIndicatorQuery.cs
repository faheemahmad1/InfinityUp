using System;
using System.Collections.Generic;
using UnityEngine;

namespace Poi
{
    /// <summary>
    /// High-priority POIs outside the camera footprint, limited to cells near the player.
    /// Edge views are not part of the world-marker budget.
    /// </summary>
    public sealed class EdgeIndicatorQuery
    {
        public int SlotsVisited { get; private set; }

        readonly PoiStore store;
        readonly UniformGridIndex index;
        readonly List<int> band = new List<int>(64);
        int[] heapSlot = new int[16];
        float[] heapDist = new float[16];
        int heapCount;

        public EdgeIndicatorQuery(PoiStore store, UniformGridIndex index)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.index = index ?? throw new ArgumentNullException(nameof(index));
        }

        public int Select(
            float playerX,
            float playerZ,
            float radius,
            float minX,
            float minZ,
            float maxX,
            float maxZ,
            PoiPriority minimumPriority,
            int budget,
            int typeMask,
            List<PoiHit> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            SlotsVisited = 0;
            if (budget <= 0 || radius <= 0f)
            {
                return 0;
            }

            float radiusSq = radius * radius;
            int remaining = budget;
            index.CellRange(playerX - radius, playerZ - radius, playerX + radius, playerZ + radius, out int x0, out int z0, out int x1, out int z1);
            if (x1 < x0 || z1 < z0)
            {
                return 0;
            }

            for (int priority = PoiEnums.PriorityCount - 1; priority >= (int)minimumPriority; priority--)
            {
                band.Clear();
                var bandPriority = (PoiPriority)priority;
                for (int z = z0; z <= z1; z++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        int cell = z * index.CellCountX + x;
                        int slot = index.Head(cell, bandPriority);
                        while (slot >= 0)
                        {
                            SlotsVisited++;
                            ref PoiStore.Slot s = ref store.Slots[slot];
                            int next = s.next;
                            if (OutsideFootprint(ref s, minX, minZ, maxX, maxZ, playerX, playerZ, radiusSq, typeMask))
                            {
                                band.Add(slot);
                            }

                            slot = next;
                        }
                    }
                }

                if (band.Count == 0)
                {
                    continue;
                }

                if (band.Count <= remaining)
                {
                    for (int i = 0; i < band.Count; i++)
                    {
                        results.Add(MakeEdge(band[i], playerX, playerZ, minX, minZ, maxX, maxZ));
                    }

                    remaining -= band.Count;
                    if (remaining == 0)
                    {
                        break;
                    }
                }
                else
                {
                    SelectNearest(remaining, playerX, playerZ);
                    for (int i = 0; i < heapCount; i++)
                    {
                        results.Add(MakeEdge(heapSlot[i], playerX, playerZ, minX, minZ, maxX, maxZ));
                    }

                    break;
                }
            }

            return results.Count;
        }

        public static bool TryEdgePoint(
            float originX,
            float originZ,
            float poiX,
            float poiZ,
            float minX,
            float minZ,
            float maxX,
            float maxZ,
            out float edgeX,
            out float edgeZ)
        {
            edgeX = originX;
            edgeZ = originZ;
            if (poiX >= minX && poiX <= maxX && poiZ >= minZ && poiZ <= maxZ)
            {
                return false;
            }

            float dx = poiX - originX;
            float dz = poiZ - originZ;
            float t = float.PositiveInfinity;
            if (dx > 0.0001f)
            {
                t = Mathf.Min(t, (maxX - originX) / dx);
            }
            else if (dx < -0.0001f)
            {
                t = Mathf.Min(t, (minX - originX) / dx);
            }

            if (dz > 0.0001f)
            {
                t = Mathf.Min(t, (maxZ - originZ) / dz);
            }
            else if (dz < -0.0001f)
            {
                t = Mathf.Min(t, (minZ - originZ) / dz);
            }

            if (float.IsPositiveInfinity(t) || t < 0f)
            {
                return false;
            }

            float inset = t * 0.92f;
            edgeX = originX + dx * inset;
            edgeZ = originZ + dz * inset;
            return true;
        }

        bool OutsideFootprint(ref PoiStore.Slot slot, float minX, float minZ, float maxX, float maxZ, float playerX, float playerZ, float radiusSq, int typeMask)
        {
            if (!slot.alive)
            {
                return false;
            }

            if ((typeMask & PoiEnums.TypeBit(slot.type)) == 0)
            {
                return false;
            }

            if (slot.x >= minX && slot.x <= maxX && slot.z >= minZ && slot.z <= maxZ)
            {
                return false;
            }

            return GroundFootprint.GroundDistanceSq(slot.x, slot.z, playerX, playerZ) <= radiusSq;
        }

        PoiHit MakeEdge(int slot, float playerX, float playerZ, float minX, float minZ, float maxX, float maxZ)
        {
            ref PoiStore.Slot s = ref store.Slots[slot];
            TryEdgePoint(playerX, playerZ, s.x, s.z, minX, minZ, maxX, maxZ, out float edgeX, out float edgeZ);
            return new PoiHit
            {
                slot = slot,
                id = s.id,
                name = s.name,
                type = s.type,
                priority = s.priority,
                icon = s.icon,
                lod = MarkerLod.Icon,
                x = edgeX,
                z = edgeZ,
                distanceSq = GroundFootprint.GroundDistanceSq(s.x, s.z, playerX, playerZ)
            };
        }

        void SelectNearest(int count, float playerX, float playerZ)
        {
            if (heapSlot.Length < count)
            {
                heapSlot = new int[count];
                heapDist = new float[count];
            }

            heapCount = 0;
            for (int i = 0; i < band.Count; i++)
            {
                int slot = band[i];
                ref PoiStore.Slot s = ref store.Slots[slot];
                float distanceSq = GroundFootprint.GroundDistanceSq(s.x, s.z, playerX, playerZ);
                if (heapCount < count)
                {
                    heapSlot[heapCount] = slot;
                    heapDist[heapCount] = distanceSq;
                    SiftUp(heapCount);
                    heapCount++;
                }
                else if (distanceSq < heapDist[0])
                {
                    heapSlot[0] = slot;
                    heapDist[0] = distanceSq;
                    SiftDown(0);
                }
            }
        }

        void SiftUp(int child)
        {
            while (child > 0)
            {
                int parent = (child - 1) >> 1;
                if (heapDist[parent] >= heapDist[child])
                {
                    break;
                }

                Swap(parent, child);
                child = parent;
            }
        }

        void SiftDown(int parent)
        {
            while (true)
            {
                int left = (parent << 1) + 1;
                if (left >= heapCount)
                {
                    return;
                }

                int right = left + 1;
                int largest = right < heapCount && heapDist[right] > heapDist[left] ? right : left;
                if (heapDist[parent] >= heapDist[largest])
                {
                    return;
                }

                Swap(parent, largest);
                parent = largest;
            }
        }

        void Swap(int a, int b)
        {
            int slot = heapSlot[a];
            heapSlot[a] = heapSlot[b];
            heapSlot[b] = slot;
            float distance = heapDist[a];
            heapDist[a] = heapDist[b];
            heapDist[b] = distance;
        }
    }
}
