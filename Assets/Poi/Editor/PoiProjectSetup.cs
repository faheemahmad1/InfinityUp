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
            string stampPath = "Assets/Poi/Art/Placeholder/map-look.txt";
            string stamp = File.Exists(stampPath) ? File.ReadAllText(stampPath).Trim() : string.Empty;
            bool paint = stamp != "5";
            Sprite[] sprites = BuildSprites(paint);
            Sprite playerSprite = BuildPlayerSprite(paint);
            Material ground = BuildGroundMaterial(paint);
            Material local = BuildLocalMaterial(paint);
            if (paint)
            {
                File.WriteAllText(stampPath, "5");
                AssetDatabase.ImportAsset(stampPath);
            }
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
            presentation.playerSprite = playerSprite;
            presentation.groundMaterial = ground;
            presentation.localMaterial = local;
            EditorUtility.SetDirty(presentation);
            MarkAddressable(AssetDatabase.AssetPathToGUID(PrefabPath), presentation.markerAddress);
            for (int i = 0; i < sprites.Length; i++)
            {
                string path = AssetDatabase.GetAssetPath(sprites[i]);
                MarkAddressable(AssetDatabase.AssetPathToGUID(path), presentation.iconAddresses[i]);
            }

            EnsureScene(config, seed, presentation, types);
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                settings = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>("Assets/AddressableAssetsData/AddressableAssetSettings.asset");
                if (settings != null)
                {
                    AddressableAssetSettingsDefaultObject.Settings = settings;
                }
            }

            if (settings != null)
            {
                for (int i = 0; i < settings.DataBuilders.Count; i++)
                {
                    var builder = settings.GetDataBuilder(i);
                    if (builder != null && builder.GetType().Name == "BuildScriptFastMode")
                    {
                        settings.ActivePlayModeDataBuilderIndex = i;
                        break;
                    }
                }
            }

            AssetDatabase.SaveAssets();
        }

        static Sprite[] BuildSprites(bool paint)
        {
            var colors = new[]
            {
                new Color(0.62f, 0.36f, 0.95f, 1f),
                new Color(0.30f, 0.78f, 0.38f, 1f),
                new Color(0.90f, 0.28f, 0.22f, 1f),
                new Color(0.95f, 0.55f, 0.20f, 1f),
                new Color(0.25f, 0.55f, 0.95f, 1f),
                new Color(0.75f, 0.78f, 0.82f, 1f)
            };
            string[] names = { "Landmark", "Resource", "Hazard", "Settlement", "Objective", "Fallback" };
            string[] shapes = { "star", "crate", "triangle", "house", "flag", "pin" };
            var sprites = new Sprite[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                string path = "Assets/Poi/Art/Placeholder/" + names[i] + ".png";
                if (paint || !File.Exists(path))
                {
                    WriteSymbol(path, shapes[i], colors[i]);
                }

                sprites[i] = ImportSprite(path, 128f);
            }

            return sprites;
        }

        static Sprite BuildPlayerSprite(bool paint)
        {
            const string path = "Assets/Poi/Art/Placeholder/Player.png";
            if (paint || !File.Exists(path))
            {
                WriteSymbol(path, "arrow", new Color(0.25f, 0.62f, 0.98f, 1f));
            }

            return ImportSprite(path, 128f);
        }

        static Sprite ImportSprite(string path, float pixelsPerUnit)
        {
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void WriteSymbol(string path, string shape, Color color)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            var ring = new Color(0.05f, 0.08f, 0.1f, 0.92f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - center) / center;
                    float ny = (y - center) / center;
                    float radius = Mathf.Sqrt(nx * nx + ny * ny);
                    bool mark = ShapeContains(shape, nx, ny);
                    bool border = radius > 0.78f && radius < 0.94f;
                    pixels[y * size + x] = mark ? color : border ? ring : new Color(0f, 0f, 0f, 0f);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        static bool ShapeContains(string shape, float nx, float ny)
        {
            if (shape == "star")
            {
                float angle = Mathf.Atan2(ny, nx) + Mathf.PI * 0.5f;
                float sector = Mathf.PI * 2f / 5f;
                float wrapped = angle % sector;
                if (wrapped < 0f)
                {
                    wrapped += sector;
                }

                float t = wrapped / sector;
                float edge = t < 0.5f ? t * 2f : (1f - t) * 2f;
                float limit = Mathf.Lerp(0.46f, 0.18f, edge);
                return Mathf.Sqrt(nx * nx + ny * ny) < limit;
            }

            if (shape == "crate")
            {
                bool box = Mathf.Abs(nx) < 0.40f && Mathf.Abs(ny) < 0.40f;
                bool inner = Mathf.Abs(nx) < 0.26f && Mathf.Abs(ny) < 0.26f;
                bool cross = inner && (Mathf.Abs(nx) < 0.07f || Mathf.Abs(ny) < 0.07f);
                return (box && !inner) || cross;
            }

            if (shape == "triangle")
            {
                float half = (0.50f - ny) * 0.72f;
                return ny > -0.40f && ny < 0.50f && half > 0f && Mathf.Abs(nx) < half;
            }

            if (shape == "house")
            {
                bool body = Mathf.Abs(nx) < 0.32f && ny > -0.42f && ny < 0.05f;
                float roof = (0.50f - ny) * 0.95f;
                bool top = ny >= 0.02f && ny < 0.50f && roof > 0f && Mathf.Abs(nx) < roof;
                return body || top;
            }

            if (shape == "flag")
            {
                bool pole = nx > -0.30f && nx < -0.16f && ny > -0.46f && ny < 0.46f;
                bool cloth = nx >= -0.16f && nx < 0.40f && ny > 0.05f && ny < 0.42f;
                return pole || cloth;
            }

            if (shape == "arrow")
            {
                bool head = ny > 0.02f && Mathf.Abs(nx) < (0.62f - ny) * 0.85f && ny < 0.62f;
                bool shaft = Mathf.Abs(nx) < 0.12f && ny > -0.52f && ny <= 0.12f;
                return head || shaft;
            }

            float dx = nx;
            float dy = ny - 0.08f;
            return Mathf.Sqrt(dx * dx + dy * dy) < 0.28f;
        }

        static Material BuildGroundMaterial(bool paint)
        {
            const string path = "Assets/Poi/Art/Placeholder/Ground.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                material = new Material(shader) { color = Color.white };
                AssetDatabase.CreateAsset(material, path);
            }

            if (!paint && material.mainTexture != null)
            {
                return material;
            }

            const string texturePath = "Assets/Poi/Art/Placeholder/GrassTile.png";
            WriteGrass(texturePath);
            var texture = ImportTexture(texturePath, TextureWrapMode.Repeat, false);
            material.mainTexture = texture;
            material.mainTextureScale = new Vector2(16000f / 64f, 16000f / 64f);
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", material.mainTextureScale);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            material.color = Color.white;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material BuildLocalMaterial(bool paint)
        {
            const string path = "Assets/Poi/Art/Placeholder/LocalMap.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                material = new Material(shader) { color = Color.white };
                AssetDatabase.CreateAsset(material, path);
            }

            if (!paint && material.mainTexture != null)
            {
                return material;
            }

            const string texturePath = "Assets/Poi/Art/Placeholder/LocalMap.png";
            WriteLocal(texturePath);
            var texture = ImportTexture(texturePath, TextureWrapMode.Clamp, true);
            material.mainTexture = texture;
            material.mainTextureScale = Vector2.one;
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            material.color = Color.white;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Texture2D ImportTexture(string path, TextureWrapMode wrap, bool mipmaps)
        {
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = mipmaps;
            importer.wrapMode = wrap;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static float Speckle(int x, int y)
        {
            int h = (x * 374761393) ^ (y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 255) / 255f;
        }

        static void WriteGrass(string path)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float mix = 0.35f + Speckle(x, y) * 0.3f;
                    pixels[y * size + x] = Color.Lerp(new Color(0.18f, 0.34f, 0.14f), new Color(0.34f, 0.52f, 0.22f), mix);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        static void WriteLocal(string path)
        {
            const int size = 512;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            var pixels = new Color[size * size];
            int center = size / 2;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int noise = (int)(Speckle(x, y) * 255f);
                    Color pixel = Color.Lerp(new Color(0.22f, 0.40f, 0.18f), new Color(0.30f, 0.48f, 0.22f), noise / 255f);
                    bool road = Mathf.Abs(x - center) <= 6 || Mathf.Abs(y - center) <= 6;
                    int lx = x - (center + 90);
                    int ly = y - (center - 70);
                    int lake = lx * lx + ly * ly;
                    int c1 = (x - (center - 40)) * (x - (center - 40)) + (y - (center + 30)) * (y - (center + 30));
                    int c2 = (x - (center + 55)) * (x - (center + 55)) + (y - (center + 20)) * (y - (center + 20));
                    if (lake < 70 * 70)
                    {
                        pixel = new Color(0.12f, 0.36f, 0.58f);
                    }
                    else if (lake < 78 * 78)
                    {
                        pixel = new Color(0.55f, 0.58f, 0.36f);
                    }
                    else if (road)
                    {
                        pixel = new Color(0.72f, 0.66f, 0.48f);
                    }
                    else if (c1 < 28 * 28 || c2 < 22 * 22)
                    {
                        pixel = new Color(0.16f, 0.26f, 0.14f);
                    }

                    pixels[y * size + x] = pixel;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
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
