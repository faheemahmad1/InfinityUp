using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Poi
{
    public sealed class MarkerView : MonoBehaviour, IIconReceiver
    {
        public SpriteRenderer IconRenderer;
        public TextMeshPro Label;
        public readonly MarkerBinding Binding = new MarkerBinding();

        public int PoiId => Binding.PoiId;

        public void Bind(PoiHit hit, IIconProvider icons)
        {
            bool showLabel = hit.lod == MarkerLod.IconAndLabel;
            Binding.Bind(hit.id, hit.name, showLabel, hit.icon);
            if (IconRenderer != null)
            {
                IconRenderer.sprite = null;
            }

            if (Label != null)
            {
                Label.text = Binding.Name;
                Label.gameObject.SetActive(showLabel);
            }

            transform.position = new Vector3(hit.x, 1.5f, hit.z);
            EnsurePicker();
            icons?.Request(hit.icon, Binding.Generation, this);
        }

        void EnsurePicker()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null)
            {
                box = gameObject.AddComponent<BoxCollider>();
            }

            box.isTrigger = true;
            box.size = new Vector3(14f, 2f, 14f);
        }

        public void MoveTo(float x, float z)
        {
            transform.position = new Vector3(x, 1.5f, z);
        }

        public void ReleaseView()
        {
            Binding.Release();
            if (IconRenderer != null)
            {
                IconRenderer.sprite = null;
            }

            if (Label != null)
            {
                Label.text = string.Empty;
                Label.gameObject.SetActive(false);
            }

            gameObject.SetActive(false);
        }

        public void OnIconReady(int generation, object token)
        {
            if (!Binding.TryApplyIcon(generation, token))
            {
                return;
            }

            if (IconRenderer != null && token is Sprite sprite)
            {
                IconRenderer.sprite = sprite;
            }
        }
    }

    public sealed class MarkerPool
    {
        readonly List<MarkerView> inactive = new List<MarkerView>(256);
        readonly List<MarkerView> all = new List<MarkerView>(256);
        readonly Transform root;

        public int Count => all.Count;

        public MarkerPool(Transform root)
        {
            this.root = root;
        }

        public void Prewarm(MarkerView prefab, int count)
        {
            while (all.Count < count)
            {
                MarkerView view = Object.Instantiate(prefab, root);
                view.gameObject.SetActive(false);
                all.Add(view);
                inactive.Add(view);
            }
        }

        public bool TryRent(out MarkerView view)
        {
            int last = inactive.Count - 1;
            if (last < 0)
            {
                view = null;
                return false;
            }

            view = inactive[last];
            inactive.RemoveAt(last);
            view.gameObject.SetActive(true);
            return true;
        }

        public void Return(MarkerView view)
        {
            view.ReleaseView();
            inactive.Add(view);
        }
    }

    public sealed class MarkerPresenter
    {
        readonly Dictionary<int, MarkerView> active = new Dictionary<int, MarkerView>(256);
        readonly List<int> activeIds = new List<int>(256);
        readonly List<int> releaseIds = new List<int>(256);
        readonly HashSet<int> desired = new HashSet<int>(256);
        readonly MarkerPool pool;
        readonly IIconProvider icons;

        public int ActiveCount => active.Count;

        public MarkerPresenter(MarkerPool pool, IIconProvider icons)
        {
            this.pool = pool;
            this.icons = icons;
        }

        public void Sync(List<PoiHit> hits)
        {
            desired.Clear();
            for (int i = 0; i < hits.Count; i++)
            {
                desired.Add(hits[i].id);
            }

            releaseIds.Clear();
            for (int i = 0; i < activeIds.Count; i++)
            {
                if (!desired.Contains(activeIds[i]))
                {
                    releaseIds.Add(activeIds[i]);
                }
            }

            for (int i = 0; i < releaseIds.Count; i++)
            {
                int id = releaseIds[i];
                pool.Return(active[id]);
                active.Remove(id);
                activeIds.Remove(id);
            }

            for (int i = 0; i < hits.Count; i++)
            {
                PoiHit hit = hits[i];
                if (active.TryGetValue(hit.id, out MarkerView view))
                {
                    bool showLabel = hit.lod == MarkerLod.IconAndLabel;
                    string nextName = showLabel ? hit.name : string.Empty;
                    if (view.Binding.ShowLabel != showLabel || view.Binding.Name != nextName || view.Binding.Icon != hit.icon)
                    {
                        view.Bind(hit, icons);
                    }
                    else
                    {
                        view.MoveTo(hit.x, hit.z);
                    }
                }
                else if (pool.TryRent(out view))
                {
                    view.Bind(hit, icons);
                    active.Add(hit.id, view);
                    activeIds.Add(hit.id);
                }
            }
        }
    }

    public static class MarkerPrefabLoader
    {
        public static void LoadAsync(string address, System.Action<MarkerView> onLoaded)
        {
            var handle = Addressables.LoadAssetAsync<GameObject>(address);
            handle.Completed += operation =>
            {
                if (operation.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                {
                    onLoaded?.Invoke(null);
                    return;
                }

                MarkerView view = operation.Result.GetComponent<MarkerView>();
                onLoaded?.Invoke(view);
            };
        }
    }
}
