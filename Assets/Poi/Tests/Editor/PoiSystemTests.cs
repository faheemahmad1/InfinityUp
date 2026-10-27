using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Poi.Tests
{
    public sealed class PoiSystemTests
    {
        readonly List<PoiHit> hits = new List<PoiHit>(64);

        [Test]
        public void InsertRemoveAndCrossCellMoveStayConsistent()
        {
            PoiService service = NewService();
            int id = service.Add("A", PoiType.Landmark, PoiPriority.High, IconKey.Landmark, 0f, 0f);
            Assert.IsTrue(service.Store.TryGet(id, out PoiSnapshot before));
            Assert.AreEqual(0f, before.x, 0.001f);

            service.Move(id, 500f, 0f);
            PoiQuery oldCell = Query(0f, 0f, 100f, 8);
            PoiQuery newCell = Query(500f, 0f, 100f, 8);
            service.Selector.Select(oldCell, hits);
            Assert.AreEqual(0, hits.Count);
            service.Selector.Select(newCell, hits);
            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(id, hits[0].id);

            Assert.IsTrue(service.Remove(id));
            Assert.IsFalse(service.Store.TryGet(id, out _));
            service.Selector.Select(newCell, hits);
            Assert.AreEqual(0, hits.Count);
            Assert.AreEqual(0, service.Store.AliveCount);
        }

        [Test]
        public void OutsideRectOrDisplayRangeIsExcluded()
        {
            PoiService service = NewService();
            service.Add("Inside", PoiType.Resource, PoiPriority.Normal, IconKey.Resource, 10f, 10f);
            service.Add("Far", PoiType.Resource, PoiPriority.Critical, IconKey.Resource, 450f, 0f);
            service.Add("OutsideRect", PoiType.Resource, PoiPriority.Critical, IconKey.Resource, 180f, 0f);
            PoiQuery query = Query(0f, 0f, 150f, 8);
            query.displayRange = 200f;
            service.Selector.Select(query, hits);
            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual("Inside", hits[0].name);
        }

        [Test]
        public void HigherPriorityBeatsNearerLowerPriority()
        {
            PoiService service = NewService();
            int nearLow = service.Add("Near", PoiType.Hazard, PoiPriority.Low, IconKey.Hazard, 10f, 0f);
            int farCritical = service.Add("Far", PoiType.Hazard, PoiPriority.Critical, IconKey.Hazard, 180f, 0f);
            PoiQuery query = Query(0f, 0f, 400f, 1);
            service.Selector.Select(query, hits);
            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(farCritical, hits[0].id);
            Assert.AreNotEqual(nearLow, hits[0].id);
        }

        [Test]
        public void EqualPriorityKeepsNearestAndExaminesTheWholeBand()
        {
            PoiService service = NewService();
            for (int i = 0; i < 20; i++)
            {
                service.Add("P" + i, PoiType.Settlement, PoiPriority.Critical, IconKey.Settlement, 10f * (i + 1), 0f);
            }

            PoiQuery query = Query(0f, 0f, 400f, 5);
            query.displayRange = 1000f;
            service.Selector.Select(query, hits);
            Assert.AreEqual(5, hits.Count);
            Assert.AreEqual(20, service.Selector.SlotsVisited);
            float farthestKept = hits.Max(hit => hit.distanceSq);
            float nearestExcluded = float.MaxValue;
            for (int i = 0; i < 20; i++)
            {
                float x = 10f * (i + 1);
                float distance = x * x;
                bool kept = hits.Any(hit => Mathf.Abs(hit.x - x) < 0.01f);
                if (!kept && distance < nearestExcluded)
                {
                    nearestExcluded = distance;
                }
            }

            Assert.Less(farthestKept, nearestExcluded);
        }

        [Test]
        public void GroundDistanceRanksCloserPoiUnderATallCamera()
        {
            PoiService service = NewService();
            int close = service.Add("Close", PoiType.Objective, PoiPriority.High, IconKey.Objective, 40f, 0f);
            service.Add("Far", PoiType.Objective, PoiPriority.High, IconKey.Objective, 180f, 0f);
            PoiQuery query = Query(0f, 0f, 400f, 1);
            service.Selector.Select(query, hits);
            Assert.AreEqual(close, hits[0].id);
            float cameraDistanceClose = Mathf.Sqrt(40f * 40f + 400f * 400f);
            float cameraDistanceFar = Mathf.Sqrt(180f * 180f + 400f * 400f);
            Assert.Less(Mathf.Abs(cameraDistanceClose - cameraDistanceFar), 30f);
            Assert.AreEqual(40f * 40f, hits[0].distanceSq, 0.01f);
        }

        [Test]
        public void CameraHeightChangesFootprintAndSelection()
        {
            var rotation = Quaternion.Euler(90f, 0f, 0f);
            GroundFootprint.FromPose(new Vector3(0f, 50f, 0f), rotation, 60f, 16f / 9f, out float minLow, out _, out float maxLow, out _);
            GroundFootprint.FromPose(new Vector3(0f, 400f, 0f), rotation, 60f, 16f / 9f, out float minHigh, out _, out float maxHigh, out _);
            Assert.Greater(maxHigh - minHigh, maxLow - minLow);

            PoiService service = NewService();
            int edge = service.Add("Edge", PoiType.Landmark, PoiPriority.Critical, IconKey.Landmark, 200f, 0f);
            var low = new PoiQuery
            {
                minX = minLow,
                maxX = maxLow,
                minZ = -1000f,
                maxZ = 1000f,
                playerX = 0f,
                playerZ = 0f,
                closeRange = 150f,
                displayRange = 2000f,
                budget = 10,
                minPriority = PoiPriority.Low,
                typeMask = PoiEnums.AllTypesMask
            };
            service.Selector.Select(low, hits);
            Assert.AreEqual(0, hits.Count);
            low.minX = minHigh;
            low.maxX = maxHigh;
            service.Selector.Select(low, hits);
            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(edge, hits[0].id);
            Assert.LessOrEqual(hits.Count, low.budget);
        }

        [Test]
        public void PlayerMovementBringsPoisInAndOut()
        {
            PoiService service = NewService();
            int id = service.Add("Shift", PoiType.Resource, PoiPriority.Normal, IconKey.Resource, 100f, 0f);
            service.Selector.Select(Query(0f, 0f, 40f, 4), hits);
            Assert.AreEqual(0, hits.Count);
            service.Selector.Select(Query(100f, 0f, 40f, 4), hits);
            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(id, hits[0].id);
        }

        [Test]
        public void FootprintMatchesCameraProjection()
        {
            var go = new GameObject("footprint-camera");
            var camera = go.AddComponent<Camera>();
            go.transform.SetPositionAndRotation(new Vector3(25f, 400f, -15f), Quaternion.Euler(90f, 0f, 0f));
            camera.fieldOfView = 60f;
            camera.aspect = 16f / 9f;
            GroundFootprint.FromPose(go.transform.position, go.transform.rotation, camera.fieldOfView, camera.aspect, out float minX, out float minZ, out float maxX, out float maxZ);
            GroundFootprint.FromCamera(camera, out float camMinX, out float camMinZ, out float camMaxX, out float camMaxZ);
            Assert.AreEqual(camMinX, minX, 0.05f);
            Assert.AreEqual(camMinZ, minZ, 0.05f);
            Assert.AreEqual(camMaxX, maxX, 0.05f);
            Assert.AreEqual(camMaxZ, maxZ, 0.05f);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void FiltersApplyOnTheNextQuery()
        {
            PoiService service = NewService();
            service.Add("Low", PoiType.Landmark, PoiPriority.Low, IconKey.Landmark, 5f, 0f);
            int hazard = service.Add("Hazard", PoiType.Hazard, PoiPriority.High, IconKey.Hazard, 8f, 0f);
            PoiQuery query = Query(0f, 0f, 100f, 8);
            query.typeMask = PoiEnums.TypeBit(PoiType.Hazard);
            query.minPriority = PoiPriority.High;
            service.Selector.Select(query, hits);
            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(hazard, hits[0].id);
            query.typeMask = PoiEnums.TypeBit(PoiType.Landmark);
            service.Selector.Select(query, hits);
            Assert.AreEqual(0, hits.Count);
        }

        [Test]
        public void RapidAddMoveRemoveReusesSlots()
        {
            PoiService service = NewService(64);
            var ids = new List<int>();
            for (int i = 0; i < 40; i++)
            {
                ids.Add(service.Add("N" + i, PoiType.Objective, PoiPriority.Normal, IconKey.Objective, i * 3f, 0f));
            }

            for (int i = 0; i < 20; i++)
            {
                service.Move(ids[i], i * 3f, 80f);
            }

            for (int i = 0; i < 10; i++)
            {
                Assert.IsTrue(service.Remove(ids[i]));
            }

            Assert.AreEqual(30, service.Store.AliveCount);
            int reused = service.Add("Reused", PoiType.Resource, PoiPriority.Critical, IconKey.Resource, 1f, 1f);
            Assert.IsFalse(service.Store.TryGet(ids[0], out _));
            Assert.IsTrue(service.Store.TryGet(reused, out _));
            service.Selector.Select(Query(0f, 40f, 500f, 64), hits);
            Assert.LessOrEqual(hits.Count, 64);
            foreach (PoiHit hit in hits)
            {
                Assert.IsTrue(service.Store.TryGet(hit.id, out PoiSnapshot snapshot));
                Assert.IsTrue(snapshot.alive);
            }
        }

        [Test]
        public void BudgetHoldsWhilePositionsAndMembershipChange()
        {
            PoiService service = NewService();
            var simulator = new PoiFeedSimulator(service, 7);
            simulator.SetTargetCount(80);
            simulator.Tick(0.2f, 80, 0f, 0f, 100f, 0f);
            Assert.AreEqual(80, service.Store.AliveCount);
            PoiQuery query = Query(0f, 0f, 8000f, 10);
            query.displayRange = 20000f;
            for (int frame = 0; frame < 5; frame++)
            {
                simulator.Tick(0.05f, 10, 5f, 5f, 100f, 30f);
                int count = service.Selector.Select(query, hits);
                Assert.LessOrEqual(count, query.budget);
                Assert.AreEqual(count, hits.Count);
            }
        }

        [Test]
        public void StaleIconIsRejectedAfterReleaseRebindAndRemoval()
        {
            var binding = new MarkerBinding();
            binding.Bind(4, "Alpha", true, IconKey.Landmark);
            int first = binding.Generation;
            object firstToken = new object();
            binding.Release();
            Assert.IsFalse(binding.TryApplyIcon(first, firstToken));
            Assert.IsFalse(binding.HasSprite);
            Assert.AreEqual(string.Empty, binding.Name);

            binding.Bind(4, "Alpha", true, IconKey.Landmark);
            int stale = binding.Generation;
            binding.Bind(9, "Beta", true, IconKey.Objective);
            Assert.IsFalse(binding.TryApplyIcon(stale, firstToken));
            Assert.AreEqual("Beta", binding.Name);
            Assert.AreEqual(9, binding.PoiId);
            object second = new object();
            Assert.IsTrue(binding.TryApplyIcon(binding.Generation, second));
            Assert.AreSame(second, binding.SpriteToken);

            binding.Release();
            Assert.IsFalse(binding.HasSprite);
            Assert.AreEqual(string.Empty, binding.Name);
        }

        [Test]
        public void CloseRangeUsesIconAndLabel()
        {
            PoiService service = NewService();
            service.Add("Near", PoiType.Landmark, PoiPriority.Normal, IconKey.Landmark, 20f, 0f);
            service.Add("Mid", PoiType.Landmark, PoiPriority.Normal, IconKey.Landmark, 200f, 0f);
            PoiQuery query = Query(0f, 0f, 400f, 8);
            service.Selector.Select(query, hits);
            PoiHit near = hits.First(hit => hit.name == "Near");
            PoiHit mid = hits.First(hit => hit.name == "Mid");
            Assert.AreEqual(MarkerLod.IconAndLabel, near.lod);
            Assert.AreEqual(MarkerLod.Icon, mid.lod);
        }

        [Test]
        public void RemovedNameIsDropped()
        {
            PoiService service = NewService();
            int id = service.Add("Temp", PoiType.Hazard, PoiPriority.Low, IconKey.Hazard, 0f, 0f);
            int slot = service.Store.SlotOf(id);
            service.Remove(id);
            Assert.IsNull(service.Store.Slots[slot].name);
            Assert.IsFalse(service.Store.Slots[slot].alive);
        }

        [Test]
        public void SmallFootprintDoesNotWalkTheWholeStore()
        {
            var service = new PoiService(16000f, 400f, PoiService.MaxCapacity);
            var simulator = new PoiFeedSimulator(service, 11);
            simulator.SetTargetCount(250000);
            while (service.Store.AliveCount < 250000)
            {
                simulator.Tick(0f, 20000, 0f, 0f, 0f, 0f);
            }

            service.Add("Anchor", PoiType.Objective, PoiPriority.Critical, IconKey.Objective, 0f, 0f);
            PoiQuery query = Query(0f, 0f, 200f, 250);
            service.Selector.Select(query, hits);
            TestContext.WriteLine("Examined slots at 250000: " + service.Selector.SlotsVisited);
            Assert.Less(service.Selector.SlotsVisited, 250000);
            Assert.LessOrEqual(hits.Count, 250);
        }

        [Test]
        public void JsonNamesMapOntoEnums()
        {
            Assert.IsTrue(PoiEnums.TryParseType("hazard", out PoiType type));
            Assert.AreEqual(PoiType.Hazard, type);
            Assert.IsFalse(PoiEnums.TryParsePriority("nope", out _));
            Assert.IsFalse(PoiEnums.TryParseIcon("missing", out _));
        }

        static PoiService NewService(int capacity = 256)
        {
            return new PoiService(16000f, 400f, capacity);
        }

        static PoiQuery Query(float x, float z, float half, int budget)
        {
            return new PoiQuery
            {
                minX = x - half,
                maxX = x + half,
                minZ = z - half,
                maxZ = z + half,
                playerX = x,
                playerZ = z,
                closeRange = 150f,
                displayRange = 500f,
                budget = budget,
                minPriority = PoiPriority.Low,
                typeMask = PoiEnums.AllTypesMask
            };
        }
    }
}
