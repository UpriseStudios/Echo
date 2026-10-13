using System;
using UnityEngine;

namespace Echo.Editor.Art
{
    /// <summary>
    /// The shared 16-bit body every character is drawn on: 24 x 34 px (room for a tall hat), feet at the bottom, facing right, about
    /// 0.6 x 0.8 units at 32 px per unit so it matches the player hitbox. Four run frames (stride, pass,
    /// stride, pass) and a tucked jump. Legs and arms are plotted from joint positions; the head, chest and
    /// back item come from the character's text grids; a 1-px dark outline goes around the result.
    /// </summary>
    public static class PixelRig
    {
        public const int W = 24, H = 34;
        public const int RunFrames = 4;
        public const float PixelsPerUnit = 32f;

        struct Pose
        {
            public int Bob;
            public Vector2Int NearKnee, NearFoot, FarKnee, FarFoot, NearHand, FarHand;
        }

        static Pose P(int bob, int nkx, int nky, int nfx, int nfy, int fkx, int fky, int ffx, int ffy, int nhx, int nhy, int fhx, int fhy) =>
            new Pose
            {
                Bob = bob,
                NearKnee = new Vector2Int(nkx, nky), NearFoot = new Vector2Int(nfx, nfy),
                FarKnee = new Vector2Int(fkx, fky), FarFoot = new Vector2Int(ffx, ffy),
                NearHand = new Vector2Int(nhx, nhy), FarHand = new Vector2Int(fhx, fhy),
            };

        // Arms swing against the legs. Passing frames lift the body a pixel.
        static readonly Pose[] Run =
        {
            P(0, 15, 5, 17, 1, 8, 5, 6, 2, 8, 11, 16, 12),
            P(1, 13, 5, 13, 0, 11, 6, 8, 4, 12, 10, 13, 11),
            P(0, 8, 5, 6, 2, 15, 5, 17, 1, 16, 12, 8, 11),
            P(1, 11, 6, 8, 4, 13, 5, 13, 0, 12, 10, 13, 11),
        };

        static readonly Pose Tuck = P(1, 16, 8, 14, 4, 13, 7, 10, 4, 17, 15, 15, 16);

        public static PixelCanvas Frame(PixelCharacter c, int frame) => Apply(Draw(c, Run[frame]), c.Effect, frame);
        public static PixelCanvas Jump(PixelCharacter c) => Apply(Draw(c, Tuck), c.Effect, RunFrames);

        // Variants of one figure: colours inverted, or channels split sideways with a row torn per frame.
        static PixelCanvas Apply(PixelCanvas k, PixelEffect effect, int frame)
        {
            if (effect == PixelEffect.None) return k;
            var o = new PixelCanvas(k.Width, k.Height);
            int tear = 8 + frame * 5 % 17;
            for (int y = 0; y < k.Height; y++)
                for (int x = 0; x < k.Width; x++)
                {
                    if (effect == PixelEffect.Negative)
                    {
                        Color32 c = k[x, y];
                        if (c.a > 0) o[x, y] = new Color32((byte)(255 - c.r), (byte)(255 - c.g), (byte)(255 - c.b), c.a);
                        continue;
                    }
                    int sx = y == tear || y == tear + 1 ? x - 1 : x;
                    Color32 red = k[sx - 1, y], mid = k[sx, y], blue = k[sx + 1, y];
                    byte a = (byte)Mathf.Max(red.a, Mathf.Max(mid.a, blue.a));
                    if (a > 0) o[x, y] = new Color32(red.a > 0 ? red.r : (byte)0, mid.a > 0 ? mid.g : (byte)0, blue.a > 0 ? blue.b : (byte)0, a);
                }
            return o;
        }

        static PixelCanvas Draw(PixelCharacter c, Pose p)
        {
            Validate(c);
            var k = new PixelCanvas(W, H);
            int b = p.Bob;
            var hipNear = new Vector2Int(13, 9 + b);
            var hipFar = new Vector2Int(10, 9 + b);
            var shoulderNear = new Vector2Int(13, 15 + b);
            var shoulderFar = new Vector2Int(10, 15 + b);

            if (c.Back != null) k.Blit(c.Back, c.Palette, c.BackAt.x, c.BackAt.y + b);

            // Far limbs a shade darker so the near ones read in front.
            Leg(k, hipFar, p.FarKnee, p.FarFoot, PixelCanvas.Shade(c.Legs, 0.72f), PixelCanvas.Shade(c.Boots, 0.72f));
            Arm(k, shoulderFar, p.FarHand, PixelCanvas.Shade(c.Sleeve, 0.72f), PixelCanvas.Shade(c.Hand, 0.72f));
            Leg(k, hipNear, p.NearKnee, p.NearFoot, c.Legs, c.Boots);

            k.Rect(8, 9 + b, 15, 16 + b, c.Torso);
            k.Rect(15, 9 + b, 15, 16 + b, c.TorsoShade);
            k.Rect(8, 9 + b, 15, 9 + b, c.TorsoShade);
            if (c.Chest != null) k.Blit(c.Chest, c.Palette, 8, 9 + b);

            if (c.Skirt)
            {
                // Widens by a pixel every two rows down from the waist.
                for (int y = 10 + b; y >= c.SkirtBottom; y--)
                {
                    int spread = (10 + b - y) / 2;
                    k.Rect(7 - spread, y, 16 + spread, y, c.SkirtColor);
                    k[16 + spread, y] = c.SkirtShade;
                }
                k.Rect(7 - (10 + b - c.SkirtBottom) / 2, c.SkirtBottom, 16 + (10 + b - c.SkirtBottom) / 2, c.SkirtBottom, c.SkirtShade);
            }

            int headW = c.Head[0].Length;
            k.Blit(c.Head, c.Palette, W / 2 - headW / 2 + c.HeadX, 17 + b);

            Arm(k, shoulderNear, p.NearHand, c.Sleeve, c.Hand);
            k.Outline(c.Outline);
            return k;
        }

        static void Leg(PixelCanvas k, Vector2Int hip, Vector2Int knee, Vector2Int foot, Color32 leg, Color32 boot)
        {
            k.Line(hip.x, hip.y, knee.x, knee.y, leg, 2);
            k.Line(knee.x, knee.y, foot.x, foot.y, leg, 2);
            k.Rect(foot.x - 1, foot.y, foot.x + 2, foot.y + 1, boot);
        }

        static void Arm(PixelCanvas k, Vector2Int shoulder, Vector2Int hand, Color32 sleeve, Color32 skin)
        {
            k.Line(shoulder.x, shoulder.y, hand.x, hand.y, sleeve, 2);
            k.Rect(hand.x, hand.y, hand.x + 1, hand.y + 1, skin);
        }

        // A typo in a grid would silently shift pixels, so every grid must be rectangular and fully coloured.
        static void Validate(PixelCharacter c)
        {
            Check(c, c.Head, "head");
            Check(c, c.Chest, "chest");
            Check(c, c.Back, "back");
            if (c.Chest != null && (c.Chest.Length != 8 || c.Chest[0].Length != 8))
                throw new ArgumentException($"{c.Id}: chest must be 8 x 8");
        }

        static void Check(PixelCharacter c, string[] rows, string what)
        {
            if (rows == null) return;
            foreach (string row in rows)
            {
                if (row.Length != rows[0].Length)
                    throw new ArgumentException($"{c.Id} {what}: row '{row}' is {row.Length} wide, expected {rows[0].Length}");
                foreach (char ch in row)
                    if (ch != '.' && !c.Palette.ContainsKey(ch))
                        throw new ArgumentException($"{c.Id} {what}: no colour for '{ch}'");
            }
        }
    }
}
