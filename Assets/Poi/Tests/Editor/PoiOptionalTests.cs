using System.Collections.Generic;
using NUnit.Framework;

namespace Poi.Tests
{
    public sealed class PoiOptionalTests
    {
        [Test]
        public void OffScreenHighPriorityGetsAnEdgeAndLowDoesNot()
        {
            PoiService service = new PoiService(16000f, 400f, 256);
            int offscreen = service.Add("Far High", PoiType.Hazard, PoiPriority.High, IconKey.Hazard, 200f, 0f);
            int low = service.Add("Far Low", PoiType.Landmark, PoiPriority.Low, IconKey.Landmark, 220f, 0f);
            int inside = service.Add("Near High", PoiType.Objective, PoiPriority.High, IconKey.Objective, 10f, 0f);
            for (int i = 0; i < 30; i++)
            {
                service.Add("Away " + i, PoiType.Resource, PoiPriority.Critical, IconKey.Resource, 7000f, i * 10f);
            }

            var query = new EdgeIndicatorQuery(service.Store, service.Index);
            var hits = new List<PoiHit>();
            query.Select(0f, 0f, 2000f, -50f, -50f, 50f, 50f, PoiPriority.High, 16, PoiEnums.AllTypesMask, hits);

            Assert.IsTrue(Contains(hits, offscreen));
            Assert.IsFalse(Contains(hits, low));
            Assert.IsFalse(Contains(hits, inside));
            Assert.Less(query.SlotsVisited, service.Store.AliveCount);
            Assert.LessOrEqual(hits.Count, 16);
        }

        [Test]
        public void DeclutterKeepsHigherPriorityAndNearerEqualPriority()
        {
            var mixed = new List<PoiHit>
            {
                Hit(1, PoiPriority.Low, 5f, 0f, 25f),
                Hit(2, PoiPriority.Critical, 0f, 0f, 0f)
            };
            MarkerDeclutter.Apply(mixed, 18f);
            Assert.AreEqual(1, mixed.Count);
            Assert.AreEqual(2, mixed[0].id);

            var equal = new List<PoiHit>
            {
                Hit(3, PoiPriority.Normal, 10f, 0f, 100f),
                Hit(4, PoiPriority.Normal, 0f, 0f, 1f)
            };
            MarkerDeclutter.Apply(equal, 18f);
            Assert.AreEqual(1, equal.Count);
            Assert.AreEqual(4, equal[0].id);
        }

        [Test]
        public void PickUsesTheMarkerAndDetailsClearAfterRemoval()
        {
            PoiService service = new PoiService(16000f, 400f, 64);
            int id = service.AddWithId(7, "Kept", PoiType.Settlement, PoiPriority.High, IconKey.Settlement, 10f, 0f);
            var hits = new List<PoiHit> { Hit(id, PoiPriority.High, 10f, 0f, 100f) };
            Assert.AreEqual(id, MarkerPick.Pick(hits, 12f, 0f, 18f));
            Assert.IsTrue(service.Store.TryGet(id, out PoiSnapshot snapshot));
            Assert.AreEqual("Kept", snapshot.name);

            Assert.IsTrue(service.Remove(id));
            Assert.IsFalse(service.Store.TryGet(id, out _));
            hits.Clear();
            Assert.AreEqual(-1, MarkerPick.Pick(hits, 12f, 0f, 18f));
        }

        [Test]
        public void SocketAppliesInOrderAndReplaysAfterReconnect()
        {
            PoiService service = new PoiService(16000f, 400f, 64);
            var receiver = new PoiFeedReceiver(service);
            var socket = new MockPoiSocket();

            FeedMessage first = socket.MakeAdd(1, "A", PoiType.Landmark, PoiPriority.High, IconKey.Landmark, 0f, 0f);
            FeedMessage second = socket.MakeAdd(2, "B", PoiType.Hazard, PoiPriority.Critical, IconKey.Hazard, 30f, 0f);
            Assert.IsFalse(receiver.Accept(second));
            Assert.AreEqual(0, service.Store.AliveCount);
            Assert.IsTrue(receiver.Accept(first));
            Assert.IsTrue(service.Store.Contains(1));
            Assert.IsTrue(service.Store.Contains(2));
            Assert.IsFalse(receiver.Accept(first));
            Assert.AreEqual(2, service.Store.AliveCount);

            socket.Disconnect();
            FeedMessage move = socket.MakeMove(1, 80f, 15f);
            Assert.IsFalse(socket.TryReceive(out _));
            Assert.AreEqual(0f, service.Store.Slots[service.Store.SlotOf(1)].x, 0.01f);

            socket.Connect();
            socket.ReplayAfter(receiver.LastApplied);
            Assert.IsTrue(socket.TryReceive(out FeedMessage replay));
            Assert.AreEqual(move.sequence, replay.sequence);
            Assert.IsTrue(receiver.Accept(replay));
            Assert.AreEqual(80f, service.Store.Slots[service.Store.SlotOf(1)].x, 0.01f);
            Assert.IsFalse(receiver.Accept(replay));
            Assert.AreEqual(2, service.Store.AliveCount);
        }

        static bool Contains(List<PoiHit> hits, int id)
        {
            for (int i = 0; i < hits.Count; i++)
            {
                if (hits[i].id == id)
                {
                    return true;
                }
            }

            return false;
        }

        static PoiHit Hit(int id, PoiPriority priority, float x, float z, float distanceSq)
        {
            return new PoiHit
            {
                id = id,
                priority = priority,
                x = x,
                z = z,
                distanceSq = distanceSq
            };
        }
    }
}
