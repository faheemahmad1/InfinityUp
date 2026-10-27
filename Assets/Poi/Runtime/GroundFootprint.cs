using UnityEngine;

namespace Poi
{
    /// <summary>
    /// Ground rectangle seen by a pitch-90 perspective camera.
    /// Corners are frustum rays intersected with y = 0, which matches Camera.ViewportPointToRay.
    /// ViewportToWorldPoint is not used: its distance is along camera forward, so off-center
    /// points would miss the ground plane.
    /// </summary>
    public static class GroundFootprint
    {
        public static void FromPose(
            Vector3 cameraPosition,
            Quaternion cameraRotation,
            float verticalFovDegrees,
            float aspect,
            out float minX,
            out float minZ,
            out float maxX,
            out float maxZ)
        {
            float tanHalf = Mathf.Tan(verticalFovDegrees * 0.5f * Mathf.Deg2Rad);
            Vector3 forward = cameraRotation * Vector3.forward;
            Vector3 right = cameraRotation * Vector3.right;
            Vector3 up = cameraRotation * Vector3.up;

            minX = float.PositiveInfinity;
            minZ = float.PositiveInfinity;
            maxX = float.NegativeInfinity;
            maxZ = float.NegativeInfinity;
            Accumulate(cameraPosition, forward, right, up, tanHalf, aspect, 0f, 0f, ref minX, ref minZ, ref maxX, ref maxZ);
            Accumulate(cameraPosition, forward, right, up, tanHalf, aspect, 1f, 0f, ref minX, ref minZ, ref maxX, ref maxZ);
            Accumulate(cameraPosition, forward, right, up, tanHalf, aspect, 0f, 1f, ref minX, ref minZ, ref maxX, ref maxZ);
            Accumulate(cameraPosition, forward, right, up, tanHalf, aspect, 1f, 1f, ref minX, ref minZ, ref maxX, ref maxZ);
        }

        public static void FromCamera(Camera camera, out float minX, out float minZ, out float maxX, out float maxZ)
        {
            minX = float.PositiveInfinity;
            minZ = float.PositiveInfinity;
            maxX = float.NegativeInfinity;
            maxZ = float.NegativeInfinity;
            Hit(camera.ViewportPointToRay(new Vector3(0f, 0f, 0f)), ref minX, ref minZ, ref maxX, ref maxZ);
            Hit(camera.ViewportPointToRay(new Vector3(1f, 0f, 0f)), ref minX, ref minZ, ref maxX, ref maxZ);
            Hit(camera.ViewportPointToRay(new Vector3(0f, 1f, 0f)), ref minX, ref minZ, ref maxX, ref maxZ);
            Hit(camera.ViewportPointToRay(new Vector3(1f, 1f, 0f)), ref minX, ref minZ, ref maxX, ref maxZ);
        }

        public static float GroundDistanceSq(float x, float z, float playerX, float playerZ)
        {
            float dx = x - playerX;
            float dz = z - playerZ;
            return dx * dx + dz * dz;
        }

        static void Accumulate(
            Vector3 origin,
            Vector3 forward,
            Vector3 right,
            Vector3 up,
            float tanHalf,
            float aspect,
            float viewportX,
            float viewportY,
            ref float minX,
            ref float minZ,
            ref float maxX,
            ref float maxZ)
        {
            float ndcX = viewportX * 2f - 1f;
            float ndcY = viewportY * 2f - 1f;
            Vector3 direction = forward + right * (ndcX * tanHalf * aspect) + up * (ndcY * tanHalf);
            float t = -origin.y / direction.y;
            float x = origin.x + direction.x * t;
            float z = origin.z + direction.z * t;
            if (x < minX) minX = x;
            if (z < minZ) minZ = z;
            if (x > maxX) maxX = x;
            if (z > maxZ) maxZ = z;
        }

        static void Hit(Ray ray, ref float minX, ref float minZ, ref float maxX, ref float maxZ)
        {
            float t = -ray.origin.y / ray.direction.y;
            float x = ray.origin.x + ray.direction.x * t;
            float z = ray.origin.z + ray.direction.z * t;
            if (x < minX) minX = x;
            if (z < minZ) minZ = z;
            if (x > maxX) maxX = x;
            if (z > maxZ) maxZ = z;
        }
    }
}
