using System.IO;
using System.Linq;
using Echo.Core.Courses;
using Echo.Core.Sim;
using UnityEngine;

namespace Echo.Tests.EditMode
{
    internal static class TestCourses
    {
        public const string FlatTier1 = @"{ ""id"": ""flat"", ""tier"": 1, ""length"": 10000 }";

        public static ChunkLibrary Library(params string[] json) => ChunkLibrary.FromJson(json);

        public static Course Flat() => new Course(Library(FlatTier1), 1);

        /// <summary>Tutorial chunk whose floor ends at x = 3000, then a 5-unit gap.</summary>
        public static Course Ledge() => new Course(Library(
            @"{ ""id"": ""ledge"", ""tier"": 0, ""length"": 10000, ""gaps"": [{ ""x"": 3000, ""w"": 5000 }] }",
            FlatTier1), 1);

        public static string ContentDir => Path.Combine(Application.dataPath, "Echo", "Content", "Chunks");

        public static ChunkLibrary Content() =>
            ChunkLibrary.FromJson(Directory.GetFiles(ContentDir, "*.json").Select(File.ReadAllText));

        /// <summary>Steps with no taps from <paramref name="tick"/> until <paramref name="until"/> (exclusive).</summary>
        public static void Idle(ref RunnerState s, Course course, ref int tick, int until) =>
            Idle(ref s, course, ref tick, until, SimConfig.Default);

        public static void Idle(ref RunnerState s, Course course, ref int tick, int until, in SimConfig cfg)
        {
            for (; tick < until; tick++) Runner.Step(ref s, false, course, tick, cfg.RunSpeedBase, cfg);
        }

        public static void StepOnce(ref RunnerState s, bool tap, Course course, ref int tick) =>
            StepOnce(ref s, tap, course, ref tick, SimConfig.Default);

        public static void StepOnce(ref RunnerState s, bool tap, Course course, ref int tick, in SimConfig cfg)
        {
            Runner.Step(ref s, tap, course, tick, cfg.RunSpeedBase, cfg);
            tick++;
        }
    }
}
