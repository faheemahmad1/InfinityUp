using System;
using System.Collections.Generic;

namespace Poi
{
    public interface IPoiSelector
    {
        int SlotsVisited { get; }
        int Select(PoiQuery query, List<PoiHit> results);
    }

    /// <summary>
    /// Priority first, then nearest inside that priority.
    /// A full priority band inside the footprint is examined even when it is larger than the budget.
    /// Lower priorities are skipped once the budget is filled.
    /// </summary>
    public sealed class PoiSelector : IPoiSelector
    {
        public int SlotsVisited { get; private set; }

        readonly PoiStore store;
        readonly UniformGridIndex index;
        readonly List<int> band = new List<int>(256);
        int[] heapSlot = new int[16];
        float[] heapDist = new float[16];
        int heapCount;

        public PoiSelector(PoiStore store, UniformGridIndex index)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.index = index ?? throw new ArgumentNullException(nameof(index));
        }

        public int Select(PoiQuery query, List<PoiHit> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            SlotsVisited = 0;
            if (query.budget <= 0)
            {
                return 0;
            }

            float displaySq = query.displayRange * query.displayRange;
            float closeSq = query.closeRange * query.closeRange;
            int remaining = query.budget;
            index.CellRange(query.minX, query.minZ, query.maxX, query.maxZ, out int x0, out int z0, out int x1, out int z1);
            if (x1 < x0 || z1 < z0)
            {
                return 0;
            }

            for (int priority = PoiEnums.PriorityCount - 1; priority >= (int)query.minPriority; priority--)
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
                            if (Passes(ref s, query, displaySq))
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
                        results.Add(MakeHit(band[i], query.playerX, query.playerZ, closeSq));
                    }

                    remaining -= band.Count;
                    if (remaining == 0)
                    {
                        break;
                    }
                }
                else
                {
                    SelectNearest(remaining, query.playerX, query.playerZ);
                    for (int i = 0; i < heapCount; i++)
                    {
                        results.Add(MakeHit(heapSlot[i], query.playerX, query.playerZ, closeSq));
                    }

                    break;
                }
            }

            return results.Count;
        }

        bool Passes(ref PoiStore.Slot slot, PoiQuery query, float displaySq)
        {
            if (!slot.alive)
            {
                return false;
            }

            if ((query.typeMask & PoiEnums.TypeBit(slot.type)) == 0)
            {
                return false;
            }

            if (slot.x < query.minX || slot.x > query.maxX || slot.z < query.minZ || slot.z > query.maxZ)
            {
                return false;
            }

            float distanceSq = GroundFootprint.GroundDistanceSq(slot.x, slot.z, query.playerX, query.playerZ);
            return distanceSq <= displaySq;
        }

        PoiHit MakeHit(int slot, float playerX, float playerZ, float closeSq)
        {
            ref PoiStore.Slot s = ref store.Slots[slot];
            float distanceSq = GroundFootprint.GroundDistanceSq(s.x, s.z, playerX, playerZ);
            return new PoiHit
            {
                slot = slot,
                id = s.id,
                name = s.name,
                type = s.type,
                priority = s.priority,
                icon = s.icon,
                lod = distanceSq <= closeSq ? MarkerLod.IconAndLabel : MarkerLod.Icon,
                x = s.x,
                z = s.z,
                distanceSq = distanceSq
            };
        }

        void SelectNearest(int count, float playerX, float playerZ)
        {
            EnsureHeap(count);
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

        void EnsureHeap(int count)
        {
            if (heapSlot.Length >= count)
            {
                return;
            }

            heapSlot = new int[count];
            heapDist = new float[count];
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
