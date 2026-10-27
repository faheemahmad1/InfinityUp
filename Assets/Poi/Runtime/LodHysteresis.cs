using System.Collections.Generic;

namespace Poi
{
    /// <summary>
    /// Keeps the previous icon or icon-plus-label until the POI is clearly on the other side of close range.
    /// The first time a POI is shown, the threshold is close range itself.
    /// </summary>
    public static class LodHysteresis
    {
        public static void Apply(List<PoiHit> hits, PoiStore store, float closeRange, float band)
        {
            if (hits == null || store == null)
            {
                return;
            }

            if (band < 0f)
            {
                band = 0f;
            }

            float enter = closeRange - band;
            if (enter < 0f)
            {
                enter = 0f;
            }

            float exit = closeRange + band;
            for (int i = 0; i < hits.Count; i++)
            {
                PoiHit hit = hits[i];
                ref PoiStore.Slot slot = ref store.Slots[hit.slot];
                float distance = (float)System.Math.Sqrt(hit.distanceSq);
                MarkerLod next = Choose(slot.shownLod, distance, closeRange, enter, exit);
                slot.shownLod = next;
                hit.lod = next;
                hits[i] = hit;
            }
        }

        public static MarkerLod Choose(MarkerLod previous, float distance, float closeRange, float enter, float exit)
        {
            if (previous == MarkerLod.IconAndLabel)
            {
                return distance <= exit ? MarkerLod.IconAndLabel : MarkerLod.Icon;
            }

            if (previous == MarkerLod.Icon)
            {
                return distance <= enter ? MarkerLod.IconAndLabel : MarkerLod.Icon;
            }

            return distance <= closeRange ? MarkerLod.IconAndLabel : MarkerLod.Icon;
        }
    }
}
