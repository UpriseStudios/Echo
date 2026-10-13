using System.IO;
using System.Linq;
using Echo.Core.Courses;
using Echo.Core.Sim;
using UnityEditor;
using UnityEngine;

namespace Echo.Editor
{
    /// <summary>Runs ChunkValidator over Content/Chunks from the editor (tech spec 6).</summary>
    public static class ChunkValidatorMenu
    {
        [MenuItem("Echo/Validate Chunks")]
        public static void ValidateAll()
        {
            // Content must be clearable with the single jump; the double jump only forgives mistakes.
            var cfg = SimConfig.Default.WithAirJumps(0);
            int failed = 0;
            var files = Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json").OrderBy(f => f);
            foreach (string file in files)
            {
                Chunk chunk;
                try { chunk = ChunkParser.Parse(File.ReadAllText(file)); }
                catch (ChunkFormatException e)
                {
                    failed++;
                    Debug.LogError($"[ChunkValidator] {Path.GetFileName(file)}: {e.Message}");
                    continue;
                }
                var result = ChunkValidator.Validate(chunk, cfg);
                if (result.Clearable) continue;
                failed++;
                Debug.LogError($"[ChunkValidator] {chunk.Id}: {result}");
            }
            if (failed == 0) Debug.Log("[ChunkValidator] every chunk is clearable");
        }
    }
}
