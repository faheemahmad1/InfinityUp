namespace Poi
{
    /// <summary>
    /// Pure bind state. A late icon apply is ignored when the generation no longer matches.
    /// </summary>
    public sealed class MarkerBinding
    {
        public int Generation { get; private set; }
        public int PoiId { get; private set; }
        public IconKey Icon { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public bool ShowLabel { get; private set; }
        public bool HasSprite { get; private set; }
        public object SpriteToken { get; private set; }

        public void Bind(int poiId, string name, bool showLabel, IconKey icon)
        {
            Generation++;
            PoiId = poiId;
            Icon = icon;
            ShowLabel = showLabel;
            Name = showLabel ? name ?? string.Empty : string.Empty;
            HasSprite = false;
            SpriteToken = null;
        }

        public void Release()
        {
            Generation++;
            PoiId = 0;
            Icon = IconKey.Fallback;
            ShowLabel = false;
            Name = string.Empty;
            HasSprite = false;
            SpriteToken = null;
        }

        public bool TryApplyIcon(int generation, object token)
        {
            if (generation != Generation || token == null)
            {
                return false;
            }

            SpriteToken = token;
            HasSprite = true;
            return true;
        }
    }

    public interface IIconReceiver
    {
        void OnIconReady(int generation, object token);
    }

    public interface IIconProvider
    {
        void Request(IconKey key, int generation, IIconReceiver receiver);
    }
}
