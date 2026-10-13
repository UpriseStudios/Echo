using Echo.Game.Views;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>Daily Haunt: the single name label sits on the next leader ghost ahead of the player.</summary>
    public class LeaderLabelTests
    {
        [Test]
        public void TheLabel_GoesOnTheNearestNamedLeaderAhead_WhateverThePoolOrder()
        {
            var x = new[] { 40f, 12f, 25f, 8f, 13f };
            var named = new[] { true, true, true, true, false };
            Assert.AreEqual(1, LeaderLabel.NextAhead(10f, x, named, 5), "12 is next; 8 is passed, 13 is not a leader");
            Assert.AreEqual(2, LeaderLabel.NextAhead(12f, x, named, 5), "level with one is passing it: on to the next");
            Assert.AreEqual(0, LeaderLabel.NextAhead(30f, x, named, 5));
            Assert.AreEqual(-1, LeaderLabel.NextAhead(50f, x, named, 5), "every leader passed: no label");
            Assert.AreEqual(3, LeaderLabel.NextAhead(0f, x, named, 5));
            Assert.AreEqual(-1, LeaderLabel.NextAhead(0f, x, named, 0), "no ghosts");
        }
    }
}
