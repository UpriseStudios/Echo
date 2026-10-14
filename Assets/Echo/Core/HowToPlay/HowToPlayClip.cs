using System.Collections.Generic;
using Echo.Core.Courses;
using Echo.Core.Reels;

namespace Echo.Core.HowToPlay
{
    /// <summary>
    /// One how-to-play clip (a user revision): a short scripted run played back by the game's own replay, with captions
    /// at the moments that matter. Built by <see cref="HowToPlayDirector"/>; never saved.
    /// </summary>
    public sealed class HowToPlayClip
    {
        /// <summary>Which of the topics it belongs to (0-based) and that topic's name, e.g. "GHOSTS".</summary>
        public int Topic;
        public string TopicName = "";

        public Course Course;
        public Reel Reel;
        public readonly List<HowToPlayBeat> Beats = new List<HowToPlayBeat>();

        /// <summary>The run tick the clip ends on (it holds after a death until then).</summary>
        public int EndTick;

        /// <summary>The Ghost Lantern's paths are shown (a view-only power-up).</summary>
        public bool Lantern;

        /// <summary>The power-up buttons are shown on the floor.</summary>
        public bool PowerButtons;

        public HowToPlayBeat BeatAt(int tick)
        {
            HowToPlayBeat found = null;
            foreach (HowToPlayBeat b in Beats)
                if (b.ActiveAt(tick)) found = b;   // the latest-starting one wins where two overlap
            return found;
        }
    }
}
