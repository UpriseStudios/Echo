using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Echo.Core.Courses;
using Echo.Core.Sim;

namespace Echo.Server
{
    /// <summary>
    /// The game's content as the server sees it: the chunk library embedded from Assets/Echo/Content/Chunks, the
    /// default sim config, and the content version they make. Built once and never changed, so it is safe to share
    /// across Cloud Code invocations on the same worker.
    /// </summary>
    public sealed class GameContent
    {
        static GameContent _shared;

        public ChunkLibrary Library { get; }
        public SimConfig Config { get; }
        public string ContentVersion { get; }

        GameContent(ChunkLibrary library, SimConfig config)
        {
            Library = library;
            Config = config;
            ContentVersion = Echo.Core.Courses.ContentVersion.Compute(library, config);
        }

        public static GameContent Shared => _shared ??= Load();

        static GameContent Load()
        {
            Assembly assembly = typeof(GameContent).Assembly;
            var json = new List<string>();
            foreach (string name in assembly.GetManifestResourceNames())
            {
                if (!name.StartsWith("chunks/") || !name.EndsWith(".json")) continue;
                using Stream stream = assembly.GetManifestResourceStream(name);
                using var reader = new StreamReader(stream);
                json.Add(reader.ReadToEnd());
            }
            return new GameContent(ChunkLibrary.FromJson(json), SimConfig.Default);
        }

        public Course CourseFor(uint seed) => new Course(Library, seed);
    }
}
