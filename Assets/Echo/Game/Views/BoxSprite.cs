using UnityEngine;

namespace Echo.Game.Views
{
    /// <summary>A shared 1×1 unit white sprite with a bottom-left pivot, scaled to draw any grey-box rectangle.</summary>
    public static class BoxSprite
    {
        static Sprite _sprite;

        public static Sprite Get()
        {
            if (_sprite != null) return _sprite;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "EchoBox" };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.zero, 1f);
            _sprite.name = "EchoBox";
            return _sprite;
        }

        static Sprite _outline;

        /// <summary>A 9-sliced hollow square for ghosts' outline look; size it with SpriteRenderer.size.</summary>
        public static Sprite Outline()
        {
            if (_outline != null) return _outline;
            const int size = 16, edge = 2;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, name = "EchoOutline" };
            var clear = new Color(1f, 1f, 1f, 0.12f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool border = x < edge || y < edge || x >= size - edge || y >= size - edge;
                    tex.SetPixel(x, y, border ? Color.white : clear);
                }
            tex.Apply();
            // 32 px per unit makes the 2 px edge 1/16 of a unit at any box size.
            _outline = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.zero, 32f, 0, SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));
            _outline.name = "EchoOutline";
            return _outline;
        }

        public static SpriteRenderer CreateOutline(Transform parent, string name, Material material, int sortingOrder)
        {
            var sr = Create(parent, name, material, sortingOrder);
            sr.sprite = Outline();
            sr.drawMode = SpriteDrawMode.Sliced;
            return sr;
        }

        /// <summary>Places a sliced renderer at a rectangle; unlike <see cref="Place"/> it keeps scale 1 so the edge stays thin.</summary>
        public static void PlaceSliced(SpriteRenderer sr, float x, float y, float w, float h)
        {
            var t = sr.transform;
            t.localPosition = new Vector3(x, y, 0f);
            t.localScale = Vector3.one;
            sr.size = new Vector2(w, h);
        }

        public static SpriteRenderer Create(Transform parent, string name, Material material, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Get();
            sr.sharedMaterial = material;
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        /// <summary>Places a box renderer at a world-space rectangle given by its bottom-left corner and size.</summary>
        public static void Place(SpriteRenderer sr, float x, float y, float w, float h)
        {
            var t = sr.transform;
            t.localPosition = new Vector3(x, y, 0f);
            t.localScale = new Vector3(w, h, 1f);
        }
    }
}
