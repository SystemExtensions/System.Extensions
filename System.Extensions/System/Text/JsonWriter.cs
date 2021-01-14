
namespace System.Text
{
    using System.IO;
    using System.Data;
    using System.Buffers;
    using System.Runtime.Serialization;
    using System.Runtime.InteropServices;
    using System.Reflection;
    using System.Linq.Expressions;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    public abstract class JsonWriter
    {
        #region abstract
        public abstract void WriteStartArray();
        public abstract void WriteEndArray();
        public abstract void WriteStartObject();
        public abstract void WriteEndObject();
        public abstract void WriteProperty(string name);
        public abstract void WriteString(string value);
        public abstract void WriteString(ReadOnlySpan<char> value);
        public abstract void WriteNumber(byte value);
        public abstract void WriteNumber(sbyte value);
        public abstract void WriteNumber(short value);
        public abstract void WriteNumber(ushort value);
        public abstract void WriteNumber(int value);
        public abstract void WriteNumber(uint value);
        public abstract void WriteNumber(long value);
        public abstract void WriteNumber(ulong value);
        public abstract void WriteNumber(float value);
        public abstract void WriteNumber(double value);
        public abstract void WriteNumber(decimal value);
        public abstract void WriteNumber(ReadOnlySpan<char> value);
        public abstract void WriteBoolean(bool value);
        public abstract void WriteNull();
        #endregion
        public static JsonWriter Create(BufferWriter<char> writer)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            return new BufferJsonWriter(writer);
        }
        public static JsonWriter Create(TextWriter writer)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            return new TextJsonWriter(writer);
        }
        public static JsonWriter CreateIndent(BufferWriter<char> writer, string indent, string newLine)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            return new IndentBufferJsonWriter(writer, indent, newLine);
        }
        public static JsonWriter CreateIndent(TextWriter writer, string indent, string newLine)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            return new IndentTextJsonWriter(writer, indent, newLine);
        }
        #region ToJson
        [ThreadStatic] private static BufferJsonWriter _BufferJsonWriter;
        [ThreadStatic] private static TextJsonWriter _TextJsonWriter;
        public static string ToJson<T>(T value)
        {
            var sb = StringExtensions.ThreadRent(out var disposable);
            try
            {
                ToJson(value, sb);
                return sb.ToString();
            }
            finally
            {
                disposable.Dispose();
            }
        }
        public static void ToJson<T>(T value, BufferWriter<char> writer)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            Register<T>(out var handler);
            if (handler == null)
                throw new NotSupportedException($"JsonWriter:{typeof(T)}");

            var jsonWriter = _BufferJsonWriter;
            if (jsonWriter == null)
            {
                jsonWriter = new BufferJsonWriter(writer);
            }
            else
            {
                _BufferJsonWriter = null;
                jsonWriter.Set(writer);
            }
            try
            {
                handler(value, jsonWriter);
            }
            finally
            {
                jsonWriter.Reset();
                _BufferJsonWriter = jsonWriter;
            }
        }
        public static void ToJson<T>(T value, TextWriter writer)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            Register<T>(out var handler);
            if (handler == null)
                throw new NotSupportedException($"JsonWriter:{typeof(T)}");

            var jsonWriter = _TextJsonWriter;
            if (jsonWriter == null)
            {
                jsonWriter = new TextJsonWriter(writer);
            }
            else
            {
                _TextJsonWriter = null;
                jsonWriter.Set(writer);
            }
            try
            {
                handler(value, jsonWriter);
            }
            finally
            {
                jsonWriter.Reset();
                _TextJsonWriter = jsonWriter;
            }
        }
        public static string ToJson(Type type, object value)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            var sb = StringExtensions.ThreadRent(out var disposable);
            try
            {
                ToJson(type, value, sb);
                return sb.ToString();
            }
            finally
            {
                disposable.Dispose();
            }
        }
        public static void ToJson(Type type, object value, BufferWriter<char> writer)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            Register(type, out var handler);
            if (handler == null)
                throw new NotSupportedException($"JsonWriter:{type}");

            var jsonWriter = _BufferJsonWriter;
            if (jsonWriter == null)
            {
                jsonWriter = new BufferJsonWriter(writer);
            }
            else
            {
                _BufferJsonWriter = null;
                jsonWriter.Set(writer);
            }
            try
            {
                handler(value, jsonWriter);
            }
            finally
            {
                jsonWriter.Reset();
                _BufferJsonWriter = jsonWriter;
            }
        }
        public static void ToJson(Type type, object value, TextWriter writer)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            Register(type, out var handler);
            if (handler == null)
                throw new NotSupportedException($"JsonWriter:{type}");

            var jsonWriter = _TextJsonWriter;
            if (jsonWriter == null)
            {
                jsonWriter = new TextJsonWriter(writer);
            }
            else
            {
                _TextJsonWriter = null;
                jsonWriter.Set(writer);
            }
            try
            {
                handler(value, jsonWriter);
            }
            finally
            {
                jsonWriter.Reset();
                _TextJsonWriter = jsonWriter;
            }
        }
        #endregion
        #region ToJsonIndent
        [ThreadStatic] private static IndentBufferJsonWriter _IndentBufferJsonWriter;
        [ThreadStatic] private static IndentTextJsonWriter _IndentTextJsonWriter;
        public static string ToJsonIndent<T>(T value)
        {
            return ToJsonIndent(value, "\t", Environment.NewLine);
        }
        public static void ToJsonIndent<T>(T value, BufferWriter<char> writer)
        {
            ToJsonIndent(value, writer, "\t", Environment.NewLine);
        }
        public static void ToJsonIndent<T>(T value, TextWriter writer)
        {
            ToJsonIndent(value, writer, "\t", Environment.NewLine);
        }
        public static string ToJsonIndent<T>(T value, string indent, string newLine)
        {
            var sb = StringExtensions.ThreadRent(out var disposable);
            try
            {
                ToJsonIndent(value, sb, indent, newLine);
                return sb.ToString();
            }
            finally
            {
                disposable.Dispose();
            }
        }
        public static void ToJsonIndent<T>(T value, BufferWriter<char> writer, string indent, string newLine)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            Register<T>(out var handler);
            if (handler == null)
                throw new NotSupportedException($"JsonWriter:{typeof(T)}");

            var jsonWriter = _IndentBufferJsonWriter;
            if (jsonWriter == null)
            {
                jsonWriter = new IndentBufferJsonWriter(writer, indent, newLine);
            }
            else
            {
                _IndentBufferJsonWriter = null;
                jsonWriter.Set(writer, indent, newLine);
            }
            try
            {
                handler(value, jsonWriter);
            }
            finally
            {
                jsonWriter.Reset();
                _IndentBufferJsonWriter = jsonWriter;
            }
        }
        public static void ToJsonIndent<T>(T value, TextWriter writer, string indent, string newLine)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            Register<T>(out var handler);
            if (handler == null)
                throw new NotSupportedException($"JsonWriter:{typeof(T)}");

            var jsonWriter = _IndentTextJsonWriter;
            if (jsonWriter == null)
            {
                jsonWriter = new IndentTextJsonWriter(writer, indent, newLine);
            }
            else
            {
                _IndentTextJsonWriter = null;
                jsonWriter.Set(writer, indent, newLine);
            }
            try
            {
                handler(value, jsonWriter);
            }
            finally
            {
                jsonWriter.Reset();
                _IndentTextJsonWriter = jsonWriter;
            }
        }
        public static string ToJsonIndent(Type type, object value)
        {
            return ToJsonIndent(type, value, "\t", Environment.NewLine);
        }
        public static void ToJsonIndent(Type type, object value, BufferWriter<char> writer)
        {
            ToJsonIndent(type, value, writer, "\t", Environment.NewLine);
        }
        public static void ToJsonIndent(Type type, object value, TextWriter writer)
        {
            ToJsonIndent(type, value, writer, "\t", Environment.NewLine);
        }
        public static string ToJsonIndent(Type type, object value, string indent, string newLine)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            var sb = StringExtensions.ThreadRent(out var disposable);
            try
            {
                ToJsonIndent(type, value, sb, indent, newLine);
                return sb.ToString();
            }
            finally
            {
                disposable.Dispose();
            }
        }
        public static void ToJsonIndent(Type type, object value, BufferWriter<char> writer, string indent, string newLine)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            Register(type, out var handler);
            if (handler == null)
                throw new NotSupportedException($"JsonWriter:{type}");

            var jsonWriter = _IndentBufferJsonWriter;
            if (jsonWriter == null)
            {
                jsonWriter = new IndentBufferJsonWriter(writer, indent, newLine);
            }
            else
            {
                _IndentBufferJsonWriter = null;
                jsonWriter.Set(writer, indent, newLine);
            }
            try
            {
                handler(value, jsonWriter);
            }
            finally
            {
                jsonWriter.Reset();
                _IndentBufferJsonWriter = jsonWriter;
            }
        }
        public static void ToJsonIndent(Type type, object value, TextWriter writer, string indent, string newLine)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            Register(type, out var handler);
            if (handler == null)
                throw new NotSupportedException($"JsonWriter:{type}");

            var jsonWriter = _IndentTextJsonWriter;
            if (jsonWriter == null)
            {
                jsonWriter = new IndentTextJsonWriter(writer, indent, newLine);
            }
            else
            {
                _IndentTextJsonWriter = null;
                jsonWriter.Set(writer, indent, newLine);
            }
            try
            {
                handler(value, jsonWriter);
            }
            finally
            {
                jsonWriter.Reset();
                _IndentTextJsonWriter = jsonWriter;
            }
        }
        #endregion
        #region private
        private class BufferJsonWriter : JsonWriter
        {
            private static int _MaxDepth = 64;
            private static string[] _ControlEscape = new[]
            {
                "\\u0000", "\\u0001", "\\u0002", "\\u0003", "\\u0004", "\\u0005", "\\u0006", "\\u0007",
                "\\b", "\\t", "\\n", "\\u000B", "\\f", "\\r", "\\u000E", "\\u000F",
                "\\u0010", "\\u0011", "\\u0012", "\\u0013", "\\u0014", "\\u0015", "\\u0016", "\\u0017",
                "\\u0018", "\\u0019", "\\u001A", "\\u001B", "\\u001C", "\\u001D", "\\u001E", "\\u001F"
            };
            private bool[] _isComma;
            private bool[] _isObject;
            private int _depth = -1;
            private BufferWriter<char> _writer;
            public BufferJsonWriter(BufferWriter<char> writer)
            {
                _writer = writer;
                _isComma = new bool[_MaxDepth];
                _isObject = new bool[_MaxDepth];
            }
            public void Set(BufferWriter<char> writer) => _writer = writer;
            public void Reset()
            {
                _writer = null;
                _depth = -1;
            }
            public override void WriteStartArray()
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                _depth += 1;
                if (_depth >= _MaxDepth)
                    throw new InvalidOperationException($"MaxDepth:{_MaxDepth}");

                _writer.Write('[');

                _isComma[_depth] = false;
                _isObject[_depth] = false;
            }
            public override void WriteEndArray()
            {
                if (_depth == -1 || _isObject[_depth])
                    throw new InvalidOperationException(nameof(WriteEndArray));

                _depth -= 1;
                _writer.Write(']');
            }
            public override void WriteStartObject()
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                _depth += 1;
                if (_depth >= _MaxDepth)
                    throw new InvalidOperationException($"MaxDepth:{_MaxDepth}");

                _writer.Write('{');

                _isObject[_depth] = true;
                _isComma[_depth] = false;
            }
            public override void WriteEndObject()
            {
                if (_depth == -1 || !_isObject[_depth])
                    throw new InvalidOperationException(nameof(WriteEndObject));

                _depth -= 1;
                _writer.Write('}');
            }
            public override void WriteProperty(string name)
            {
                if (name == null)
                    throw new ArgumentNullException(nameof(name));
                if (_depth == -1 || !_isObject[_depth])
                    throw new InvalidOperationException(nameof(WriteProperty));

                if (_isComma[_depth])
                    _writer.Write(',');
                else
                    _isComma[_depth] = true;

                var length = name.Length;
                _writer.Write('"');
                string tempEscape;
                var tempOffset = 0;
                for (int i = 0; i < length; i++)
                {
                    var ch = name[i];
                    switch (ch)
                    {
                        case '\u0000':
                        case '\u0001':
                        case '\u0002':
                        case '\u0003':
                        case '\u0004':
                        case '\u0005':
                        case '\u0006':
                        case '\u0007':
                        case '\u0008':
                        case '\u0009':
                        case '\u000A':
                        case '\u000B':
                        case '\u000C':
                        case '\u000D':
                        case '\u000E':
                        case '\u000F':
                        case '\u0010':
                        case '\u0011':
                        case '\u0012':
                        case '\u0013':
                        case '\u0014':
                        case '\u0015':
                        case '\u0016':
                        case '\u0017':
                        case '\u0018':
                        case '\u0019':
                        case '\u001A':
                        case '\u001B':
                        case '\u001C':
                        case '\u001D':
                        case '\u001E':
                        case '\u001F':
                            tempEscape = _ControlEscape[ch];
                            break;
                        case '"':
                            tempEscape = "\\\"";
                            break;
                        case '\\':
                            tempEscape = "\\\\";
                            break;
                        case '\u0085':
                            tempEscape = "\\u0085";
                            break;
                        case '\u2028':
                            tempEscape = "\\u2028";
                            break;
                        case '\u2029':
                            tempEscape = "\\u2029";
                            break;
                        default:
                            continue;
                    }
                    var count = i - tempOffset;
                    if (count > 0)
                        _writer.Write(name.AsSpan(tempOffset, count));
                    _writer.Write(tempEscape);
                    tempOffset = i + 1;
                }

                if (tempOffset < length)
                    _writer.Write(name.AsSpan(tempOffset));
                _writer.Write('"');

                _writer.Write(':');
            }
            public override void WriteString(string value)
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                if (value == null)
                {
                    _writer.Write("null");
                    return;
                }
                var length = value.Length;
                _writer.Write('"');
                string tempEscape;
                var tempOffset = 0;
                for (int i = 0; i < length; i++)
                {
                    var ch = value[i];
                    switch (ch)
                    {
                        case '\u0000':
                        case '\u0001':
                        case '\u0002':
                        case '\u0003':
                        case '\u0004':
                        case '\u0005':
                        case '\u0006':
                        case '\u0007':
                        case '\u0008':
                        case '\u0009':
                        case '\u000A':
                        case '\u000B':
                        case '\u000C':
                        case '\u000D':
                        case '\u000E':
                        case '\u000F':
                        case '\u0010':
                        case '\u0011':
                        case '\u0012':
                        case '\u0013':
                        case '\u0014':
                        case '\u0015':
                        case '\u0016':
                        case '\u0017':
                        case '\u0018':
                        case '\u0019':
                        case '\u001A':
                        case '\u001B':
                        case '\u001C':
                        case '\u001D':
                        case '\u001E':
                        case '\u001F':
                            tempEscape = _ControlEscape[ch];
                            break;
                        case '"':
                            tempEscape = "\\\"";
                            break;
                        case '\\':
                            tempEscape = "\\\\";
                            break;
                        case '\u0085':
                            tempEscape = "\\u0085";
                            break;
                        case '\u2028':
                            tempEscape = "\\u2028";
                            break;
                        case '\u2029':
                            tempEscape = "\\u2029";
                            break;
                        default:
                            continue;
                    }
                    var count = i - tempOffset;
                    if (count > 0)
                        _writer.Write(value.AsSpan(tempOffset, count));
                    _writer.Write(tempEscape);
                    tempOffset = i + 1;
                }

                if (tempOffset < length)
                    _writer.Write(value.AsSpan(tempOffset));

                _writer.Write('"');
            }
            public override void WriteString(ReadOnlySpan<char> value)
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                if (value == null)
                {
                    _writer.Write("null");
                    return;
                }
                var length = value.Length;
                _writer.Write('"');
                string tempEscape;
                var tempOffset = 0;
                for (int i = 0; i < length; i++)
                {
                    var ch = value[i];
                    switch (ch)
                    {
                        case '\u0000':
                        case '\u0001':
                        case '\u0002':
                        case '\u0003':
                        case '\u0004':
                        case '\u0005':
                        case '\u0006':
                        case '\u0007':
                        case '\u0008':
                        case '\u0009':
                        case '\u000A':
                        case '\u000B':
                        case '\u000C':
                        case '\u000D':
                        case '\u000E':
                        case '\u000F':
                        case '\u0010':
                        case '\u0011':
                        case '\u0012':
                        case '\u0013':
                        case '\u0014':
                        case '\u0015':
                        case '\u0016':
                        case '\u0017':
                        case '\u0018':
                        case '\u0019':
                        case '\u001A':
                        case '\u001B':
                        case '\u001C':
                        case '\u001D':
                        case '\u001E':
                        case '\u001F':
                            tempEscape = _ControlEscape[ch];
                            break;
                        case '"':
                            tempEscape = "\\\"";
                            break;
                        case '\\':
                            tempEscape = "\\\\";
                            break;
                        case '\u0085':
                            tempEscape = "\\u0085";
                            break;
                        case '\u2028':
                            tempEscape = "\\u2028";
                            break;
                        case '\u2029':
                            tempEscape = "\\u2029";
                            break;
                        default:
                            continue;
                    }
                    var count = i - tempOffset;
                    if (count > 0)
                        _writer.Write(value.Slice(tempOffset, count));
                    _writer.Write(tempEscape);
                    tempOffset = i + 1;
                }

                if (tempOffset < length)
                    _writer.Write(value.Slice(tempOffset));

                _writer.Write('"');
            }
            public override void WriteNumber(byte value)
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                if (value.TryFormat(_writer.GetSpan(), out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Advance(charsWritten);
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(sbyte value)
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                if (value.TryFormat(_writer.GetSpan(), out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Advance(charsWritten);
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(short value)
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                if (value.TryFormat(_writer.GetSpan(), out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Advance(charsWritten);
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(ushort value)
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                if (value.TryFormat(_writer.GetSpan(), out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Advance(charsWritten);
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(int value)
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                if (value.TryFormat(_writer.GetSpan(), out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Advance(charsWritten);
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(uint value)
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                if (value.TryFormat(_writer.GetSpan(), out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Advance(charsWritten);
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(long value)
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                if (value.TryFormat(_writer.GetSpan(), out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Advance(charsWritten);
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(ulong value)
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                if (value.TryFormat(_writer.GetSpan(), out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Advance(charsWritten);
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(float value)
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                if (value.TryFormat(_writer.GetSpan(), out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Advance(charsWritten);
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(double value)
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                if (value.TryFormat(_writer.GetSpan(), out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Advance(charsWritten);
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(decimal value)
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                if (value.TryFormat(_writer.GetSpan(), out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Advance(charsWritten);
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(ReadOnlySpan<char> value)
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                _writer.Write(value);
            }
            public override void WriteBoolean(bool value)
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                _writer.Write(value ? "true" : "false");
            }
            public override void WriteNull()
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                _writer.Write("null");
            }
        }
        private class TextJsonWriter : JsonWriter
        {
            private static int _MaxDepth = 64;
            private static string[] _ControlEscape = new[]
            {
                "\\u0000", "\\u0001", "\\u0002", "\\u0003", "\\u0004", "\\u0005", "\\u0006", "\\u0007",
                "\\b", "\\t", "\\n", "\\u000B", "\\f", "\\r", "\\u000E", "\\u000F",
                "\\u0010", "\\u0011", "\\u0012", "\\u0013", "\\u0014", "\\u0015", "\\u0016", "\\u0017",
                "\\u0018", "\\u0019", "\\u001A", "\\u001B", "\\u001C", "\\u001D", "\\u001E", "\\u001F"
            };
            private bool[] _isComma;
            private bool[] _isObject;
            private int _depth = -1;
            private TextWriter _writer;
            public TextJsonWriter(TextWriter writer)
            {
                _writer = writer;
                _isComma = new bool[_MaxDepth];
                _isObject = new bool[_MaxDepth];
            }
            public void Set(TextWriter writer) => _writer = writer;
            public void Reset()
            {
                _writer = null;
                _depth = -1;
            }
            public override void WriteStartArray()
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                _depth += 1;
                if (_depth >= _MaxDepth)
                    throw new InvalidOperationException($"MaxDepth:{_MaxDepth}");

                _writer.Write('[');

                _isComma[_depth] = false;
                _isObject[_depth] = false;
            }
            public override void WriteEndArray()
            {
                if (_depth == -1 || _isObject[_depth])
                    throw new InvalidOperationException(nameof(WriteEndArray));

                _depth -= 1;
                _writer.Write(']');
            }
            public override void WriteStartObject()
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                _depth += 1;
                if (_depth >= _MaxDepth)
                    throw new InvalidOperationException($"MaxDepth:{_MaxDepth}");

                _writer.Write('{');

                _isObject[_depth] = true;
                _isComma[_depth] = false;
            }
            public override void WriteEndObject()
            {
                if (_depth == -1 || !_isObject[_depth])
                    throw new InvalidOperationException(nameof(WriteEndObject));

                _depth -= 1;
                _writer.Write('}');
            }
            public override void WriteProperty(string name)
            {
                if (name == null)
                    throw new ArgumentNullException(nameof(name));
                if (_depth == -1 || !_isObject[_depth])
                    throw new InvalidOperationException(nameof(WriteProperty));

                if (_isComma[_depth])
                    _writer.Write(',');
                else
                    _isComma[_depth] = true;

                var length = name.Length;
                _writer.Write('"');
                string tempEscape;
                var tempOffset = 0;
                for (int i = 0; i < length; i++)
                {
                    var ch = name[i];
                    switch (ch)
                    {
                        case '\u0000':
                        case '\u0001':
                        case '\u0002':
                        case '\u0003':
                        case '\u0004':
                        case '\u0005':
                        case '\u0006':
                        case '\u0007':
                        case '\u0008':
                        case '\u0009':
                        case '\u000A':
                        case '\u000B':
                        case '\u000C':
                        case '\u000D':
                        case '\u000E':
                        case '\u000F':
                        case '\u0010':
                        case '\u0011':
                        case '\u0012':
                        case '\u0013':
                        case '\u0014':
                        case '\u0015':
                        case '\u0016':
                        case '\u0017':
                        case '\u0018':
                        case '\u0019':
                        case '\u001A':
                        case '\u001B':
                        case '\u001C':
                        case '\u001D':
                        case '\u001E':
                        case '\u001F':
                            tempEscape = _ControlEscape[ch];
                            break;
                        case '"':
                            tempEscape = "\\\"";
                            break;
                        case '\\':
                            tempEscape = "\\\\";
                            break;
                        case '\u0085':
                            tempEscape = "\\u0085";
                            break;
                        case '\u2028':
                            tempEscape = "\\u2028";
                            break;
                        case '\u2029':
                            tempEscape = "\\u2029";
                            break;
                        default:
                            continue;
                    }
                    var count = i - tempOffset;
                    if (count > 0)
                        _writer.Write(name.AsSpan(tempOffset, count));
                    _writer.Write(tempEscape);
                    tempOffset = i + 1;
                }

                if (tempOffset < length)
                    _writer.Write(name.AsSpan(tempOffset));
                _writer.Write('"');

                _writer.Write(':');
            }
            public override void WriteString(string value)
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                if (value == null)
                {
                    _writer.Write("null");
                    return;
                }
                var length = value.Length;
                _writer.Write('"');
                string tempEscape;
                var tempOffset = 0;
                for (int i = 0; i < length; i++)
                {
                    var ch = value[i];
                    switch (ch)
                    {
                        case '\u0000':
                        case '\u0001':
                        case '\u0002':
                        case '\u0003':
                        case '\u0004':
                        case '\u0005':
                        case '\u0006':
                        case '\u0007':
                        case '\u0008':
                        case '\u0009':
                        case '\u000A':
                        case '\u000B':
                        case '\u000C':
                        case '\u000D':
                        case '\u000E':
                        case '\u000F':
                        case '\u0010':
                        case '\u0011':
                        case '\u0012':
                        case '\u0013':
                        case '\u0014':
                        case '\u0015':
                        case '\u0016':
                        case '\u0017':
                        case '\u0018':
                        case '\u0019':
                        case '\u001A':
                        case '\u001B':
                        case '\u001C':
                        case '\u001D':
                        case '\u001E':
                        case '\u001F':
                            tempEscape = _ControlEscape[ch];
                            break;
                        case '"':
                            tempEscape = "\\\"";
                            break;
                        case '\\':
                            tempEscape = "\\\\";
                            break;
                        case '\u0085':
                            tempEscape = "\\u0085";
                            break;
                        case '\u2028':
                            tempEscape = "\\u2028";
                            break;
                        case '\u2029':
                            tempEscape = "\\u2029";
                            break;
                        default:
                            continue;
                    }
                    var count = i - tempOffset;
                    if (count > 0)
                        _writer.Write(value.AsSpan(tempOffset, count));
                    _writer.Write(tempEscape);
                    tempOffset = i + 1;
                }

                if (tempOffset < length)
                    _writer.Write(value.AsSpan(tempOffset));

                _writer.Write('"');
            }
            public override void WriteString(ReadOnlySpan<char> value)
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                if (value == null)
                {
                    _writer.Write("null");
                    return;
                }
                var length = value.Length;
                _writer.Write('"');
                string tempEscape;
                var tempOffset = 0;
                for (int i = 0; i < length; i++)
                {
                    var ch = value[i];
                    switch (ch)
                    {
                        case '\u0000':
                        case '\u0001':
                        case '\u0002':
                        case '\u0003':
                        case '\u0004':
                        case '\u0005':
                        case '\u0006':
                        case '\u0007':
                        case '\u0008':
                        case '\u0009':
                        case '\u000A':
                        case '\u000B':
                        case '\u000C':
                        case '\u000D':
                        case '\u000E':
                        case '\u000F':
                        case '\u0010':
                        case '\u0011':
                        case '\u0012':
                        case '\u0013':
                        case '\u0014':
                        case '\u0015':
                        case '\u0016':
                        case '\u0017':
                        case '\u0018':
                        case '\u0019':
                        case '\u001A':
                        case '\u001B':
                        case '\u001C':
                        case '\u001D':
                        case '\u001E':
                        case '\u001F':
                            tempEscape = _ControlEscape[ch];
                            break;
                        case '"':
                            tempEscape = "\\\"";
                            break;
                        case '\\':
                            tempEscape = "\\\\";
                            break;
                        case '\u0085':
                            tempEscape = "\\u0085";
                            break;
                        case '\u2028':
                            tempEscape = "\\u2028";
                            break;
                        case '\u2029':
                            tempEscape = "\\u2029";
                            break;
                        default:
                            continue;
                    }
                    var count = i - tempOffset;
                    if (count > 0)
                        _writer.Write(value.Slice(tempOffset, count));
                    _writer.Write(tempEscape);
                    tempOffset = i + 1;
                }

                if (tempOffset < length)
                    _writer.Write(value.Slice(tempOffset));

                _writer.Write('"');
            }
            public override void WriteNumber(byte value)
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                Span<char> chars = stackalloc char[3];
                if (value.TryFormat(chars, out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Write(chars.Slice(0, charsWritten));
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(sbyte value)
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                Span<char> chars = stackalloc char[4];
                if (value.TryFormat(chars, out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Write(chars.Slice(0, charsWritten));
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(short value)
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                Span<char> chars = stackalloc char[6];
                if (value.TryFormat(chars, out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Write(chars.Slice(0, charsWritten));
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(ushort value)
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                Span<char> chars = stackalloc char[5];
                if (value.TryFormat(chars, out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Write(chars.Slice(0, charsWritten));
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(int value)
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                Span<char> chars = stackalloc char[11];
                if (value.TryFormat(chars, out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Write(chars.Slice(0, charsWritten));
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(uint value)
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                Span<char> chars = stackalloc char[10];
                if (value.TryFormat(chars, out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Write(chars.Slice(0, charsWritten));
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(long value)
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                Span<char> chars = stackalloc char[20];
                if (value.TryFormat(chars, out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Write(chars.Slice(0, charsWritten));
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(ulong value)
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                Span<char> chars = stackalloc char[20];
                if (value.TryFormat(chars, out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Write(chars.Slice(0, charsWritten));
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(float value)
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                Span<char> chars = stackalloc char[24];
                if (value.TryFormat(chars, out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Write(chars.Slice(0, charsWritten));
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(double value)
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                Span<char> chars = stackalloc char[24];
                if (value.TryFormat(chars, out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Write(chars.Slice(0, charsWritten));
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(decimal value)
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                Span<char> chars = stackalloc char[24];
                if (value.TryFormat(chars, out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Write(chars.Slice(0, charsWritten));
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(ReadOnlySpan<char> value)
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                _writer.Write(value);
            }
            public override void WriteBoolean(bool value)
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                _writer.Write(value ? "true" : "false");
            }
            public override void WriteNull()
            {
                if (_depth != -1 && !_isObject[_depth])
                    if (_isComma[_depth])
                        _writer.Write(',');
                    else
                        _isComma[_depth] = true;

                _writer.Write("null");
            }
        }
        private class IndentBufferJsonWriter : JsonWriter
        {
            private static int _MaxDepth = 64;
            private static string[] _ControlEscape = new[]
            {
                "\\u0000", "\\u0001", "\\u0002", "\\u0003", "\\u0004", "\\u0005", "\\u0006", "\\u0007",
                "\\b", "\\t", "\\n", "\\u000B", "\\f", "\\r", "\\u000E", "\\u000F",
                "\\u0010", "\\u0011", "\\u0012", "\\u0013", "\\u0014", "\\u0015", "\\u0016", "\\u0017",
                "\\u0018", "\\u0019", "\\u001A", "\\u001B", "\\u001C", "\\u001D", "\\u001E", "\\u001F"
            };
            private bool[] _isComma;
            private bool[] _isObject;
            private int _depth = -1;
            private BufferWriter<char> _writer;
            private string _indent;
            private string _newLine;
            public IndentBufferJsonWriter(BufferWriter<char> writer, string indent, string newLine)
            {
                _writer = writer;
                _isComma = new bool[_MaxDepth];
                _isObject = new bool[_MaxDepth];
                _indent = indent;
                _newLine = newLine;
            }
            public void Set(BufferWriter<char> writer, string indent, string newLine)
            {
                _writer = writer;
                _indent = indent;
                _newLine = newLine;
            }
            public void Reset()
            {
                _writer = null;
                _depth = -1;
            }
            public void WriteIndent(int count)
            {
                for (int i = 0; i < count; i++)
                {
                    _writer.Write(_indent);
                }
            }
            public void WriteLine()
            {
                _writer.Write(_newLine);
            }
            public override void WriteStartArray()
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;
                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }
                _depth += 1;
                if (_depth >= _MaxDepth)
                    throw new InvalidOperationException($"MaxDepth:{_MaxDepth}");

                _writer.Write('[');
                _isComma[_depth] = false;
                _isObject[_depth] = false;
            }
            public override void WriteEndArray()
            {
                if (_depth == -1 || _isObject[_depth])
                    throw new InvalidOperationException(nameof(WriteEndArray));

                WriteLine();
                WriteIndent(_depth);
                _depth -= 1;
                _writer.Write(']');
            }
            public override void WriteStartObject()
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;
                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }
                _depth += 1;
                if (_depth >= _MaxDepth)
                    throw new InvalidOperationException($"MaxDepth:{_MaxDepth}");

                _writer.Write('{');
                _isObject[_depth] = true;
                _isComma[_depth] = false;
            }
            public override void WriteEndObject()
            {
                if (_depth == -1 || !_isObject[_depth])
                    throw new InvalidOperationException(nameof(WriteEndObject));

                WriteLine();
                WriteIndent(_depth);
                _depth -= 1;
                _writer.Write('}');
            }
            public override void WriteProperty(string name)
            {
                if (name == null)
                    throw new ArgumentNullException(nameof(name));
                if (_depth == -1 || !_isObject[_depth])
                    throw new InvalidOperationException(nameof(WriteProperty));

                if (_isComma[_depth])
                    _writer.Write(',');
                else
                    _isComma[_depth] = true;

                var length = name.Length;
                WriteLine();
                WriteIndent(_depth + 1);
                _writer.Write('"');
                string tempEscape;
                var tempOffset = 0;
                for (int i = 0; i < length; i++)
                {
                    var ch = name[i];
                    switch (ch)
                    {
                        case '\u0000':
                        case '\u0001':
                        case '\u0002':
                        case '\u0003':
                        case '\u0004':
                        case '\u0005':
                        case '\u0006':
                        case '\u0007':
                        case '\u0008':
                        case '\u0009':
                        case '\u000A':
                        case '\u000B':
                        case '\u000C':
                        case '\u000D':
                        case '\u000E':
                        case '\u000F':
                        case '\u0010':
                        case '\u0011':
                        case '\u0012':
                        case '\u0013':
                        case '\u0014':
                        case '\u0015':
                        case '\u0016':
                        case '\u0017':
                        case '\u0018':
                        case '\u0019':
                        case '\u001A':
                        case '\u001B':
                        case '\u001C':
                        case '\u001D':
                        case '\u001E':
                        case '\u001F':
                            tempEscape = _ControlEscape[ch];
                            break;
                        case '"':
                            tempEscape = "\\\"";
                            break;
                        case '\\':
                            tempEscape = "\\\\";
                            break;
                        case '\u0085':
                            tempEscape = "\\u0085";
                            break;
                        case '\u2028':
                            tempEscape = "\\u2028";
                            break;
                        case '\u2029':
                            tempEscape = "\\u2029";
                            break;
                        default:
                            continue;
                    }
                    var count = i - tempOffset;
                    if (count > 0)
                        _writer.Write(name.AsSpan(tempOffset, count));
                    _writer.Write(tempEscape);
                    tempOffset = i + 1;
                }

                if (tempOffset < length)
                    _writer.Write(name.AsSpan(tempOffset));
                _writer.Write('"');

                _writer.Write(':');
                _writer.Write(' ');
            }
            public override void WriteString(string value)
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                if (value == null)
                {
                    _writer.Write("null");
                    return;
                }
                var length = value.Length;
                _writer.Write('"');
                string tempEscape;
                var tempOffset = 0;
                for (int i = 0; i < length; i++)
                {
                    var ch = value[i];
                    switch (ch)
                    {
                        case '\u0000':
                        case '\u0001':
                        case '\u0002':
                        case '\u0003':
                        case '\u0004':
                        case '\u0005':
                        case '\u0006':
                        case '\u0007':
                        case '\u0008':
                        case '\u0009':
                        case '\u000A':
                        case '\u000B':
                        case '\u000C':
                        case '\u000D':
                        case '\u000E':
                        case '\u000F':
                        case '\u0010':
                        case '\u0011':
                        case '\u0012':
                        case '\u0013':
                        case '\u0014':
                        case '\u0015':
                        case '\u0016':
                        case '\u0017':
                        case '\u0018':
                        case '\u0019':
                        case '\u001A':
                        case '\u001B':
                        case '\u001C':
                        case '\u001D':
                        case '\u001E':
                        case '\u001F':
                            tempEscape = _ControlEscape[ch];
                            break;
                        case '"':
                            tempEscape = "\\\"";
                            break;
                        case '\\':
                            tempEscape = "\\\\";
                            break;
                        case '\u0085':
                            tempEscape = "\\u0085";
                            break;
                        case '\u2028':
                            tempEscape = "\\u2028";
                            break;
                        case '\u2029':
                            tempEscape = "\\u2029";
                            break;
                        default:
                            continue;
                    }
                    var count = i - tempOffset;
                    if (count > 0)
                        _writer.Write(value.AsSpan(tempOffset, count));
                    _writer.Write(tempEscape);
                    tempOffset = i + 1;
                }

                if (tempOffset < length)
                    _writer.Write(value.AsSpan(tempOffset));

                _writer.Write('"');
            }
            public override void WriteString(ReadOnlySpan<char> value)
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                if (value == null)
                {
                    _writer.Write("null");
                    return;
                }
                var length = value.Length;
                _writer.Write('"');
                string tempEscape;
                var tempOffset = 0;
                for (int i = 0; i < length; i++)
                {
                    var ch = value[i];
                    switch (ch)
                    {
                        case '\u0000':
                        case '\u0001':
                        case '\u0002':
                        case '\u0003':
                        case '\u0004':
                        case '\u0005':
                        case '\u0006':
                        case '\u0007':
                        case '\u0008':
                        case '\u0009':
                        case '\u000A':
                        case '\u000B':
                        case '\u000C':
                        case '\u000D':
                        case '\u000E':
                        case '\u000F':
                        case '\u0010':
                        case '\u0011':
                        case '\u0012':
                        case '\u0013':
                        case '\u0014':
                        case '\u0015':
                        case '\u0016':
                        case '\u0017':
                        case '\u0018':
                        case '\u0019':
                        case '\u001A':
                        case '\u001B':
                        case '\u001C':
                        case '\u001D':
                        case '\u001E':
                        case '\u001F':
                            tempEscape = _ControlEscape[ch];
                            break;
                        case '"':
                            tempEscape = "\\\"";
                            break;
                        case '\\':
                            tempEscape = "\\\\";
                            break;
                        case '\u0085':
                            tempEscape = "\\u0085";
                            break;
                        case '\u2028':
                            tempEscape = "\\u2028";
                            break;
                        case '\u2029':
                            tempEscape = "\\u2029";
                            break;
                        default:
                            continue;
                    }
                    var count = i - tempOffset;
                    if (count > 0)
                        _writer.Write(value.Slice(tempOffset, count));
                    _writer.Write(tempEscape);
                    tempOffset = i + 1;
                }

                if (tempOffset < length)
                    _writer.Write(value.Slice(tempOffset));

                _writer.Write('"');
            }
            public override void WriteNumber(byte value)
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                if (value.TryFormat(_writer.GetSpan(), out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Advance(charsWritten);
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(sbyte value)
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                if (value.TryFormat(_writer.GetSpan(), out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Advance(charsWritten);
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(short value)
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                if (value.TryFormat(_writer.GetSpan(), out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Advance(charsWritten);
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(ushort value)
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                if (value.TryFormat(_writer.GetSpan(), out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Advance(charsWritten);
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(int value)
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                if (value.TryFormat(_writer.GetSpan(), out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Advance(charsWritten);
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(uint value)
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                if (value.TryFormat(_writer.GetSpan(), out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Advance(charsWritten);
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(long value)
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                if (value.TryFormat(_writer.GetSpan(), out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Advance(charsWritten);
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(ulong value)
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                if (value.TryFormat(_writer.GetSpan(), out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Advance(charsWritten);
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(float value)
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                if (value.TryFormat(_writer.GetSpan(), out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Advance(charsWritten);
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(double value)
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                if (value.TryFormat(_writer.GetSpan(), out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Advance(charsWritten);
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(decimal value)
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                if (value.TryFormat(_writer.GetSpan(), out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Advance(charsWritten);
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(ReadOnlySpan<char> value)
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                _writer.Write(value);
            }
            public override void WriteBoolean(bool value)
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                _writer.Write(value ? "true" : "false");
            }
            public override void WriteNull()
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                _writer.Write("null");
            }
        }
        private class IndentTextJsonWriter : JsonWriter
        {
            private static int _MaxDepth = 64;
            private static string[] _ControlEscape = new[]
            {
                "\\u0000", "\\u0001", "\\u0002", "\\u0003", "\\u0004", "\\u0005", "\\u0006", "\\u0007",
                "\\b", "\\t", "\\n", "\\u000B", "\\f", "\\r", "\\u000E", "\\u000F",
                "\\u0010", "\\u0011", "\\u0012", "\\u0013", "\\u0014", "\\u0015", "\\u0016", "\\u0017",
                "\\u0018", "\\u0019", "\\u001A", "\\u001B", "\\u001C", "\\u001D", "\\u001E", "\\u001F"
            };
            private bool[] _isComma;
            private bool[] _isObject;
            private int _depth = -1;
            private TextWriter _writer;
            private string _indent;
            private string _newLine;
            public IndentTextJsonWriter(TextWriter writer, string indent, string newLine)
            {
                _writer = writer;
                _isComma = new bool[_MaxDepth];
                _isObject = new bool[_MaxDepth];
                _indent = indent;
                _newLine = newLine;
            }
            public void Set(TextWriter writer, string indent, string newLine)
            {
                _writer = writer;
                _indent = indent;
                _newLine = newLine;
            }
            public void Reset()
            {
                _writer = null;
                _depth = -1;
            }
            public void WriteIndent(int count)
            {
                for (int i = 0; i < count; i++)
                {
                    _writer.Write(_indent);
                }
            }
            public void WriteLine()
            {
                _writer.Write(_newLine);
            }
            public override void WriteStartArray()
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;
                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                _depth += 1;
                if (_depth >= _MaxDepth)
                    throw new InvalidOperationException($"MaxDepth:{_MaxDepth}");

                _writer.Write('[');

                _isComma[_depth] = false;
                _isObject[_depth] = false;
            }
            public override void WriteEndArray()
            {
                if (_depth == -1 || _isObject[_depth])
                    throw new InvalidOperationException(nameof(WriteEndArray));

                WriteLine();
                WriteIndent(_depth);
                _depth -= 1;
                _writer.Write(']');
            }
            public override void WriteStartObject()
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;
                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                _depth += 1;
                if (_depth >= _MaxDepth)
                    throw new InvalidOperationException($"MaxDepth:{_MaxDepth}");

                _writer.Write('{');

                _isObject[_depth] = true;
                _isComma[_depth] = false;
            }
            public override void WriteEndObject()
            {
                if (_depth == -1 || !_isObject[_depth])
                    throw new InvalidOperationException(nameof(WriteEndObject));

                WriteLine();
                WriteIndent(_depth);
                _depth -= 1;
                _writer.Write('}');
            }
            public override void WriteProperty(string name)
            {
                if (name == null)
                    throw new ArgumentNullException(nameof(name));
                if (_depth == -1 || !_isObject[_depth])
                    throw new InvalidOperationException(nameof(WriteProperty));

                if (_isComma[_depth])
                    _writer.Write(',');
                else
                    _isComma[_depth] = true;

                var length = name.Length;
                WriteLine();
                WriteIndent(_depth + 1);
                _writer.Write('"');
                string tempEscape;
                var tempOffset = 0;
                for (int i = 0; i < length; i++)
                {
                    var ch = name[i];
                    switch (ch)
                    {
                        case '\u0000':
                        case '\u0001':
                        case '\u0002':
                        case '\u0003':
                        case '\u0004':
                        case '\u0005':
                        case '\u0006':
                        case '\u0007':
                        case '\u0008':
                        case '\u0009':
                        case '\u000A':
                        case '\u000B':
                        case '\u000C':
                        case '\u000D':
                        case '\u000E':
                        case '\u000F':
                        case '\u0010':
                        case '\u0011':
                        case '\u0012':
                        case '\u0013':
                        case '\u0014':
                        case '\u0015':
                        case '\u0016':
                        case '\u0017':
                        case '\u0018':
                        case '\u0019':
                        case '\u001A':
                        case '\u001B':
                        case '\u001C':
                        case '\u001D':
                        case '\u001E':
                        case '\u001F':
                            tempEscape = _ControlEscape[ch];
                            break;
                        case '"':
                            tempEscape = "\\\"";
                            break;
                        case '\\':
                            tempEscape = "\\\\";
                            break;
                        case '\u0085':
                            tempEscape = "\\u0085";
                            break;
                        case '\u2028':
                            tempEscape = "\\u2028";
                            break;
                        case '\u2029':
                            tempEscape = "\\u2029";
                            break;
                        default:
                            continue;
                    }
                    var count = i - tempOffset;
                    if (count > 0)
                        _writer.Write(name.AsSpan(tempOffset, count));
                    _writer.Write(tempEscape);
                    tempOffset = i + 1;
                }

                if (tempOffset < length)
                    _writer.Write(name.AsSpan(tempOffset));
                _writer.Write('"');

                _writer.Write(':');
                _writer.Write(' ');
            }
            public override void WriteString(string value)
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                if (value == null)
                {
                    _writer.Write("null");
                    return;
                }
                var length = value.Length;
                _writer.Write('"');
                string tempEscape;
                var tempOffset = 0;
                for (int i = 0; i < length; i++)
                {
                    var ch = value[i];
                    switch (ch)
                    {
                        case '\u0000':
                        case '\u0001':
                        case '\u0002':
                        case '\u0003':
                        case '\u0004':
                        case '\u0005':
                        case '\u0006':
                        case '\u0007':
                        case '\u0008':
                        case '\u0009':
                        case '\u000A':
                        case '\u000B':
                        case '\u000C':
                        case '\u000D':
                        case '\u000E':
                        case '\u000F':
                        case '\u0010':
                        case '\u0011':
                        case '\u0012':
                        case '\u0013':
                        case '\u0014':
                        case '\u0015':
                        case '\u0016':
                        case '\u0017':
                        case '\u0018':
                        case '\u0019':
                        case '\u001A':
                        case '\u001B':
                        case '\u001C':
                        case '\u001D':
                        case '\u001E':
                        case '\u001F':
                            tempEscape = _ControlEscape[ch];
                            break;
                        case '"':
                            tempEscape = "\\\"";
                            break;
                        case '\\':
                            tempEscape = "\\\\";
                            break;
                        case '\u0085':
                            tempEscape = "\\u0085";
                            break;
                        case '\u2028':
                            tempEscape = "\\u2028";
                            break;
                        case '\u2029':
                            tempEscape = "\\u2029";
                            break;
                        default:
                            continue;
                    }
                    var count = i - tempOffset;
                    if (count > 0)
                        _writer.Write(value.AsSpan(tempOffset, count));
                    _writer.Write(tempEscape);
                    tempOffset = i + 1;
                }

                if (tempOffset < length)
                    _writer.Write(value.AsSpan(tempOffset));

                _writer.Write('"');
            }
            public override void WriteString(ReadOnlySpan<char> value)
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                if (value == null)
                {
                    _writer.Write("null");
                    return;
                }
                var length = value.Length;
                _writer.Write('"');
                string tempEscape;
                var tempOffset = 0;
                for (int i = 0; i < length; i++)
                {
                    var ch = value[i];
                    switch (ch)
                    {
                        case '\u0000':
                        case '\u0001':
                        case '\u0002':
                        case '\u0003':
                        case '\u0004':
                        case '\u0005':
                        case '\u0006':
                        case '\u0007':
                        case '\u0008':
                        case '\u0009':
                        case '\u000A':
                        case '\u000B':
                        case '\u000C':
                        case '\u000D':
                        case '\u000E':
                        case '\u000F':
                        case '\u0010':
                        case '\u0011':
                        case '\u0012':
                        case '\u0013':
                        case '\u0014':
                        case '\u0015':
                        case '\u0016':
                        case '\u0017':
                        case '\u0018':
                        case '\u0019':
                        case '\u001A':
                        case '\u001B':
                        case '\u001C':
                        case '\u001D':
                        case '\u001E':
                        case '\u001F':
                            tempEscape = _ControlEscape[ch];
                            break;
                        case '"':
                            tempEscape = "\\\"";
                            break;
                        case '\\':
                            tempEscape = "\\\\";
                            break;
                        case '\u0085':
                            tempEscape = "\\u0085";
                            break;
                        case '\u2028':
                            tempEscape = "\\u2028";
                            break;
                        case '\u2029':
                            tempEscape = "\\u2029";
                            break;
                        default:
                            continue;
                    }
                    var count = i - tempOffset;
                    if (count > 0)
                        _writer.Write(value.Slice(tempOffset, count));
                    _writer.Write(tempEscape);
                    tempOffset = i + 1;
                }

                if (tempOffset < length)
                    _writer.Write(value.Slice(tempOffset));

                _writer.Write('"');
            }
            public override void WriteNumber(byte value)
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                Span<char> chars = stackalloc char[3];
                if (value.TryFormat(chars, out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Write(chars.Slice(0, charsWritten));
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(sbyte value)
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                Span<char> chars = stackalloc char[4];
                if (value.TryFormat(chars, out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Write(chars.Slice(0, charsWritten));
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(short value)
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                Span<char> chars = stackalloc char[6];
                if (value.TryFormat(chars, out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Write(chars.Slice(0, charsWritten));
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(ushort value)
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                Span<char> chars = stackalloc char[5];
                if (value.TryFormat(chars, out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Write(chars.Slice(0, charsWritten));
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(int value)
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                Span<char> chars = stackalloc char[11];
                if (value.TryFormat(chars, out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Write(chars.Slice(0, charsWritten));
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(uint value)
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                Span<char> chars = stackalloc char[10];
                if (value.TryFormat(chars, out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Write(chars.Slice(0, charsWritten));
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(long value)
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                Span<char> chars = stackalloc char[20];
                if (value.TryFormat(chars, out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Write(chars.Slice(0, charsWritten));
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(ulong value)
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                Span<char> chars = stackalloc char[20];
                if (value.TryFormat(chars, out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Write(chars.Slice(0, charsWritten));
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(float value)
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                Span<char> chars = stackalloc char[24];
                if (value.TryFormat(chars, out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Write(chars.Slice(0, charsWritten));
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(double value)
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                Span<char> chars = stackalloc char[24];
                if (value.TryFormat(chars, out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Write(chars.Slice(0, charsWritten));
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(decimal value)
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                Span<char> chars = stackalloc char[24];
                if (value.TryFormat(chars, out var charsWritten, provider: NumberFormatInfo.InvariantInfo))
                {
                    _writer.Write(chars.Slice(0, charsWritten));
                }
                else
                {
                    _writer.Write(value.ToString(NumberFormatInfo.InvariantInfo));
                }
            }
            public override void WriteNumber(ReadOnlySpan<char> value)
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                _writer.Write(value);
            }
            public override void WriteBoolean(bool value)
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                _writer.Write(value ? "true" : "false");
            }
            public override void WriteNull()
            {
                if (_depth != -1)
                {
                    if (!_isObject[_depth])
                    {
                        if (_isComma[_depth])
                            _writer.Write(',');
                        else
                            _isComma[_depth] = true;

                        WriteLine();
                        WriteIndent(_depth + 1);
                    }
                }

                _writer.Write("null");
            }
        }
        #endregion

        #region Register
        private static readonly object _Sync = new object();
        private static Stack<object> _Handlers;
        private static Func<PropertyInfo, string> _PropertyResolver;
        private static Stack<Func<PropertyInfo, ParameterExpression, Expression>> _PropertyPredicates;
        private static Stack<Func<Type, string, ParameterExpression, ParameterExpression, Expression>> _PropertyFormats;
        private static Stack<Func<PropertyInfo, ParameterExpression, ParameterExpression, Expression>> _PropertyHandlers;
        private static Dictionary<Type, Action<object, JsonWriter>> _ObjHandlers;
        private static class Handler<T>
        {
            static Handler()
            {
                var value = Expression.Parameter(typeof(T), "value");
                var writer = Expression.Parameter(typeof(JsonWriter), "writer");
                Register(typeof(T), value, writer, out var expression, out var @delegate);
                if (expression != null)
                {
                    Value = @delegate == null
                            ? Expression.Lambda<Action<T, JsonWriter>>(expression, value, writer).Compile()
                            : (Action<T, JsonWriter>)@delegate;
                }
            }

            public static Action<T, JsonWriter> Value;
        }
        static JsonWriter()
        {
            _ObjHandlers = new Dictionary<Type, Action<object, JsonWriter>>();
            _Handlers = new Stack<object>();
            _PropertyResolver = (property) => property.Name;
            _PropertyPredicates = new Stack<Func<PropertyInfo, ParameterExpression, Expression>>();
            _PropertyFormats = new Stack<Func<Type, string, ParameterExpression, ParameterExpression, Expression>>();
            _PropertyHandlers = new Stack<Func<PropertyInfo, ParameterExpression, ParameterExpression, Expression>>();

            var writeStartArray = typeof(JsonWriter).GetMethod("WriteStartArray");
            var writeEndArray = typeof(JsonWriter).GetMethod("WriteEndArray");
            var writeStartObject = typeof(JsonWriter).GetMethod("WriteStartObject");
            var writeEndObject = typeof(JsonWriter).GetMethod("WriteEndObject");
            var writeProperty = typeof(JsonWriter).GetMethod("WriteProperty");
            var writeNull = typeof(JsonWriter).GetMethod("WriteNull");
            //default
            var typeReference = new Stack<Type>();
            Register((type, value, writer) => {
                if (typeReference.Contains(type))
                {
                    var register = typeof(JsonWriter).GetMethod("Register", new[] { typeof(Action<,>).MakeGenericType(Type.MakeGenericMethodParameter(0), typeof(JsonWriter)).MakeByRefType() });
                    var handler = Expression.Variable(typeof(Action<,>).MakeGenericType(type, typeof(JsonWriter)), "handler");
                    return Expression.Block(new[] { handler },
                        Expression.Call(register.MakeGenericMethod(type), handler),
                        Expression.Invoke(handler, value, writer));
                }
                typeReference.Push(type);
                var extensionDataExprs = default(Expression);
                var typeProperties = type.GetGeneralProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var properties = new List<(string Name, bool EmitDefaultValue, int Order, PropertyInfo Property)>();
                foreach (var property in typeProperties)
                {
                    if (!property.CanRead)
                        continue;
                    if (property.IsDefined(typeof(IgnoreDataMemberAttribute)))
                        continue;
                    string propertyName = null;
                    var order = -1;
                    var emitDefaultValue = true;
                    var dataMemberAttribute = property.GetCustomAttribute<DataMemberAttribute>();
                    if (dataMemberAttribute == null)
                    {
                        if (property.Name == "ExtensionData")
                        {
                            if (property.PropertyType.IsEnumerable(out var getEnumerator, out var moveNext, out var current)
                                && current.PropertyType.IsGenericType
                                && current.PropertyType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                            {
                                var eleKey = current.PropertyType.GetProperty("Key");
                                var eleValue = current.PropertyType.GetProperty("Value");

                                var itemKey = Expression.Variable(eleKey.PropertyType, "itemKey");
                                var itemValue = Expression.Variable(eleValue.PropertyType, "itemValue");
                                Converter.Register(eleKey.PropertyType, typeof(string), itemKey, out var itemKeyExpr, out _);
                                Register(eleValue.PropertyType, itemValue, writer, out var itemValueExpr, out _);
                                if (itemKeyExpr != null && itemValueExpr != null)
                                {
                                    var extensionData = Expression.Variable(property.PropertyType, "extensionData");
                                    var enumerator = Expression.Variable(getEnumerator.ReturnType, "enumerator");
                                    var breakLabel = Expression.Label();
                                    extensionDataExprs = Expression.Block(new[] { extensionData },
                                        Expression.Assign(extensionData, Expression.Property(value, property)),
                                        Expression.IfThen(
                                            Expression.NotEqual(extensionData, Expression.Constant(null)),
                                            Expression.Block(new[] { enumerator },
                                                Expression.Assign(enumerator, Expression.Call(extensionData, getEnumerator)),
                                                Expression.Loop(
                                                    Expression.IfThenElse(Expression.Call(enumerator, moveNext),
                                                    Expression.Block(new[] { itemKey, itemValue },
                                                        Expression.Assign(itemKey, Expression.Property(Expression.Property(enumerator, current), eleKey)),
                                                        Expression.Call(writer, writeProperty, itemKeyExpr),
                                                        Expression.Assign(itemValue, Expression.Property(Expression.Property(enumerator, current), eleValue)),
                                                        itemValueExpr
                                                        ),
                                                    Expression.Break(breakLabel)
                                                    ), breakLabel)
                                                )
                                            )
                                        );
                                }
                            }
                            continue;
                        }
                        propertyName = property.GetMethod.IsPublic ? _PropertyResolver(property) : null;
                    }
                    else
                    {
                        propertyName = dataMemberAttribute.Name == null ? _PropertyResolver(property) : dataMemberAttribute.Name;
                        order = dataMemberAttribute.Order;
                        emitDefaultValue = dataMemberAttribute.EmitDefaultValue;
                    }
                    if (propertyName == null)
                        continue;

                    var i = 0;
                    for (; i < properties.Count; i++)
                    {
                        if (order > properties[i].Order)
                            break;
                    }
                    if (i < properties.Count)
                        properties.Insert(i, (propertyName, emitDefaultValue, order, property));
                    else
                        properties.Add((propertyName, emitDefaultValue, order, property));
                }

                var exprs = new List<Expression>();
                exprs.Add(Expression.Call(writer, writeStartObject));
                foreach (var (name, emitDefaultValue, _, property) in properties)
                {
                    var propertyValue = Expression.Variable(property.PropertyType, $"value{property.Name}");
                    RegisterProperty(property, propertyValue, writer, out var propertyExpression);
                    if (propertyExpression == null)
                        Register(property.PropertyType, propertyValue, writer, out propertyExpression, out _);

                    RegisterProperty(property, propertyValue, out var propertyPredicate);
                    if (!emitDefaultValue)
                    {
                        if (propertyPredicate == null)
                        {
                            propertyPredicate = Expression.NotEqual(propertyValue, Expression.Default(property.PropertyType));
                        }
                        else
                        {
                            propertyPredicate = Expression.AndAlso(
                                Expression.NotEqual(propertyValue, Expression.Default(property.PropertyType)),
                                propertyPredicate
                                );
                        }
                    }
                    if (propertyPredicate == null)
                    {
                        exprs.Add(Expression.Block(new[] { propertyValue },
                                    Expression.Assign(propertyValue, Expression.Property(value, property)),
                                    Expression.Call(writer, writeProperty, Expression.Constant(name)),
                                    propertyExpression));
                    }
                    else
                    {
                        exprs.Add(Expression.Block(new[] { propertyValue },
                                    Expression.Assign(propertyValue, Expression.Property(value, property)),
                                    Expression.IfThen(
                                        propertyPredicate,
                                         Expression.Block(
                                            Expression.Call(writer, writeProperty, Expression.Constant(name)),
                                            propertyExpression)
                                        )));
                    }
                }
                if (extensionDataExprs != null)
                {
                    exprs.Add(extensionDataExprs);
                }
                exprs.Add(Expression.Call(writer, writeEndObject));

                if (type.IsValueType)
                {
                    typeReference.Pop();
                    return Expression.Block(exprs);
                }
                else
                {
                    typeReference.Pop();
                    return Expression.IfThenElse(
                        Expression.Equal(value, Expression.Constant(null)),
                        Expression.Call(writer, writeNull),
                        Expression.Block(exprs));
                }
            });
            //void Invoke(JsonWriter)
            Register((type, value, writer) => {
                var write = type.GetMethod("Invoke", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(JsonWriter) }, null);
                if (write == null)
                    return null;

                if (type.IsValueType)
                {
                    return Expression.Call(value, write, writer);
                }
                else
                {
                    return Expression.IfThenElse(
                        Expression.Equal(value, Expression.Constant(null)),
                        Expression.Call(writer, writeNull),
                        Expression.Call(value, write, writer));
                }
            });
            //IEnumerable<>
            Register((type, value, writer) => {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(IEnumerable<>))
                    return null;

                var getEnumerator = type.GetMethod("GetEnumerator");
                var moveNext = typeof(IEnumerator).GetMethod("MoveNext");
                var current = getEnumerator.ReturnType.GetProperty("Current");

                var item = Expression.Variable(current.PropertyType, "item");
                var enumerator = Expression.Variable(getEnumerator.ReturnType, "enumerator");
                var breakLabel = Expression.Label();
                Register(current.PropertyType, item, writer, out var itemExpr, out _);
                if (itemExpr == null)
                    return Expression.Empty();

                return Expression.IfThenElse(
                    Expression.Equal(value, Expression.Constant(null, type)),
                    Expression.Call(writer, writeNull),
                    Expression.Block(new[] { enumerator },
                        Expression.Call(writer, writeStartArray),
                        Expression.Assign(enumerator, Expression.Call(value, getEnumerator)),
                        Expression.Loop(
                            Expression.IfThenElse(
                                Expression.Call(enumerator, moveNext),
                                Expression.Block(new[] { item },
                                    Expression.Assign(item, Expression.Property(enumerator, current)),
                                    itemExpr
                            ),
                            Expression.Break(breakLabel)
                            ), breakLabel),
                        Expression.Call(writer, writeEndArray)
                    ));
            });
            //ISet<>
            Register((type, value, writer) => {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(ISet<>))
                    return null;

                var getEnumerator = typeof(IEnumerable<>).MakeGenericType(type.GetGenericArguments()).GetMethod("GetEnumerator");
                var moveNext = typeof(IEnumerator).GetMethod("MoveNext");
                var current = getEnumerator.ReturnType.GetProperty("Current");

                var item = Expression.Variable(current.PropertyType, "item");
                var enumerator = Expression.Variable(getEnumerator.ReturnType, "enumerator");
                var breakLabel = Expression.Label();
                Register(current.PropertyType, item, writer, out var itemExpr, out _);
                if (itemExpr == null)
                    return Expression.Empty();

                return Expression.IfThenElse(
                    Expression.Equal(value, Expression.Constant(null, type)),
                    Expression.Call(writer, writeNull),
                    Expression.Block(new[] { enumerator },
                        Expression.Call(writer, writeStartArray),
                        Expression.Assign(enumerator, Expression.Call(value, getEnumerator)),
                        Expression.Loop(
                            Expression.IfThenElse(
                                Expression.Call(enumerator, moveNext),
                                Expression.Block(new[] { item },
                                    Expression.Assign(item, Expression.Property(enumerator, current)),
                                    itemExpr
                            ),
                            Expression.Break(breakLabel)
                            ), breakLabel),
                        Expression.Call(writer, writeEndArray)
                    ));
            });
            //HashSet<>
            Register((type, value, writer) => {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(HashSet<>))
                    return null;

                var getEnumerator = type.GetMethod("GetEnumerator");
                var moveNext = getEnumerator.ReturnType.GetMethod("MoveNext");
                var current = getEnumerator.ReturnType.GetProperty("Current");

                var item = Expression.Variable(current.PropertyType, "item");
                var enumerator = Expression.Variable(getEnumerator.ReturnType, "enumerator");
                var breakLabel = Expression.Label();
                Register(current.PropertyType, item, writer, out var itemExpr, out _);
                if (itemExpr == null)
                    return Expression.Empty();

                return Expression.IfThenElse(
                    Expression.Equal(value, Expression.Constant(null, type)),
                    Expression.Call(writer, writeNull),
                    Expression.Block(new[] { enumerator },
                        Expression.Call(writer, writeStartArray),
                        Expression.Assign(enumerator, Expression.Call(value, getEnumerator)),
                        Expression.Loop(
                            Expression.IfThenElse(
                                Expression.Call(enumerator, moveNext),
                                Expression.Block(new[] { item },
                                    Expression.Assign(item, Expression.Property(enumerator, current)),
                                    itemExpr
                            ),
                            Expression.Break(breakLabel)
                            ), breakLabel),
                        Expression.Call(writer, writeEndArray)
                    ));
            });
            //IList<>
            Register((type, value, writer) =>
            {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(IList<>))
                    return null;

                var eleType = type.GetGenericArguments()[0];
                var getItem = type.GetMethod("get_Item", BindingFlags.Instance | BindingFlags.Public);
                var eleValue = Expression.Variable(eleType, "item");
                var i = Expression.Variable(typeof(int), "i");
                var count = Expression.Variable(typeof(int), "count");
                var getCount = typeof(ICollection<>).MakeGenericType(eleType).GetProperty("Count");
                var forBreak = Expression.Label();
                Register(eleType, eleValue, writer, out var expression, out _);
                if (expression == null)
                    return Expression.Empty();
                return Expression.Block(
                    Expression.IfThenElse(
                        Expression.Equal(value, Expression.Constant(null)),
                        Expression.Call(writer, writeNull),
                        Expression.Block(new[] { i, count },
                            Expression.Assign(i, Expression.Constant(0)),
                            Expression.Assign(count, Expression.Property(value, getCount)),
                            Expression.Call(writer, writeStartArray),
                                Expression.Loop(
                                   Expression.IfThenElse(
                                       Expression.LessThan(i, count),
                                       Expression.Block(new[] { eleValue },
                                           Expression.Assign(eleValue, Expression.Call(value, getItem, i)),
                                           expression,
                                           Expression.PostIncrementAssign(i)
                                       ),
                                       Expression.Break(forBreak)
                                   ),
                               forBreak
                            ),
                            Expression.Call(writer, writeEndArray)
                        )
                    )
                );
            });
            //List<>
            Register((type, value, writer) =>
            {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(List<>))
                    return null;

                var eleType = type.GetGenericArguments()[0];
                var getItem = type.GetMethod("get_Item", BindingFlags.Instance | BindingFlags.Public);
                var eleValue = Expression.Variable(eleType, "item");
                var i = Expression.Variable(typeof(int), "i");
                var count = Expression.Variable(typeof(int), "count");
                var forBreak = Expression.Label();
                Register(eleType, eleValue, writer, out var expression, out _);
                if (expression == null)
                    return Expression.Empty();
                return Expression.Block(
                    Expression.IfThenElse(
                        Expression.Equal(value, Expression.Constant(null)),
                        Expression.Call(writer, writeNull),
                        Expression.Block(new[] { i, count },
                            Expression.Assign(i, Expression.Constant(0)),
                            Expression.Assign(count, Expression.Property(value, "Count")),
                            Expression.Call(writer, writeStartArray),
                                Expression.Loop(
                                   Expression.IfThenElse(
                                       Expression.LessThan(i, count),
                                       Expression.Block(new[] { eleValue },
                                           Expression.Assign(eleValue, Expression.Call(value, getItem, i)),
                                           expression,
                                           Expression.PostIncrementAssign(i)
                                       ),
                                       Expression.Break(forBreak)
                                   ),
                               forBreak
                            ),
                            Expression.Call(writer, writeEndArray)
                        )
                    )
                );
            });
            //Queue<>
            Register((type, value, writer) => {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Queue<>))
                    return null;

                var getEnumerator = type.GetMethod("GetEnumerator");
                var moveNext = getEnumerator.ReturnType.GetMethod("MoveNext");
                var current = getEnumerator.ReturnType.GetProperty("Current");

                var item = Expression.Variable(current.PropertyType, "item");
                var enumerator = Expression.Variable(getEnumerator.ReturnType, "enumerator");
                var breakLabel = Expression.Label();
                Register(current.PropertyType, item, writer, out var itemExpr, out _);
                if (itemExpr == null)
                    return Expression.Empty();

                return Expression.IfThenElse(
                    Expression.Equal(value, Expression.Constant(null, type)),
                    Expression.Call(writer, writeNull),
                    Expression.Block(new[] { enumerator },
                        Expression.Call(writer, writeStartArray),
                        Expression.Assign(enumerator, Expression.Call(value, getEnumerator)),
                        Expression.Loop(
                            Expression.IfThenElse(
                                Expression.Call(enumerator, moveNext),
                                Expression.Block(new[] { item },
                                    Expression.Assign(item, Expression.Property(enumerator, current)),
                                    itemExpr
                            ),
                            Expression.Break(breakLabel)
                            ), breakLabel),
                        Expression.Call(writer, writeEndArray)
                    ));
            });
            //Stack<>
            Register((type, value, writer) => {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Stack<>))
                    return null;

                var getEnumerator = type.GetMethod("GetEnumerator");
                var moveNext = getEnumerator.ReturnType.GetMethod("MoveNext");
                var current = getEnumerator.ReturnType.GetProperty("Current");

                var item = Expression.Variable(current.PropertyType, "item");
                var enumerator = Expression.Variable(getEnumerator.ReturnType, "enumerator");
                var breakLabel = Expression.Label();
                Register(current.PropertyType, item, writer, out var itemExpr, out _);
                if (itemExpr == null)
                    return Expression.Empty();

                return Expression.IfThenElse(
                    Expression.Equal(value, Expression.Constant(null, type)),
                    Expression.Call(writer, writeNull),
                    Expression.Block(new[] { enumerator },
                        Expression.Call(writer, writeStartArray),
                        Expression.Assign(enumerator, Expression.Call(value, getEnumerator)),
                        Expression.Loop(
                            Expression.IfThenElse(
                                Expression.Call(enumerator, moveNext),
                                Expression.Block(new[] { item },
                                    Expression.Assign(item, Expression.Property(enumerator, current)),
                                    itemExpr
                            ),
                            Expression.Break(breakLabel)
                            ), breakLabel),
                        Expression.Call(writer, writeEndArray)
                    ));
            });
            //IDictionary<,>
            Register((type, value, writer) => {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(IDictionary<,>))
                    return null;

                var keyValuePair = typeof(KeyValuePair<,>).MakeGenericType(type.GetGenericArguments());
                var eleKey = keyValuePair.GetProperty("Key");
                var eleValue = keyValuePair.GetProperty("Value");
                var getEnumerator = typeof(IEnumerable<>).MakeGenericType(keyValuePair).GetMethod("GetEnumerator", Type.EmptyTypes);
                var moveNext = typeof(IEnumerator).GetMethod("MoveNext", Type.EmptyTypes);
                var current = getEnumerator.ReturnType.GetProperty("Current");

                var itemKey = Expression.Variable(eleKey.PropertyType, "itemKey");
                var itemValue = Expression.Variable(eleValue.PropertyType, "itemValue");
                var enumerator = Expression.Variable(getEnumerator.ReturnType, "enumerator");
                var breakLabel = Expression.Label();
                Converter.Register(eleKey.PropertyType, typeof(string), itemKey, out var itemKeyExpr, out _);
                if (itemKeyExpr == null)
                    return Expression.Empty();
                Register(eleValue.PropertyType, itemValue, writer, out var itemValueExpr, out _);
                if (itemValueExpr == null)
                    return Expression.Empty();
                return Expression.IfThenElse(
                    Expression.Equal(value, Expression.Constant(null, type)),
                    Expression.Call(writer, writeNull),
                    Expression.Block(new[] { enumerator },
                        Expression.Call(writer, writeStartObject),
                        Expression.Assign(enumerator, Expression.Call(value, getEnumerator)),
                        Expression.Loop(
                            Expression.IfThenElse(Expression.Call(enumerator, moveNext),
                            Expression.Block(new[] { itemKey, itemValue },
                                Expression.Assign(itemKey, Expression.Property(Expression.Property(enumerator, current), eleKey)),
                                Expression.Call(writer, writeProperty, itemKeyExpr),
                                Expression.Assign(itemValue, Expression.Property(Expression.Property(enumerator, current), eleValue)),
                                itemValueExpr
                            ),
                            Expression.Break(breakLabel)
                            ), breakLabel),
                        Expression.Call(writer, writeEndObject)
                    ));
            });
            //Dictionary<,>
            Register((type, value, writer) => {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Dictionary<,>))
                    return null;

                var getEnumerator = type.GetMethod("GetEnumerator", Type.EmptyTypes);
                var moveNext = getEnumerator.ReturnType.GetMethod("MoveNext", Type.EmptyTypes);
                var current = getEnumerator.ReturnType.GetProperty("Current");
                var eleKey = current.PropertyType.GetProperty("Key");
                var eleValue = current.PropertyType.GetProperty("Value");

                var itemKey = Expression.Variable(eleKey.PropertyType, "itemKey");
                var itemValue = Expression.Variable(eleValue.PropertyType, "itemValue");
                var enumerator = Expression.Variable(getEnumerator.ReturnType, "enumerator");
                var breakLabel = Expression.Label();
                Converter.Register(eleKey.PropertyType, typeof(string), itemKey, out var itemKeyExpr, out _);
                if (itemKeyExpr == null)
                    return Expression.Empty();
                Register(eleValue.PropertyType, itemValue, writer, out var itemValueExpr, out _);
                if (itemValueExpr == null)
                    return Expression.Empty();
                return Expression.IfThenElse(
                    Expression.Equal(value, Expression.Constant(null, type)),
                    Expression.Call(writer, writeNull),
                    Expression.Block(new[] { enumerator },
                        Expression.Call(writer, writeStartObject),
                        Expression.Assign(enumerator, Expression.Call(value, getEnumerator)),
                        Expression.Loop(
                            Expression.IfThenElse(Expression.Call(enumerator, moveNext),
                            Expression.Block(new[] { itemKey, itemValue },
                                Expression.Assign(itemKey, Expression.Property(Expression.Property(enumerator, current), eleKey)),
                                Expression.Call(writer, writeProperty, itemKeyExpr),
                                Expression.Assign(itemValue, Expression.Property(Expression.Property(enumerator, current), eleValue)),
                                itemValueExpr
                            ),
                            Expression.Break(breakLabel)
                            ), breakLabel),
                        Expression.Call(writer, writeEndObject)
                    ));
            });
            //Nullable<>
            Register((type, value, writer) =>
            {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Nullable<>))
                    return null;

                var nullableType = type.GetGenericArguments()[0];
                var nullableValue = Expression.Variable(nullableType, "nullableValue");
                Register(nullableType, nullableValue, writer, out var expression, out _);
                if (expression == null)
                    return Expression.Empty();
                return Expression.IfThenElse(
                        Expression.Property(value, "HasValue"),
                        Expression.Block(new[] { nullableValue },
                            Expression.Assign(nullableValue, Expression.Property(value, "Value")),
                            expression),
                        Expression.Call(writer, writeNull)
                        );
            });
            //ValueTuple
            Register((type, value, writer) =>
            {
                if (!type.IsGenericType)
                    return null;

                var typeDefinition = type.GetGenericTypeDefinition();
                if (typeDefinition == typeof(ValueTuple<>) || typeDefinition == typeof(ValueTuple<,>)
                || typeDefinition == typeof(ValueTuple<,,>) || typeDefinition == typeof(ValueTuple<,,,>)
                || typeDefinition == typeof(ValueTuple<,,,,>) || typeDefinition == typeof(ValueTuple<,,,,,>)
                || typeDefinition == typeof(ValueTuple<,,,,,,>) || typeDefinition == typeof(ValueTuple<,,,,,,,>))
                {
                    var eleTypes = type.GetGenericArguments();
                    var variables = new List<ParameterExpression>();
                    var exprs = new List<Expression>() { Expression.Call(writer, writeStartArray) };
                    var itemIndex = 1;
                    foreach (var eleType in eleTypes)
                    {
                        var item = Expression.Variable(eleType, $"item{itemIndex}");
                        variables.Add(item);
                        Register(eleType, item, writer, out var expression, out _);
                        if (expression == null)
                            return Expression.Empty();
                        var fieldName = itemIndex == 8 ? "Rest" : $"Item{itemIndex++}";
                        exprs.Add(Expression.Assign(item, Expression.Field(value, fieldName)));
                        exprs.Add(expression);
                    }
                    exprs.Add(Expression.Call(writer, writeEndArray));
                    return Expression.Block(variables, exprs);
                }
                return null;
            });
            //Tuple
            Register((type, value, writer) =>
            {
                if (!type.IsGenericType)
                    return null;

                var typeDefinition = type.GetGenericTypeDefinition();
                if (typeDefinition == typeof(Tuple<>) || typeDefinition == typeof(Tuple<,>)
                || typeDefinition == typeof(Tuple<,,>) || typeDefinition == typeof(Tuple<,,,>)
                || typeDefinition == typeof(Tuple<,,,,>) || typeDefinition == typeof(Tuple<,,,,,>)
                || typeDefinition == typeof(Tuple<,,,,,,>) || typeDefinition == typeof(Tuple<,,,,,,,>))
                {
                    var eleTypes = type.GetGenericArguments();
                    var variables = new List<ParameterExpression>();
                    var exprs = new List<Expression>() { Expression.Call(writer, writeStartArray) };
                    var itemIndex = 1;
                    foreach (var eleType in eleTypes)
                    {
                        var item = Expression.Variable(eleType, $"item{itemIndex}");
                        variables.Add(item);
                        Register(eleType, item, writer, out var expression, out _);
                        if (expression == null)
                            return Expression.Empty();
                        var propertyName = itemIndex == 8 ? "Rest" : $"Item{itemIndex++}";
                        exprs.Add(Expression.Assign(item, Expression.Property(value, propertyName)));
                        exprs.Add(expression);
                    }
                    exprs.Add(Expression.Call(writer, writeEndArray));
                    return Expression.Block(variables, exprs);
                }
                return null;
            });
            //KeyValuePair<,>
            Register((type, value, writer) =>
            {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(KeyValuePair<,>))
                    return null;

                var typeArgs = type.GetGenericArguments();
                var pairKey = Expression.Variable(typeArgs[0], "pairKey");
                var pairValue = Expression.Variable(typeArgs[1], "pairValue");
                Register(typeArgs[0], pairKey, writer, out var pairKeyExpr, out _);
                Register(typeArgs[1], pairValue, writer, out var pairValueExpr, out _);
                if (pairKeyExpr == null || pairValueExpr == null)
                    return Expression.Empty();
                return Expression.Block(new[] { pairKey, pairValue },
                    Expression.Assign(pairKey, Expression.Property(value, "Key")),
                    Expression.Assign(pairValue, Expression.Property(value, "Value")),
                    Expression.Call(writer, writeStartArray),
                    pairKeyExpr,
                    pairValueExpr,
                    Expression.Call(writer, writeEndArray)
                    );
            });
            //Array
            Register((type, value, writer) =>
            {
                if (!type.IsArray)
                    return null;

                var eleType = type.GetElementType();
                var eleValue = Expression.Variable(eleType, "item");
                var i = Expression.Variable(typeof(int), "i");
                var length = Expression.Variable(typeof(int), "length");
                var forBreak = Expression.Label();
                Register(eleType, eleValue, writer, out var expression, out _);
                if (expression == null)
                    return Expression.Empty();
                return Expression.IfThenElse(
                        Expression.Equal(value, Expression.Constant(null)),
                        Expression.Call(writer, writeNull),
                        Expression.Block(new[] { i, length },
                            Expression.Assign(i, Expression.Constant(0)),
                            Expression.Assign(length, Expression.ArrayLength(value)),
                            Expression.Call(writer, writeStartArray),
                                Expression.Loop(
                                   Expression.IfThenElse(
                                       Expression.LessThan(i, length),
                                       Expression.Block(new[] { eleValue },
                                           Expression.Assign(eleValue, Expression.ArrayAccess(value, i)),
                                           expression,
                                           Expression.PostIncrementAssign(i)
                                       ),
                                       Expression.Break(forBreak)
                                   ),
                               forBreak
                            ),
                            Expression.Call(writer, writeEndArray)
                        )
                    );
            });
            //Enum
            Register((type, value, writer) => {
                if (!type.IsEnum)
                    return null;

                return Expression.Call(writer, typeof(JsonWriter).GetMethod("WriteNumber", new[] { typeof(int) }), Expression.Convert(value, typeof(int)));
            });
            //object
            Register<object>((value, writer) => {
                if (value == null)
                {
                    writer.WriteNull();
                    return;
                }
                var type = value.GetType();
                if (type == typeof(object))
                {
                    writer.WriteStartObject();
                    writer.WriteEndObject();
                }
                else
                {
                    Register(type, out var handler);
                    handler?.Invoke(value, writer);
                }
            });
            Register<char>((value, writer) =>
            {
                writer.WriteString(MemoryMarshal.CreateSpan(ref value, 1));
            });
            Register(typeof(string), (value, writer) => { return Expression.Call(writer, typeof(JsonWriter).GetMethod("WriteString", new[] { typeof(string) }), value); });
            Register(typeof(bool), (value, writer) => { return Expression.Call(writer, typeof(JsonWriter).GetMethod("WriteBoolean"), value); });
            Register(typeof(byte), (value, writer) => { return Expression.Call(writer, typeof(JsonWriter).GetMethod("WriteNumber", new[] { typeof(byte) }), value); });
            Register(typeof(sbyte), (value, writer) => { return Expression.Call(writer, typeof(JsonWriter).GetMethod("WriteNumber", new[] { typeof(sbyte) }), value); });
            Register(typeof(short), (value, writer) => { return Expression.Call(writer, typeof(JsonWriter).GetMethod("WriteNumber", new[] { typeof(short) }), value); });
            Register(typeof(ushort), (value, writer) => { return Expression.Call(writer, typeof(JsonWriter).GetMethod("WriteNumber", new[] { typeof(ushort) }), value); });
            Register(typeof(int), (value, writer) => { return Expression.Call(writer, typeof(JsonWriter).GetMethod("WriteNumber", new[] { typeof(int) }), value); });
            Register(typeof(uint), (value, writer) => { return Expression.Call(writer, typeof(JsonWriter).GetMethod("WriteNumber", new[] { typeof(uint) }), value); });
            Register(typeof(long), (value, writer) => { return Expression.Call(writer, typeof(JsonWriter).GetMethod("WriteNumber", new[] { typeof(long) }), value); });
            Register(typeof(ulong), (value, writer) => { return Expression.Call(writer, typeof(JsonWriter).GetMethod("WriteNumber", new[] { typeof(ulong) }), value); });
            Register(typeof(float), (value, writer) => { return Expression.Call(writer, typeof(JsonWriter).GetMethod("WriteNumber", new[] { typeof(float) }), value); });
            Register(typeof(double), (value, writer) => { return Expression.Call(writer, typeof(JsonWriter).GetMethod("WriteNumber", new[] { typeof(double) }), value); });
            Register(typeof(decimal), (value, writer) => { return Expression.Call(writer, typeof(JsonWriter).GetMethod("WriteNumber", new[] { typeof(decimal) }), value); });
            Register<char[]>((value, writer) => { writer.WriteString(value.AsSpan()); });
            Register<byte[]>((value, writer) =>
            {
                //use Hex?||TryToBase64Chars?
                writer.WriteString(value == null ? null : Convert.ToBase64String(value));
            });
            Register<Uri>((value, writer) => {
                if (value == null)
                {
                    writer.WriteNull();
                    return;
                }
                writer.WriteString(value.OriginalString);
            });
            Register<DateTime>((value, writer) => {
                var utc = value.ToUniversalTime();
                Span<char> chars = stackalloc char[48];//36?
                if (utc.TryFormat(chars, out var charsWritten, "R"))
                {
                    writer.WriteString(chars.Slice(0, charsWritten));
                }
                else
                {
                    writer.WriteString(utc.ToString("R"));
                }
            });
            Register<DateTimeOffset>((value, writer) => {
                Span<char> chars = stackalloc char[48];
                if (value.TryFormat(chars, out var charsWritten, "R"))
                {
                    writer.WriteString(chars.Slice(0, charsWritten));
                }
                else
                {
                    writer.WriteString(value.ToString("R"));
                }
            });
            Register(typeof(TimeSpan), (value, writer) => {
                return Expression.Call(writer, typeof(JsonWriter).GetMethod("WriteNumber", new[] { typeof(long) }), Expression.Property(value, "Ticks"));
            });
            Register<Guid>((value, writer) => {
                Span<char> chars = stackalloc char[32];
                if (value.TryFormat(chars, out var charsWritten, "N"))//(Null)D??
                {
                    writer.WriteString(chars.Slice(0, charsWritten));
                }
                else
                {
                    writer.WriteString(value.ToString());
                }
            });
            Register(typeof(DBNull), (value, writer) => { return Expression.Call(writer, writeNull); });
            Register<DataTable>((value, writer) => {
                if (value == null)
                {
                    writer.WriteNull();
                    return;
                }
                writer.WriteStartArray();
                foreach (DataRow row in value.Rows)
                {
                    writer.WriteStartObject();
                    foreach (DataColumn column in value.Columns)
                    {
                        object columnValue = row[column];
                        if (columnValue == null)
                        {
                            writer.WriteProperty(column.ColumnName);
                            writer.WriteNull();
                        }
                        else
                        {
                            Register(columnValue.GetType(), out var handler);
                            if (handler != null)
                            {
                                writer.WriteProperty(column.ColumnName);
                                handler(columnValue, writer);
                            }
                        }
                    }
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            });
            //format
            RegisterProperty((property, value, writer) => {
                var dataFormatAttribute = property.GetCustomAttribute<DataFormatAttribute>();
                if (dataFormatAttribute == null)
                    return null;

                RegisterProperty(property.PropertyType, dataFormatAttribute.Format, value, writer, out var expression);
                return expression;
            });
            //ToString(string)
            RegisterProperty((type, format, value, writer) =>
            {
                var toString = type.GetMethod("ToString", new[] { typeof(string) });
                if (toString == null)
                    return null;

                var writeString = typeof(JsonWriter).GetMethod("WriteString", new[] { typeof(string) });
                if (type.IsValueType)
                {
                    return Expression.Call(writer, writeString, Expression.Call(value, toString, Expression.Constant(format, typeof(string))));
                }
                else
                {
                    return Expression.IfThenElse(
                        Expression.Equal(value, Expression.Constant(null)),
                        Expression.Call(writer, writeNull),
                        Expression.Call(writer, writeString, Expression.Call(value, toString, Expression.Constant(format, typeof(string))))
                        );
                }
            });
            //void Invoke(JsonWriter,string)
            RegisterProperty((type, format, value, writer) =>
            {
                var write = type.GetMethod("Invoke", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(JsonWriter), typeof(string) }, null);
                if (write == null)
                    return null;

                if (type.IsValueType)
                {
                    return Expression.Call(value, write, writer, Expression.Constant(format, typeof(string)));
                }
                else
                {
                    return Expression.IfThenElse(
                        Expression.Equal(value, Expression.Constant(null)),
                        Expression.Call(writer, writeNull),
                        Expression.Call(value, write, writer, Expression.Constant(format, typeof(string)))
                        );
                }
            });
            //Nullable<>
            RegisterProperty((type, format, value, writer) =>
            {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Nullable<>))
                    return null;

                var nullableType = type.GetGenericArguments()[0];
                var nullableValue = Expression.Variable(nullableType, "nullableValue");
                RegisterProperty(nullableType, format, nullableValue, writer, out var expression);
                if (expression == null)
                    return Expression.Empty();
                return Expression.IfThenElse(
                        Expression.Property(value, "HasValue"),
                        Expression.Block(new[] { nullableValue },
                            Expression.Assign(nullableValue, Expression.Property(value, "Value")),
                            expression),
                        Expression.Call(writer, writeNull)
                        );
            });
            RegisterProperty<long>((format, value, writer) => {
                Span<char> chars = stackalloc char[20];
                if (value.TryFormat(chars, out var charsWritten, format))
                {
                    writer.WriteString(chars.Slice(0, charsWritten));
                }
                else
                {
                    writer.WriteString(value.ToString(format));
                }
            });
            RegisterProperty<ulong>((format, value, writer) => {
                Span<char> chars = stackalloc char[20];
                if (value.TryFormat(chars, out var charsWritten, format))
                {
                    writer.WriteString(chars.Slice(0, charsWritten));
                }
                else
                {
                    writer.WriteString(value.ToString(format));
                }
            });
            RegisterProperty<DateTime>((format, value, writer) => {
                Span<char> chars = stackalloc char[48];
                if (value.TryFormat(chars, out var charsWritten, format))
                {
                    writer.WriteString(chars.Slice(0, charsWritten));
                }
                else
                {
                    writer.WriteString(value.ToString(format));
                }
            });
            RegisterProperty<DateTimeOffset>((format, value, writer) => {
                Span<char> chars = stackalloc char[48];
                if (value.TryFormat(chars, out var charsWritten, format))
                {
                    writer.WriteString(chars.Slice(0, charsWritten));
                }
                else
                {
                    writer.WriteString(value.ToString(format));
                }
            });
            RegisterProperty<Guid>((format, value, writer) => {
                Span<char> chars = stackalloc char[68];
                if (value.TryFormat(chars, out var charsWritten, format))
                {
                    writer.WriteString(chars.Slice(0, charsWritten));
                }
                else
                {
                    writer.WriteString(value.ToString(format));
                }
            });
        }
        public static void RegisterProperty(Func<PropertyInfo, string> propertyResolver)
        {
            if (propertyResolver == null)
                throw new ArgumentNullException(nameof(propertyResolver));

            lock (_Sync)
            {
                _PropertyResolver = propertyResolver;
            }
        }
        public static void RegisterProperty<T>(Predicate<T> propertyPredicate)
        {
            if (propertyPredicate == null)
                throw new ArgumentNullException(nameof(propertyPredicate));

            RegisterProperty((property, value) => {
                if (property.PropertyType != typeof(T))
                    return null;

                return Expression.Invoke(Expression.Constant(propertyPredicate), value);
            });
        }
        public static void RegisterProperty(Func<PropertyInfo, ParameterExpression, Expression> propertyPredicate)
        {
            if (propertyPredicate == null)
                throw new ArgumentNullException(nameof(propertyPredicate));

            lock (_Sync)
            {
                _PropertyPredicates.Push(propertyPredicate);
            }
        }
        public static void RegisterProperty(PropertyInfo property, ParameterExpression value, out Expression expression)
        {
            if (property == null)
                throw new ArgumentNullException(nameof(property));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            expression = null;
            lock (_Sync)
            {
                foreach (var propertyPredicate in _PropertyPredicates)
                {
                    expression = propertyPredicate(property, value);
                    if (expression != null)
                    {
                        if (expression.Type == typeof(void) && expression.NodeType == ExpressionType.Default)
                            expression = null;
                        return;
                    }
                }
            }
        }
        public static void RegisterProperty<T>(Predicate<string> formatPredicate, Action<T, JsonWriter> propertyFormat)
        {
            if (formatPredicate == null)
                throw new ArgumentNullException(nameof(formatPredicate));
            if (propertyFormat == null)
                throw new ArgumentNullException(nameof(propertyFormat));

            RegisterProperty((type, format, value, writer) => {
                if (type != typeof(T) || !formatPredicate(format))
                    return null;

                return Expression.Invoke(Expression.Constant(propertyFormat), value, writer);
            });
        }
        public static void RegisterProperty<T>(Action<string, T, JsonWriter> propertyFormat)
        {
            if (propertyFormat == null)
                throw new ArgumentNullException(nameof(propertyFormat));

            RegisterProperty((type, format, value, writer) => {
                if (type != typeof(T))
                    return null;

                return Expression.Invoke(Expression.Constant(propertyFormat), Expression.Constant(format, typeof(string)), value, writer);
            });
        }
        public static void RegisterProperty(Func<Type, string, ParameterExpression, ParameterExpression, Expression> propertyFormat)
        {
            if (propertyFormat == null)
                throw new ArgumentNullException(nameof(propertyFormat));

            lock (_Sync)
            {
                _PropertyFormats.Push(propertyFormat);
            }
        }
        public static void RegisterProperty(Type type, string format, ParameterExpression value, ParameterExpression writer, out Expression expression)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            expression = null;
            lock (_Sync)
            {
                foreach (var propertyFormat in _PropertyFormats)
                {
                    expression = propertyFormat(type, format, value, writer);
                    if (expression != null)
                    {
                        if (expression.Type == typeof(void) && expression.NodeType == ExpressionType.Default)
                            expression = null;
                        return;
                    }
                }
            }
        }
        public static void RegisterProperty(Func<PropertyInfo, ParameterExpression, ParameterExpression, Expression> propertyHandler)
        {
            if (propertyHandler == null)
                throw new ArgumentNullException(nameof(propertyHandler));

            lock (_Sync)
            {
                _PropertyHandlers.Push(propertyHandler);
            }
        }
        public static void RegisterProperty(PropertyInfo property, ParameterExpression value, ParameterExpression writer, out Expression expression)
        {
            if (property == null)
                throw new ArgumentNullException(nameof(property));
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            expression = null;
            lock (_Sync)
            {
                foreach (var handler in _PropertyHandlers)
                {
                    expression = handler(property, value, writer);
                    if (expression != null)
                    {
                        if (expression.Type == typeof(void) && expression.NodeType == ExpressionType.Default)
                            expression = null;
                        return;
                    }
                }
            }
        }
        public static void Register<T>(Action<T, JsonWriter> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_Sync)
            {
                _Handlers.Push(new Tuple<Type, Delegate>(typeof(T), handler));
            }
        }
        public static void Register(Type type, Func<ParameterExpression, ParameterExpression, Expression> handler)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Register((_type, value, writer) => {
                if (_type != type)
                    return null;

                return handler(value, writer);
            });
        }
        public static void Register(Func<Type, ParameterExpression, ParameterExpression, Expression> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_Sync)
            {
                _Handlers.Push(handler);
            }
        }
        public static void Register(Type type, ParameterExpression value, ParameterExpression writer, out Expression expression, out Delegate @delegate)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            expression = null;
            @delegate = null;
            lock (_Sync)
            {
                foreach (var handler in _Handlers)
                {
                    if (handler is Func<Type, ParameterExpression, ParameterExpression, Expression> exprHandler)
                    {
                        expression = exprHandler.Invoke(type, value, writer);
                        if (expression != null)
                        {
                            if (expression.Type == typeof(void) && expression.NodeType == ExpressionType.Default)
                                expression = null;
                            return;
                        }
                    }
                    else
                    {
                        (var _type, var _delegate) = (Tuple<Type, Delegate>)handler;
                        if (_type == type)
                        {
                            expression = Expression.Invoke(Expression.Constant(_delegate), value, writer);
                            @delegate = _delegate;
                            return;
                        }
                    }
                }
            }
        }
        public static void Register<T>(out Action<T, JsonWriter> handler)
        {
            handler = Handler<T>.Value;
        }
        public static void Register(Type type, out Action<object, JsonWriter> handler)
        {
            if (!_ObjHandlers.TryGetValue(type, out handler))
            {
                lock (_Sync)
                {
                    if (!_ObjHandlers.TryGetValue(type, out handler))
                    {
                        var value = Expression.Variable(type, "value");
                        var writer = Expression.Parameter(typeof(JsonWriter), "writer");
                        Register(type, value, writer, out var expression, out _);
                        if (expression != null)
                        {
                            var objValue = Expression.Parameter(typeof(object), "objValue");
                            var expr = Expression.Block(new[] { value }, Expression.Assign(value, Expression.Convert(objValue, type)), expression);
                            handler = Expression.Lambda<Action<object, JsonWriter>>(expr, objValue, writer).Compile();
                        }
                        var objHandlers = new Dictionary<Type, Action<object, JsonWriter>>(_ObjHandlers);
                        objHandlers.Add(type, handler);
                        _ObjHandlers = objHandlers;
                    }
                }
            }
        }
        #endregion
    }
}
