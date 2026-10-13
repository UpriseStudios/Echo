using System.Text;

namespace Echo.Core.Courses
{
    /// <summary>
    /// Recursive-descent JSON reader for chunk files. Numbers must be 32-bit integers;
    /// fractions and exponents are rejected because Core has no floats (tech spec 3).
    /// </summary>
    internal sealed class JsonReader
    {
        readonly string _src;
        int _pos;

        JsonReader(string src)
        {
            _src = src;
        }

        public static JsonNode Parse(string json)
        {
            if (json == null) throw new ChunkFormatException("JSON is null");
            var r = new JsonReader(json);
            JsonNode root = r.ReadValue();
            r.SkipWhitespace();
            if (r._pos != json.Length) throw r.Error("unexpected trailing content");
            return root;
        }

        JsonNode ReadValue()
        {
            SkipWhitespace();
            if (_pos >= _src.Length) throw Error("unexpected end of input");
            char c = _src[_pos];
            switch (c)
            {
                case '{': return ReadObject();
                case '[': return ReadArray();
                case '"': return new JsonNode { Kind = JsonKind.String, Str = ReadString() };
                case 't': ExpectWord("true"); return new JsonNode { Kind = JsonKind.Bool, Bool = true };
                case 'f': ExpectWord("false"); return new JsonNode { Kind = JsonKind.Bool, Bool = false };
                case 'n': ExpectWord("null"); return new JsonNode { Kind = JsonKind.Null };
            }
            if (c == '-' || (c >= '0' && c <= '9')) return ReadInt();
            throw Error($"unexpected character '{c}'");
        }

        JsonNode ReadObject()
        {
            var node = new JsonNode { Kind = JsonKind.Object };
            _pos++;
            SkipWhitespace();
            if (Peek() == '}') { _pos++; return node; }
            while (true)
            {
                SkipWhitespace();
                if (Peek() != '"') throw Error("expected property name");
                string key = ReadString();
                if (node.Get(key) != null) throw Error($"duplicate key \"{key}\"");
                SkipWhitespace();
                Expect(':');
                node.Keys.Add(key);
                node.Items.Add(ReadValue());
                SkipWhitespace();
                if (Peek() == ',') { _pos++; continue; }
                Expect('}');
                return node;
            }
        }

        JsonNode ReadArray()
        {
            var node = new JsonNode { Kind = JsonKind.Array };
            _pos++;
            SkipWhitespace();
            if (Peek() == ']') { _pos++; return node; }
            while (true)
            {
                node.Items.Add(ReadValue());
                SkipWhitespace();
                if (Peek() == ',') { _pos++; continue; }
                Expect(']');
                return node;
            }
        }

        string ReadString()
        {
            _pos++;
            var sb = new StringBuilder();
            while (true)
            {
                if (_pos >= _src.Length) throw Error("unterminated string");
                char c = _src[_pos++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (_pos >= _src.Length) throw Error("unterminated escape");
                char e = _src[_pos++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u': sb.Append(ReadHex4()); break;
                    default: throw Error($"invalid escape '\\{e}'");
                }
            }
        }

        char ReadHex4()
        {
            if (_pos + 4 > _src.Length) throw Error("truncated \\u escape");
            int v = 0;
            for (int i = 0; i < 4; i++)
            {
                char h = _src[_pos++];
                int d = h >= '0' && h <= '9' ? h - '0'
                      : h >= 'a' && h <= 'f' ? h - 'a' + 10
                      : h >= 'A' && h <= 'F' ? h - 'A' + 10
                      : -1;
                if (d < 0) throw Error("invalid \\u escape");
                v = v * 16 + d;
            }
            return (char)v;
        }

        JsonNode ReadInt()
        {
            int start = _pos;
            bool negative = Peek() == '-';
            if (negative) _pos++;
            if (_pos >= _src.Length || _src[_pos] < '0' || _src[_pos] > '9') throw Error("expected digit");
            long value = 0;
            while (_pos < _src.Length && _src[_pos] >= '0' && _src[_pos] <= '9')
            {
                value = value * 10 + (_src[_pos++] - '0');
                if (value > (long)int.MaxValue + 1) throw ErrorAt(start, "integer out of range");
            }
            if (_pos < _src.Length && (_src[_pos] == '.' || _src[_pos] == 'e' || _src[_pos] == 'E'))
                throw ErrorAt(start, "non-integer number; chunk values are integer sub-units");
            if (negative) value = -value;
            if (value > int.MaxValue || value < int.MinValue) throw ErrorAt(start, "integer out of range");
            return new JsonNode { Kind = JsonKind.Int, Int = (int)value };
        }

        void ExpectWord(string word)
        {
            if (string.CompareOrdinal(_src, _pos, word, 0, word.Length) != 0) throw Error($"expected '{word}'");
            _pos += word.Length;
        }

        void Expect(char c)
        {
            if (Peek() != c) throw Error($"expected '{c}'");
            _pos++;
        }

        char Peek() => _pos < _src.Length ? _src[_pos] : '\0';

        void SkipWhitespace()
        {
            while (_pos < _src.Length)
            {
                char c = _src[_pos];
                if (c != ' ' && c != '\t' && c != '\n' && c != '\r' && c != '﻿') return;
                _pos++;
            }
        }

        ChunkFormatException Error(string message) => ErrorAt(_pos, message);

        ChunkFormatException ErrorAt(int pos, string message) =>
            new ChunkFormatException($"JSON error at offset {pos}: {message}");
    }
}
