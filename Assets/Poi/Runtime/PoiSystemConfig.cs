using UnityEngine;

namespace Poi
{
    [CreateAssetMenu(fileName = "PoiSystemConfig", menuName = "POI/System Config")]
    public sealed class PoiSystemConfig : ScriptableObject
    {
        public float worldSize = 16000f;
        public float cellSize = 400f;
        public float closeRange = 150f;
        public float displayRange = 500f;
        public int markerBudget = 250;
        public int edgeBudget = 16;
        public float edgeRadius = 2000f;
        public PoiPriority edgeMinimumPriority = PoiPriority.High;
        public float declutterRadius = 18f;
        public float pickRadius = 18f;
        public float minCameraHeight = 50f;
        public float maxCameraHeight = 400f;
        public float cameraHeight = 400f;
        public float fieldOfView = 60f;
        public int initialCount = 1000;
        public float additionsPerSecond = 10f;
        public float removalsPerSecond = 10f;
        [Range(0f, 100f)] public float movingPercent = 5f;
        public float moveSpeed = 40f;
        public float playerSpeed = 120f;
        public PoiPriority minimumPriority = PoiPriority.Low;
        public int typeMask = PoiEnums.AllTypesMask;
        public int version;

        public float CameraAspect = 16f / 9f;

        void OnValidate()
        {
            if (worldSize < 1000f) worldSize = 1000f;
            if (cellSize < 50f) cellSize = 50f;
            if (closeRange < 1f) closeRange = 1f;
            if (displayRange < closeRange) displayRange = closeRange;
            if (markerBudget < 1) markerBudget = 1;
            if (markerBudget > 300) markerBudget = 300;
            if (edgeBudget < 1) edgeBudget = 1;
            if (edgeBudget > 32) edgeBudget = 32;
            if (edgeRadius < 0f) edgeRadius = 0f;
            if (declutterRadius < 0f) declutterRadius = 0f;
            if (pickRadius < 1f) pickRadius = 1f;
            if (minCameraHeight < 10f) minCameraHeight = 10f;
            if (maxCameraHeight < minCameraHeight) maxCameraHeight = minCameraHeight;
            cameraHeight = Mathf.Clamp(cameraHeight, minCameraHeight, maxCameraHeight);
            if (fieldOfView < 20f) fieldOfView = 20f;
            if (fieldOfView > 90f) fieldOfView = 90f;
            if (initialCount < 0) initialCount = 0;
            if (initialCount > PoiService.MaxCapacity) initialCount = PoiService.MaxCapacity;
            if (additionsPerSecond < 0f) additionsPerSecond = 0f;
            if (removalsPerSecond < 0f) removalsPerSecond = 0f;
            movingPercent = Mathf.Clamp(movingPercent, 0f, 100f);
            if (moveSpeed < 0f) moveSpeed = 0f;
            if (playerSpeed < 0f) playerSpeed = 0f;
            version++;
        }
    }
}
