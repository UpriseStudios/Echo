using UnityEngine;

namespace Echo.Game.Views
{
    /// <summary>
    /// Art pass phase 6: a soft blob shadow on the floor under a figure in the Far Far Future, smaller and fainter the
    /// higher it is, so a jump reads against the 3D city. Drawn on the floor's edge (order 1), under every hazard.
    /// </summary>
    public static class BlobShadow
    {
        const float Width = 0.9f, FadeHeight = 3f;

        public static void Place(SpriteRenderer shadow, float x, float height, float alpha)
        {
            float lift = Mathf.Clamp01(height / FadeHeight);
            float s = Width * (1f - 0.55f * lift);
            Vector2 size = shadow.sprite.bounds.size;
            shadow.transform.localPosition = new Vector3(x, 0.03f, -0.1f);
            shadow.transform.localScale = new Vector3(s / size.x, s * 0.28f / size.y, 1f);
            shadow.color = new Color(0f, 0.02f, 0.06f, 0.55f * alpha * (1f - 0.7f * lift));
            shadow.enabled = alpha > 0.01f;
        }
    }
}
