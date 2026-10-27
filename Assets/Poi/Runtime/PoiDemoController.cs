using System.Collections.Generic;
using System.Diagnostics;
using TMPro;
using UnityEngine;

namespace Poi
{
    public sealed class PoiDemoController : MonoBehaviour
    {
        public PoiSystemConfig config;
        public PoiSeedCatalog seed;
        public PoiPresentationCatalog presentation;
        public PoiTypeCatalog types;

        public PoiService Service { get; private set; }
        public PoiFeedSimulator Simulator { get; private set; }
        public int ActiveMarkers { get; private set; }
        public int LastExamined { get; private set; }
        public double LastLookupMs { get; private set; }
        public double LastPoiAllocatedBytes { get; private set; }
        public double LastTotalFrameAllocatedBytes { get; private set; }
        public double LastHudAllocatedBytes { get; private set; }
        public long LastFrameAllocatedBytes { get; private set; }
        public bool MarkersReady { get; private set; }

        PoiDemoWorld world;
        PoiDemoHud hud;
        MarkerPresenter presenter;
        MarkerPresenter edgePresenter;
        MarkerPool markerPool;
        EdgeIndicatorQuery edgeQuery;
        MockPoiSocket mockSocket;
        PoiFeedReceiver mockReceiver;
        bool mockFeedEnabled;
        float nextMockEmit;
        int nextMockId = 1000000;
        int selectedId = -1;
        MarkerView markerPrefab;
        readonly List<PoiHit> hits = new List<PoiHit>(256);
        readonly List<PoiHit> edgeHits = new List<PoiHit>(16);
        readonly Stopwatch lookupClock = new Stopwatch();
        bool built;
        int lastBudget = -1;
        PoiPlayCapture playCapture;
        long frameAllocStart;

        void Awake()
        {
            EnsureConfig();
            var composition = PoiComposition.Create(config.worldSize, config.cellSize, 20260327, seed);
            Service = composition.Service;
            Simulator = composition.Simulator;
            edgeQuery = composition.Edges;
            mockSocket = new MockPoiSocket();
            mockReceiver = new PoiFeedReceiver(Service);

            Simulator.SetTargetCount(config.initialCount);
            world = new PoiDemoWorld();
            world.Build(config, presentation);
            hud = new PoiDemoHud();
            hud.Build(
                config,
                presentation,
                types,
                Simulator,
                AddOne,
                MoveOne,
                RemoveOne,
                () => mockSocket.Disconnect(),
                ReconnectMock,
                on => mockFeedEnabled = on,
                ClearSelection);
            BeginMarkerLoad();
            playCapture = new PoiPlayCapture(this);
            playCapture.BeginIfRequested();
            built = true;
            StartCoroutine(WatchFrameAllocations());
        }

        System.Collections.IEnumerator WatchFrameAllocations()
        {
            var wait = new WaitForEndOfFrame();
            while (true)
            {
                yield return wait;
                LastFrameAllocatedBytes = System.GC.GetAllocatedBytesForCurrentThread() - frameAllocStart;
            }
        }

        void Update()
        {
            if (!built)
            {
                return;
            }

            frameAllocStart = System.GC.GetAllocatedBytesForCurrentThread();
            long poiStart = frameAllocStart;
            bool captureDrive = playCapture != null && playCapture.Active;
            world.Move(config, Time.deltaTime, captureDrive);
            world.PositionCamera(config);
            Simulator.Tick(
                Time.deltaTime,
                5000,
                config.additionsPerSecond,
                config.removalsPerSecond,
                config.movingPercent,
                config.moveSpeed);
            SelectAndPresent();
            LastPoiAllocatedBytes = System.GC.GetAllocatedBytesForCurrentThread() - poiStart;
            long hudStart = System.GC.GetAllocatedBytesForCurrentThread();
            if (playCapture == null || !playCapture.Measuring)
            {
                hud.Tick(
                    Time.deltaTime,
                    Service.Store.AliveCount,
                    Simulator.MoverCount,
                    ActiveMarkers,
                    LastExamined,
                    LastLookupMs,
                    Simulator.LastIntegrateMs,
                    Simulator.LastRelinkMs,
                    config.cameraHeight);
            }
            HandleMarkerClick();
            RefreshDetails();
            PumpMockFeed();
            LastHudAllocatedBytes = System.GC.GetAllocatedBytesForCurrentThread() - hudStart;
            LastTotalFrameAllocatedBytes = System.GC.GetAllocatedBytesForCurrentThread() - frameAllocStart;
            playCapture.AfterFrame();
        }

        void SelectAndPresent()
        {
            if (presenter == null || world.Camera == null)
            {
                LastLookupMs = 0d;
                return;
            }

            GroundFootprint.FromCamera(world.Camera, out float minX, out float minZ, out float maxX, out float maxZ);
            var query = new PoiQuery
            {
                minX = minX,
                minZ = minZ,
                maxX = maxX,
                maxZ = maxZ,
                playerX = world.Player.position.x,
                playerZ = world.Player.position.z,
                closeRange = config.closeRange,
                displayRange = config.displayRange,
                budget = config.markerBudget,
                minPriority = config.minimumPriority,
                typeMask = config.typeMask
            };
            lookupClock.Restart();
            Service.Selector.Select(query, hits);
            lookupClock.Stop();
            LastLookupMs = lookupClock.Elapsed.TotalMilliseconds;
            LastExamined = Service.Selector.SlotsVisited;
            LodHysteresis.Apply(hits, Service.Store, config.closeRange, config.lodHysteresis);
            MarkerDeclutter.Apply(hits, config.declutterRadius);
            if (config.showEdgeIndicators)
            {
                edgeQuery.Select(
                    world.Player.position.x,
                    world.Player.position.z,
                    config.edgeRadius,
                    minX,
                    minZ,
                    maxX,
                    maxZ,
                    config.edgeMinimumPriority,
                    config.edgeBudget,
                    config.typeMask,
                    edgeHits);
            }
            else
            {
                edgeHits.Clear();
            }

            EnsurePoolSize();
            presenter.Sync(hits);
            if (edgePresenter != null)
            {
                edgePresenter.Sync(edgeHits);
            }

            ActiveMarkers = presenter.ActiveCount;
        }

        public void AddOne()
        {
            if (Service == null)
            {
                return;
            }

            Vector3 p = world != null && world.Player != null ? world.Player.position : Vector3.zero;
            Service.Add("Manual", PoiType.Objective, PoiPriority.Critical, IconKey.Objective, p.x + 20f, p.z + 20f);
            Simulator.SetTargetCount(Service.Store.AliveCount);
            config.initialCount = Service.Store.AliveCount;
        }

        public void MoveOne()
        {
            if (Service == null || Service.Store.AliveCount == 0)
            {
                return;
            }

            int slot = Service.Store.AliveSlots[0];
            int id = Service.Store.Slots[slot].id;
            Service.Move(id, Service.Store.Slots[slot].x + 30f, Service.Store.Slots[slot].z);
        }

        public void RemoveOne()
        {
            if (Service == null || Service.Store.AliveCount == 0)
            {
                return;
            }

            int slot = Service.Store.AliveSlots[Service.Store.AliveCount - 1];
            int id = Service.Store.Slots[slot].id;
            Simulator.NotifyRemoved(slot);
            Service.Remove(id);
            Simulator.SetTargetCount(Service.Store.AliveCount);
            config.initialCount = Service.Store.AliveCount;
        }

        void EnsureConfig()
        {
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<PoiSystemConfig>();
            }

            if (presentation == null)
            {
                presentation = ScriptableObject.CreateInstance<PoiPresentationCatalog>();
                presentation.iconAddresses = DefaultIconAddresses();
            }

            if (presentation.iconAddresses == null || presentation.iconAddresses.Length < PoiEnums.IconCount)
            {
                presentation.iconAddresses = DefaultIconAddresses();
            }
        }

        void BeginMarkerLoad()
        {
            string address = presentation != null ? presentation.markerAddress : "Poi/Marker";
            MarkerPrefabLoader.LoadAsync(address, prefab =>
            {
                if (prefab == null)
                {
                    prefab = BuildFallbackMarker();
                }

                markerPrefab = prefab;
                var root = new GameObject("Markers").transform;
                markerPool = new MarkerPool(root);
                markerPool.Prewarm(prefab, Mathf.Max(1, config.markerBudget));
                lastBudget = config.markerBudget;
                var icons = new AddressableIconProvider(presentation.iconAddresses);
                presenter = new MarkerPresenter(markerPool, icons);
                var edgeRoot = new GameObject("EdgeMarkers").transform;
                var edges = new MarkerPool(edgeRoot);
                edges.Prewarm(prefab, Mathf.Max(1, config.edgeBudget));
                edgePresenter = new MarkerPresenter(edges, icons);
                MarkersReady = true;
            });
        }

        void EnsurePoolSize()
        {
            if (config.markerBudget == lastBudget || markerPool == null || markerPrefab == null)
            {
                return;
            }

            if (config.markerBudget > markerPool.Count)
            {
                markerPool.Prewarm(markerPrefab, config.markerBudget);
            }

            lastBudget = config.markerBudget;
        }

        static MarkerView BuildFallbackMarker()
        {
            var root = new GameObject("MarkerFallback");
            var view = root.AddComponent<MarkerView>();
            var icon = new GameObject("Icon");
            icon.transform.SetParent(root.transform, false);
            icon.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            icon.transform.localScale = Vector3.one * 12f;
            view.IconRenderer = icon.AddComponent<SpriteRenderer>();
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(root.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0f, 10f);
            labelGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var label = labelGo.AddComponent<TextMeshPro>();
            label.fontSize = 6f;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(40f, 8f);
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null)
            {
                label.font = font;
            }

            view.Label = label;
            root.SetActive(false);
            return view;
        }

        void HandleMarkerClick()
        {
            if (world.Camera == null || !Input.GetMouseButtonDown(0))
            {
                return;
            }

            if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Ray ray = world.Camera.ScreenPointToRay(Input.mousePosition);
            if (Mathf.Abs(ray.direction.y) < 0.0001f)
            {
                return;
            }

            float t = (1.5f - ray.origin.y) / ray.direction.y;
            if (t < 0f)
            {
                return;
            }

            Vector3 point = ray.GetPoint(t);
            int id = MarkerPick.Pick(hits, point.x, point.z, config.pickRadius);
            if (id < 0)
            {
                id = MarkerPick.Pick(edgeHits, point.x, point.z, config.pickRadius);
            }

            if (id >= 0)
            {
                selectedId = id;
            }
        }

        void RefreshDetails()
        {
            if (selectedId < 0 || Service == null || !Service.Store.TryGet(selectedId, out PoiSnapshot snapshot) || !snapshot.alive)
            {
                selectedId = -1;
                hud.HideDetails();
                return;
            }

            hud.ShowDetails(snapshot);
        }

        void ClearSelection()
        {
            selectedId = -1;
        }

        void ReconnectMock()
        {
            mockFeedEnabled = true;
            mockSocket.Connect();
            mockSocket.ReplayAfter(mockReceiver.LastApplied);
        }

        void PumpMockFeed()
        {
            if (!mockFeedEnabled || playCapture.Active || mockSocket == null)
            {
                return;
            }

            if (Time.unscaledTime >= nextMockEmit)
            {
                nextMockEmit = Time.unscaledTime + config.mockInterval;
                Vector3 p = world.Player != null ? world.Player.position : Vector3.zero;
                int id = nextMockId++;
                mockSocket.MakeAdd(
                    id,
                    "Socket " + id,
                    config.mockType,
                    config.mockPriority,
                    IconKey.Landmark,
                    p.x + config.mockOffsetX,
                    p.z + config.mockOffsetZ);
            }

            while (mockSocket.TryReceive(out FeedMessage message))
            {
                mockReceiver.Accept(message);
            }
        }

        public static string[] DefaultIconAddresses()
        {
            return new[]
            {
                "Poi/Icon/Landmark",
                "Poi/Icon/Resource",
                "Poi/Icon/Hazard",
                "Poi/Icon/Settlement",
                "Poi/Icon/Objective",
                "Poi/Icon/Fallback"
            };
        }
    }
}
