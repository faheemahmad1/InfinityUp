using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditorInternal;
using UnityEngine;

namespace Poi.EditorTools
{
    [InitializeOnLoad]
    public static class PoiAutomation
    {
        const string BenchFlag = "Docs/run-bench.flag";
        const string PlayFlag = "Docs/run-play.flag";
        const string PlayDone = "Docs/play-capture.done";
        const string ScenePath = "Assets/Scenes/PoiDemo.unity";
        static bool playExitRequested;
        static bool profileSaved;

        static PoiAutomation()
        {
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            if (File.Exists(PlayDone) && EditorApplication.isPlaying && !playExitRequested)
            {
                playExitRequested = true;
                EditorApplication.ExitPlaymode();
                return;
            }

            if (File.Exists(PlayDone) && !EditorApplication.isPlayingOrWillChangePlaymode && !profileSaved)
            {
                profileSaved = true;
                Directory.CreateDirectory("Docs/Profiles");
                string dataPath = Path.GetFullPath("Docs/Profiles/poi-250k.data");
                string rawPath = Path.GetFullPath("Docs/Profiles/poi-250k.raw");
                string note;
                try
                {
                    bool loaded = File.Exists(rawPath) && ProfilerDriver.LoadProfile(rawPath, false);
                    ProfilerDriver.SaveProfile(dataPath);
                    long bytes = File.Exists(dataPath) ? new FileInfo(dataPath).Length : 0;
                    note = "Profiler raw loaded=" + loaded + " SaveProfile bytes=" + bytes + " path=" + dataPath;
                }
                catch (Exception exception)
                {
                    note = "Profiler SaveProfile failed: " + exception.Message;
                }

                File.AppendAllText("Docs/Measurements/playmode.txt", "\n" + note + "\n");
                File.Delete(PlayDone);
                Debug.Log("POI play capture saved");
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (File.Exists(BenchFlag))
            {
                File.Delete(BenchFlag);
                PoiEditModeBenchmark.Run();
                return;
            }

            if (File.Exists(PlayFlag))
            {
                File.Delete(PlayFlag);
                playExitRequested = false;
                profileSaved = false;
                UseAssetDatabasePlayMode();
                File.WriteAllText("Docs/play-capture.request", "start");
                if (EditorSceneManagerOpen())
                {
                    EditorApplication.EnterPlaymode();
                    Debug.Log("POI play capture started");
                }
            }
        }

        static void UseAssetDatabasePlayMode()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                settings = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>("Assets/AddressableAssetsData/AddressableAssetSettings.asset");
                if (settings != null)
                {
                    AddressableAssetSettingsDefaultObject.Settings = settings;
                }
            }

            if (settings == null)
            {
                return;
            }

            for (int i = 0; i < settings.DataBuilders.Count; i++)
            {
                var builder = settings.GetDataBuilder(i);
                if (builder != null && builder.GetType().Name == "BuildScriptFastMode")
                {
                    settings.ActivePlayModeDataBuilderIndex = i;
                    return;
                }
            }
        }

        static bool EditorSceneManagerOpen()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);
            return scene.IsValid();
        }
    }
}
