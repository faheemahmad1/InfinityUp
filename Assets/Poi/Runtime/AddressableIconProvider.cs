using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Poi
{
    public sealed class AddressableIconProvider : IIconProvider
    {
        struct Pending
        {
            public IIconReceiver receiver;
            public int generation;
            public IconKey key;
        }

        readonly string[] addresses;
        readonly Sprite[] cache;
        readonly bool[] loading;
        Pending[] pending = new Pending[32];
        int pendingCount;

        public AddressableIconProvider(string[] addresses)
        {
            if (addresses == null || addresses.Length < PoiEnums.IconCount)
            {
                throw new ArgumentException("An address is required for every IconKey.", nameof(addresses));
            }

            this.addresses = addresses;
            cache = new Sprite[PoiEnums.IconCount];
            loading = new bool[PoiEnums.IconCount];
        }

        public void Request(IconKey key, int generation, IIconReceiver receiver)
        {
            int index = (int)key;
            Sprite cached = cache[index];
            if (cached != null)
            {
                receiver.OnIconReady(generation, cached);
                return;
            }

            if (pendingCount >= pending.Length)
            {
                Array.Resize(ref pending, pending.Length * 2);
            }

            pending[pendingCount++] = new Pending
            {
                receiver = receiver,
                generation = generation,
                key = key
            };

            if (loading[index])
            {
                return;
            }

            loading[index] = true;
            string address = addresses[index];
            AsyncOperationHandle<Sprite> handle = Addressables.LoadAssetAsync<Sprite>(address);
            handle.Completed += loaded => OnLoaded(index, loaded);
        }

        void OnLoaded(int index, AsyncOperationHandle<Sprite> handle)
        {
            loading[index] = false;
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                cache[index] = handle.Result;
            }
            else
            {
                cache[index] = cache[(int)IconKey.Fallback];
            }

            int write = 0;
            for (int i = 0; i < pendingCount; i++)
            {
                if ((int)pending[i].key != index)
                {
                    pending[write++] = pending[i];
                    continue;
                }

                Sprite sprite = cache[index];
                if (sprite != null && pending[i].receiver != null)
                {
                    pending[i].receiver.OnIconReady(pending[i].generation, sprite);
                }
            }

            pendingCount = write;
        }
    }
}
