using System;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Scoring;
using Echo.Core.Sim;

namespace Echo.Core.Rules
{
    /// <summary>
    /// One run: the player, every ghost and the recorder, advanced one fixed tick at a time.
    /// After death it stays pending (Continue can resume it) until RunRules.Commit.
    /// </summary>
    public sealed class RunSimulation
    {
        readonly Course _course;
        readonly SimConfig _cfg;
        // The config ghosts collide with: the base one, or larger hitboxes for the Giant Ghosts twist.
        readonly SimConfig _ghostCfg;
        readonly int _speed;
        readonly GhostManager _ghosts = new GhostManager();
        readonly RunRecorder _recorder = new RunRecorder();
        RunnerState _player = RunnerState.AtStart();
        bool[] _passed;
        bool _held;

        // Copycat twist: the flat floor it runs on, and the player's last 64 inputs for it to copy.
        readonly Course _phantom;
        readonly bool[] _tapLog = new bool[64], _downLog = new bool[64];
        CopycatState _copycat;
        int _nearMissCount;
        int _lastNearMiss = -1, _nearMissBefore = -1;

        public RunSimulation(Course course, GhostSet ghosts, int runIndex, int speedPct, in SimConfig cfg, RunOptions options = default)
        {
            _course = course ?? throw new ArgumentNullException(nameof(course));
            if (ghosts == null) throw new ArgumentNullException(nameof(ghosts));
            _cfg = cfg;
            Options = options;
            _ghostCfg = options.Twist == Twist.GiantGhosts ? cfg.WithGhostScale(WeeklyTwists.GiantGhostPct) : cfg;
            RunIndex = runIndex;
            SpeedPct = speedPct;
            _speed = cfg.SpeedForPct(speedPct);
            _ghosts.Begin(ghosts.All, _ghostCfg, course, _speed);
            // Highlights reel (a user revision): the ghosts as they were at the start, since kills change them.
            var start = new GhostRecord[ghosts.Count];
            for (int i = 0; i < start.Length; i++) start[i] = ghosts[i].Clone();
            StartGhosts = start;
            _passed = new bool[Math.Max(1, ghosts.Count)];
            GhostsAtStart = ghosts.Count;
            NextLifeScore = cfg.LifeEveryScore;
            BountyIndex = options.Bounties ? PickBounty(ghosts, course.Seed, runIndex) : -1;
            if (options.Duel && _ghosts.Count == 1)
            {
                int deathX = _ghosts.RecordAt(0).DeathX;
                DuelTargetX = deathX >= 0 ? deathX : _ghosts.DeathX(0, course, _ghostCfg);
            }
            if (options.Twist == Twist.Copycat)
            {
                _phantom = Echo.Core.Rules.Copycat.PhantomFloor();
                PlanCopycat(Echo.Core.Rules.Copycat.FirstChunk);
            }
        }

        /// <summary>The Copycat twist's current or next encounter (Planned false when there is none).</summary>
        public ref readonly CopycatState Copycat => ref _copycat;

        // ---- power-ups (a user revision): used during the run, earned outside it ----

        /// <summary>The Ghost Shield is up: the next ghost (or Copycat) touch breaks it instead of killing.</summary>
        public bool ShieldArmed { get; private set; }
        public bool ShieldUsed { get; private set; }

        /// <summary>Times the Shield has saved the player (0 or 1), so views can burst it.</summary>
        public int ShieldBreaks { get; private set; }

        /// <summary>Phase keeps ghosts harmless until the player passes this x; -1 when not used.</summary>
        public int PhaseUntilX { get; private set; } = -1;
        public bool PhaseUsed => PhaseUntilX >= 0;
        public bool PhaseActive => PhaseUntilX >= 0 && _player.X < PhaseUntilX;

        /// <summary>An Extra 1-UP brought into the run, and whether it has been spent.</summary>
        public bool ExtraLifeHeld { get; private set; }
        public bool ExtraLifeUsed { get; private set; }

        int _ghostSafeTicks;

        // Ghost touches do nothing during Phase and for a moment after the Shield breaks.
        bool GhostsSafe => _ghostSafeTicks > 0 || PhaseActive;

        /// <summary>Arms the Ghost Shield; once per run.</summary>
        public bool UseShield()
        {
            if (ShieldUsed || !_player.Alive) return false;
            ShieldUsed = true;
            ShieldArmed = true;
            ShieldTick = Tick;
            return true;
        }

        /// <summary>Starts Phase for PowerUpRules.PhaseMetres from here; once per run.</summary>
        public bool UsePhase()
        {
            if (PhaseUsed || !_player.Alive) return false;
            PhaseUntilX = _player.X + PowerUpRules.PhaseMetres * 1000;
            PhaseTick = Tick;
            return true;
        }

        /// <summary>Brings an Extra 1-UP into the run: spent automatically on a death, after any life earned in the run.</summary>
        public void GrantExtraLife() => ExtraLifeHeld = true;

        bool HasLifeToSpend => Lives > 0 || (ExtraLifeHeld && !ExtraLifeUsed);

        // The Shield takes the hit: no death, no kill for the ghost, and a short grace to get clear.
        bool ShieldTakesIt()
        {
            if (!ShieldArmed) return false;
            ShieldArmed = false;
            ShieldBreaks++;
            _ghostSafeTicks = PowerUpRules.ShieldGraceTicks;
            return true;
        }

        // Highlights reel: when each thing done between ticks happened, so a reel can do it again on the same tick.
        /// <summary>The ghosts at the start of the run, unchanged by its kills.</summary>
        public System.Collections.Generic.IReadOnlyList<GhostRecord> StartGhosts { get; }
        public int ShieldTick { get; private set; } = -1;
        public int PhaseTick { get; private set; } = -1;
        public int ContinueTick { get; private set; } = -1;

        /// <summary>Copycats the player has got past this run.</summary>
        public int CopycatsPassed { get; private set; }

        public RunOptions Options { get; }

        /// <summary>Ghost Bounties: index of the bounty ghost this run, or -1.</summary>
        public int BountyIndex { get; }
        public bool BountyClaimed { get; private set; }

        /// <summary>Nemesis Duel: where the Nemesis's own run ended; getting past it wins.</summary>
        public int DuelTargetX { get; } = -1;
        public bool DuelWon => Options.Duel && DuelTargetX >= 0 && MaxX > DuelTargetX;

        public int RunIndex { get; }
        public int SpeedPct { get; }
        public int GhostsAtStart { get; }
        public Course Course => _course;

        /// <summary>The next tick to be stepped; also the number of ticks stepped so far.</summary>
        public int Tick { get; private set; }

        public int MaxX { get; private set; }
        public int NearMisses { get; private set; }
        public DeathCause Cause { get; private set; }
        public GhostRecord Killer { get; private set; }
        public bool ContinueUsed { get; private set; }

        /// <summary>Lives held now (1-UP, a user revision): spent automatically on death.</summary>
        public int Lives { get; private set; }

        /// <summary>Lives earned and spent so far this run, for the HUD and effects.</summary>
        public int LivesEarned { get; private set; }
        public int LivesUsed { get; private set; }

        /// <summary>The score at which the next life is awarded.</summary>
        public int NextLifeScore { get; private set; }

        // Achievement facts (skins addendum), kept as the run happens so nothing is re-derived later.
        /// <summary>Ghosts the player has overtaken this run.</summary>
        public int GhostsPassed { get; private set; }

        /// <summary>Times a ghost touched the player, even if a life or Continue then undid it.</summary>
        public int GhostHits { get; private set; }

        /// <summary>Two near-misses within 2 s, and three within 3 s, at any point in the run.</summary>
        public bool NearMissPair { get; private set; }
        public bool NearMissTriple { get; private set; }
        public bool BanishUsed { get; internal set; }
        public bool Committed { get; internal set; }

        /// <summary>The skin the player wore this run, stored on its ghost (skins addendum). Cosmetic only.</summary>
        public string SkinId { get; set; } = "";

        /// <summary>The accessory worn (cosmetic only), kept on the ghost like the skin.</summary>
        public string AccessoryId { get; set; } = "";

        public ref readonly RunnerState Player => ref _player;
        public GhostManager Ghosts => _ghosts;
        public bool IsDead => !_player.Alive;
        public int DeathTick => _recorder.DeathTick;
        public int Distance => ScoreRules.Distance(MaxX);
        public int Score => ScoreRules.Score(Distance, NearMisses, GhostsAtStart, BountyClaimed ? 1 : 0);

        /// <param name="held">Finger down at this tick (variable jump). Default true: a tap holds, as before.</param>
        public void Step(bool tap, bool held = true)
        {
            if (!_player.Alive) return;

            // Only a tap presses, so held follows the same rule the ghost replay uses; a lift is recorded as a release.
            bool down = held && (tap || _held);
            if ((tap || _held) && !down) _recorder.RecordRelease(Tick);
            _held = down;

            // Sampled before the step so ghosts and hazards share the same ReviveInvulnTicks window.
            bool invulnerable = _player.Invuln > 0;

            if (tap) _recorder.RecordTap(Tick);
            Runner.Step(ref _player, tap, _course, Tick, _speed, _cfg, down);
            _ghosts.Step(_course, Tick, _ghostCfg);
            if (_phantom != null) StepCopycat(tap, down);
            if (_player.X > MaxX) MaxX = _player.X;
            if (_player.Alive) CountOvertakes();

            if (!_player.Alive)
                Die(_player.Y < _cfg.DeathFloorY ? DeathCause.Fall : DeathCause.Hazard, null);
            // tech spec 7: ghosts are harmless for the first second; Crowd Control keeps them harmless until 300 m.
            else if (Tick >= _cfg.GhostGraceTicks && (Options.Twist != Twist.CrowdControl || Distance >= WeeklyTwists.CrowdControlMetres))
                TouchGhosts(invulnerable);
            if (_player.Alive && _copycat.Active) TouchCopycat(invulnerable);

            Tick++;

            AwardLives();
            if (_ghostSafeTicks > 0) _ghostSafeTicks--;
            if (!_player.Alive && HasLifeToSpend) SpendLife();
        }

        // Copycat twist: appears at CopyFrom, replays the inputs from DelayTicks before (only those since it
        // appeared), and leaves at EndTick, when the next one is planned. Mirrors CopycatPlanner's search exactly.
        void StepCopycat(bool tap, bool down)
        {
            _tapLog[Tick & 63] = tap;
            _downLog[Tick & 63] = down;
            if (!_copycat.Planned || Tick < _copycat.CopyFrom) return;
            _copycat.Active = true;
            int from = Tick - Echo.Core.Rules.Copycat.DelayTicks;
            bool copied = from >= _copycat.CopyFrom;
            Echo.Core.Rules.Copycat.Step(ref _copycat, copied && _tapLog[from & 63], copied && _downLog[from & 63], _phantom, Tick, _speed, _cfg);
            if (Tick + 1 >= _copycat.EndTick) PlanCopycat(_copycat.Chunk + Echo.Core.Rules.Copycat.EveryChunks);
        }

        void TouchCopycat(bool invulnerable)
        {
            if (_copycat.Passed || Tick < _cfg.GhostGraceTicks) return;
            if (!invulnerable && !GhostsSafe && Runner.PlayerBox(_player, _cfg).Overlaps(Echo.Core.Rules.Copycat.Box(_copycat, _cfg))
                && !ShieldTakesIt())
            {
                _player.Alive = false;
                Die(DeathCause.Copycat, null);
                return;
            }
            if (_copycat.X >= _player.X) return;
            // Getting past it counts as a near-miss (user design).
            _copycat.Passed = true;
            CopycatsPassed++;
            NearMisses++;
            NoteNearMiss();
        }

        // The next encounter from fromChunk on, timed from where the player is now (a revive moves them back).
        void PlanCopycat(int fromChunk)
        {
            int offset = _player.X - Tick * _speed;
            if (!CopycatPlanner.PlanFrom(_course, _phantom, _cfg, _speed, offset, Tick, fromChunk, _ghosts, out _copycat))
                _copycat = default;
        }

        // A revive moves the player back, so the copycat in play leaves and the next is planned from there.
        void ResetCopycat()
        {
            if (_phantom == null) return;
            PlanCopycat(Math.Max(_course.ChunkIndexAt(_player.X) + 2, _copycat.Chunk + 1));
        }

        // Ghost Bounties: with 3 or more ghosts, one seeded pick that is neither the Nemesis nor made harmless.
        int PickBounty(GhostSet ghosts, uint seed, int runIndex)
        {
            if (ghosts.Count < 3) return -1;
            GhostRecord nemesis = ghosts.Nemesis;
            int eligible = 0;
            for (int i = 0; i < _ghosts.Count; i++)
                if (_ghosts.RecordAt(i) != nemesis && !_ghosts.ReplayAt(i).Harmless) eligible++;
            if (eligible == 0) return -1;
            var rng = new Xorshift32(seed ^ unchecked((uint)runIndex * 2654435761u) | 1u);
            int pick = rng.NextInt(eligible);
            for (int i = 0; i < _ghosts.Count; i++)
            {
                if (_ghosts.RecordAt(i) == nemesis || _ghosts.ReplayAt(i).Harmless) continue;
                if (pick-- == 0) return i;
            }
            return -1;
        }

        void CountOvertakes()
        {
            for (int i = 0; i < _ghosts.Count && i < _passed.Length; i++)
            {
                if (_passed[i] || !_ghosts.IsActive(i) || _ghosts.ReplayAt(i).State.X >= _player.X) continue;
                _passed[i] = true;
                GhostsPassed++;
            }
        }

        // Keeps the two previous near-miss ticks: enough to spot 2 in 2 s and 3 in 3 s.
        void NoteNearMiss()
        {
            _nearMissCount++;
            if (_nearMissCount >= 2 && Tick - _lastNearMiss <= 2 * _cfg.TickRate) NearMissPair = true;
            if (_nearMissCount >= 3 && Tick - _nearMissBefore <= 3 * _cfg.TickRate) NearMissTriple = true;
            _nearMissBefore = _lastNearMiss;
            _lastNearMiss = Tick;
        }

        // Every LifeEveryScore points earns a life; at the cap the milestone passes without one.
        void AwardLives()
        {
            if (Options.OneLife) return;   // modes doc: one life, so no 1-UPs
            while (Score >= NextLifeScore)
            {
                if (Lives < _cfg.MaxLives)
                {
                    Lives++;
                    LivesEarned++;
                }
                NextLifeScore += _cfg.LifeEveryScore;
            }
        }

        // 1-UP: the same revive as Continue (recorded, so ghosts replay it) without the ad or using up Continue.
        void SpendLife()
        {
            // Lives earned in the run go first, so an Extra 1-UP is only spent when nothing else can save the run.
            if (Lives > 0) Lives--;
            else ExtraLifeUsed = true;
            LivesUsed++;
            _recorder.RecordRevive(Tick);
            ReviveAndHoldGhosts();
            Cause = DeathCause.None;
            Killer = null;
            ResetCopycat();
        }

        void TouchGhosts(bool invulnerable)
        {
            Aabb playerBox = Runner.PlayerBox(_player, _cfg);
            if (!invulnerable)
            {
                int k = GhostsSafe ? -1 : _ghosts.FirstOverlapping(playerBox, _ghostCfg);
                if (k >= 0 && !ShieldTakesIt())
                {
                    GhostRecord killer = _ghosts.RecordAt(k);
                    killer.Kills++;
                    killer.LastKillRun = RunIndex;
                    GhostHits++;
                    _player.Alive = false;
                    Die(DeathCause.Ghost, killer);
                    return;
                }
            }
            int near = _ghosts.MarkNearMisses(playerBox, _ghostCfg);
            if (BountyIndex >= 0 && !BountyClaimed && _ghosts.ReplayAt(BountyIndex).NearMissed) BountyClaimed = true;
            NearMisses += near;
            for (int n = 0; n < near; n++) NoteNearMiss();
        }

        void Die(DeathCause cause, GhostRecord killer)
        {
            Cause = cause;
            Killer = killer;
            _recorder.RecordDeath(Tick);
        }

        /// <summary>Continue (tech spec 7). Eligibility and the rewarded ad are checked by the caller via RunRules.</summary>
        public void Revive()
        {
            if (_player.Alive) throw new InvalidOperationException("can only revive a dead runner");
            if (ContinueUsed || BanishUsed || Committed) throw new InvalidOperationException("continue is no longer available");
            ContinueUsed = true;
            ContinueTick = Tick;
            _recorder.RecordRevive(Tick);
            ReviveAndHoldGhosts();
            Cause = DeathCause.None;
            Killer = null;
            ResetCopycat();
        }

        // The revive puts the player back at the last safe x; the ghosts' clocks go back by the same running time.
        void ReviveAndHoldGhosts()
        {
            int before = _player.X;
            Runner.Revive(ref _player, _cfg);
            int back = before - _player.X;
            if (back > 0) _ghosts.HoldBack((back + _speed - 1) / _speed, Tick, _course, _ghostCfg);
        }

        public GhostRecord ToGhostRecord()
        {
            GhostRecord record = _recorder.ToRecord(RunIndex, SpeedPct);
            record.SkinId = SkinId ?? "";
            record.AccessoryId = AccessoryId ?? "";
            record.DeathX = IsDead ? _player.X : -1;
            record.Cause = Cause;
            record.KillerRunIndex = Killer?.RunIndex ?? 0;
            return record;
        }

    }
}
