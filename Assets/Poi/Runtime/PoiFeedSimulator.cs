using System;

namespace Poi
{
    /// <summary>
    /// Live dataset driver. Movement iterates only the mover list.
    /// Membership is rebuilt when the moving percent changes, not every frame.
    /// </summary>
    public sealed class PoiFeedSimulator
    {
        public int TargetCount { get; private set; }
        public int MoverCount { get; private set; }
        public double LastIntegrateMs { get; private set; }
        public double LastRelinkMs { get; private set; }

        readonly PoiService service;
        readonly Random random;
        readonly System.Diagnostics.Stopwatch clock = new System.Diagnostics.Stopwatch();
        int[] movers;
        float[] dirX;
        float[] dirZ;
        int moverCapacity;
        float addAccumulator;
        float removeAccumulator;
        float appliedMovingPercent = float.NaN;
        int sequence;

        public PoiFeedSimulator(PoiService service, int seed)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            random = new Random(seed);
            movers = new int[256];
            dirX = new float[256];
            dirZ = new float[256];
            moverCapacity = 256;
        }

        public void SetTargetCount(int count)
        {
            if (count < 0)
            {
                count = 0;
            }

            if (count > PoiService.MaxCapacity)
            {
                count = PoiService.MaxCapacity;
            }

            TargetCount = count;
        }

        public void Tick(float deltaTime, int batchLimit, float additionsPerSecond, float removalsPerSecond, float movingPercent, float moveSpeed)
        {
            if (deltaTime < 0f)
            {
                deltaTime = 0f;
            }

            if (batchLimit < 1)
            {
                batchLimit = 1;
            }

            if (service.Store.AliveCount != TargetCount)
            {
                ResizeTowardTarget(batchLimit);
            }
            else
            {
                int population = service.Store.AliveCount;
                ApplyRates(deltaTime, additionsPerSecond, removalsPerSecond, movingPercent);
                if (service.Store.AliveCount != population)
                {
                    TargetCount = service.Store.AliveCount;
                }
            }

            if (movingPercent != appliedMovingPercent)
            {
                RebuildMovers(movingPercent);
            }

            IntegrateMovers(deltaTime, moveSpeed);
        }

        public void NotifyRemoved(int slot)
        {
            int index = service.Store.Slots[slot].moverIndex;
            if (index < 0)
            {
                return;
            }

            RemoveMoverAt(index);
        }

        void RemoveMoverAt(int index)
        {
            int slot = movers[index];
            int last = MoverCount - 1;
            int movedSlot = movers[last];
            movers[index] = movedSlot;
            dirX[index] = dirX[last];
            dirZ[index] = dirZ[last];
            service.Store.Slots[movedSlot].moverIndex = index;
            service.Store.Slots[slot].moverIndex = -1;
            MoverCount = last;
        }

        void ResizeTowardTarget(int batchLimit)
        {
            int guard = batchLimit;
            while (guard-- > 0 && service.Store.AliveCount < TargetCount)
            {
                AddGenerated(appliedMovingPercent);
            }

            guard = batchLimit;
            while (guard-- > 0 && service.Store.AliveCount > TargetCount)
            {
                int slot = service.Store.AliveSlots[service.Store.AliveCount - 1];
                int id = service.Store.Slots[slot].id;
                NotifyRemoved(slot);
                service.Remove(id);
            }
        }

        void ApplyRates(float deltaTime, float additionsPerSecond, float removalsPerSecond, float movingPercent)
        {
            if (additionsPerSecond > 0f && service.Store.AliveCount < PoiService.MaxCapacity)
            {
                addAccumulator += additionsPerSecond * deltaTime;
                int guard = 64;
                while (addAccumulator >= 1f && guard-- > 0 && service.Store.AliveCount < PoiService.MaxCapacity)
                {
                    addAccumulator -= 1f;
                    AddGenerated(movingPercent);
                }
            }

            if (removalsPerSecond > 0f && service.Store.AliveCount > 0)
            {
                removeAccumulator += removalsPerSecond * deltaTime;
                int guard = 64;
                while (removeAccumulator >= 1f && guard-- > 0 && service.Store.AliveCount > 0)
                {
                    removeAccumulator -= 1f;
                    int slot = service.Store.AliveSlots[random.Next(service.Store.AliveCount)];
                    NotifyRemoved(slot);
                    service.Remove(service.Store.Slots[slot].id);
                }
            }
        }

        void AddGenerated(float movingPercent)
        {
            float half = service.WorldSize * 0.5f;
            float x = Lerp(-half, half, (float)random.NextDouble());
            float z = Lerp(-half, half, (float)random.NextDouble());
            var type = (PoiType)random.Next(PoiEnums.TypeCount);
            var priority = (PoiPriority)random.Next(PoiEnums.PriorityCount);
            var icon = (IconKey)(int)type;
            int id = service.Add("POI " + (++sequence), type, priority, icon, x, z);
            int slot = service.Store.SlotOf(id);
            if (movingPercent > 0f && service.Store.Slots[slot].moverIndex < 0 && random.NextDouble() * 100.0 <= movingPercent)
            {
                AddMover(slot);
            }
        }

        void RebuildMovers(float movingPercent)
        {
            for (int i = 0; i < MoverCount; i++)
            {
                service.Store.Slots[movers[i]].moverIndex = -1;
            }

            MoverCount = 0;
            appliedMovingPercent = movingPercent;
            if (movingPercent <= 0f)
            {
                return;
            }

            int alive = service.Store.AliveCount;
            for (int i = 0; i < alive; i++)
            {
                if (random.NextDouble() * 100.0 <= movingPercent)
                {
                    AddMover(service.Store.AliveSlots[i]);
                }
            }
        }

        void AddMover(int slot)
        {
            if (MoverCount >= moverCapacity)
            {
                int grown = moverCapacity * 2;
                Array.Resize(ref movers, grown);
                Array.Resize(ref dirX, grown);
                Array.Resize(ref dirZ, grown);
                moverCapacity = grown;
            }

            double angle = random.NextDouble() * Math.PI * 2.0;
            movers[MoverCount] = slot;
            dirX[MoverCount] = (float)Math.Cos(angle);
            dirZ[MoverCount] = (float)Math.Sin(angle);
            service.Store.Slots[slot].moverIndex = MoverCount;
            MoverCount++;
        }

        void IntegrateMovers(float deltaTime, float moveSpeed)
        {
            if (moveSpeed == 0f || deltaTime == 0f || MoverCount == 0)
            {
                LastIntegrateMs = 0d;
                LastRelinkMs = 0d;
                return;
            }

            float half = service.WorldSize * 0.5f;
            float step = moveSpeed * deltaTime;
            clock.Restart();
            EnsureScratch(MoverCount);
            int moving = 0;
            for (int i = 0; i < MoverCount; i++)
            {
                int slot = movers[i];
                if (!service.Store.Slots[slot].alive)
                {
                    RemoveMoverAt(i);
                    i--;
                    continue;
                }

                float x = service.Store.Slots[slot].x + dirX[i] * step;
                float z = service.Store.Slots[slot].z + dirZ[i] * step;
                if (x < -half || x > half)
                {
                    dirX[i] = -dirX[i];
                    x = x < -half ? -half : half;
                }

                if (z < -half || z > half)
                {
                    dirZ[i] = -dirZ[i];
                    z = z < -half ? -half : half;
                }

                scratchSlot[moving] = slot;
                scratchX[moving] = x;
                scratchZ[moving] = z;
                moving++;
            }

            LastIntegrateMs = clock.Elapsed.TotalMilliseconds;
            clock.Restart();
            for (int i = 0; i < moving; i++)
            {
                int id = service.Store.Slots[scratchSlot[i]].id;
                service.Move(id, scratchX[i], scratchZ[i]);
            }

            LastRelinkMs = clock.Elapsed.TotalMilliseconds;
        }

        int[] scratchSlot = new int[256];
        float[] scratchX = new float[256];
        float[] scratchZ = new float[256];

        void EnsureScratch(int count)
        {
            if (scratchSlot.Length >= count)
            {
                return;
            }

            scratchSlot = new int[count];
            scratchX = new float[count];
            scratchZ = new float[count];
        }

        static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }
    }
}
