using Echo.Game.Clips;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>The clip editor's timeline rules: defaults at the end, GIF 10 s, video 60 s, at least 1 s.</summary>
    public class ClipRangeTests
    {
        [Test]
        public void Defaults_AreTheFinalMoments_EndingAfterTheShatter()
        {
            ClipRange gif = ClipRange.Default(ClipFormat.Gif, 6000);
            Assert.AreEqual(6000 + ClipRange.TailTicks, gif.End);
            Assert.AreEqual(5f, gif.Seconds);
            ClipRange video = ClipRange.Default(ClipFormat.Video, 6000);
            Assert.AreEqual(15f, video.Seconds);
            Assert.AreEqual(0, ClipRange.Default(ClipFormat.Video, 200).Start, "a short run starts at the start");
        }

        [Test]
        public void AGif_IsAtMostTenSeconds_TheOtherHandleFollows()
        {
            ClipRange r = ClipRange.Clamp(1000, 3000, ClipFormat.Gif, 6000, movingEnd: true);
            Assert.AreEqual(3000, r.End, "the handle being moved stays put");
            Assert.AreEqual(3000 - ClipRange.GifMaxTicks, r.Start);
            r = ClipRange.Clamp(1000, 3000, ClipFormat.Gif, 6000, movingEnd: false);
            Assert.AreEqual(1000, r.Start);
            Assert.AreEqual(1000 + ClipRange.GifMaxTicks, r.End);
        }

        [Test]
        public void AVideo_CanBeAMinute()
        {
            ClipRange r = ClipRange.Clamp(0, 5000, ClipFormat.Video, 6000, movingEnd: true);
            Assert.AreEqual(5000 - ClipRange.VideoMaxTicks, r.Start);
            Assert.AreEqual(60f, r.Seconds);
        }

        [Test]
        public void AClip_IsAtLeastASecond_AndStaysOnTheTimeline()
        {
            ClipRange r = ClipRange.Clamp(500, 510, ClipFormat.Gif, 6000, movingEnd: false);
            Assert.AreEqual(ClipRange.MinTicks, r.Ticks);
            r = ClipRange.Clamp(-50, 9999, ClipFormat.Video, 3000, movingEnd: true);
            Assert.AreEqual(ClipRange.Length(3000), r.End);
            Assert.GreaterOrEqual(r.Start, 0);
            r = ClipRange.Clamp(ClipRange.Length(3000), ClipRange.Length(3000), ClipFormat.Gif, 3000, movingEnd: false);
            Assert.AreEqual(ClipRange.MinTicks, r.Ticks, "a start dragged to the very end leaves a second before it");
            Assert.AreEqual(ClipRange.Length(3000), r.End);
        }

        [Test]
        public void TimesRead_MinutesSecondsTenths()
        {
            Assert.AreEqual("0:00.0", ClipRange.Time(0));
            Assert.AreEqual("1:23.4", ClipRange.Time(83 * 60 + 24));
        }
    }
}
