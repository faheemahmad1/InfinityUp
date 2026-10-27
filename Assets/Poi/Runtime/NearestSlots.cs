using System.Collections.Generic;

namespace Poi
{
    /// <summary>
    /// Keeps the nearest slots in a band. The farthest of the kept set sits at index 0.
    /// </summary>
    public sealed class NearestSlots
    {
        int[] heapSlot = new int[16];
        float[] heapDist = new float[16];
        int heapCount;

        public int Count => heapCount;

        public int SlotAt(int index)
        {
            return heapSlot[index];
        }

        public void Fill(List<int> band, PoiStore store, int count, float playerX, float playerZ)
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
