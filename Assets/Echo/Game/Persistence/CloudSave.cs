using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;

namespace Echo.Game.Persistence
{
    /// <summary>
    /// The save as Google Play Games keeps it (Play's Level Up guidelines: cloud save with a conflict policy): the
    /// whole save document, gzipped. Saves never merge; the one with more runs played wins (then the higher best),
    /// and the player is asked before a cloud save replaces this device's.
    /// </summary>
    public static class CloudSave
    {
        public static byte[] Pack(SaveData save)
        {
            byte[] json = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(save));
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal)) gzip.Write(json, 0, json.Length);
            return output.ToArray();
        }

        /// <summary>The save in <paramref name="data"/>, or null when it's unreadable or from a newer version of Echo.</summary>
        public static SaveData Unpack(byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            try
            {
                using var input = new GZipStream(new MemoryStream(data), CompressionMode.Decompress);
                using var reader = new StreamReader(input, Encoding.UTF8);
                var save = JsonConvert.DeserializeObject<SaveData>(reader.ReadToEnd());
                return save != null && save.Schema >= 1 && save.Schema <= SaveData.CurrentSchema ? save : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The conflict policy: the cloud's save is further along than this device's.</summary>
        public static bool CloudIsAhead(SaveData device, SaveData cloud)
        {
            int deviceRuns = device.Stats?.TotalRuns ?? 0, cloudRuns = cloud.Stats?.TotalRuns ?? 0;
            return cloudRuns > deviceRuns || (cloudRuns == deviceRuns && cloud.Best > device.Best);
        }

        /// <summary>"312 runs, best 1,240", for the restore question and the Saved Game's description.</summary>
        public static string Describe(SaveData save) => $"{save.Stats?.TotalRuns ?? 0:N0} runs, best {save.Best:N0}";
    }
}
