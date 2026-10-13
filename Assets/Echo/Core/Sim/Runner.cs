using System;
using Echo.Core.Courses;

namespace Echo.Core.Sim
{
    /// <summary>One fixed 60 Hz tick of runner movement. Shared by the player and every ghost.</summary>
    public static class Runner
    {
        // tech spec 5: the steps run in exactly this order; replays depend on it.
        /// <param name="held">Finger still down (user revision: variable jump). Rising with it up falls faster.</param>
        public static void Step(ref RunnerState s, bool tap, Course course, int tick, int speed, in SimConfig cfg, bool held = true)
        {
            if (!s.Alive) return;

            // 1
            if (tap) s.Buffer = cfg.JumpBufferTicks;

            // 2 (and the ground refills the air jumps)
            s.Coyote = s.Grounded ? cfg.CoyoteTicks : Math.Max(s.Coyote - 1, 0);
            if (s.Grounded) s.AirJumpsLeft = cfg.AirJumps;

            // 3
            if (s.Buffer > 0 && s.Coyote > 0)
            {
                s.Vy = cfg.JumpVelocity;
                s.Grounded = false;
                s.Coyote = 0;
                s.Buffer = 0;
                s.JumpAge = 0;
            }
            // 3b, double jump (user revision): a tap in the air, past coyote time, spends an air jump at once.
            // With none left the tap only buffers, as before, for a jump on landing.
            else if (tap && s.Coyote == 0 && s.AirJumpsLeft > 0)
            {
                s.Vy = cfg.AirJumpVelocity;
                s.AirJumpsLeft--;
                s.Buffer = 0;
                s.JumpAge = 0;
            }

            // 4
            s.Buffer = Math.Max(s.Buffer - 1, 0);

            // 5 (variable jump: rising with the finger released uses the stronger ReleaseGravity, but only once the
            // jump is HopHoldTicks old, so any quick tap makes the same hop)
            if (!s.Grounded)
            {
                int gravity = !held && s.Vy > 0 && s.JumpAge >= cfg.HopHoldTicks ? cfg.ReleaseGravity : cfg.Gravity;
                s.Vy = Math.Max(s.Vy - gravity, -cfg.MaxFallSpeed);
                if (s.JumpAge < cfg.HopHoldTicks) s.JumpAge++;
            }

            // 6
            int prevY = s.Y;
            s.X += speed;
            s.Y += s.Vy;

            // 7
            bool floor = course.HasFloorAt(s.X);
            if (s.Vy <= 0 && prevY >= 0 && s.Y <= 0 && floor)
            {
                s.Y = 0;
                s.Vy = 0;
                s.Grounded = true;
                s.LastSafeX = s.X;
            }
            else if (s.Grounded && !floor)
            {
                s.Grounded = false;
            }

            // 8
            if (s.Invuln == 0 && course.OverlapsHazard(PlayerBox(s, cfg), tick)) s.Alive = false;

            // 9
            if (s.Y < cfg.DeathFloorY) s.Alive = false;

            // 10
            s.Invuln = Math.Max(s.Invuln - 1, 0);
        }

        /// <summary>
        /// Continue (tech spec 7): back to the last grounded x with invulnerability. Every field is set
        /// from the state alone, so a ghost replaying the revive lands in exactly the same state.
        /// </summary>
        public static void Revive(ref RunnerState s, in SimConfig cfg)
        {
            s.X = s.LastSafeX;
            s.Y = 0;
            s.Vy = 0;
            s.Grounded = true;
            s.Alive = true;
            s.Coyote = 0;
            s.Buffer = 0;
            s.Invuln = cfg.ReviveInvulnTicks;
            s.AirJumpsLeft = cfg.AirJumps;
            s.JumpAge = 0;
        }

        public static Aabb PlayerBox(in RunnerState s, in SimConfig cfg) =>
            Aabb.FromBottomCentre(s.X, s.Y, cfg.PlayerHitboxW, cfg.PlayerHitboxH);

        public static Aabb GhostBox(in RunnerState s, in SimConfig cfg) =>
            Aabb.FromBottomCentre(s.X, s.Y, cfg.GhostHitboxW, cfg.GhostHitboxH);
    }
}
