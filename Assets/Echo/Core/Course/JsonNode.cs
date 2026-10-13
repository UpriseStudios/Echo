using System.Collections.Generic;

namespace Echo.Core.Courses
{
    /// <summary>Minimal JSON DOM. Object members keep source order so nothing depends on hash order.</summary>
    internal sealed class JsonNode
    {
        public JsonKind Kind;
        public int Int;
        public bool Bool;
        public string Str;
        public readonly List<string> Keys = new List<string>();
        public readonly List<JsonNode> Items = new List<JsonNode>();

        public JsonNode Get(string key)
        {
            for (int i = 0; i < Keys.Count; i++)
                if (Keys[i] == key) return Items[i];
            return null;
        }
    }
}
