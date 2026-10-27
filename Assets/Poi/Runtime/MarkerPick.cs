using System.Collections.Generic;

namespace Poi
{
    public static class MarkerPick
    {
        public static int Pick(List<PoiHit> hits, float x, float z, float radius)
        {
            if (hits == null || radius < 0f)
            {
                return -1;
            }

            float limit = radius * radius;
            int id = -1;
            float best = limit;
            for (int i = 0; i < hits.Count; i++)
            {
                float dx = hits[i].x - x;
                float dz = hits[i].z - z;
                float distanceSq = dx * dx + dz * dz;
                if (distanceSq <= best)
                {
                    best = distanceSq;
                    id = hits[i].id;
                }
            }

            return id;
        }
    }
}
