using System.Collections.Generic;

namespace Poi
{
    /// <summary>
    /// Drops a later hit whose ground position falls inside the radius of an already kept hit.
    /// Higher priority is kept. The same priority keeps the nearer hit.
    /// </summary>
    public static class MarkerDeclutter
    {
        public static int Apply(List<PoiHit> hits, float radius)
        {
            if (hits == null || hits.Count == 0 || radius <= 0f)
            {
                return hits == null ? 0 : hits.Count;
            }

            Order(hits);
            float limit = radius * radius;
            int kept = 0;
            for (int i = 0; i < hits.Count; i++)
            {
                PoiHit candidate = hits[i];
                bool overlaps = false;
                for (int k = 0; k < kept; k++)
                {
                    float dx = candidate.x - hits[k].x;
                    float dz = candidate.z - hits[k].z;
                    if (dx * dx + dz * dz < limit)
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (overlaps)
                {
                    continue;
                }

                hits[kept] = candidate;
                kept++;
            }

            if (kept < hits.Count)
            {
                hits.RemoveRange(kept, hits.Count - kept);
            }

            return kept;
        }

        static void Order(List<PoiHit> hits)
        {
            for (int i = 1; i < hits.Count; i++)
            {
                PoiHit key = hits[i];
                int j = i - 1;
                while (j >= 0 && ComesAfter(hits[j], key))
                {
                    hits[j + 1] = hits[j];
                    j--;
                }

                hits[j + 1] = key;
            }
        }

        static bool ComesAfter(PoiHit earlier, PoiHit key)
        {
            if (earlier.priority != key.priority)
            {
                return earlier.priority < key.priority;
            }

            return earlier.distanceSq > key.distanceSq;
        }
    }
}
