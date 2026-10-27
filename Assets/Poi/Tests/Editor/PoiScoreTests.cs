using System.Collections.Generic;
using NUnit.Framework;

namespace Poi.Tests
{
    public sealed class PoiScoreTests
    {
        [Test]
        public void FootprintExaminedCountDoesNotGrowWithPoisOutsideIt()
        {
            int near = ExaminedAroundOrigin(8, 0);
            int withFar = ExaminedAroundOrigin(8, 250000);
            Assert.AreEqual(near, withFar);
            Assert.Less(withFar, 64);
        }

        [Test]
        public void MoversOutsideTheFootprintDoNotChangeTheExaminedCount()
        {
            var service = new PoiService(16000f, 400f, PoiService.MaxCapacity);
            var simulator = new PoiFeedSimulator(service, 11);
            for (int i = 0; i < 12500; i++)
            {
                service.Add("Far", PoiType.Resource, PoiPriority.Normal, IconKey.Resource, 4200f, -3000f + (i % 40) * 20f);
            }

            simulator.SetTargetCount(12500);
            simulator.Tick(0.016f, 20000, 0f, 0f, 100f, 0f);
            int before = Examine(service);
            simulator.Tick(0.2f, 20000, 0f, 0f, 100f, 80f);
            int after = Examine(service);
            Assert.AreEqual(before, after);
            Assert.AreEqual(0, after);
            Assert.AreEqual(12500, simulator.MoverCount);
        }

        [Test]
        public void AMoverLeavesItsOldCell()
        {
            var service = new PoiService(16000f, 400f, 64);
            var simulator = new PoiFeedSimulator(service, 5);
            int id = service.Add("Mover", PoiType.Landmark, PoiPriority.Normal, IconKey.Landmark, 4200f, 100f);
            simulator.SetTargetCount(1);
            simulator.Tick(0.016f, 10, 0f, 0f, 100f, 0f);
            int slot = service.Store.SlotOf(id);
            int oldCell = service.Store.Slots[slot].cell;
            simulator.Tick(1f, 10, 0f, 0f, 100f, 800f);
            slot = service.Store.SlotOf(id);
            int newCell = service.Store.Slots[slot].cell;
            Assert.AreNotEqual(oldCell, newCell);
            int head = service.Index.Head(oldCell, PoiPriority.Normal);
            while (head >= 0)
            {
                Assert.AreNotEqual(id, service.Store.Slots[head].id);
                head = service.Store.Slots[head].next;
            }

            head = service.Index.Head(newCell, PoiPriority.Normal);
            bool found = false;
            while (head >= 0)
            {
                if (service.Store.Slots[head].id == id)
                {
                    found = true;
                }

                head = service.Store.Slots[head].next;
            }

            Assert.IsTrue(found);
        }

        [Test]
        public void LodHoldsAcrossTheBand()
        {
            var service = new PoiService(16000f, 400f, 16);
            int id = service.Add("Ring", PoiType.Landmark, PoiPriority.Normal, IconKey.Landmark, 20f, 0f);
            var hits = new List<PoiHit>(4);
            Assert.AreEqual(MarkerLod.IconAndLabel, LodAt(service, hits, id));
            service.Move(id, 155f, 0f);
            Assert.AreEqual(MarkerLod.IconAndLabel, LodAt(service, hits, id));
            service.Move(id, 170f, 0f);
            Assert.AreEqual(MarkerLod.Icon, LodAt(service, hits, id));
            service.Move(id, 145f, 0f);
            Assert.AreEqual(MarkerLod.Icon, LodAt(service, hits, id));
            service.Move(id, 130f, 0f);
            Assert.AreEqual(MarkerLod.IconAndLabel, LodAt(service, hits, id));
        }

        [Test]
        public void HudStatsBufferDoesNotAllocate()
        {
            var buffer = new char[256];
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            int length = HudText.WriteStats(buffer, 250000, 12500, 250, 800, 0.125d, 0.5d, 1.25d, 400f);
            long after = System.GC.GetAllocatedBytesForCurrentThread();
            Assert.Greater(length, 0);
            Assert.AreEqual(before, after);
        }

        static int ExaminedAroundOrigin(int nearCount, int farCount)
        {
            var service = new PoiService(16000f, 400f, PoiService.MaxCapacity);
            for (int i = 0; i < nearCount; i++)
            {
                service.Add("Near", PoiType.Landmark, PoiPriority.Normal, IconKey.Landmark, i * 10f, 0f);
            }

            for (int i = 0; i < farCount; i++)
            {
                service.Add("Far", PoiType.Resource, PoiPriority.Low, IconKey.Resource, 7000f, -2000f + (i % 80) * 40f);
            }

            return Examine(service);
        }

        static int Examine(PoiService service)
        {
            var hits = new List<PoiHit>(16);
            var query = new PoiQuery
            {
                minX = -100f,
                minZ = -100f,
                maxX = 100f,
                maxZ = 100f,
                playerX = 0f,
                playerZ = 0f,
                closeRange = 150f,
                displayRange = 500f,
                budget = 250,
                minPriority = PoiPriority.Low,
                typeMask = PoiEnums.AllTypesMask
            };
            service.Selector.Select(query, hits);
            return service.Selector.SlotsVisited;
        }

        static MarkerLod LodAt(PoiService service, List<PoiHit> hits, int id)
        {
            var query = new PoiQuery
            {
                minX = -400f,
                minZ = -400f,
                maxX = 400f,
                maxZ = 400f,
                playerX = 0f,
                playerZ = 0f,
                closeRange = 150f,
                displayRange = 500f,
                budget = 8,
                minPriority = PoiPriority.Low,
                typeMask = PoiEnums.AllTypesMask
            };
            service.Selector.Select(query, hits);
            LodHysteresis.Apply(hits, service.Store, 150f, 10f);
            for (int i = 0; i < hits.Count; i++)
            {
                if (hits[i].id == id)
                {
                    return hits[i].lod;
                }
            }

            return MarkerLod.None;
        }
    }
}
