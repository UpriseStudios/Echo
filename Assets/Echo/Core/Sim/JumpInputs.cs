namespace Echo.Core.Sim
{
    /// <summary>
    /// The inputs tried on each tick by the searches that prove courses and overtakes possible (ChunkValidator,
    /// EncounterPlanner). With the variable jump a finger can lift at any moment while rising, but the proofs
    /// only need the two extremes: press and hold (the full jump) and press and lift at once (the smallest hop).
    /// Anything clearable with those is clearable by a player who can also do everything in between, and trying
    /// every lift tick multiplied the searched states by the length of a jump.
    ///
    /// The proofs must only use moves a player who never double-jumps really has. In the game every press in the
    /// air is the air jump (step 3b), so a press is only tried when it makes a ground jump: on the ground, or with
    /// coyote time left after step 2. A press buffered in the air jumps on the tick after landing, exactly as a
    /// press on that tick does, so nothing is lost. Pressing again mid-air to change the finger was not a real move.
    /// </summary>
    public static class JumpInputs
    {
        public const int Count = 3;

        /// <summary>0: no tap (the finger stays as it was); 1: tap and hold; 2: tap and lift at once.</summary>
        public static bool Tap(int branch) => branch >= 1;

        public static bool Held(int branch, bool heldBefore) => branch switch
        {
            0 => heldBefore,
            1 => true,
            _ => false,
        };

        /// <summary>Step 2 sets coyote time on the ground and counts it down in the air; step 3 jumps while it is above 0.</summary>
        public static bool Applies(int branch, in RunnerState s, bool heldBefore) => branch == 0 || s.Grounded || s.Coyote >= 2;

        /// <summary>
        /// The finger only changes anything while rising (Runner step 5). Without a tap a runner can only rise if it
        /// already is, or if a buffered tap jumps it, so otherwise a held finger is the same as a lifted one, and a
        /// search treats it as lifted. A new tap sets the finger itself. This halves the states a search visits.
        /// </summary>
        public static bool Canonical(in RunnerState s, bool held) => held && (s.Vy > 0 || s.Buffer > 0);
    }
}
