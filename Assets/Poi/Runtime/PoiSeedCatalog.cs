using System;
using UnityEngine;

namespace Poi
{
    [CreateAssetMenu(fileName = "PoiSeedCatalog", menuName = "POI/Seed Catalog")]
    public sealed class PoiSeedCatalog : ScriptableObject
    {
        public PoiSeedEntry[] entries = Array.Empty<PoiSeedEntry>();
    }
}
