using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Echo.Game.Persistence
{
    /// <summary>
    /// Reads and writes the save file (tech spec 10): written to a temp file, then swapped in.
    /// A file that fails to load is renamed to .bak and the game starts fresh.
    /// </summary>
    public sealed class SaveSystem
    {
        public SaveSystem(string path)
        {
            FilePath = path;
        }

        public string FilePath { get; }
        public string BackupPath => FilePath + ".bak";

        /// <summary>Where a save from an older schema is kept, untouched, when it is first loaded.</summary>
        public string SchemaBackupPath(int schema) => FilePath + ".schema" + schema + ".bak";
        string TempPath => FilePath + ".tmp";

        /// <summary>The saved document, or null when there is none or it was unreadable (then backed up).</summary>
        public SaveData Load()
        {
            if (!File.Exists(FilePath)) return null;
            try
            {
                var data = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(FilePath));
                if (data == null) throw new InvalidDataException("empty save");
                // Older schemas load and SaveMapper migrates them; only an unknown (newer) schema is refused.
                if (data.Schema < 1 || data.Schema > SaveData.CurrentSchema) throw new InvalidDataException($"unknown schema {data.Schema}");
                // An older schema is kept as it was before the first save in the new one rewrites it (version 2.0
                // rule: never lose progress), next to the save as save.json.schema2.bak.
                if (data.Schema < SaveData.CurrentSchema)
                {
                    string kept = SchemaBackupPath(data.Schema);
                    if (!File.Exists(kept)) File.Copy(FilePath, kept);
                }
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] could not load {FilePath}; moved to .bak, starting fresh. {e.Message}");
                if (File.Exists(BackupPath)) File.Delete(BackupPath);
                File.Move(FilePath, BackupPath);
                return null;
            }
        }

        public void Save(SaveData data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(TempPath, JsonConvert.SerializeObject(data, Formatting.Indented));
            if (!File.Exists(FilePath))
            {
                File.Move(TempPath, FilePath);
                return;
            }
            try
            {
                File.Replace(TempPath, FilePath, null);
            }
            catch (PlatformNotSupportedException)
            {
                File.Delete(FilePath);
                File.Move(TempPath, FilePath);
            }
        }
    }
}
