namespace Poi
{
    /// <summary>
    /// Writes HUD lines into a caller-owned buffer. No strings are created.
    /// </summary>
    public static class HudText
    {
        public static int WriteStats(
            char[] buffer,
            int pois,
            int movers,
            int markers,
            int examined,
            double lookupMs,
            double simMs,
            double gridMs,
            float height)
        {
            int n = 0;
            n = Write(buffer, n, "POIs ");
            n = WriteInt(buffer, n, pois);
            n = Write(buffer, n, "\nMovers ");
            n = WriteInt(buffer, n, movers);
            n = Write(buffer, n, "\nMarkers ");
            n = WriteInt(buffer, n, markers);
            n = Write(buffer, n, "\nExamined ");
            n = WriteInt(buffer, n, examined);
            n = Write(buffer, n, "\nLookup ms ");
            n = WriteFixed(buffer, n, lookupMs, 3);
            n = Write(buffer, n, "\nSim ms ");
            n = WriteFixed(buffer, n, simMs, 3);
            n = Write(buffer, n, "\nGrid ms ");
            n = WriteFixed(buffer, n, gridMs, 3);
            n = Write(buffer, n, "\nHeight ");
            n = WriteInt(buffer, n, (int)height);
            return n;
        }

        public static int WriteDetails(char[] buffer, PoiSnapshot snapshot)
        {
            int n = 0;
            n = Write(buffer, n, "Id ");
            n = WriteInt(buffer, n, snapshot.id);
            n = Write(buffer, n, "\n");
            n = Write(buffer, n, snapshot.name);
            n = Write(buffer, n, "\n");
            n = Write(buffer, n, TypeName(snapshot.type));
            n = Write(buffer, n, "  ");
            n = Write(buffer, n, PriorityName(snapshot.priority));
            n = Write(buffer, n, "\n");
            n = WriteInt(buffer, n, (int)snapshot.x);
            n = Write(buffer, n, ", ");
            n = WriteInt(buffer, n, (int)snapshot.z);
            return n;
        }

        static string TypeName(PoiType type)
        {
            switch (type)
            {
                case PoiType.Landmark: return "Landmark";
                case PoiType.Resource: return "Resource";
                case PoiType.Hazard: return "Hazard";
                case PoiType.Settlement: return "Settlement";
                case PoiType.Objective: return "Objective";
                default: return "Type";
            }
        }

        static string PriorityName(PoiPriority priority)
        {
            switch (priority)
            {
                case PoiPriority.Low: return "Low";
                case PoiPriority.Normal: return "Normal";
                case PoiPriority.High: return "High";
                case PoiPriority.Critical: return "Critical";
                default: return "Priority";
            }
        }

        static int Write(char[] buffer, int index, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return index;
            }

            for (int i = 0; i < value.Length && index < buffer.Length; i++)
            {
                buffer[index++] = value[i];
            }

            return index;
        }

        static int WriteInt(char[] buffer, int index, int value)
        {
            if (value < 0)
            {
                if (index < buffer.Length)
                {
                    buffer[index++] = '-';
                }

                value = -value;
            }

            int start = index;
            int digits = value;
            do
            {
                if (index < buffer.Length)
                {
                    buffer[index++] = (char)('0' + (digits % 10));
                }

                digits /= 10;
            }
            while (digits > 0);

            int end = index - 1;
            while (start < end)
            {
                char swap = buffer[start];
                buffer[start] = buffer[end];
                buffer[end] = swap;
                start++;
                end--;
            }

            return index;
        }

        static int WriteFixed(char[] buffer, int index, double value, int decimals)
        {
            if (value < 0d)
            {
                value = 0d;
            }

            int scale = 1;
            for (int i = 0; i < decimals; i++)
            {
                scale *= 10;
            }

            int scaled = (int)(value * scale + 0.5d);
            int whole = scaled / scale;
            int fraction = scaled % scale;
            index = WriteInt(buffer, index, whole);
            if (index < buffer.Length)
            {
                buffer[index++] = '.';
            }

            int pad = scale / 10;
            while (pad > 0)
            {
                int digit = fraction / pad;
                if (index < buffer.Length)
                {
                    buffer[index++] = (char)('0' + digit);
                }

                fraction -= digit * pad;
                pad /= 10;
            }

            return index;
        }
    }
}
