using System;
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

        static void DrawLocked(string name, bool locked)
        {
            using (new EditorGUI.DisabledScope(locked))
            {
                EditorGUILayout.PropertyField(serializedObjectField(name));
            }
        }

        static SerializedProperty serializedObjectField(string name)
        {
            return null;
        }
    }
}
