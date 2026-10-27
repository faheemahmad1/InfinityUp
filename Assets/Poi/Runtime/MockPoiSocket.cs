using System.Collections.Generic;

namespace Poi
{
    public enum FeedOp : byte
    {
        Add = 0,
        Move = 1,
        Change = 2,
        Remove = 3
    }

    public struct FeedMessage
    {
        public int sequence;
        public FeedOp op;
        public int id;
        public string name;
        public PoiType type;
        public PoiPriority priority;
        public IconKey icon;
        public float x;
        public float z;
        public PoiChangeKind kind;
    }

    /// <summary>
    /// In-process stand-in for a socket. Messages keep sequence numbers.
    /// Disconnect holds them. Reconnect replays everything after the last applied sequence.
    /// </summary>
    public sealed class MockPoiSocket
    {
        readonly List<FeedMessage> log = new List<FeedMessage>();
        readonly Queue<int> outgoing = new Queue<int>();
        int sequence;

        public bool Connected { get; private set; } = true;
        public int LoggedCount => log.Count;

        public FeedMessage MakeAdd(int id, string name, PoiType type, PoiPriority priority, IconKey icon, float x, float z)
        {
            return Push(new FeedMessage
            {
                op = FeedOp.Add,
                id = id,
                name = name,
                type = type,
                priority = priority,
                icon = icon,
                x = x,
                z = z
            });
        }

        public FeedMessage MakeMove(int id, float x, float z)
        {
            return Push(new FeedMessage
            {
                op = FeedOp.Move,
                id = id,
                x = x,
                z = z
            });
        }

        public FeedMessage MakeRemove(int id)
        {
            return Push(new FeedMessage
            {
                op = FeedOp.Remove,
                id = id
            });
        }

        public bool TryReceive(out FeedMessage message)
        {
            message = default;
            if (!Connected || outgoing.Count == 0)
            {
                return false;
            }

            message = log[outgoing.Dequeue()];
            return true;
        }

        public void Disconnect()
        {
            Connected = false;
            outgoing.Clear();
        }

        public void Connect()
        {
            Connected = true;
        }

        public void ReplayAfter(int lastApplied)
        {
            if (!Connected)
            {
                return;
            }

            outgoing.Clear();
            for (int i = 0; i < log.Count; i++)
            {
                if (log[i].sequence > lastApplied)
                {
                    outgoing.Enqueue(i);
                }
            }
        }

        FeedMessage Push(FeedMessage message)
        {
            sequence++;
            message.sequence = sequence;
            log.Add(message);
            if (Connected)
            {
                outgoing.Enqueue(log.Count - 1);
            }

            return message;
        }
    }

    /// <summary>
    /// Applies socket messages in sequence order. A later sequence waits. A repeated sequence is ignored.
    /// </summary>
    public sealed class PoiFeedReceiver
    {
        readonly PoiService service;
        readonly List<FeedMessage> pending = new List<FeedMessage>();

        public int Expected { get; private set; } = 1;
        public int LastApplied { get; private set; }

        public PoiFeedReceiver(PoiService service)
        {
            this.service = service;
        }

        public bool Accept(FeedMessage message)
        {
            if (message.sequence < Expected)
            {
                return false;
            }

            if (message.sequence > Expected)
            {
                for (int i = 0; i < pending.Count; i++)
                {
                    if (pending[i].sequence == message.sequence)
                    {
                        return false;
                    }
                }

                pending.Add(message);
                for (int i = pending.Count - 1; i > 0; i--)
                {
                    if (pending[i].sequence >= pending[i - 1].sequence)
                    {
                        break;
                    }

                    FeedMessage swap = pending[i - 1];
                    pending[i - 1] = pending[i];
                    pending[i] = swap;
                }

                return false;
            }

            Apply(message);
            Drain();
            return true;
        }

        void Drain()
        {
            while (pending.Count > 0 && pending[0].sequence == Expected)
            {
                FeedMessage next = pending[0];
                pending.RemoveAt(0);
                Apply(next);
            }
        }

        void Apply(FeedMessage message)
        {
            switch (message.op)
            {
                case FeedOp.Add:
                    service.AddWithId(message.id, message.name, message.type, message.priority, message.icon, message.x, message.z);
                    break;
                case FeedOp.Move:
                    service.Move(message.id, message.x, message.z);
                    break;
                case FeedOp.Change:
                    service.Change(message.id, message.kind, message.name, message.type, message.priority, message.icon);
                    break;
                case FeedOp.Remove:
                    service.Remove(message.id);
                    break;
            }

            LastApplied = message.sequence;
            Expected = message.sequence + 1;
        }
    }
}
