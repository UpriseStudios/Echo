using System;

namespace Echo.Core.Sim
{
    /// <summary>Every tunable sim constant (tech spec 4). Part of the content version.</summary>
    public readonly struct SimConfig
    {
        public readonly int TickRate;
        public readonly int RunSpeedBase;
        public readonly int SpeedPerClearPct;
        public readonly int MaxSpeedPct;
        public readonly int Gravity;
        public readonly int JumpVelocity;
        public readonly int MaxFallSpeed;
        public readonly int CoyoteTicks;
        public readonly int JumpBufferTicks;
        public readonly int PlayerHitboxW;
        public readonly int PlayerHitboxH;
        public readonly int GhostHitboxW;
        public readonly int GhostHitboxH;
        public readonly int GhostGraceTicks;
        public readonly int NearMissDistance;
        public readonly int DeathFloorY;
        public readonly int GhostCap;
        public readonly int ReviveInvulnTicks;

        // Ghost echoes (gameplay revision of tech spec 7): a ghost replays on its own clock, GhostClockNum
        // of its ticks per GhostClockDen player ticks, with a head start so the player overtakes it at a chunk
        // boundary just before its grave, never before FirstEncounterChunk (EncounterPlanner). EncounterEveryChunks
        // spaced the old fixed slots; it is unused since grave placement but kept so ContentVersion and saves stay valid.
        public readonly int GhostClockNum;
        public readonly int GhostClockDen;
        public readonly int FirstEncounterChunk;
        public readonly int EncounterEveryChunks;

        // Double jump (a revision of tech spec 5, chosen by the user): AirJumps extra jumps per time in the air,
        // refilled on the ground, each at AirJumpVelocity (about 80% of the ground jump's height).
        public readonly int AirJumps;
        public readonly int AirJumpVelocity;

        // 1-UP (also chosen by the user): a life every LifeEveryScore points, at most MaxLives held, spent
        // automatically on death as a revive. Lives last only for the run.
        public readonly int LifeEveryScore;
        public readonly int MaxLives;

        // Variable jump (chosen by the user): while rising with the finger released, gravity is ReleaseGravity
        // instead of Gravity, so a quick tap tops out near 64% of a held jump (with HopHoldTicks) and a held jump is unchanged.
        public readonly int ReleaseGravity;

        // A finger is never down for less than a few frames, so lifting within HopHoldTicks of a jump makes the
        // same hop as lifting at exactly HopHoldTicks: every quick tap hops the same height (device testing: a
        // one-frame hop was not something a thumb could do, and the proofs assumed it).
        public readonly int HopHoldTicks;

        public SimConfig(
            int tickRate, int runSpeedBase, int speedPerClearPct, int maxSpeedPct,
            int gravity, int jumpVelocity, int maxFallSpeed, int coyoteTicks, int jumpBufferTicks,
            int playerHitboxW, int playerHitboxH, int ghostHitboxW, int ghostHitboxH,
            int ghostGraceTicks, int nearMissDistance, int deathFloorY, int ghostCap, int reviveInvulnTicks,
            int ghostClockNum = 1, int ghostClockDen = 4, int firstEncounterChunk = 2, int encounterEveryChunks = 2,
            int airJumps = 1, int airJumpVelocity = 225, int lifeEveryScore = 1000, int maxLives = 1,
            int releaseGravity = 60, int hopHoldTicks = 6)
        {
            TickRate = tickRate;
            RunSpeedBase = runSpeedBase;
            SpeedPerClearPct = speedPerClearPct;
            MaxSpeedPct = maxSpeedPct;
            Gravity = gravity;
            JumpVelocity = jumpVelocity;
            MaxFallSpeed = maxFallSpeed;
            CoyoteTicks = coyoteTicks;
            JumpBufferTicks = jumpBufferTicks;
            PlayerHitboxW = playerHitboxW;
            PlayerHitboxH = playerHitboxH;
            GhostHitboxW = ghostHitboxW;
            GhostHitboxH = ghostHitboxH;
            GhostGraceTicks = ghostGraceTicks;
            NearMissDistance = nearMissDistance;
            DeathFloorY = deathFloorY;
            GhostCap = ghostCap;
            ReviveInvulnTicks = reviveInvulnTicks;
            if (ghostClockDen <= 0 || ghostClockNum <= 0 || ghostClockNum > ghostClockDen)
                throw new ArgumentOutOfRangeException(nameof(ghostClockNum), "ghost clock must be a fraction in (0, 1]");
            GhostClockNum = ghostClockNum;
            GhostClockDen = ghostClockDen;
            FirstEncounterChunk = firstEncounterChunk;
            EncounterEveryChunks = Math.Max(1, encounterEveryChunks);
            AirJumps = Math.Max(0, airJumps);
            AirJumpVelocity = airJumpVelocity;
            LifeEveryScore = Math.Max(1, lifeEveryScore);
            MaxLives = Math.Max(0, maxLives);
            ReleaseGravity = Math.Max(gravity, releaseGravity);
            HopHoldTicks = Math.Max(0, hopHoldTicks);
        }

        /// <summary>Same rules with ghosts locked to the player, as in the original tech spec 7 (used by rule tests).</summary>
        public SimConfig WithLockstepGhosts() => new SimConfig(TickRate, RunSpeedBase, SpeedPerClearPct, MaxSpeedPct,
            Gravity, JumpVelocity, MaxFallSpeed, CoyoteTicks, JumpBufferTicks, PlayerHitboxW, PlayerHitboxH,
            GhostHitboxW, GhostHitboxH, GhostGraceTicks, NearMissDistance, DeathFloorY, GhostCap, ReviveInvulnTicks,
            1, 1, FirstEncounterChunk, EncounterEveryChunks, AirJumps, AirJumpVelocity, LifeEveryScore, MaxLives, ReleaseGravity, HopHoldTicks);

        /// <summary>Same rules with a different number of air jumps (0 restores the original single jump).</summary>
        public SimConfig WithAirJumps(int airJumps) => new SimConfig(TickRate, RunSpeedBase, SpeedPerClearPct, MaxSpeedPct,
            Gravity, JumpVelocity, MaxFallSpeed, CoyoteTicks, JumpBufferTicks, PlayerHitboxW, PlayerHitboxH,
            GhostHitboxW, GhostHitboxH, GhostGraceTicks, NearMissDistance, DeathFloorY, GhostCap, ReviveInvulnTicks,
            GhostClockNum, GhostClockDen, FirstEncounterChunk, EncounterEveryChunks, airJumps, AirJumpVelocity, LifeEveryScore, MaxLives, ReleaseGravity, HopHoldTicks);

        /// <summary>Same rules with a different life milestone and cap (cap 0 turns lives off).</summary>
        public SimConfig WithLives(int lifeEveryScore, int maxLives) => new SimConfig(TickRate, RunSpeedBase, SpeedPerClearPct, MaxSpeedPct,
            Gravity, JumpVelocity, MaxFallSpeed, CoyoteTicks, JumpBufferTicks, PlayerHitboxW, PlayerHitboxH,
            GhostHitboxW, GhostHitboxH, GhostGraceTicks, NearMissDistance, DeathFloorY, GhostCap, ReviveInvulnTicks,
            GhostClockNum, GhostClockDen, FirstEncounterChunk, EncounterEveryChunks, AirJumps, AirJumpVelocity, lifeEveryScore, maxLives, ReleaseGravity, HopHoldTicks);

        /// <summary>Same rules with a different release gravity (equal to Gravity turns variable jumps off).</summary>
        public SimConfig WithReleaseGravity(int releaseGravity) => new SimConfig(TickRate, RunSpeedBase, SpeedPerClearPct, MaxSpeedPct,
            Gravity, JumpVelocity, MaxFallSpeed, CoyoteTicks, JumpBufferTicks, PlayerHitboxW, PlayerHitboxH,
            GhostHitboxW, GhostHitboxH, GhostGraceTicks, NearMissDistance, DeathFloorY, GhostCap, ReviveInvulnTicks,
            GhostClockNum, GhostClockDen, FirstEncounterChunk, EncounterEveryChunks, AirJumps, AirJumpVelocity, LifeEveryScore, MaxLives, releaseGravity, HopHoldTicks);

        /// <summary>Same rules with ghost hitboxes scaled (the Giant Ghosts twist). Runtime only, never hashed.</summary>
        /// <summary>Same rules with a different hop hold (0: a lift counts from the very first tick).</summary>
        public SimConfig WithHopHold(int hopHoldTicks) => new SimConfig(TickRate, RunSpeedBase, SpeedPerClearPct, MaxSpeedPct,
            Gravity, JumpVelocity, MaxFallSpeed, CoyoteTicks, JumpBufferTicks, PlayerHitboxW, PlayerHitboxH,
            GhostHitboxW, GhostHitboxH, GhostGraceTicks, NearMissDistance, DeathFloorY, GhostCap, ReviveInvulnTicks,
            GhostClockNum, GhostClockDen, FirstEncounterChunk, EncounterEveryChunks, AirJumps, AirJumpVelocity, LifeEveryScore, MaxLives, ReleaseGravity, hopHoldTicks);

        public SimConfig WithGhostScale(int pct) => new SimConfig(TickRate, RunSpeedBase, SpeedPerClearPct, MaxSpeedPct,
            Gravity, JumpVelocity, MaxFallSpeed, CoyoteTicks, JumpBufferTicks, PlayerHitboxW, PlayerHitboxH,
            GhostHitboxW * pct / 100, GhostHitboxH * pct / 100, GhostGraceTicks, NearMissDistance, DeathFloorY, GhostCap, ReviveInvulnTicks,
            GhostClockNum, GhostClockDen, FirstEncounterChunk, EncounterEveryChunks, AirJumps, AirJumpVelocity, LifeEveryScore, MaxLives, ReleaseGravity, HopHoldTicks);

        public static SimConfig Default => new SimConfig(
            tickRate: 60,
            runSpeedBase: 110,
            speedPerClearPct: 5,
            maxSpeedPct: 150,
            gravity: 14,
            jumpVelocity: 252,
            maxFallSpeed: 400,
            coyoteTicks: 5,
            jumpBufferTicks: 6,
            playerHitboxW: 600,
            playerHitboxH: 800,
            ghostHitboxW: 480,
            ghostHitboxH: 640,
            ghostGraceTicks: 60,
            nearMissDistance: 300,
            deathFloorY: -2000,
            ghostCap: 30,
            reviveInvulnTicks: 60);

        public int SpeedPctForClears(int clears) =>
            Math.Min(100 + SpeedPerClearPct * Math.Max(0, clears), MaxSpeedPct);

        public int SpeedForPct(int speedPct) => RunSpeedBase * speedPct / 100;

        /// <summary>Every value in a fixed order, for hashing into the content version.</summary>
        public void WriteValues(int[] into)
        {
            into[0] = TickRate; into[1] = RunSpeedBase; into[2] = SpeedPerClearPct; into[3] = MaxSpeedPct;
            into[4] = Gravity; into[5] = JumpVelocity; into[6] = MaxFallSpeed; into[7] = CoyoteTicks;
            into[8] = JumpBufferTicks; into[9] = PlayerHitboxW; into[10] = PlayerHitboxH; into[11] = GhostHitboxW;
            into[12] = GhostHitboxH; into[13] = GhostGraceTicks; into[14] = NearMissDistance; into[15] = DeathFloorY;
            into[16] = GhostCap; into[17] = ReviveInvulnTicks;
            into[18] = GhostClockNum; into[19] = GhostClockDen; into[20] = FirstEncounterChunk; into[21] = EncounterEveryChunks;
            into[22] = AirJumps; into[23] = AirJumpVelocity; into[24] = LifeEveryScore; into[25] = MaxLives;
            into[26] = ReleaseGravity; into[27] = HopHoldTicks;
        }

        public const int ValueCount = 28;
    }
}
