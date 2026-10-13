using System;
using System.Collections.Generic;

namespace Echo.Core.Courses
{
    /// <summary>Turns chunk JSON (tech spec 6) into a validated <see cref="Chunk"/>.</summary>
    public static class ChunkParser
    {
        // tech spec 6: chunks start and end with at least 1 unit of hazard-free floor, so they join in any order.
        public const int EdgeClearance = 1000;

        static readonly string[] ChunkKeys = { "id", "tier", "length", "gaps", "spikes", "ceilings", "movers" };
        static readonly string[] GapKeys = { "x", "w" };
        static readonly string[] SpikeKeys = { "x", "w", "h" };
        static readonly string[] CeilingKeys = { "x", "w", "y" };
        static readonly string[] MoverKeys = { "x", "w", "h", "yMin", "yMax", "period", "phase" };

        public static Chunk Parse(string json)
        {
            JsonNode root = JsonReader.Parse(json);
            if (root.Kind != JsonKind.Object) throw new ChunkFormatException("chunk must be a JSON object");

            string id = RequireString(root, "id", "chunk");
            string where = $"chunk \"{id}\"";
            RejectUnknownKeys(root, ChunkKeys, where);

            int tier = RequireInt(root, "tier", where);
            if (tier < 0 || tier > TierSchedule.MaxTier)
                throw new ChunkFormatException($"{where}: tier must be 0..{TierSchedule.MaxTier}, got {tier}");

            int length = RequireInt(root, "length", where);
            if (length < 2 * EdgeClearance)
                throw new ChunkFormatException($"{where}: length {length} is shorter than both edge clearances");

            var gaps = ReadArray(root, "gaps", where, GapKeys, (n, w) =>
                new Gap(RequireInt(n, "x", w), RequirePositive(n, "w", w)));
            var spikes = ReadArray(root, "spikes", where, SpikeKeys, (n, w) =>
                new Spike(RequireInt(n, "x", w), RequirePositive(n, "w", w), RequirePositive(n, "h", w)));
            var ceilings = ReadArray(root, "ceilings", where, CeilingKeys, (n, w) =>
                new Ceiling(RequireInt(n, "x", w), RequirePositive(n, "w", w), RequirePositive(n, "y", w)));
            var movers = ReadArray(root, "movers", where, MoverKeys, ReadMover);

            foreach (var g in gaps) CheckSpan(g.X, g.W, length, where, "gap");
            foreach (var s in spikes) CheckSpan(s.X, s.W, length, where, "spike");
            foreach (var c in ceilings) CheckSpan(c.X, c.W, length, where, "ceiling");
            foreach (var m in movers) CheckSpan(m.X, m.W, length, where, "mover");

            return new Chunk(id, tier, length, gaps, spikes, ceilings, movers);
        }

        static Mover ReadMover(JsonNode n, string where)
        {
            var m = new Mover(
                RequireInt(n, "x", where), RequirePositive(n, "w", where), RequirePositive(n, "h", where),
                RequireInt(n, "yMin", where), RequireInt(n, "yMax", where),
                RequireInt(n, "period", where), RequireInt(n, "phase", where));
            if (m.YMax < m.YMin) throw new ChunkFormatException($"{where}: yMax is below yMin");
            if (m.Period < 2) throw new ChunkFormatException($"{where}: period must be at least 2 ticks");
            if (m.Phase < 0) throw new ChunkFormatException($"{where}: phase must not be negative");
            return m;
        }

        static void CheckSpan(int x, int w, int length, string where, string what)
        {
            if (x < EdgeClearance || x + w > length - EdgeClearance)
                throw new ChunkFormatException(
                    $"{where}: {what} at x={x} w={w} breaks the {EdgeClearance} sub-unit clear floor at either end");
        }

        static T[] ReadArray<T>(JsonNode root, string key, string where, string[] keys, Func<JsonNode, string, T> read)
        {
            JsonNode arr = root.Get(key);
            if (arr == null || arr.Kind == JsonKind.Null) return Array.Empty<T>();
            if (arr.Kind != JsonKind.Array) throw new ChunkFormatException($"{where}: \"{key}\" must be an array");
            var result = new T[arr.Items.Count];
            for (int i = 0; i < result.Length; i++)
            {
                JsonNode item = arr.Items[i];
                string itemWhere = $"{where} {key}[{i}]";
                if (item.Kind != JsonKind.Object) throw new ChunkFormatException($"{itemWhere}: must be an object");
                RejectUnknownKeys(item, keys, itemWhere);
                result[i] = read(item, itemWhere);
            }
            return result;
        }

        static void RejectUnknownKeys(JsonNode obj, string[] allowed, string where)
        {
            foreach (string k in obj.Keys)
                if (Array.IndexOf(allowed, k) < 0)
                    throw new ChunkFormatException($"{where}: unknown key \"{k}\"");
        }

        static int RequireInt(JsonNode obj, string key, string where)
        {
            JsonNode n = obj.Get(key);
            if (n == null || n.Kind != JsonKind.Int)
                throw new ChunkFormatException($"{where}: \"{key}\" must be an integer");
            return n.Int;
        }

        static int RequirePositive(JsonNode obj, string key, string where)
        {
            int v = RequireInt(obj, key, where);
            if (v <= 0) throw new ChunkFormatException($"{where}: \"{key}\" must be positive");
            return v;
        }

        static string RequireString(JsonNode obj, string key, string where)
        {
            JsonNode n = obj.Get(key);
            if (n == null || n.Kind != JsonKind.String || string.IsNullOrEmpty(n.Str))
                throw new ChunkFormatException($"{where}: \"{key}\" must be a non-empty string");
            return n.Str;
        }

        public static List<Chunk> ParseAll(IEnumerable<string> jsonFiles)
        {
            var list = new List<Chunk>();
            foreach (string json in jsonFiles) list.Add(Parse(json));
            return list;
        }
    }
}
