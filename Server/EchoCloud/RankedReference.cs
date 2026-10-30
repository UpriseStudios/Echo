using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace EchoCloud
{
    /// <summary>
    /// A reference run (modes doc, Ghost Duels): an opponent for an arena when no player is near enough, with the rating
    /// it stands for. Made by `ReplayProbe references` into RankedReferences.json, shipped inside the module.
    /// </summary>
    public sealed class RankedReference
    {
        public int Index { get; set; }
        public uint Seed { get; set; }
        public int Rating { get; set; }
        public int Distance { get; set; }
        public string Name { get; set; } = "";
        public string Packet { get; set; } = "";

        static List<RankedReference> _shipped;

        /// <summary>The references built into the module (none if the file is missing).</summary>
        public static IReadOnlyList<RankedReference> Shipped => _shipped ??= Load();

        static List<RankedReference> Load()
        {
            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EchoCloud.RankedReferences.json");
            if (stream == null) return new List<RankedReference>();
            using var reader = new StreamReader(stream);
            List<RankedReference> list = JsonSerializer.Deserialize<List<RankedReference>>(reader.ReadToEnd()) ?? new List<RankedReference>();
            for (int i = 0; i < list.Count; i++) list[i].Index = i;
            return list;
        }
    }
}
