using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Poi;
using UnityEditor;
using UnityEngine;

namespace Poi.EditorTools
{
    [CustomEditor(typeof(PoiSystemConfig))]
    public sealed class PoiSystemConfigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            bool playing = Application.isPlaying;
            DrawLocked("worldSize", playing);
            DrawLocked("cellSize", playing);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("closeRange"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("displayRange"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("markerBudget"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("edgeBudget"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("edgeRadius"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("edgeMinimumPriority"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("declutterRadius"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pickRadius"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("showEdgeIndicators"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lodHysteresis"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("mockInterval"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("mockOffsetX"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("mockOffsetZ"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("mockType"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("mockPriority"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("minCameraHeight"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxCameraHeight"));
            EditorGUILayout.Slider(serializedObject.FindProperty("cameraHeight"), 50f, 400f);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fieldOfView"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("initialCount"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("additionsPerSecond"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("removalsPerSecond"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("movingPercent"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("moveSpeed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("playerSpeed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("minimumPriority"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("typeMask"));
            serializedObject.ApplyModifiedProperties();
            if (playing)
            {
                EditorGUILayout.HelpBox("World size and cell size stay fixed during Play. Other fields apply on the next frame and are saved with this asset.", MessageType.Info);
            }
        }

        void DrawLocked(string propertyName, bool locked)
        {
            using (new EditorGUI.DisabledScope(locked))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyName));
            }
        }
    }

    [CustomEditor(typeof(PoiDemoController))]
    public sealed class PoiDemoControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var demo = (PoiDemoController)target;
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Add one")) demo.AddOne();
                if (GUILayout.Button("Move one")) demo.MoveOne();
                if (GUILayout.Button("Remove one")) demo.RemoveOne();
            }
        }
    }

    [CustomPropertyDrawer(typeof(PoiSeedEntry))]
    public sealed class PoiSeedEntryDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.PropertyField(position, property, label, true);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }

    [Serializable]
    sealed class SeedFile
    {
        public SeedRow[] entries;
    }

    [Serializable]
    sealed class SeedRow
    {
        public int id;
        public string name;
        public string type;
        public float x;
        public float z;
        public string iconKey;
        public string priority;
    }

    public static class PoiSeedImporter
    {
        [MenuItem("POI/Import Seed JSON")]
        public static void Import()
        {
            const string jsonPath = "Assets/Poi/Data/poi_seed.json";
            const string assetPath = "Assets/Poi/Config/PoiSeedCatalog.asset";
            if (!File.Exists(jsonPath))
            {
                UnityEngine.Debug.LogError("Missing " + jsonPath);
                return;
            }

            SeedFile file = JsonUtility.FromJson<SeedFile>(File.ReadAllText(jsonPath));
            var catalog = AssetDatabase.LoadAssetAtPath<PoiSeedCatalog>(assetPath);
            if (catalog == null)
            {
                PoiProjectSetup.Ensure();
                catalog = AssetDatabase.LoadAssetAtPath<PoiSeedCatalog>(assetPath);
            }

            var parsed = new List<PoiSeedEntry>();
            if (file != null && file.entries != null)
            {
                for (int i = 0; i < file.entries.Length; i++)
                {
                    SeedRow row = file.entries[i];
                    if (!PoiEnums.TryParseType(row.type, out PoiType type) ||
                        !PoiEnums.TryParsePriority(row.priority, out PoiPriority priority) ||
                        !PoiEnums.TryParseIcon(row.iconKey, out IconKey icon))
                    {
                        UnityEngine.Debug.LogError("Rejected seed row " + row.id + " because an enum name was unknown.");
                        continue;
                    }

                    parsed.Add(new PoiSeedEntry
                    {
                        id = row.id,
                        name = row.name,
                        type = type,
                        x = row.x,
                        z = row.z,
                        icon = icon,
                        priority = priority
                    });
                }
            }

            catalog.entries = parsed.ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }
    }

    public static class PoiEditModeBenchmark
    {
        [MenuItem("POI/Run Split Benchmark")]
        public static void Run()
        {
            Directory.CreateDirectory("Docs/Measurements");
            string path = "Docs/Measurements/editmode-split.txt";
            using (var writer = new StreamWriter(path, false))
            {
                writer.WriteLine("Mode: edit-mode microbenchmark");
                writer.WriteLine("Frame time: not measured");
                writer.WriteLine("Total frame GC: not measured");
                writer.WriteLine("HUD GC: not measured");
                writer.WriteLine("Hardware: " + SystemInfo.processorType + " / " + SystemInfo.systemMemorySize + " MB");
                writer.WriteLine("Editor: " + Application.unityVersion);
                writer.WriteLine("Moving percent: 5. The 250000 row is 12500 movers.");
                writer.WriteLine("columns: pois, movers, lookupMs, integrateMs, relinkMs, poiGcBytes, examined, markersBudget");
                Measure(writer, 1000);
                Measure(writer, 25000);
                Measure(writer, 250000);
            }

            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("Wrote " + path);
        }

        static void Measure(StreamWriter writer, int count)
        {
            var service = new PoiService(16000f, 400f, PoiService.MaxCapacity);
            var simulator = new PoiFeedSimulator(service, 99);
            simulator.SetTargetCount(count);
            while (service.Store.AliveCount < count)
            {
                simulator.Tick(0f, 20000, 0f, 0f, 0f, 0f);
            }

            simulator.Tick(0.016f, 1, 0f, 0f, 5f, 0f);
            var rotation = Quaternion.Euler(90f, 0f, 0f);
            GroundFootprint.FromPose(new Vector3(0f, 400f, 0f), rotation, 60f, 16f / 9f, out float minX, out float minZ, out float maxX, out float maxZ);
            var query = new PoiQuery
            {
                minX = minX,
                maxX = maxX,
                minZ = minZ,
                maxZ = maxZ,
                playerX = 12f,
                playerZ = -8f,
                closeRange = 150f,
                displayRange = 500f,
                budget = 250,
                minPriority = PoiPriority.Low,
                typeMask = PoiEnums.AllTypesMask
            };
            var hits = new List<PoiHit>(256);
            for (int i = 0; i < 10; i++)
            {
                simulator.Tick(0.016f, 1, 0f, 0f, 5f, 40f);
                service.Selector.Select(query, hits);
            }

            long gcBefore = GC.GetAllocatedBytesForCurrentThread();
            var clock = Stopwatch.StartNew();
            double integrate = 0d;
            double relink = 0d;
            double lookup = 0d;
            int examined = 0;
            const int samples = 30;
            for (int i = 0; i < samples; i++)
            {
                simulator.Tick(0.016f, 1, 20f, 20f, 5f, 40f);
                integrate += simulator.LastIntegrateMs;
                relink += simulator.LastRelinkMs;
                var lookupClock = Stopwatch.StartNew();
                service.Selector.Select(query, hits);
                lookupClock.Stop();
                lookup += lookupClock.Elapsed.TotalMilliseconds;
                examined += service.Selector.SlotsVisited;
                query.playerX += 3f;
                query.minX += 3f;
                query.maxX += 3f;
            }

            long gcAfter = GC.GetAllocatedBytesForCurrentThread();
            clock.Stop();
            writer.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0},{1},{2:0.000},{3:0.000},{4:0.000},{5},{6},{7}",
                count,
                simulator.MoverCount,
                lookup / samples,
                integrate / samples,
                relink / samples,
                (gcAfter - gcBefore) / samples,
                examined / samples,
                hits.Count));
        }
    }
}
