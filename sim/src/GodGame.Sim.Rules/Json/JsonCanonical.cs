using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GodGame.Sim.Rules.Json
{
    /// <summary>
    /// The canonical form of a JSON value: no whitespace, object keys sorted by ordinal comparison, integers in
    /// invariant decimal, strings with a fixed escaping, and no <c>$comment</c> members anywhere. Two files that mean
    /// the same thing give the same bytes, so hashes over this form ignore formatting, key order and comments. The
    /// output is pure ASCII (everything from 0x7F up is a lowercase \uXXXX escape), so it never depends on a text
    /// encoder.
    /// </summary>
    public static class JsonCanonical
    {
        public const string CommentKey = "$comment";

        public static byte[] ToUtf8(JsonValue value) => Encoding.ASCII.GetBytes(ToText(value));

        public static string ToText(JsonValue value)
        {
            var builder = new StringBuilder();
            Write(builder, value);
            return builder.ToString();
        }

        private static void Write(StringBuilder builder, JsonValue value)
        {
            switch (value.Kind)
            {
                case JsonKind.Null:
                    builder.Append("null");
                    break;
                case JsonKind.Bool:
                    builder.Append(value.AsBool ? "true" : "false");
                    break;
                case JsonKind.Integer:
                    builder.Append(value.AsInteger.ToString(CultureInfo.InvariantCulture));
                    break;
                case JsonKind.String:
                    WriteString(builder, value.AsString);
                    break;
                case JsonKind.Array:
                    builder.Append('[');
                    for (int i = 0; i < value.Items.Count; i++)
                    {
                        if (i > 0)
                        {
                            builder.Append(',');
                        }
                        Write(builder, value.Items[i]);
                    }
                    builder.Append(']');
                    break;
                case JsonKind.Object:
                    var members = new List<KeyValuePair<string, JsonValue>>();
                    foreach (var member in value.Members)
                    {
                        if (!string.Equals(member.Key, CommentKey, StringComparison.Ordinal))
                        {
                            members.Add(member);
                        }
                    }
                    // Keys are unique, so the ordinal sort is total and the result is the same everywhere.
                    members.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
                    builder.Append('{');
                    for (int i = 0; i < members.Count; i++)
                    {
                        if (i > 0)
                        {
                            builder.Append(',');
                        }
                        WriteString(builder, members[i].Key);
                        builder.Append(':');
                        Write(builder, members[i].Value);
                    }
                    builder.Append('}');
                    break;
                default:
                    throw new InvalidOperationException("Unknown JSON kind " + value.Kind + ".");
            }
        }

        private static void WriteString(StringBuilder builder, string text)
        {
            builder.Append('"');
            foreach (char c in text)
            {
                if (c == '"' || c == '\\')
                {
                    builder.Append('\\').Append(c);
                }
                else if (c < 0x20 || c >= 0x7F)
                {
                    builder
                        .Append("\\u")
                        .Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                }
                else
                {
                    builder.Append(c);
                }
            }
            builder.Append('"');
        }
    }
}
