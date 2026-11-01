using System;
using System.Collections.Generic;
using System.Globalization;

namespace GodGame.Sim.Rules.Json
{
    public enum JsonKind
    {
        Null,
        Bool,
        Integer,
        String,
        Array,
        Object,
    }

    /// <summary>A rules data file is malformed or breaks its schema; the message says where.</summary>
    public sealed class DataFormatException : FormatException
    {
        public DataFormatException(string message)
            : base(message) { }
    }

    /// <summary>
    /// A parsed JSON value. Numbers are 64-bit integers only: rules data has no floats (data rules), so decimals
    /// arrive as strings. Object members keep file order for error messages; lookups go through a dictionary, which
    /// is never iterated, so nothing here depends on hash order.
    /// </summary>
    public sealed class JsonValue
    {
        private readonly long _integer;
        private readonly string? _string;
        private readonly List<JsonValue>? _items;
        private readonly List<KeyValuePair<string, JsonValue>>? _members;
        private readonly Dictionary<string, JsonValue>? _lookup;

        private JsonValue(JsonKind kind, string path, long integer = 0, string? text = null)
        {
            Kind = kind;
            Path = path;
            _integer = integer;
            _string = text;
            if (kind == JsonKind.Array)
            {
                _items = new List<JsonValue>();
            }
            else if (kind == JsonKind.Object)
            {
                _members = new List<KeyValuePair<string, JsonValue>>();
                _lookup = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
            }
        }

        public JsonKind Kind { get; }

        /// <summary>Where the value sits in its file, like <c>unitTypes[2].health</c>, for error messages.</summary>
        public string Path { get; }

        internal static JsonValue Null(string path) => new JsonValue(JsonKind.Null, path);

        internal static JsonValue Bool(string path, bool value) =>
            new JsonValue(JsonKind.Bool, path, value ? 1 : 0);

        internal static JsonValue Integer(string path, long value) =>
            new JsonValue(JsonKind.Integer, path, value);

        internal static JsonValue String(string path, string value) =>
            new JsonValue(JsonKind.String, path, 0, value);

        internal static JsonValue NewArray(string path) => new JsonValue(JsonKind.Array, path);

        internal static JsonValue NewObject(string path) => new JsonValue(JsonKind.Object, path);

        internal void AddItem(JsonValue item) => _items!.Add(item);

        internal bool TryAddMember(string key, JsonValue value)
        {
            if (_lookup!.ContainsKey(key))
            {
                return false;
            }
            _lookup.Add(key, value);
            _members!.Add(new KeyValuePair<string, JsonValue>(key, value));
            return true;
        }

        public bool AsBool => Expect(JsonKind.Bool)._integer != 0;

        public long AsInteger => Expect(JsonKind.Integer)._integer;

        public string AsString => Expect(JsonKind.String)._string!;

        public IReadOnlyList<JsonValue> Items => Expect(JsonKind.Array)._items!;

        /// <summary>Members in file order.</summary>
        public IReadOnlyList<KeyValuePair<string, JsonValue>> Members =>
            Expect(JsonKind.Object)._members!;

        public bool Has(string key) => Expect(JsonKind.Object)._lookup!.ContainsKey(key);

        public JsonValue this[string key] =>
            Expect(JsonKind.Object)._lookup!.TryGetValue(key, out var value)
                ? value
                : throw new DataFormatException(Path + ": missing \"" + key + "\".");

        public int Int(string key, int min = int.MinValue, int max = int.MaxValue)
        {
            JsonValue value = this[key];
            long n = value.AsInteger;
            if (n < min || n > max)
            {
                throw new DataFormatException(
                    value.Path
                        + ": "
                        + n.ToString(CultureInfo.InvariantCulture)
                        + " is outside "
                        + min.ToString(CultureInfo.InvariantCulture)
                        + " to "
                        + max.ToString(CultureInfo.InvariantCulture)
                        + "."
                );
            }
            return (int)n;
        }

        public string Str(string key) => this[key].AsString;

        public IReadOnlyList<JsonValue> Array(string key) => this[key].Items;

        /// <summary>Rejects members the schema doesn't know, so a typo can't silently fall back to a default.</summary>
        public void OnlyKeys(params string[] allowed)
        {
            foreach (var member in Members)
            {
                if (System.Array.IndexOf(allowed, member.Key) < 0)
                {
                    throw new DataFormatException(Path + ": unknown \"" + member.Key + "\".");
                }
            }
        }

        private JsonValue Expect(JsonKind kind)
        {
            if (Kind != kind)
            {
                throw new DataFormatException(
                    Path + ": expected " + kind + ", found " + Kind + "."
                );
            }
            return this;
        }
    }
}
