using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Poi
{
    public sealed partial class PoiDemoController : MonoBehaviour
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
        public bool MarkersReady { get; private set; }

        Transform player;
        Camera cam;
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
        GameObject detailsPanel;
        Text detailsText;
        MarkerView markerPrefab;
        readonly List<PoiHit> hits = new List<PoiHit>(256);
        readonly List<PoiHit> edgeHits = new List<PoiHit>(16);
        readonly Stopwatch lookupClock = new Stopwatch();
        float hudTimer;
        Text stats;
        Slider countSlider;
        Slider heightSlider;
        Slider addSlider;
        Slider removeSlider;
        Slider movePercentSlider;
        Slider moveSpeedSlider;
        Slider prioritySlider;
        Toggle[] typeToggles;
        bool built;
        int lastBudget = -1;
        long frameAllocStart;

        void Awake()
        {
            EnsureConfig();
            float world = config.worldSize;
            float cell = config.cellSize;
            Service = new PoiService(world, cell);
            Simulator = new PoiFeedSimulator(Service, 20260327);
            edgeQuery = new EdgeIndicatorQuery(Service.Store, Service.Index);
            mockSocket = new MockPoiSocket();
            mockReceiver = new PoiFeedReceiver(Service);
            if (seed != null && seed.entries != null)
            {
                Service.LoadSeed(seed.entries);
            }

            Simulator.SetTargetCount(config.initialCount);
            BuildWorld();
            BuildHud();
            BeginMarkerLoad();
            BeginCaptureIfRequested();
            built = true;
        }

        void Update()
        {
            if (!built)
            {
                return;
            }

            frameAllocStart = System.GC.GetAllocatedBytesForCurrentThread();
            long poiStart = frameAllocStart;
            MovePlayer();
            PositionCamera();
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
            UpdateHud();
            HandleMarkerClick();
            RefreshDetails();
            PumpMockFeed();
            LastHudAllocatedBytes = System.GC.GetAllocatedBytesForCurrentThread() - hudStart;
            LastTotalFrameAllocatedBytes = System.GC.GetAllocatedBytesForCurrentThread() - frameAllocStart;
            CaptureAfterFrame();
        }

        void SelectAndPresent()
        {
            if (presenter == null || cam == null)
            {
                LastLookupMs = 0d;
                return;
            }

            GroundFootprint.FromCamera(cam, out float minX, out float minZ, out float maxX, out float maxZ);
            var query = new PoiQuery
            {
                minX = minX,
                minZ = minZ,
                maxX = maxX,
                maxZ = maxZ,
                playerX = player.position.x,
                playerZ = player.position.z,
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
            MarkerDeclutter.Apply(hits, config.declutterRadius);
            edgeQuery.Select(
                player.position.x,
                player.position.z,
                config.edgeRadius,
                minX,
                minZ,
                maxX,
                maxZ,
                config.edgeMinimumPriority,
                config.edgeBudget,
                config.typeMask,
                edgeHits);
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

            Vector3 p = player != null ? player.position : Vector3.zero;
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

        void BuildWorld()
        {
            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            float scale = config.worldSize / 10f;
            ground.transform.localScale = new Vector3(scale, 1f, scale);
            var renderer = ground.GetComponent<Renderer>();
            if (presentation != null && presentation.groundMaterial != null)
            {
                renderer.sharedMaterial = presentation.groundMaterial;
            }
            else
            {
                renderer.material.color = presentation != null ? presentation.groundColor : new Color(0.16f, 0.22f, 0.16f);
            }

            var playerGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerGo.name = "Player";
            playerGo.transform.position = new Vector3(0f, 1f, 0f);
            playerGo.transform.localScale = new Vector3(4f, 4f, 4f);
            playerGo.GetComponent<Renderer>().material.color = presentation != null ? presentation.playerColor : Color.yellow;
            Object.Destroy(playerGo.GetComponent<Collider>());
            player = playerGo.transform;

            var camGo = new GameObject("TopDownCamera");
            cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 2000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.1f);
            PositionCamera();
        }

        void MovePlayer()
        {
            float x = Input.GetAxisRaw("Horizontal");
            float z = Input.GetAxisRaw("Vertical");
            if (captureActive)
            {
                x = 1f;
                z = 0.35f;
            }
            else if (x == 0f && z == 0f)
            {
                return;
            }

            Vector3 next = player.position + new Vector3(x, 0f, z).normalized * (config.playerSpeed * Time.deltaTime);
            float half = config.worldSize * 0.5f;
            next.x = Mathf.Clamp(next.x, -half, half);
            next.z = Mathf.Clamp(next.z, -half, half);
            next.y = 1f;
            player.position = next;
        }

        void PositionCamera()
        {
            config.cameraHeight = Mathf.Clamp(config.cameraHeight, config.minCameraHeight, config.maxCameraHeight);
            cam.fieldOfView = config.fieldOfView;
            cam.transform.SetPositionAndRotation(
                new Vector3(player.position.x, config.cameraHeight, player.position.z),
                Quaternion.Euler(90f, 0f, 0f));
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

        void BuildHud()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem");
                events.AddComponent<EventSystem>();
                events.AddComponent<StandaloneInputModule>();
            }

            var canvasGo = new GameObject("PoiHud");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();
            var panel = CreateRect("Panel", canvasGo.transform);
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 0.5f);
            panel.sizeDelta = new Vector2(320f, 0f);
            panel.offsetMin = new Vector2(8f, 8f);
            panel.offsetMax = new Vector2(328f, -8f);
            var image = panel.gameObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.55f);
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 4f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            stats = CreateLabel(panel, "Stats");
            countSlider = CreateSlider(panel, "POI count", 1000f, 250000f, config.initialCount, value =>
            {
                config.initialCount = Mathf.RoundToInt(value);
                Simulator.SetTargetCount(config.initialCount);
            });
            heightSlider = CreateSlider(panel, "Camera height", config.minCameraHeight, config.maxCameraHeight, config.cameraHeight, value => config.cameraHeight = value);
            addSlider = CreateSlider(panel, "Adds / sec", 0f, 200f, config.additionsPerSecond, value => config.additionsPerSecond = value);
            removeSlider = CreateSlider(panel, "Removes / sec", 0f, 200f, config.removalsPerSecond, value => config.removalsPerSecond = value);
            movePercentSlider = CreateSlider(panel, "Moving percent", 0f, 100f, config.movingPercent, value => config.movingPercent = value);
            moveSpeedSlider = CreateSlider(panel, "Move speed", 0f, 200f, config.moveSpeed, value => config.moveSpeed = value);
            prioritySlider = CreateSlider(panel, "Min priority", 0f, 3f, (float)config.minimumPriority, value => config.minimumPriority = (PoiPriority)Mathf.RoundToInt(value));
            typeToggles = new Toggle[PoiEnums.TypeCount];
            for (int i = 0; i < PoiEnums.TypeCount; i++)
            {
                int captured = i;
                var type = (PoiType)i;
                string label = types != null && types.rows != null && i < types.rows.Length && !string.IsNullOrEmpty(types.rows[i].displayName)
                    ? types.rows[i].displayName
                    : type.ToString();
                typeToggles[i] = CreateToggle(panel, label, (config.typeMask & PoiEnums.TypeBit(type)) != 0, on =>
                {
                    int bit = PoiEnums.TypeBit((PoiType)captured);
                    if (on) config.typeMask |= bit;
                    else config.typeMask &= ~bit;
                });
            }

            CreateButton(panel, "Add one", AddOne);
            CreateButton(panel, "Move one", MoveOne);
            CreateButton(panel, "Remove one", RemoveOne);
            CreateToggle(panel, "Mock socket", false, on => mockFeedEnabled = on);
            CreateButton(panel, "Disconnect", () => mockSocket.Disconnect());
            CreateButton(panel, "Reconnect", ReconnectMock);
            BuildDetailsPanel(canvasGo.transform);
        }

        void BuildDetailsPanel(Transform canvas)
        {
            var panel = CreateRect("Details", canvas);
            panel.anchorMin = new Vector2(1f, 1f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(1f, 1f);
            panel.anchoredPosition = new Vector2(-12f, -12f);
            panel.sizeDelta = new Vector2(280f, 160f);
            panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);
            detailsText = CreateLabel(panel, "Details");
            detailsText.rectTransform.offsetMin = new Vector2(8f, 28f);
            detailsText.rectTransform.offsetMax = new Vector2(-8f, -8f);
            var close = CreateRect("Close", panel);
            close.anchorMin = new Vector2(0f, 0f);
            close.anchorMax = new Vector2(1f, 0f);
            close.pivot = new Vector2(0.5f, 0f);
            close.sizeDelta = new Vector2(0f, 24f);
            close.anchoredPosition = new Vector2(0f, 4f);
            close.gameObject.AddComponent<Image>().color = new Color(0.2f, 0.25f, 0.3f, 1f);
            var button = close.gameObject.AddComponent<Button>();
            button.onClick.AddListener(ClearSelection);
            var label = CreateLabel(close, "Close");
            label.alignment = TextAnchor.MiddleCenter;
            detailsPanel = panel.gameObject;
            detailsPanel.SetActive(false);
        }

        void HandleMarkerClick()
        {
            if (cam == null || !Input.GetMouseButtonDown(0))
            {
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
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
            if (detailsPanel == null)
            {
                return;
            }

            if (selectedId < 0 || Service == null || !Service.Store.TryGet(selectedId, out PoiSnapshot snapshot) || !snapshot.alive)
            {
                selectedId = -1;
                detailsPanel.SetActive(false);
                return;
            }

            detailsPanel.SetActive(true);
            detailsText.text = "Id " + snapshot.id
                + "\n" + snapshot.name
                + "\n" + snapshot.type
                + "  " + snapshot.priority
                + "\n" + snapshot.x.ToString("0") + ", " + snapshot.z.ToString("0");
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
            if (!mockFeedEnabled || captureActive || mockSocket == null)
            {
                return;
            }

            if (Time.unscaledTime >= nextMockEmit)
            {
                nextMockEmit = Time.unscaledTime + 0.5f;
                Vector3 p = player != null ? player.position : Vector3.zero;
                int id = nextMockId++;
                mockSocket.MakeAdd(id, "Socket " + id, PoiType.Landmark, PoiPriority.High, IconKey.Landmark, p.x + 180f, p.z - 60f);
            }

            while (mockSocket.TryReceive(out FeedMessage message))
            {
                mockReceiver.Accept(message);
            }
        }

        void UpdateHud()
        {
            hudTimer += Time.deltaTime;
            if (hudTimer < 0.25f || stats == null)
            {
                return;
            }

            hudTimer = 0f;
            var builder = new StringBuilder(256);
            builder.Append("POIs ").Append(Service.Store.AliveCount);
            builder.Append("\nMovers ").Append(Simulator.MoverCount);
            builder.Append("\nMarkers ").Append(ActiveMarkers);
            builder.Append("\nExamined ").Append(LastExamined);
            builder.Append("\nLookup ms ").Append(LastLookupMs.ToString("0.000"));
            builder.Append("\nSim ms ").Append(Simulator.LastIntegrateMs.ToString("0.000"));
            builder.Append("\nGrid ms ").Append(Simulator.LastRelinkMs.ToString("0.000"));
            builder.Append("\nHeight ").Append(config.cameraHeight.ToString("0"));
            stats.text = builder.ToString();
            SyncSlider(heightSlider, config.cameraHeight);
        }

        static void SyncSlider(Slider slider, float value)
        {
            if (slider == null || slider.IsActive() == false)
            {
                return;
            }

            slider.SetValueWithoutNotify(value);
        }

        static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        static Text CreateLabel(RectTransform parent, string value)
        {
            var rect = CreateRect(value, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = value;
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 120f;
            return text;
        }

        static Slider CreateSlider(RectTransform parent, string caption, float min, float max, float value, UnityEngine.Events.UnityAction<float> changed)
        {
            CreateLabel(parent, caption).GetComponent<LayoutElement>().minHeight = 18f;
            var rect = CreateRect(caption + " Slider", parent);
            rect.gameObject.AddComponent<LayoutElement>().minHeight = 20f;
            var slider = rect.gameObject.AddComponent<Slider>();
            var bg = CreateRect("Background", rect);
            Stretch(bg);
            bg.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
            var fillArea = CreateRect("Fill Area", rect);
            Stretch(fillArea);
            var fill = CreateRect("Fill", fillArea);
            Stretch(fill);
            fill.gameObject.AddComponent<Image>().color = new Color(0.3f, 0.7f, 0.4f, 0.9f);
            var handle = CreateRect("Handle", rect);
            handle.sizeDelta = new Vector2(12f, 20f);
            handle.gameObject.AddComponent<Image>().color = Color.white;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            slider.onValueChanged.AddListener(changed);
            return slider;
        }

        static Toggle CreateToggle(RectTransform parent, string caption, bool value, UnityEngine.Events.UnityAction<bool> changed)
        {
            var rect = CreateRect(caption, parent);
            rect.gameObject.AddComponent<LayoutElement>().minHeight = 20f;
            var toggle = rect.gameObject.AddComponent<Toggle>();
            var box = CreateRect("Box", rect);
            box.anchorMin = new Vector2(0f, 0.5f);
            box.anchorMax = new Vector2(0f, 0.5f);
            box.sizeDelta = new Vector2(16f, 16f);
            box.anchoredPosition = new Vector2(10f, 0f);
            box.gameObject.AddComponent<Image>().color = Color.white;
            var check = CreateRect("Check", box);
            Stretch(check);
            check.offsetMin = new Vector2(3f, 3f);
            check.offsetMax = new Vector2(-3f, -3f);
            check.gameObject.AddComponent<Image>().color = new Color(0.2f, 0.8f, 0.3f);
            toggle.targetGraphic = box.GetComponent<Image>();
            toggle.graphic = check.GetComponent<Image>();
            toggle.isOn = value;
            toggle.onValueChanged.AddListener(changed);
            var label = CreateLabel(rect, caption);
            label.rectTransform.offsetMin = new Vector2(24f, 0f);
            label.GetComponent<LayoutElement>().minHeight = 18f;
            return toggle;
        }

        static void CreateButton(RectTransform parent, string caption, UnityEngine.Events.UnityAction clicked)
        {
            var rect = CreateRect(caption, parent);
            rect.gameObject.AddComponent<LayoutElement>().minHeight = 24f;
            rect.gameObject.AddComponent<Image>().color = new Color(0.2f, 0.25f, 0.3f, 1f);
            var button = rect.gameObject.AddComponent<Button>();
            button.onClick.AddListener(clicked);
            var label = CreateLabel(rect, caption);
            label.alignment = TextAnchor.MiddleCenter;
            label.GetComponent<LayoutElement>().minHeight = 24f;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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
