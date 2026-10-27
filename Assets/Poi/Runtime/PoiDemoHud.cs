using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Poi
{
    /// <summary>
    /// Screen HUD. Stats and the details panel both write into reused character buffers.
    /// </summary>
    public sealed class PoiDemoHud
    {
        GameObject detailsPanel;
        TMP_Text detailsText;
        TMP_Text stats;
        Slider heightSlider;
        float hudTimer;
        readonly char[] statsBuffer = new char[256];
        readonly char[] detailsBuffer = new char[192];
        readonly char[] detailsShown = new char[192];
        int detailsLength;

        public void Build(
            PoiSystemConfig config,
            PoiPresentationCatalog presentation,
            PoiTypeCatalog types,
            PoiFeedSimulator simulator,
            UnityAction addOne,
            UnityAction moveOne,
            UnityAction removeOne,
            UnityAction disconnect,
            UnityAction reconnect,
            UnityAction<bool> mockEnabled,
            UnityAction clearSelection)
        {
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem");
                events.AddComponent<EventSystem>();
                events.AddComponent<StandaloneInputModule>();
            }

            var canvasGo = new GameObject("PoiHud");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var panel = CreateRect("Panel", canvasGo.transform);
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 0.5f);
            panel.sizeDelta = new Vector2(420f, 0f);
            panel.offsetMin = new Vector2(12f, 12f);
            panel.offsetMax = new Vector2(432f, -12f);
            var image = panel.gameObject.AddComponent<Image>();
            image.color = new Color(0.05f, 0.09f, 0.14f, 0.9f);
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 4f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;

            var title = CreateLabel(panel, "Map");
            title.fontSize = 22;
            title.fontStyle = FontStyle.Bold;
            title.GetComponent<LayoutElement>().minHeight = 32f;
            title.GetComponent<LayoutElement>().preferredHeight = 32f;
            stats = CreateStats(panel);
            var controls = CreateScroll(panel);
            BuildLegend(controls, presentation);
            CreateSlider(controls, "POI count", 1000f, 250000f, config.initialCount, value =>
            {
                config.initialCount = Mathf.RoundToInt(value);
                simulator.SetTargetCount(config.initialCount);
            });
            heightSlider = CreateSlider(controls, "Camera height", config.minCameraHeight, config.maxCameraHeight, config.cameraHeight, value => config.cameraHeight = value);
            CreateSlider(controls, "Adds / sec", 0f, 200f, config.additionsPerSecond, value => config.additionsPerSecond = value);
            CreateSlider(controls, "Removes / sec", 0f, 200f, config.removalsPerSecond, value => config.removalsPerSecond = value);
            CreateSlider(controls, "Moving percent", 0f, 100f, config.movingPercent, value => config.movingPercent = value);
            CreateSlider(controls, "Move speed", 0f, 200f, config.moveSpeed, value => config.moveSpeed = value);
            CreateSlider(controls, "Min priority", 0f, 3f, (float)config.minimumPriority, value => config.minimumPriority = (PoiPriority)Mathf.RoundToInt(value));
            for (int i = 0; i < PoiEnums.TypeCount; i++)
            {
                int captured = i;
                var type = (PoiType)i;
                string label = types != null && types.rows != null && i < types.rows.Length && !string.IsNullOrEmpty(types.rows[i].displayName)
                    ? types.rows[i].displayName
                    : type.ToString();
                CreateToggle(controls, label, (config.typeMask & PoiEnums.TypeBit(type)) != 0, on =>
                {
                    int bit = PoiEnums.TypeBit((PoiType)captured);
                    if (on)
                    {
                        config.typeMask |= bit;
                    }
                    else
                    {
                        config.typeMask &= ~bit;
                    }
                });
            }

            CreateButton(controls, "Add one", addOne);
            CreateButton(controls, "Move one", moveOne);
            CreateButton(controls, "Remove one", removeOne);
            CreateToggle(controls, "Edge indicators", config.showEdgeIndicators, on => config.showEdgeIndicators = on);
            CreateToggle(controls, "Mock socket", false, mockEnabled);
            CreateButton(controls, "Disconnect", disconnect);
            CreateButton(controls, "Reconnect", reconnect);
            BuildDetailsPanel(canvasGo.transform, clearSelection);
        }

        public void Tick(
            float deltaTime,
            int pois,
            int movers,
            int markers,
            int examined,
            double lookupMs,
            double simMs,
            double gridMs,
            float cameraHeight)
        {
            hudTimer += deltaTime;
            if (hudTimer < 0.25f || stats == null)
            {
                return;
            }

            hudTimer = 0f;
            int length = HudText.WriteStats(statsBuffer, pois, movers, markers, examined, lookupMs, simMs, gridMs, cameraHeight);
            stats.SetCharArray(statsBuffer, 0, length);
            SyncSlider(heightSlider, cameraHeight);
        }

        public void HideDetails()
        {
            if (detailsPanel != null && detailsPanel.activeSelf)
            {
                detailsPanel.SetActive(false);
            }
        }

        public void ShowDetails(PoiSnapshot snapshot)
        {
            if (detailsPanel == null || detailsText == null)
            {
                return;
            }

            if (!detailsPanel.activeSelf)
            {
                detailsPanel.SetActive(true);
            }

            int length = HudText.WriteDetails(detailsBuffer, snapshot);
            if (SameDetails(length))
            {
                return;
            }

            detailsLength = length;
            for (int i = 0; i < length; i++)
            {
                detailsShown[i] = detailsBuffer[i];
            }

            detailsText.SetCharArray(detailsBuffer, 0, length);
        }

        void BuildDetailsPanel(Transform canvas, UnityAction clearSelection)
        {
            var panel = CreateRect("Details", canvas);
            panel.anchorMin = new Vector2(1f, 1f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(1f, 1f);
            panel.anchoredPosition = new Vector2(-12f, -12f);
            panel.sizeDelta = new Vector2(340f, 200f);
            panel.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.09f, 0.14f, 0.9f);
            detailsText = CreateDetailsText(panel);
            var close = CreateRect("Close", panel);
            close.anchorMin = new Vector2(0f, 0f);
            close.anchorMax = new Vector2(1f, 0f);
            close.pivot = new Vector2(0.5f, 0f);
            close.sizeDelta = new Vector2(0f, 24f);
            close.anchoredPosition = new Vector2(0f, 4f);
            close.gameObject.AddComponent<Image>().color = new Color(0.2f, 0.25f, 0.3f, 1f);
            var button = close.gameObject.AddComponent<Button>();
            button.onClick.AddListener(clearSelection);
            var label = CreateLabel(close, "Close");
            label.alignment = TextAnchor.MiddleCenter;
            detailsPanel = panel.gameObject;
            detailsPanel.SetActive(false);
        }

        bool SameDetails(int length)
        {
            if (length != detailsLength)
            {
                return false;
            }

            for (int i = 0; i < length; i++)
            {
                if (detailsBuffer[i] != detailsShown[i])
                {
                    return false;
                }
            }

            return true;
        }

        static TMP_Text CreateDetailsText(RectTransform parent)
        {
            var rect = CreateRect("DetailsText", parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            if (text.font == null)
            {
                text.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            }

            text.fontSize = 18f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.text = string.Empty;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(12f, 36f);
            rect.offsetMax = new Vector2(-12f, -12f);
            return text;
        }

        static void SyncSlider(Slider slider, float value)
        {
            if (slider == null || slider.IsActive() == false || Mathf.Approximately(slider.value, value))
            {
                return;
            }

            slider.SetValueWithoutNotify(value);
        }

        static void BuildLegend(RectTransform panel, PoiPresentationCatalog presentation)
        {
            string[] names = { "Landmark", "Resource", "Hazard", "Settlement", "Objective" };
            for (int i = 0; i < names.Length; i++)
            {
                var row = CreateRect(names[i], panel);
                var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                rowLayout.spacing = 8f;
                rowLayout.padding = new RectOffset(2, 2, 2, 2);
                rowLayout.childAlignment = TextAnchor.MiddleLeft;
                rowLayout.childControlWidth = false;
                rowLayout.childControlHeight = true;
                rowLayout.childForceExpandWidth = false;
                rowLayout.childForceExpandHeight = false;
                var rowElement = row.gameObject.AddComponent<LayoutElement>();
                rowElement.minHeight = 28f;
                rowElement.preferredHeight = 28f;

                var icon = CreateRect("Icon", row);
                var iconElement = icon.gameObject.AddComponent<LayoutElement>();
                iconElement.minWidth = 22f;
                iconElement.preferredWidth = 22f;
                iconElement.minHeight = 22f;
                iconElement.preferredHeight = 22f;
                var image = icon.gameObject.AddComponent<Image>();
                image.preserveAspect = true;
                if (presentation != null && presentation.placeholderSprites != null && i < presentation.placeholderSprites.Length)
                {
                    image.sprite = presentation.placeholderSprites[i];
                }

                var label = CreateLabel(row, names[i]);
                label.alignment = TextAnchor.MiddleLeft;
                var labelElement = label.GetComponent<LayoutElement>();
                labelElement.minWidth = 160f;
                labelElement.preferredWidth = 180f;
                labelElement.minHeight = 24f;
                labelElement.preferredHeight = 24f;
            }
        }

        static RectTransform CreateScroll(RectTransform panel)
        {
            var scrollRect = CreateRect("Scroll", panel);
            var scrollElement = scrollRect.gameObject.AddComponent<LayoutElement>();
            scrollElement.flexibleHeight = 1f;
            scrollElement.minHeight = 240f;
            var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            var viewport = CreateRect("Viewport", scrollRect);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, 0f);
            content.offsetMax = new Vector2(0f, 0f);
            var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 6f;
            contentLayout.padding = new RectOffset(4, 8, 4, 8);
            contentLayout.childControlHeight = true;
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childForceExpandWidth = true;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        static TMP_Text CreateStats(RectTransform parent)
        {
            var rect = CreateRect("Stats", parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            if (text.font == null)
            {
                text.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            }

            text.fontSize = 20f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.text = "Stats";
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 196f;
            layout.preferredHeight = 196f;
            return text;
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
            text.fontSize = 16;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = value;
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 24f;
            layout.preferredHeight = 24f;
            return text;
        }

        static Slider CreateSlider(RectTransform parent, string caption, float min, float max, float value, UnityAction<float> changed)
        {
            var captionLabel = CreateLabel(parent, caption);
            var element = captionLabel.GetComponent<LayoutElement>();
            element.minHeight = 26f;
            element.preferredHeight = 26f;
            var rect = CreateRect(caption + " Slider", parent);
            rect.gameObject.AddComponent<LayoutElement>().minHeight = 28f;
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

        static Toggle CreateToggle(RectTransform parent, string caption, bool value, UnityAction<bool> changed)
        {
            var rect = CreateRect(caption, parent);
            rect.gameObject.AddComponent<LayoutElement>().minHeight = 32f;
            var toggle = rect.gameObject.AddComponent<Toggle>();
            var box = CreateRect("Box", rect);
            box.anchorMin = new Vector2(0f, 0.5f);
            box.anchorMax = new Vector2(0f, 0.5f);
            box.sizeDelta = new Vector2(18f, 18f);
            box.anchoredPosition = new Vector2(12f, 0f);
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
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(32f, 0f);
            label.rectTransform.offsetMax = Vector2.zero;
            label.GetComponent<LayoutElement>().ignoreLayout = true;
            return toggle;
        }

        static void CreateButton(RectTransform parent, string caption, UnityAction clicked)
        {
            var rect = CreateRect(caption, parent);
            rect.gameObject.AddComponent<LayoutElement>().minHeight = 32f;
            rect.gameObject.AddComponent<Image>().color = new Color(0.16f, 0.22f, 0.28f, 1f);
            var button = rect.gameObject.AddComponent<Button>();
            button.onClick.AddListener(clicked);
            var label = CreateLabel(rect, caption);
            label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.GetComponent<LayoutElement>().ignoreLayout = true;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
