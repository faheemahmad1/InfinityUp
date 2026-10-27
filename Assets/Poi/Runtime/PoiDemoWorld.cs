using UnityEngine;

namespace Poi
{
    /// <summary>
    /// The flat ground, the player token, and the top-down camera. The frame loop does not build them.
    /// </summary>
    public sealed class PoiDemoWorld
    {
        public Transform Player { get; private set; }
        public Camera Camera { get; private set; }

        public void Build(PoiSystemConfig config, PoiPresentationCatalog presentation)
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

            Object.Destroy(ground.GetComponent<Collider>());
            if (presentation != null && presentation.localMaterial != null)
            {
                var local = GameObject.CreatePrimitive(PrimitiveType.Plane);
                local.name = "LocalMap";
                local.transform.localScale = new Vector3(80f, 1f, 80f);
                local.transform.position = new Vector3(0f, 1f, 0f);
                local.GetComponent<Renderer>().sharedMaterial = presentation.localMaterial;
                Object.Destroy(local.GetComponent<Collider>());
            }

            var playerGo = new GameObject("Player");
            playerGo.transform.position = new Vector3(0f, 1.5f, 0f);
            var token = new GameObject("Token");
            token.transform.SetParent(playerGo.transform, false);
            token.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            token.transform.localScale = Vector3.one * 10f;
            var tokenRenderer = token.AddComponent<SpriteRenderer>();
            tokenRenderer.sprite = presentation != null ? presentation.playerSprite : null;
            tokenRenderer.sortingOrder = 20;
            Player = playerGo.transform;

            var camGo = new GameObject("TopDownCamera");
            Camera = camGo.AddComponent<Camera>();
            Camera.tag = "MainCamera";
            Camera.nearClipPlane = 0.3f;
            Camera.farClipPlane = 2000f;
            Camera.depth = 1f;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = new Color(0.08f, 0.09f, 0.1f);
            var sceneCameras = Object.FindObjectsOfType<Camera>();
            for (int i = 0; i < sceneCameras.Length; i++)
            {
                if (sceneCameras[i] != Camera)
                {
                    sceneCameras[i].gameObject.SetActive(false);
                }
            }

            PositionCamera(config);
        }

        public void Move(PoiSystemConfig config, float deltaTime, bool captureDrive)
        {
            float x = captureDrive ? 1f : Input.GetAxisRaw("Horizontal");
            float z = captureDrive ? 0.35f : Input.GetAxisRaw("Vertical");
            if (!captureDrive && x == 0f && z == 0f)
            {
                return;
            }

            Vector3 next = Player.position + new Vector3(x, 0f, z).normalized * (config.playerSpeed * deltaTime);
            float half = config.worldSize * 0.5f;
            next.x = Mathf.Clamp(next.x, -half, half);
            next.z = Mathf.Clamp(next.z, -half, half);
            next.y = 1f;
            Player.position = next;
        }

        public void PositionCamera(PoiSystemConfig config)
        {
            config.cameraHeight = Mathf.Clamp(config.cameraHeight, config.minCameraHeight, config.maxCameraHeight);
            Camera.fieldOfView = config.fieldOfView;
            Camera.transform.SetPositionAndRotation(
                new Vector3(Player.position.x, config.cameraHeight, Player.position.z),
                Quaternion.Euler(90f, 0f, 0f));
        }
    }
}
