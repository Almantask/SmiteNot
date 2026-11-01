using System.Globalization;
using System.Text;

namespace GodGame.Sim.Rules.Json
{
    /// <summary>
    /// A small strict JSON reader for rules, maps and plans (plan, section 3.2: vendored, no NuGet). It accepts
    /// standard JSON except numbers with a fraction or exponent, which rules data forbids; it rejects duplicate keys,
    /// trailing commas and anything after the top-level value. Culture never matters.
    /// </summary>
    public static class JsonReader
    {
        private const int MaxDepth = 64;

        public static JsonValue Parse(string text, string fileName = "json")
        {
            var parser = new Parser(text, fileName);
            parser.SkipWhitespace();
            JsonValue value = parser.ReadValue(fileName, 0);
            parser.SkipWhitespace();
            if (!parser.AtEnd)
            {
                throw parser.Error("unexpected text after the value");
            }
            return value;
        }

        private sealed class Parser
        {
            private readonly string _text;
            private readonly string _file;
            private int _pos;

            public Parser(string text, string file)
            {
                _text = text ?? throw new System.ArgumentNullException(nameof(text));
                _file = file;
                // A UTF-8 byte-order mark is harmless; skip it.
                if (_text.Length > 0 && _text[0] == '\uFEFF')
                {
                    _pos = 1;
                }
            }

            public bool AtEnd => _pos >= _text.Length;

            public JsonValue ReadValue(string path, int depth)
            {
                if (depth > MaxDepth)
                {
                    throw Error("nested too deeply");
                }
                if (AtEnd)
                {
                    throw Error("unexpected end of file");
                }
                char c = _text[_pos];
                switch (c)
                {
                    case '{':
                        return ReadObject(path, depth);
                    case '[':
                        return ReadArray(path, depth);
                    case '"':
                        return JsonValue.String(path, ReadString());
                    case 't':
                        Expect("true");
                        return JsonValue.Bool(path, true);
                    case 'f':
                        Expect("false");
                        return JsonValue.Bool(path, false);
                    case 'n':
                        Expect("null");
                        return JsonValue.Null(path);
                    default:
                        if (c == '-' || (c >= '0' && c <= '9'))
                        {
                            return JsonValue.Integer(path, ReadInteger());
                        }
                        throw Error("unexpected '" + c + "'");
                }
            }

            private JsonValue ReadObject(string path, int depth)
            {
                var obj = JsonValue.NewObject(path);
                _pos++; // {
                SkipWhitespace();
                if (Peek('}'))
                {
                    _pos++;
                    return obj;
                }
                while (true)
                {
                    SkipWhitespace();
                    if (!Peek('"'))
                    {
                        throw Error("expected a key");
                    }
                    string key = ReadString();
                    SkipWhitespace();
                    Consume(':');
                    SkipWhitespace();
                    var value = ReadValue(path + "." + key, depth + 1);
                    if (!obj.TryAddMember(key, value))
                    {
                        throw Error("duplicate key \"" + key + "\"");
                    }
                    SkipWhitespace();
                    if (Peek(','))
                    {
                        _pos++;
                        continue;
                    }
                    Consume('}');
                    return obj;
                }
            }

            private JsonValue ReadArray(string path, int depth)
            {
                var array = JsonValue.NewArray(path);
                _pos++; // [
                SkipWhitespace();
                if (Peek(']'))
                {
                    _pos++;
                    return array;
                }
                for (int index = 0; ; index++)
                {
                    SkipWhitespace();
                    array.AddItem(
                        ReadValue(
                            path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                            depth + 1
                        )
                    );
                    SkipWhitespace();
                    if (Peek(','))
                    {
                        _pos++;
                        continue;
                    }
                    Consume(']');
                    return array;
                }
            }

            private string ReadString()
            {
                _pos++; // opening quote
                var builder = new StringBuilder();
                while (true)
                {
                    if (AtEnd)
                    {
                        throw Error("unterminated string");
                    }
                    char c = _text[_pos++];
                    if (c == '"')
                    {
                        string result = builder.ToString();
                        RejectUnpairedSurrogates(result);
                        return result;
                    }
                    if (c < 0x20)
                    {
                        throw Error("control character in a string");
                    }
                    if (c != '\\')
                    {
                        builder.Append(c);
                        continue;
                    }
                    if (AtEnd)
                    {
                        throw Error("unterminated escape");
                    }
                    char e = _text[_pos++];
                    switch (e)
                    {
                        case '"':
                        case '\\':
                        case '/':
                            builder.Append(e);
                            break;
                        case 'b':
                            builder.Append('\b');
                            break;
                        case 'f':
                            builder.Append('\f');
                            break;
                        case 'n':
                            builder.Append('\n');
                            break;
                        case 'r':
                            builder.Append('\r');
                            break;
                        case 't':
                            builder.Append('\t');
                            break;
                        case 'u':
                            builder.Append((char)ReadHex4());
                            break;
                        default:
                            throw Error("unknown escape \\" + e);
                    }
                }
            }

            // An unpaired surrogate has no UTF-8 form: encoders replace it, so two different ids could hash alike.
            private void RejectUnpairedSurrogates(string text)
            {
                for (int i = 0; i < text.Length; i++)
                {
                    if (
                        char.IsHighSurrogate(text[i])
                        && i + 1 < text.Length
                        && char.IsLowSurrogate(text[i + 1])
                    )
                    {
                        i++;
                    }
                    else if (char.IsSurrogate(text[i]))
                    {
                        throw Error("unpaired surrogate in a string");
                    }
                }
            }

            private int ReadHex4()
            {
                if (_pos + 4 > _text.Length)
                {
                    throw Error("short \\u escape");
                }
                int value = 0;
                for (int i = 0; i < 4; i++)
                {
                    char h = _text[_pos++];
                    int digit =
                        h >= '0' && h <= '9' ? h - '0'
                        : h >= 'a' && h <= 'f' ? h - 'a' + 10
                        : h >= 'A' && h <= 'F' ? h - 'A' + 10
                        : -1;
                    if (digit < 0)
                    {
                        throw Error("bad hex digit in \\u escape");
                    }
                    value = value * 16 + digit;
                }
                return value;
            }

            private long ReadInteger()
            {
                int start = _pos;
                bool negative = Peek('-');
                if (negative)
                {
                    _pos++;
                }
                if (AtEnd || _text[_pos] < '0' || _text[_pos] > '9')
                {
                    throw Error("expected a digit");
                }
                if (
                    _text[_pos] == '0'
                    && _pos + 1 < _text.Length
                    && _text[_pos + 1] >= '0'
                    && _text[_pos + 1] <= '9'
                )
                {
                    throw Error("leading zero");
                }
                // Accumulate as a negative number so long.MinValue parses too.
                long value = 0;
                while (!AtEnd && _text[_pos] >= '0' && _text[_pos] <= '9')
                {
                    int digit = _text[_pos++] - '0';
                    if (value < (long.MinValue + digit) / 10)
                    {
                        throw Error("integer out of range");
                    }
                    value = value * 10 - digit;
                }
                if (!AtEnd && (_text[_pos] == '.' || _text[_pos] == 'e' || _text[_pos] == 'E'))
                {
                    _pos = start;
                    throw Error("numbers must be integers; write decimals as strings (\"1.25\")");
                }
                if (negative)
                {
                    return value;
                }
                if (value == long.MinValue)
                {
                    throw Error("integer out of range");
                }
                return -value;
            }

            public void SkipWhitespace()
            {
                while (!AtEnd)
                {
                    char c = _text[_pos];
                    if (c != ' ' && c != '\t' && c != '\n' && c != '\r')
                    {
                        return;
                    }
                    _pos++;
                }
            }

            private bool Peek(char c) => !AtEnd && _text[_pos] == c;

            private void Consume(char c)
            {
                if (!Peek(c))
                {
                    throw Error("expected '" + c + "'");
                }
                _pos++;
            }

            private void Expect(string word)
            {
                if (string.CompareOrdinal(_text, _pos, word, 0, word.Length) != 0)
                {
                    throw Error("expected " + word);
                }
                _pos += word.Length;
            }

            public DataFormatException Error(string what)
            {
                int line = 1;
                int column = 1;
                for (int i = 0; i < _pos && i < _text.Length; i++)
                {
                    if (_text[i] == '\n')
                    {
                        line++;
                        column = 1;
                    }
                    else
                    {
                        column++;
                    }
                }
                return new DataFormatException(
                    _file
                        + "("
                        + line.ToString(CultureInfo.InvariantCulture)
                        + ","
                        + column.ToString(CultureInfo.InvariantCulture)
                        + "): "
                        + what
                        + "."
                );
            }
        }
    }
}
