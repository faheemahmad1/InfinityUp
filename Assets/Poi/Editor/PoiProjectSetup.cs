using System;
using System.IO;
using System.Linq;
using Poi;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Poi.EditorTools
{
    [InitializeOnLoad]
    public static class PoiProjectSetup
    {
        const string ConfigPath = "Assets/Poi/Config/PoiSystemConfig.asset";
        const string SeedPath = "Assets/Poi/Config/PoiSeedCatalog.asset";
        const string TypePath = "Assets/Poi/Config/PoiTypeCatalog.asset";
        const string PresentationPath = "Assets/Poi/Config/PoiPresentationCatalog.asset";
        const string PrefabPath = "Assets/Poi/Art/Marker.prefab";
        const string ScenePath = "Assets/Scenes/PoiDemo.unity";

        static PoiProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                string[] args = Environment.GetCommandLineArgs();
                if (args.Any(arg => arg == "-runTests"))
                {
                    return;
                }

                Ensure();
            };
        }

        [MenuItem("POI/Setup Project")]
        public static void Ensure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            Directory.CreateDirectory("Assets/Poi/Config");
            Directory.CreateDirectory("Assets/Poi/Art/Placeholder");
            Directory.CreateDirectory("Assets/Poi/Data");
            Sprite[] sprites = BuildSprites();
            Material ground = BuildGroundMaterial();
            GameObject prefab = BuildMarkerPrefab();
            var config = LoadOrCreate<PoiSystemConfig>(ConfigPath);
            var seed = LoadOrCreate<PoiSeedCatalog>(SeedPath);
            if (seed.entries == null || seed.entries.Length == 0)
            {
                seed.entries = DefaultSeed();
                EditorUtility.SetDirty(seed);
            }

            var types = LoadOrCreate<PoiTypeCatalog>(TypePath);
            types.rows = DefaultTypes();
            EditorUtility.SetDirty(types);
            var presentation = LoadOrCreate<PoiPresentationCatalog>(PresentationPath);
            presentation.iconAddresses = PoiDemoController.DefaultIconAddresses();
            presentation.markerAddress = "Poi/Marker";
            presentation.placeholderSprites = sprites;
            presentation.groundMaterial = ground;
            EditorUtility.SetDirty(presentation);
            MarkAddressable(AssetDatabase.AssetPathToGUID(PrefabPath), presentation.markerAddress);
            for (int i = 0; i < sprites.Length; i++)
            {
                string path = AssetDatabase.GetAssetPath(sprites[i]);
                MarkAddressable(AssetDatabase.AssetPathToGUID(path), presentation.iconAddresses[i]);
            }

            EnsureScene(config, seed, presentation, types);
            AssetDatabase.SaveAssets();
        }

        static Sprite[] BuildSprites()
        {
            var colors = new[]
            {
                new Color(0.95f, 0.75f, 0.2f),
                new Color(0.3f, 0.75f, 0.35f),
                new Color(0.85f, 0.25f, 0.2f),
                new Color(0.35f, 0.55f, 0.9f),
                new Color(0.85f, 0.45f, 0.9f),
                new Color(0.75f, 0.75f, 0.75f)
            };
            string[] names = { "Landmark", "Resource", "Hazard", "Settlement", "Objective", "Fallback" };
            var sprites = new Sprite[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                string path = "Assets/Poi/Art/Placeholder/" + names[i] + ".png";
                if (!File.Exists(path))
                {
                    var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                    var pixels = new Color[32 * 32];
                    for (int p = 0; p < pixels.Length; p++)
                    {
                        pixels[p] = colors[i];
                    }

                    texture.SetPixels(pixels);
                    texture.Apply();
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(texture);
                }

                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 32f;
                importer.SaveAndReimport();
                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            return sprites;
        }

        static Material BuildGroundMaterial()
        {
            const string path = "Assets/Poi/Art/Placeholder/Ground.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            material = new Material(shader) { color = new Color(0.16f, 0.22f, 0.16f) };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static GameObject BuildMarkerPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                return existing;
            }

            var root = new GameObject("Marker");
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
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        static void MarkAddressable(string guid, string address)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return;
            }

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                settings = AddressableAssetSettings.Create(
                    AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
                    AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName,
                    true,
                    true);
            }

            AddressableAssetGroup group = settings.FindGroup("POI");
            if (group == null)
            {
                group = settings.CreateGroup("POI", false, false, false, null, typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema), typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.ContentUpdateGroupSchema));
            }

            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, false);
            entry.address = address;
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
        }

        static void EnsureScene(PoiSystemConfig config, PoiSeedCatalog seed, PoiPresentationCatalog presentation, PoiTypeCatalog types)
        {
            if (File.Exists(ScenePath))
            {
                return;
            }

            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects, UnityEditor.SceneManagement.NewSceneMode.Single);
            var go = new GameObject("PoiDemo");
            var demo = go.AddComponent<PoiDemoController>();
            demo.config = config;
            demo.seed = seed;
            demo.presentation = presentation;
            demo.types = types;
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static PoiSeedEntry[] DefaultSeed()
        {
            return new[]
            {
                Entry(1, "North Gate", PoiType.Landmark, 40f, 20f, IconKey.Landmark, PoiPriority.High),
                Entry(2, "Supply Cache", PoiType.Resource, -30f, 50f, IconKey.Resource, PoiPriority.Normal),
                Entry(3, "Fault Line", PoiType.Hazard, 80f, -40f, IconKey.Hazard, PoiPriority.Critical),
                Entry(4, "Old Camp", PoiType.Settlement, -70f, -20f, IconKey.Settlement, PoiPriority.Low),
                Entry(5, "Relay", PoiType.Objective, 15f, -90f, IconKey.Objective, PoiPriority.High),
                Entry(6, "Watch Hill", PoiType.Landmark, 120f, 110f, IconKey.Landmark, PoiPriority.Normal),
                Entry(7, "Ore Pocket", PoiType.Resource, -140f, 30f, IconKey.Resource, PoiPriority.High),
                Entry(8, "Sinkhole", PoiType.Hazard, 200f, 10f, IconKey.Hazard, PoiPriority.Critical)
            };
        }

        static PoiSeedEntry Entry(int id, string name, PoiType type, float x, float z, IconKey icon, PoiPriority priority)
        {
            return new PoiSeedEntry { id = id, name = name, type = type, x = x, z = z, icon = icon, priority = priority };
        }

        static PoiTypeRow[] DefaultTypes()
        {
            return new[]
            {
                new PoiTypeRow { type = PoiType.Landmark, displayName = "Landmark", icon = IconKey.Landmark },
                new PoiTypeRow { type = PoiType.Resource, displayName = "Resource", icon = IconKey.Resource },
                new PoiTypeRow { type = PoiType.Hazard, displayName = "Hazard", icon = IconKey.Hazard },
                new PoiTypeRow { type = PoiType.Settlement, displayName = "Settlement", icon = IconKey.Settlement },
                new PoiTypeRow { type = PoiType.Objective, displayName = "Objective", icon = IconKey.Objective }
            };
        }
    }
}
