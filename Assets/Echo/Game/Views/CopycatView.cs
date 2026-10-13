using Echo.Core.Rules;
using UnityEngine;

namespace Echo.Game.Views
{
    /// <summary>
    /// The Copycat twist's mirror runner: the player's own character, facing them, in red with a soft glow, so it
    /// reads as "you, coming the other way". Interpolated between ticks; never writes sim state.
    /// </summary>
    public sealed class CopycatView : MonoBehaviour
    {
        const float StrideUnits = 0.42f;
        const float GlowScale = 1.25f;
        static readonly Color BodyColor = new Color(1f, 0.32f, 0.38f, 0.92f);
        static readonly Color GlowColor = new Color(1f, 0.15f, 0.3f, 0.3f);

        [SerializeField] Material _material;
        [SerializeField] Sprite[] _runFrames = new Sprite[0];
        [SerializeField] Sprite _jumpFrame;

        SpriteRenderer _body, _glow;

        SpriteRenderer Make(string name, int order, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sharedMaterial = _material;
            r.sortingOrder = order;
            r.flipX = true;
            r.color = color;
            r.enabled = false;
            return r;
        }

        // Created on first use: Awake order across objects is not guaranteed.
        SpriteRenderer Body => _body != null ? _body : _body = Make("Copycat", 11, BodyColor);
        SpriteRenderer Glow => _glow != null ? _glow : _glow = Make("CopycatGlow", 8, GlowColor);

        /// <summary>The player's equipped character, so the copycat is always them.</summary>
        public void SetCharacter(Sprite[] runFrames, Sprite jumpFrame)
        {
            if (runFrames != null && runFrames.Length > 0) _runFrames = runFrames;
            if (jumpFrame != null) _jumpFrame = jumpFrame;
        }

        public void Render(in CopycatState c, float alpha)
        {
            SpriteRenderer body = Body, glow = Glow;
            body.enabled = glow.enabled = c.Active;
            if (!c.Active) return;

            float x = Mathf.Lerp(c.PrevX, c.X, alpha) / SimUnits.PerUnit;
            float y = Mathf.Lerp(c.PrevBody.Y, c.Body.Y, alpha) / SimUnits.PerUnit;
            Sprite frame = Frame(c.Body.Grounded, x);
            body.sprite = glow.sprite = frame;
            var pos = new Vector3(SimUnits.Pixel(x), SimUnits.Pixel(y), 0f);
            body.transform.localPosition = pos;
            glow.transform.localPosition = pos;
            glow.transform.localScale = new Vector3(GlowScale, GlowScale, 1f);
        }

        Sprite Frame(bool grounded, float x)
        {
            if (_runFrames.Length == 0) return BoxSprite.Get();
            if (!grounded && _jumpFrame != null) return _jumpFrame;
            int i = (int)(Mathf.Abs(x) / StrideUnits) % _runFrames.Length;
            return _runFrames[i];
        }
    }
}
