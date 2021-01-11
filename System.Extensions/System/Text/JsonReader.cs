
namespace System.Text
{
    using System.IO;
    using System.Data;
    using System.Buffers;
    using System.Dynamic;
    using System.Diagnostics;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Runtime.Serialization;
    public abstract class JsonReader
    {
        #region abstract
        public abstract bool Read();
        public abstract bool IsStartArray { get; }
        public abstract bool IsEndArray { get; }
        public abstract bool IsStartObject { get; }
        public abstract bool IsEndObject { get; }
        public abstract bool IsProperty { get; }
        public abstract bool IsString { get; }
        public abstract bool IsNumber { get; }
        public abstract bool IsBoolean { get; }
        public abstract bool IsNull { get; }
        public abstract string GetProperty();
        public abstract void GetProperty(out ReadOnlySpan<char> property);
        public abstract string GetString();
        public abstract void GetString(out ReadOnlySpan<char> value);
        public abstract byte GetByte();
        public abstract sbyte GetSByte();
        public abstract short GetInt16();
        public abstract ushort GetUInt16();
        public abstract int GetInt32();
        public abstract uint GetUInt32();
        public abstract long GetInt64();
        public abstract ulong GetUInt64();
        public abstract float GetSingle();
        public abstract double GetDouble();
        public abstract decimal GetDecimal();
        public abstract void GetNumber(out ReadOnlySpan<char> value);
        public abstract bool GetBoolean();
        public abstract void Skip();
        #endregion

        #region Create
        public static JsonReader Create(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return new StringJsonReader(value);
        }
        public static unsafe JsonReader Create(char* value, int length)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            return new MemoryJsonReader(value, length);
        }
        public static JsonReader Create(ReadOnlyMemory<char> value, out IDisposable disposable)
        {
            var memoryHandle = value.Pin();
            disposable = memoryHandle;
            unsafe { return Create((char*)memoryHandle.Pointer, value.Length); }
        }
        public static JsonReader Create(ReadOnlySequence<char> value, out IDisposable disposable)
        {
            if (value.IsSingleSegment)
                return Create(value.First, out disposable);

            var reader = new SequenceJsonReader(value);
            disposable = reader;
            return reader;
        }
        public static JsonReader Create(TextReader reader, int bufferSize)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));
            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));

            return new TextJsonReader(reader, bufferSize);
        }
        #endregion
        #region CreateJson5
        public static JsonReader CreateJson5(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return new StringJson5Reader(value);
        }
        public static unsafe JsonReader CreateJson5(char* value, int length)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            return new MemoryJson5Reader(value, length);
        }
        public static JsonReader CreateJson5(ReadOnlyMemory<char> value, out IDisposable disposable)
        {
            var memoryHandle = value.Pin();
            disposable = memoryHandle;
            unsafe { return CreateJson5((char*)memoryHandle.Pointer, value.Length); }
        }
        public static JsonReader CreateJson5(ReadOnlySequence<char> value, out IDisposable disposable)
        {
            if (value.IsSingleSegment)
                return CreateJson5(value.First, out disposable);

            var reader = new SequenceJson5Reader(value);
            disposable = reader;
            return reader;
        }
        public static JsonReader CreateJson5(TextReader reader, int bufferSize)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));
            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));

            return new TextJson5Reader(reader, bufferSize);
        }
        #endregion

        #region FromJson
        [ThreadStatic] private static StringJsonReader _StringJsonReader;
        [ThreadStatic] private static MemoryJsonReader _MemoryJsonReader;
        [ThreadStatic] private static SequenceJsonReader _SequenceJsonReader;
        [ThreadStatic] private static TextJsonReader _TextJsonReader;
        public static T FromJson<T>(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            Register<T>(out var handler);
            if (handler == null)
                throw new NotSupportedException($"JsonReader:{typeof(T)}");

            var reader = _StringJsonReader;
            if (reader == null)
            {
                reader = new StringJsonReader(value);
            }
            else
            {
                _StringJsonReader = null;
                reader.Set(value);
            }
            try
            {
                if (!reader.Read())
                    throw new FormatException(nameof(reader));
                var result = handler(reader);
                if (reader.Read())
                    throw new FormatException("EOF");
                return result;
            }
            finally
            {
                reader.Reset();
                _StringJsonReader = reader;
            }
        }
        public static T FromJson<T>(ReadOnlySpan<char> value)
        {
            unsafe
            {
                fixed (char* pValue = value)
                {
                    return FromJson<T>(pValue, value.Length);
                }
            }
        }
        public static unsafe T FromJson<T>(char* value, int length)
        {
            Register<T>(out var handler);
            if (handler == null)
                throw new NotSupportedException($"JsonReader:{typeof(T)}");

            var reader = _MemoryJsonReader;
            if (reader == null)
            {
                reader = new MemoryJsonReader(value, length);
            }
            else
            {
                _MemoryJsonReader = null;
                reader.Set(value, length);
            }
            try
            {
                if (!reader.Read())
                    throw new FormatException(nameof(reader));
                var result = handler(reader);
                if (reader.Read())
                    throw new FormatException("EOF");
                return result;
            }
            finally
            {
                reader.Reset();
                _MemoryJsonReader = reader;
            }
        }
        public static T FromJson<T>(ReadOnlySequence<char> value)
        {
            if (value.IsSingleSegment)
                return FromJson<T>(value.First.Span);

            Register<T>(out var handler);
            if (handler == null)
                throw new NotSupportedException($"JsonReader:{typeof(T)}");

            var reader = _SequenceJsonReader;
            if (reader == null)
            {
                reader = new SequenceJsonReader(value);
            }
            else
            {
                _SequenceJsonReader = null;
                reader.Set(value);
            }
            try
            {
                if (!reader.Read())
                    throw new FormatException(nameof(reader));
                var result = handler(reader);
                if (reader.Read())
                    throw new FormatException("EOF");
                return result;
            }
            finally
            {
                reader.Dispose();
                reader.Reset();
                _SequenceJsonReader = reader;
            }
        }
        public static T FromJson<T>(TextReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            Register<T>(out var handler);
            if (handler == null)
                throw new NotSupportedException($"JsonReader:{typeof(T)}");

            var jsonReader = _TextJsonReader;
            if (jsonReader == null)
            {
                jsonReader = new TextJsonReader(reader, 4096);
            }
            else
            {
                _TextJsonReader = null;
                jsonReader.Set(reader);
            }
            try
            {
                if (!jsonReader.Read())
                    throw new FormatException(nameof(reader));
                var result = handler(jsonReader);
                if (jsonReader.Read())
                    throw new FormatException("EOF");
                return result;
            }
            finally
            {
                jsonReader.Reset();
                _TextJsonReader = jsonReader;
            }
        }
        public static object FromJson(Type type, string value)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            Register(type, out var handler);
            if (handler == null)
                throw new NotSupportedException($"JsonReader:{type}");

            var reader = _StringJsonReader;
            if (reader == null)
            {
                reader = new StringJsonReader(value);
            }
            else
            {
                _StringJsonReader = null;
                reader.Set(value);
            }
            try
            {
                if (!reader.Read())
                    throw new FormatException(nameof(reader));
                var result = handler(reader);
                if (reader.Read())
                    throw new FormatException("EOF");
                return result;
            }
            finally
            {
                reader.Reset();
                _StringJsonReader = reader;
            }
        }
        public static object FromJson(Type type, ReadOnlySpan<char> value)
        {
            unsafe
            {
                fixed (char* pValue = value)
                {
                    return FromJson(type, pValue, value.Length);
                }
            }
        }
        public static unsafe object FromJson(Type type, char* value, int length)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            Register(type, out var handler);
            if (handler == null)
                throw new NotSupportedException($"JsonReader:{type}");

            var reader = _MemoryJsonReader;
            if (reader == null)
            {
                reader = new MemoryJsonReader(value, length);
            }
            else
            {
                _MemoryJsonReader = null;
                reader.Set(value, length);
            }
            try
            {
                if (!reader.Read())
                    throw new FormatException(nameof(reader));
                var result = handler(reader);
                if (reader.Read())
                    throw new FormatException("EOF");
                return result;
            }
            finally
            {
                reader.Reset();
                _MemoryJsonReader = reader;
            }
        }
        public static object FromJson(Type type, ReadOnlySequence<char> value)
        {
            if (value.IsSingleSegment)
                return FromJson(type, value.First.Span);

            if (type == null)
                throw new ArgumentNullException(nameof(type));

            Register(type, out var handler);
            if (handler == null)
                throw new NotSupportedException($"JsonReader:{type}");

            var reader = _SequenceJsonReader;
            if (reader == null)
            {
                reader = new SequenceJsonReader(value);
            }
            else
            {
                _SequenceJsonReader = null;
                reader.Set(value);
            }
            try
            {
                if (!reader.Read())
                    throw new FormatException(nameof(reader));
                var result = handler(reader);
                if (reader.Read())
                    throw new FormatException("EOF");
                return result;
            }
            finally
            {
                reader.Dispose();
                reader.Reset();
                _SequenceJsonReader = reader;
            }
        }
        public static object FromJson(Type type, TextReader reader)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            Register(type, out var handler);
            if (handler == null)
                throw new NotSupportedException($"JsonReader:{type}");

            var jsonReader = _TextJsonReader;
            if (jsonReader == null)
            {
                jsonReader = new TextJsonReader(reader, 4096);
            }
            else
            {
                _TextJsonReader = null;
                jsonReader.Set(reader);
            }
            try
            {
                if (!jsonReader.Read())
                    throw new FormatException(nameof(reader));
                var result = handler(jsonReader);
                if (jsonReader.Read())
                    throw new FormatException("EOF");
                return result;
            }
            finally
            {
                jsonReader.Reset();
                _TextJsonReader = jsonReader;
            }
        }
        #endregion
        #region FromJson5
        [ThreadStatic] private static StringJson5Reader _StringJson5Reader;
        [ThreadStatic] private static MemoryJson5Reader _MemoryJson5Reader;
        [ThreadStatic] private static SequenceJson5Reader _SequenceJson5Reader;
        [ThreadStatic] private static TextJson5Reader _TextJson5Reader;
        public static T FromJson5<T>(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            Register<T>(out var handler);
            if (handler == null)
                throw new NotSupportedException($"JsonReader:{typeof(T)}");

            var reader = _StringJson5Reader;
            if (reader == null)
            {
                reader = new StringJson5Reader(value);
            }
            else
            {
                _StringJson5Reader = null;
                reader.Set(value);
            }
            try
            {
                if (!reader.Read())
                    throw new FormatException(nameof(reader));
                var result = handler(reader);
                if (reader.Read())
                    throw new FormatException("EOF");
                return result;
            }
            finally
            {
                reader.Reset();
                _StringJson5Reader = reader;
            }
        }
        public static T FromJson5<T>(ReadOnlySpan<char> value)
        {
            unsafe
            {
                fixed (char* pValue = value)
                {
                    return FromJson5<T>(pValue, value.Length);
                }
            }
        }
        public static unsafe T FromJson5<T>(char* value, int length)
        {
            Register<T>(out var handler);
            if (handler == null)
                throw new NotSupportedException($"JsonReader:{typeof(T)}");

            var reader = _MemoryJson5Reader;
            if (reader == null)
            {
                reader = new MemoryJson5Reader(value, length);
            }
            else
            {
                _MemoryJson5Reader = null;
                reader.Set(value, length);
            }
            try
            {
                if (!reader.Read())
                    throw new FormatException(nameof(reader));
                var result = handler(reader);
                if (reader.Read())
                    throw new FormatException("EOF");
                return result;
            }
            finally
            {
                reader.Reset();
                _MemoryJson5Reader = reader;
            }
        }
        public static T FromJson5<T>(ReadOnlySequence<char> value)
        {
            if (value.IsSingleSegment)
                return FromJson5<T>(value.First.Span);

            Register<T>(out var handler);
            if (handler == null)
                throw new NotSupportedException($"JsonReader:{typeof(T)}");

            var reader = _SequenceJson5Reader;
            if (reader == null)
            {
                reader = new SequenceJson5Reader(value);
            }
            else
            {
                _SequenceJson5Reader = null;
                reader.Set(value);
            }
            try
            {
                if (!reader.Read())
                    throw new FormatException(nameof(reader));
                var result = handler(reader);
                if (reader.Read())
                    throw new FormatException("EOF");
                return result;
            }
            finally
            {
                reader.Dispose();
                reader.Reset();
                _SequenceJson5Reader = reader;
            }
        }
        public static T FromJson5<T>(TextReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            Register<T>(out var handler);
            if (handler == null)
                throw new NotSupportedException($"JsonReader:{typeof(T)}");

            var jsonReader = _TextJson5Reader;
            if (jsonReader == null)
            {
                jsonReader = new TextJson5Reader(reader, 4096);
            }
            else
            {
                _TextJson5Reader = null;
                jsonReader.Set(reader);
            }
            try
            {
                if (!jsonReader.Read())
                    throw new FormatException(nameof(reader));
                var result = handler(jsonReader);
                if (jsonReader.Read())
                    throw new FormatException("EOF");
                return result;
            }
            finally
            {
                jsonReader.Reset();
                _TextJson5Reader = jsonReader;
            }
        }
        public static object FromJson5(Type type, string value)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            Register(type, out var handler);
            if (handler == null)
                throw new NotSupportedException($"JsonReader:{type}");

            var reader = _StringJson5Reader;
            if (reader == null)
            {
                reader = new StringJson5Reader(value);
            }
            else
            {
                _StringJson5Reader = null;
                reader.Set(value);
            }
            try
            {
                if (!reader.Read())
                    throw new FormatException(nameof(reader));
                var result = handler(reader);
                if (reader.Read())
                    throw new FormatException("EOF");
                return result;
            }
            finally
            {
                reader.Reset();
                _StringJson5Reader = reader;
            }
        }
        public static object FromJson5(Type type, ReadOnlySpan<char> value)
        {
            unsafe
            {
                fixed (char* pValue = value)
                {
                    return FromJson5(type, pValue, value.Length);
                }
            }
        }
        public static unsafe object FromJson5(Type type, char* value, int length)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            Register(type, out var handler);
            if (handler == null)
                throw new NotSupportedException($"JsonReader:{type}");

            var reader = _MemoryJson5Reader;
            if (reader == null)
            {
                reader = new MemoryJson5Reader(value, length);
            }
            else
            {
                _MemoryJson5Reader = null;
                reader.Set(value, length);
            }
            try
            {
                if (!reader.Read())
                    throw new FormatException(nameof(reader));
                var result = handler(reader);
                if (reader.Read())
                    throw new FormatException("EOF");
                return result;
            }
            finally
            {
                reader.Reset();
                _MemoryJson5Reader = reader;
            }
        }
        public static object FromJson5(Type type, ReadOnlySequence<char> value)
        {
            if (value.IsSingleSegment)
                return FromJson5(type, value.First.Span);

            if (type == null)
                throw new ArgumentNullException(nameof(type));

            Register(type, out var handler);
            if (handler == null)
                throw new NotSupportedException($"JsonReader:{type}");

            var reader = _SequenceJson5Reader;
            if (reader == null)
            {
                reader = new SequenceJson5Reader(value);
            }
            else
            {
                _SequenceJson5Reader = null;
                reader.Set(value);
            }
            try
            {
                if (!reader.Read())
                    throw new FormatException(nameof(reader));
                var result = handler(reader);
                if (reader.Read())
                    throw new FormatException("EOF");
                return result;
            }
            finally
            {
                reader.Dispose();
                reader.Reset();
                _SequenceJson5Reader = reader;
            }
        }
        public static object FromJson5(Type type, TextReader reader)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            Register(type, out var handler);
            if (handler == null)
                throw new NotSupportedException($"JsonReader:{type}");

            var jsonReader = _TextJson5Reader;
            if (jsonReader == null)
            {
                jsonReader = new TextJson5Reader(reader, 4096);
            }
            else
            {
                _TextJson5Reader = null;
                jsonReader.Set(reader);
            }
            try
            {
                if (!jsonReader.Read())
                    throw new FormatException(nameof(reader));
                var result = handler(jsonReader);
                if (jsonReader.Read())
                    throw new FormatException("EOF");
                return result;
            }
            finally
            {
                jsonReader.Reset();
                _TextJson5Reader = jsonReader;
            }
        }
        #endregion

        #region private
        private class StringJsonReader : JsonReader
        {
            private enum Token : byte
            {
                None,
                StartArray,
                EndArray,
                StartObject,
                EndObject,
                Property,
                String,
                Number,
                Boolean,
                Null
            }
            private static int _MaxDepth = 64;
            #region private
            private string _value;
            private int _position;
            private int _depth = -1;
            private bool[] _isObject;
            private Token _token;
            private bool _isGet;
            private bool _boolean;
            #endregion
            public StringJsonReader(string value)
            {
                _value = value;
                _token = Token.None;
                _isObject = new bool[_MaxDepth];
            }
            public void Set(string value) => _value = value;
            public void Reset()
            {
                _token = Token.None;
                _value = null;
                _position = 0;
                _depth = -1;
            }
            private char GetUnicode()
            {
                if (_position + 4 > _value.Length)
                    throw new FormatException("\\uxxxx");

                var unicode = 0;
                for (var i = 1; ;)
                {
                    var ch = _value[_position++];
                    switch (ch)
                    {
                        case '0':
                            break;
                        case '1':
                            unicode += 1;
                            break;
                        case '2':
                            unicode += 2;
                            break;
                        case '3':
                            unicode += 3;
                            break;
                        case '4':
                            unicode += 4;
                            break;
                        case '5':
                            unicode += 5;
                            break;
                        case '6':
                            unicode += 6;
                            break;
                        case '7':
                            unicode += 7;
                            break;
                        case '8':
                            unicode += 8;
                            break;
                        case '9':
                            unicode += 9;
                            break;
                        case 'a':
                        case 'A':
                            unicode += 10;
                            break;
                        case 'b':
                        case 'B':
                            unicode += 11;
                            break;
                        case 'c':
                        case 'C':
                            unicode += 12;
                            break;
                        case 'd':
                        case 'D':
                            unicode += 13;
                            break;
                        case 'e':
                        case 'E':
                            unicode += 14;
                            break;
                        case 'f':
                        case 'F':
                            unicode += 15;
                            break;
                        default:
                            throw new FormatException("\\uxxxx");
                    }
                    if (i == 4)
                        return (char)unicode;
                    i += 1;
                    unicode <<= 4;
                }
            }
            public override bool Read()
            {
                #region function
                bool Next()
                {
                    if (_depth == -1)
                        return NextValue();

                    if (_isObject[_depth])
                    {
                        while (_position < _value.Length)
                        {
                            var ch = _value[_position++];
                            switch (ch)
                            {
                                case ',':
                                    return NextProperty();
                                case '}':
                                    if (!_isObject[_depth--])
                                        throw new FormatException($"Unexpected character '{ch}'");
                                    _token = Token.EndObject;
                                    return true;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{ch}'");
                            }
                        }
                        return false;
                    }
                    else
                    {
                        while (_position < _value.Length)
                        {
                            var ch = _value[_position++];
                            switch (ch)
                            {
                                case ',':
                                    return NextValue();
                                case ']':
                                    if (_isObject[_depth--])
                                        throw new FormatException($"Unexpected character '{ch}'");
                                    _token = Token.EndArray;
                                    return true;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{ch}'");
                            }
                        }
                        return false;
                    }
                }
                bool NextProperty()
                {
                    while (_position < _value.Length)
                    {
                        var ch = _value[_position++];
                        switch (ch)
                        {
                            case '"':
                                _isGet = false;
                                _token = Token.Property;
                                return true;
                            case ' ':
                            case '\t':
                            case '\r':
                            case '\n':
                            case '\v':
                            case '\f':
                            case '\u0000':
                            case '\u0085':
                            case '\u2028':
                            case '\u2029':
                            case '\u00A0':
                            case '\uFEFF':
                                continue;
                            case '}':
                                if (_token != Token.StartObject || !_isObject[_depth--])
                                    throw new FormatException($"Unexpected character '{ch}'");
                                _token = Token.EndObject;
                                return true;
                            default:
                                throw new FormatException($"Unexpected character '{ch}'");
                        }
                    }
                    return false;
                }
                bool NextValue()
                {
                    while (_position < _value.Length)
                    {
                        var ch = _value[_position];
                        switch (ch)
                        {
                            case '{':
                                _position += 1;
                                _depth += 1;
                                _isObject[_depth] = true;
                                _token = Token.StartObject;
                                return true;
                            case '[':
                                _position += 1;
                                _depth += 1;
                                _isObject[_depth] = false;
                                _token = Token.StartArray;
                                return true;
                            case '"':
                                _position += 1;
                                _isGet = false;
                                _token = Token.String;
                                return true;
                            case 'n':
                                if (_value.Length - _position < 4 || _value[_position + 1] != 'u' ||
                                    _value[_position + 2] != 'l' || _value[_position + 3] != 'l')
                                    throw new FormatException("Unexpected character");
                                _position += 4;
                                _token = Token.Null;
                                return true;
                            case 't':
                                if (_value.Length - _position < 4 || _value[_position + 1] != 'r' ||
                                     _value[_position + 2] != 'u' || _value[_position + 3] != 'e')
                                    throw new FormatException("Unexpected character");
                                _position += 4;
                                _boolean = true;
                                _token = Token.Boolean;
                                return true;
                            case 'f':
                                if (_value.Length - _position < 5 || _value[_position + 1] != 'a' ||
                                    _value[_position + 2] != 'l' || _value[_position + 3] != 's' || _value[_position + 4] != 'e')
                                    throw new FormatException("Unexpected character");
                                _position += 5;
                                _boolean = false;
                                _token = Token.Boolean;
                                return true;
                            case '0':
                            case '1':
                            case '2':
                            case '3':
                            case '4':
                            case '5':
                            case '6':
                            case '7':
                            case '8':
                            case '9':
                            case '+':
                            case '-':
                            case '.':
                            case 'N':
                            case 'I':
                                _isGet = false;
                                _token = Token.Number;
                                return true;
                            case ' ':
                            case '\t':
                            case '\r':
                            case '\n':
                            case '\v':
                            case '\f':
                            case '\u0000':
                            case '\u0085':
                            case '\u2028':
                            case '\u2029':
                            case '\u00A0':
                            case '\uFEFF':
                                _position += 1;
                                continue;
                            case ']':
                                if (_token != Token.StartArray || _isObject[_depth--])
                                    throw new FormatException($"Unexpected character '{ch}'");
                                _position += 1;
                                _token = Token.EndArray;
                                return true;
                            default:
                                throw new FormatException($"Unexpected character '{ch}'");
                        }
                    }
                    return false;
                }
                #endregion
                switch (_token)
                {
                    case Token.None:
                        return NextValue();
                    case Token.StartObject:
                        return NextProperty();
                    case Token.Property:
                        if (!_isGet)
                            SkipProperty();
                        return NextValue();
                    case Token.String:
                        if (!_isGet)
                            SkipString();
                        return Next();
                    case Token.Number:
                        if (!_isGet)
                            SkipNumber();
                        return Next();
                    case Token.Boolean:
                    case Token.Null:
                        return Next();
                    case Token.StartArray:
                        return NextValue();
                    case Token.EndObject:
                    case Token.EndArray:
                        return Next();
                    default:
                        return false;
                }
            }
            public override bool IsStartArray => _token == Token.StartArray;
            public override bool IsEndArray => _token == Token.EndArray;
            public override bool IsStartObject => _token == Token.StartObject;
            public override bool IsEndObject => _token == Token.EndObject;
            public override bool IsProperty => _token == Token.Property;
            public override bool IsString => _token == Token.String;
            public override bool IsNumber => _token == Token.Number;
            public override bool IsBoolean => _token == Token.Boolean;
            public override bool IsNull => _token == Token.Null;
            private void SkipProperty()
            {
                Debug.Assert(!_isGet);
                for (; ; )
                {
                    if (_position == _value.Length)
                        throw new FormatException("EOF");
                    var ch = _value[_position++];
                    if (ch == '\\')
                    {
                        if (_position == _value.Length)
                            throw new FormatException("EOF");
                        ch = _value[_position++];
                        switch (ch)
                        {
                            case '0':
                            case '\'':
                            case '"':
                            case '\\':
                            case '/':
                            case 'r':
                            case 'n':
                            case 't':
                            case 'v':
                            case 'b':
                            case 'f':
                                break;
                            case 'u':
                                _position += 4;
                                break;
                            default:
                                throw new NotSupportedException("\\" + ch);
                        }
                    }
                    else if (ch == '"')
                    {
                        for (; ; )
                        {
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case ':':
                                    _isGet = true;
                                    return;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{ch}'");
                            }
                        }
                    }
                }
            }
            public override string GetProperty()
            {
                if (_token != Token.Property || _isGet)
                    throw new InvalidOperationException(nameof(GetProperty));

                var start = _position;
                char ch;
                for (; ; )
                {
                    if (_position == _value.Length)
                        throw new FormatException("EOF");
                    ch = _value[_position++];
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '"')
                    {
                        var property = _value.Substring(start, _position - start - 1);
                        for (; ; )
                        {
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case ':':
                                    _isGet = true;
                                    return property;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{ch}'");
                            }
                        }
                    }
                }

                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        sb.Write(_value.AsSpan(start, _position - start - 1));
                        for (; ; )
                        {
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case '0':
                                    sb.Write('\u0000');
                                    break;
                                case '\'':
                                    sb.Write('\'');
                                    break;
                                case '"':
                                    sb.Write('"');
                                    break;
                                case '\\':
                                    sb.Write('\\');
                                    break;
                                case '/':
                                    sb.Write('/');
                                    break;
                                case 'r':
                                    sb.Write('\r');
                                    break;
                                case 'n':
                                    sb.Write('\n');
                                    break;
                                case 't':
                                    sb.Write('\t');
                                    break;
                                case 'v':
                                    sb.Write('\v');
                                    break;
                                case 'b':
                                    sb.Write('\b');
                                    break;
                                case 'f':
                                    sb.Write('\f');
                                    break;
                                case 'u':
                                    sb.Write(GetUnicode());
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + ch);
                            }

                            if (_position == _value.Length)
                                throw new FormatException("EOF");

                            ch = _value[_position];
                            if (ch == '\\')
                            {
                                _position++;
                            }
                            else
                            {
                                start = _position;
                                _position++;
                                break;
                            }
                        }
                        for (; ; )
                        {
                            if (ch == '"')
                            {
                                sb.Write(_value.AsSpan(start, _position - start - 1));
                                for (; ; )
                                {
                                    if (_position == _value.Length)
                                        throw new FormatException("EOF");
                                    ch = _value[_position++];
                                    switch (ch)
                                    {
                                        case ':':
                                            _isGet = true;
                                            return sb.ToString();
                                        case ' ':
                                        case '\t':
                                        case '\r':
                                        case '\n':
                                        case '\v':
                                        case '\f':
                                        case '\u0000':
                                        case '\u0085':
                                        case '\u2028':
                                        case '\u2029':
                                        case '\u00A0':
                                        case '\uFEFF':
                                            continue;
                                        default:
                                            throw new FormatException($"Unexpected character '{ch}'");
                                    }
                                }
                            }
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            if (ch == '\\')
                            {
                                break;
                            }
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override void GetProperty(out ReadOnlySpan<char> property)
            {
                if (_token != Token.Property || _isGet)
                    throw new InvalidOperationException(nameof(GetProperty));

                var start = _position;
                char ch;
                for (; ; )
                {
                    if (_position == _value.Length)
                        throw new FormatException("EOF");
                    ch = _value[_position++];
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '"')
                    {
                        property = _value.AsSpan(start, _position - start - 1);
                        for (; ; )
                        {
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case ':':
                                    _isGet = true;
                                    return;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{ch}'");
                            }
                        }
                    }
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        sb.Write(_value.AsSpan(start, _position - start - 1));
                        for (; ; )
                        {
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case '0':
                                    sb.Write('\u0000');
                                    break;
                                case '\'':
                                    sb.Write('\'');
                                    break;
                                case '"':
                                    sb.Write('"');
                                    break;
                                case '\\':
                                    sb.Write('\\');
                                    break;
                                case '/':
                                    sb.Write('/');
                                    break;
                                case 'r':
                                    sb.Write('\r');
                                    break;
                                case 'n':
                                    sb.Write('\n');
                                    break;
                                case 't':
                                    sb.Write('\t');
                                    break;
                                case 'v':
                                    sb.Write('\v');
                                    break;
                                case 'b':
                                    sb.Write('\b');
                                    break;
                                case 'f':
                                    sb.Write('\f');
                                    break;
                                case 'u':
                                    sb.Write(GetUnicode());
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + ch);
                            }
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position];
                            if (ch == '\\')
                            {
                                _position++;
                            }
                            else
                            {
                                start = _position;
                                _position++;
                                break;
                            }
                        }
                        for (; ; )
                        {
                            if (ch == '"')
                            {
                                sb.Write(_value.AsSpan(start, _position - start - 1));
                                property = sb.ToString();
                                for (; ; )
                                {
                                    if (_position == _value.Length)
                                        throw new FormatException("EOF");
                                    ch = _value[_position++];
                                    switch (ch)
                                    {
                                        case ':':
                                            _isGet = true;
                                            return;
                                        case ' ':
                                        case '\t':
                                        case '\r':
                                        case '\n':
                                        case '\v':
                                        case '\f':
                                        case '\u0000':
                                        case '\u0085':
                                        case '\u2028':
                                        case '\u2029':
                                        case '\u00A0':
                                        case '\uFEFF':
                                            continue;
                                        default:
                                            throw new FormatException($"Unexpected character '{ch}'");
                                    }
                                }
                            }
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            if (ch == '\\')
                            {
                                break;
                            }
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            private void SkipString()
            {
                Debug.Assert(!_isGet);
                for (; ; )
                {
                    if (_position == _value.Length)
                        throw new FormatException("EOF");
                    var ch = _value[_position++];
                    if (ch == '\\')
                    {
                        if (_position == _value.Length)
                            throw new FormatException("EOF");
                        ch = _value[_position++];
                        switch (ch)
                        {
                            case '0':
                            case '\'':
                            case '"':
                            case '\\':
                            case '/':
                            case 'r':
                            case 'n':
                            case 't':
                            case 'v':
                            case 'b':
                            case 'f':
                                break;
                            case 'u':
                                _position += 4;
                                break;
                            default:
                                throw new NotSupportedException("\\" + ch);
                        }
                    }
                    else if (ch == '"')
                    {
                        _isGet = true;
                        return;
                    }
                }
            }
            public override string GetString()
            {
                if (_token == Token.Null)
                    return null;
                if (_token != Token.String || _isGet)
                    throw new InvalidOperationException(nameof(GetString));

                var start = _position;
                char ch;
                for (; ; )
                {
                    if (_position == _value.Length)
                        throw new FormatException("EOF");
                    ch = _value[_position++];
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '"')
                    {
                        _isGet = true;
                        return _value.Substring(start, _position - start - 1);
                    }
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        sb.Write(_value.AsSpan(start, _position - start - 1));
                        for (; ; )
                        {
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case '0':
                                    sb.Write('\u0000');
                                    break;
                                case '\'':
                                    sb.Write('\'');
                                    break;
                                case '"':
                                    sb.Write('"');
                                    break;
                                case '\\':
                                    sb.Write('\\');
                                    break;
                                case '/':
                                    sb.Write('/');
                                    break;
                                case 'r':
                                    sb.Write('\r');
                                    break;
                                case 'n':
                                    sb.Write('\n');
                                    break;
                                case 't':
                                    sb.Write('\t');
                                    break;
                                case 'v':
                                    sb.Write('\v');
                                    break;
                                case 'b':
                                    sb.Write('\b');
                                    break;
                                case 'f':
                                    sb.Write('\f');
                                    break;
                                case 'u':
                                    sb.Write(GetUnicode());
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + ch);
                            }
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position];
                            if (ch == '\\')
                            {
                                _position++;
                            }
                            else
                            {
                                start = _position;
                                _position++;
                                break;
                            }
                        }
                        for (; ; )
                        {
                            if (ch == '"')
                            {
                                sb.Write(_value.AsSpan(start, _position - start - 1));
                                _isGet = true;
                                return sb.ToString();
                            }
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            if (ch == '\\')
                            {
                                break;
                            }
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override void GetString(out ReadOnlySpan<char> value)
            {
                if (_token == Token.Null)
                {
                    value = null;
                    return;
                }
                if (_token != Token.String || _isGet)
                    throw new InvalidOperationException(nameof(GetString));

                var start = _position;
                char ch;
                for (; ; )
                {
                    if (_position == _value.Length)
                        throw new FormatException("EOF");
                    ch = _value[_position++];
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '"')
                    {
                        _isGet = true;
                        value = _value.AsSpan(start, _position - start - 1);
                        return;
                    }
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        sb.Write(_value.AsSpan(start, _position - start - 1));
                        for (; ; )
                        {
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case '0':
                                    sb.Write('\u0000');
                                    break;
                                case '\'':
                                    sb.Write('\'');
                                    break;
                                case '"':
                                    sb.Write('"');
                                    break;
                                case '\\':
                                    sb.Write('\\');
                                    break;
                                case '/':
                                    sb.Write('/');
                                    break;
                                case 'r':
                                    sb.Write('\r');
                                    break;
                                case 'n':
                                    sb.Write('\n');
                                    break;
                                case 't':
                                    sb.Write('\t');
                                    break;
                                case 'v':
                                    sb.Write('\v');
                                    break;
                                case 'b':
                                    sb.Write('\b');
                                    break;
                                case 'f':
                                    sb.Write('\f');
                                    break;
                                case 'u':
                                    sb.Write(GetUnicode());
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + ch);
                            }
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position];
                            if (ch == '\\')
                            {
                                _position++;
                            }
                            else
                            {
                                start = _position;
                                _position++;
                                break;
                            }
                        }
                        for (; ; )
                        {
                            if (ch == '"')
                            {
                                sb.Write(_value.AsSpan(start, _position - start - 1));
                                _isGet = true;
                                value = sb.ToString();
                                return;
                            }
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            if (ch == '\\')
                            {
                                break;
                            }
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override byte GetByte()
            {
                GetNumber(out var number);
                return byte.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override sbyte GetSByte()
            {
                GetNumber(out var number);
                return sbyte.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override short GetInt16()
            {
                GetNumber(out var number);
                return short.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override ushort GetUInt16()
            {
                GetNumber(out var number);
                return ushort.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override int GetInt32()
            {
                GetNumber(out var number);
                return int.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override uint GetUInt32()
            {
                GetNumber(out var number);
                return uint.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override long GetInt64()
            {
                GetNumber(out var number);
                return long.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override ulong GetUInt64()
            {
                GetNumber(out var number);
                return ulong.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override float GetSingle()
            {
                GetNumber(out var number);
                return float.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override double GetDouble()
            {
                GetNumber(out var number);
                return double.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override decimal GetDecimal()
            {
                GetNumber(out var number);
                return decimal.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            private void SkipNumber()
            {
                Debug.Assert(!_isGet);
                _position++;
                for (; ; )
                {
                    if (_position == _value.Length)
                    {
                        _isGet = true;
                        return;
                    }
                    var ch = _value[_position];
                    switch (ch)
                    {
                        case ',':
                        case '}':
                        case ']':
                            _isGet = true;
                            return;
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                        case '\v':
                        case '\f':
                        case '\u0000':
                        case '\u0085':
                        case '\u2028':
                        case '\u2029':
                        case '\u00A0':
                        case '\uFEFF':
                            _isGet = true;
                            _position += 1;
                            return;
                        default:
                            _position += 1;
                            continue;
                    }
                }
            }
            public override void GetNumber(out ReadOnlySpan<char> value)
            {
                if (_token != Token.Number || _isGet)
                    throw new InvalidOperationException(nameof(GetNumber));

                var start = _position++;
                for (; ; )
                {
                    if (_position == _value.Length)
                    {
                        _isGet = true;
                        value = _value.AsSpan(start);
                        return;
                    }
                    var ch = _value[_position];
                    switch (ch)
                    {
                        case ',':
                        case '}':
                        case ']':
                            _isGet = true;
                            value = _value.AsSpan(start, _position - start);
                            return;
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                        case '\v':
                        case '\f':
                        case '\u0000':
                        case '\u0085':
                        case '\u2028':
                        case '\u2029':
                        case '\u00A0':
                        case '\uFEFF':
                            _isGet = true;
                            value = _value.AsSpan(start, _position - start);
                            _position += 1;
                            return;
                        default:
                            _position += 1;
                            continue;
                    }
                }
            }
            public override bool GetBoolean()
            {
                if (_token != Token.Boolean)
                    throw new InvalidOperationException(nameof(GetBoolean));

                return _boolean;
            }
            public override void Skip()
            {
                switch (_token)
                {
                    case Token.None:
                        if (Read())
                        {
                            Skip();
                        }
                        return;
                    case Token.StartArray:
                        for (; ; )
                        {
                            if (!Read())
                                throw new FormatException("EOF");

                            if (_token == Token.EndArray)
                                return;

                            Skip();
                        }
                    case Token.StartObject:
                        for (; ; )
                        {
                            if (!Read())
                                throw new FormatException("EOF");

                            if (_token == Token.EndObject)
                                return;

                            Skip();
                        }
                    case Token.Property:
                        {
                            if (!Read())
                                throw new FormatException("EOF");

                            Skip();
                            return;
                        }
                    default:
                        return;
                }
            }
        }
        private class MemoryJsonReader : JsonReader
        {
            private enum Token : byte
            {
                None,
                StartArray,
                EndArray,
                StartObject,
                EndObject,
                Property,
                String,
                Number,
                Boolean,
                Null
            }
            private static int _MaxDepth = 64;
            #region private
            private int _length;
            private unsafe char* _value;
            private int _position;
            private int _depth = -1;
            private bool[] _isObject;
            private Token _token;
            private bool _isGet;
            private bool _boolean;
            #endregion
            public unsafe MemoryJsonReader(char* value, int length)
            {
                _value = value;
                _length = length;
                _token = Token.None;
                _isObject = new bool[_MaxDepth];
            }
            public unsafe void Set(char* value, int length)
            {
                _value = value;
                _length = length;
            }
            public void Reset()
            {
                _token = Token.None;
                _length = 0;
                _position = 0;
                _depth = -1;
            }
            private char GetUnicode()
            {
                if (_position + 4 > _length)
                    throw new FormatException("\\uxxxx");

                unsafe
                {
                    var unicode = 0;
                    for (var i = 1; ;)
                    {
                        var ch = _value[_position++];
                        switch (ch)
                        {
                            case '0':
                                break;
                            case '1':
                                unicode += 1;
                                break;
                            case '2':
                                unicode += 2;
                                break;
                            case '3':
                                unicode += 3;
                                break;
                            case '4':
                                unicode += 4;
                                break;
                            case '5':
                                unicode += 5;
                                break;
                            case '6':
                                unicode += 6;
                                break;
                            case '7':
                                unicode += 7;
                                break;
                            case '8':
                                unicode += 8;
                                break;
                            case '9':
                                unicode += 9;
                                break;
                            case 'a':
                            case 'A':
                                unicode += 10;
                                break;
                            case 'b':
                            case 'B':
                                unicode += 11;
                                break;
                            case 'c':
                            case 'C':
                                unicode += 12;
                                break;
                            case 'd':
                            case 'D':
                                unicode += 13;
                                break;
                            case 'e':
                            case 'E':
                                unicode += 14;
                                break;
                            case 'f':
                            case 'F':
                                unicode += 15;
                                break;
                            default:
                                throw new FormatException("\\uxxxx");
                        }
                        if (i == 4)
                            return (char)unicode;
                        i += 1;
                        unicode <<= 4;
                    }
                }
            }
            public override bool Read()
            {
                #region function
                unsafe bool Next()
                {
                    if (_depth == -1)
                        return NextValue();

                    if (_isObject[_depth])
                    {
                        while (_position < _length)
                        {
                            var ch = _value[_position++];
                            switch (ch)
                            {
                                case ',':
                                    return NextProperty();
                                case '}':
                                    if (!_isObject[_depth--])
                                        throw new FormatException($"Unexpected character '{ch}'");
                                    _token = Token.EndObject;
                                    return true;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{ch}'");
                            }
                        }
                        return false;
                    }
                    else
                    {
                        while (_position < _length)
                        {
                            var ch = _value[_position++];
                            switch (ch)
                            {
                                case ',':
                                    return NextValue();
                                case ']':
                                    if (_isObject[_depth--])
                                        throw new FormatException($"Unexpected character '{ch}'");
                                    _token = Token.EndArray;
                                    return true;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{ch}'");
                            }
                        }
                        return false;
                    }
                }
                unsafe bool NextProperty()
                {
                    while (_position < _length)
                    {
                        var ch = _value[_position++];
                        switch (ch)
                        {
                            case '"':
                                _isGet = false;
                                _token = Token.Property;
                                return true;
                            case ' ':
                            case '\t':
                            case '\r':
                            case '\n':
                            case '\v':
                            case '\f':
                            case '\u0000':
                            case '\u0085':
                            case '\u2028':
                            case '\u2029':
                            case '\u00A0':
                            case '\uFEFF':
                                continue;
                            case '}':
                                if (_token != Token.StartObject || !_isObject[_depth--])
                                    throw new FormatException($"Unexpected character '{ch}'");
                                _token = Token.EndObject;
                                return true;
                            default:
                                throw new FormatException($"Unexpected character '{ch}'");
                        }
                    }
                    return false;
                }
                unsafe bool NextValue()
                {
                    while (_position < _length)
                    {
                        var ch = _value[_position];
                        switch (ch)
                        {
                            case '{':
                                _position += 1;
                                _depth += 1;
                                _isObject[_depth] = true;
                                _token = Token.StartObject;
                                return true;
                            case '[':
                                _position += 1;
                                _depth += 1;
                                _isObject[_depth] = false;
                                _token = Token.StartArray;
                                return true;
                            case '"':
                                _position += 1;
                                _isGet = false;
                                _token = Token.String;
                                return true;
                            case 'n':
                                if (_length - _position < 4 || _value[_position + 1] != 'u' ||
                                    _value[_position + 2] != 'l' || _value[_position + 3] != 'l')
                                    throw new FormatException("Unexpected character");
                                _position += 4;
                                _token = Token.Null;
                                return true;
                            case 't':
                                if (_length - _position < 4 || _value[_position + 1] != 'r' ||
                                     _value[_position + 2] != 'u' || _value[_position + 3] != 'e')
                                    throw new FormatException("Unexpected character");
                                _position += 4;
                                _boolean = true;
                                _token = Token.Boolean;
                                return true;
                            case 'f':
                                if (_length - _position < 5 || _value[_position + 1] != 'a' ||
                                    _value[_position + 2] != 'l' || _value[_position + 3] != 's' || _value[_position + 4] != 'e')
                                    throw new FormatException("Unexpected character");
                                _position += 5;
                                _boolean = false;
                                _token = Token.Boolean;
                                return true;
                            case '0':
                            case '1':
                            case '2':
                            case '3':
                            case '4':
                            case '5':
                            case '6':
                            case '7':
                            case '8':
                            case '9':
                            case '+':
                            case '-':
                            case '.':
                            case 'N':
                            case 'I':
                                _isGet = false;
                                _token = Token.Number;
                                return true;
                            case ' ':
                            case '\t':
                            case '\r':
                            case '\n':
                            case '\v':
                            case '\f':
                            case '\u0000':
                            case '\u0085':
                            case '\u2028':
                            case '\u2029':
                            case '\u00A0':
                            case '\uFEFF':
                                _position += 1;
                                continue;
                            case ']':
                                if (_token != Token.StartArray || _isObject[_depth--])
                                    throw new FormatException($"Unexpected character '{ch}'");
                                _position += 1;
                                _token = Token.EndArray;
                                return true;
                            default:
                                throw new FormatException($"Unexpected character '{ch}'");
                        }
                    }
                    return false;
                }
                #endregion
                unsafe
                {
                    switch (_token)
                    {
                        case Token.None:
                            return NextValue();
                        case Token.StartObject:
                            return NextProperty();
                        case Token.Property:
                            if (!_isGet)
                                SkipProperty();
                            return NextValue();
                        case Token.String:
                            if (!_isGet)
                                SkipString();
                            return Next();
                        case Token.Number:
                            if (!_isGet)
                                SkipNumber();
                            return Next();
                        case Token.Boolean:
                        case Token.Null:
                            return Next();
                        case Token.StartArray:
                            return NextValue();
                        case Token.EndObject:
                        case Token.EndArray:
                            return Next();
                        default:
                            return false;
                    }
                }
            }
            public override bool IsStartArray => _token == Token.StartArray;
            public override bool IsEndArray => _token == Token.EndArray;
            public override bool IsStartObject => _token == Token.StartObject;
            public override bool IsEndObject => _token == Token.EndObject;
            public override bool IsProperty => _token == Token.Property;
            public override bool IsString => _token == Token.String;
            public override bool IsNumber => _token == Token.Number;
            public override bool IsBoolean => _token == Token.Boolean;
            public override bool IsNull => _token == Token.Null;
            private unsafe void SkipProperty()
            {
                Debug.Assert(!_isGet);
                for (; ; )
                {
                    if (_position == _length)
                        throw new FormatException("EOF");
                    var ch = _value[_position++];
                    if (ch == '\\')
                    {
                        if (_position == _length)
                            throw new FormatException("EOF");
                        ch = _value[_position++];
                        switch (ch)
                        {
                            case '0':
                            case '\'':
                            case '"':
                            case '\\':
                            case '/':
                            case 'r':
                            case 'n':
                            case 't':
                            case 'v':
                            case 'b':
                            case 'f':
                                break;
                            case 'u':
                                _position += 4;
                                break;
                            default:
                                throw new NotSupportedException("\\" + ch);
                        }
                    }
                    else if (ch == '"')
                    {
                        for (; ; )
                        {
                            if (_position == _length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case ':':
                                    _isGet = true;
                                    return;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{ch}'");
                            }
                        }
                    }
                }
            }
            public override string GetProperty()
            {
                if (_token != Token.Property || _isGet)
                    throw new InvalidOperationException(nameof(GetProperty));

                unsafe
                {
                    var start = _position;
                    char ch;
                    for (; ; )
                    {
                        if (_position == _length)
                            throw new FormatException("EOF");
                        ch = _value[_position++];
                        if (ch == '\\')
                        {
                            break;
                        }
                        else if (ch == '"')
                        {
                            var property = new string(_value, start, _position - start - 1);
                            for (; ; )
                            {
                                if (_position == _length)
                                    throw new FormatException("EOF");
                                ch = _value[_position++];
                                switch (ch)
                                {
                                    case ':':
                                        _isGet = true;
                                        return property;
                                    case ' ':
                                    case '\t':
                                    case '\r':
                                    case '\n':
                                    case '\v':
                                    case '\f':
                                    case '\u0000':
                                    case '\u0085':
                                    case '\u2028':
                                    case '\u2029':
                                    case '\u00A0':
                                    case '\uFEFF':
                                        continue;
                                    default:
                                        throw new FormatException($"Unexpected character '{ch}'");
                                }
                            }
                        }
                    }
                    var sb = StringExtensions.ThreadRent(out var disposable);
                    try
                    {
                        for (; ; )
                        {
                            sb.Write(new ReadOnlySpan<char>(_value + start, _position - start - 1));
                            for (; ; )
                            {
                                if (_position == _length)
                                    throw new FormatException("EOF");
                                ch = _value[_position++];
                                switch (ch)
                                {
                                    case '0':
                                        sb.Write('\u0000');
                                        break;
                                    case '\'':
                                        sb.Write('\'');
                                        break;
                                    case '"':
                                        sb.Write('"');
                                        break;
                                    case '\\':
                                        sb.Write('\\');
                                        break;
                                    case '/':
                                        sb.Write('/');
                                        break;
                                    case 'r':
                                        sb.Write('\r');
                                        break;
                                    case 'n':
                                        sb.Write('\n');
                                        break;
                                    case 't':
                                        sb.Write('\t');
                                        break;
                                    case 'v':
                                        sb.Write('\v');
                                        break;
                                    case 'b':
                                        sb.Write('\b');
                                        break;
                                    case 'f':
                                        sb.Write('\f');
                                        break;
                                    case 'u':
                                        sb.Write(GetUnicode());
                                        break;
                                    default:
                                        throw new NotSupportedException("\\" + ch);
                                }
                                if (_position == _length)
                                    throw new FormatException("EOF");
                                ch = _value[_position];
                                if (ch == '\\')
                                {
                                    _position++;
                                }
                                else
                                {
                                    start = _position;
                                    _position++;
                                    break;
                                }
                            }
                            for (; ; )
                            {
                                if (ch == '"')
                                {
                                    sb.Write(new ReadOnlySpan<char>(_value + start, _position - start - 1));
                                    for (; ; )
                                    {
                                        if (_position == _length)
                                            throw new FormatException("EOF");
                                        ch = _value[_position++];
                                        switch (ch)
                                        {
                                            case ':':
                                                _isGet = true;
                                                return sb.ToString();
                                            case ' ':
                                            case '\t':
                                            case '\r':
                                            case '\n':
                                            case '\v':
                                            case '\f':
                                            case '\u0000':
                                            case '\u0085':
                                            case '\u2028':
                                            case '\u2029':
                                            case '\u00A0':
                                            case '\uFEFF':
                                                continue;
                                            default:
                                                throw new FormatException($"Unexpected character '{ch}'");
                                        }
                                    }
                                }
                                if (_position == _length)
                                    throw new FormatException("EOF");
                                ch = _value[_position++];
                                if (ch == '\\')
                                {
                                    break;
                                }
                            }
                        }
                    }
                    finally
                    {
                        disposable.Dispose();
                    }
                }
            }
            public override void GetProperty(out ReadOnlySpan<char> property)
            {
                if (_token != Token.Property || _isGet)
                    throw new InvalidOperationException(nameof(GetProperty));

                unsafe
                {
                    var start = _position;
                    char ch;
                    for (; ; )
                    {
                        if (_position == _length)
                            throw new FormatException("EOF");
                        ch = _value[_position++];
                        if (ch == '\\')
                        {
                            break;
                        }
                        else if (ch == '"')
                        {
                            property = new ReadOnlySpan<char>(_value + start, _position - start - 1);
                            for (; ; )
                            {
                                if (_position == _length)
                                    throw new FormatException("EOF");
                                ch = _value[_position++];
                                switch (ch)
                                {
                                    case ':':
                                        _isGet = true;
                                        return;
                                    case ' ':
                                    case '\t':
                                    case '\r':
                                    case '\n':
                                    case '\v':
                                    case '\f':
                                    case '\u0000':
                                    case '\u0085':
                                    case '\u2028':
                                    case '\u2029':
                                    case '\u00A0':
                                    case '\uFEFF':
                                        continue;
                                    default:
                                        throw new FormatException($"Unexpected character '{ch}'");
                                }
                            }
                        }
                    }
                    var sb = StringExtensions.ThreadRent(out var disposable);
                    try
                    {
                        for (; ; )
                        {
                            sb.Write(new ReadOnlySpan<char>(_value + start, _position - start - 1));
                            for (; ; )
                            {
                                if (_position == _length)
                                    throw new FormatException("EOF");
                                ch = _value[_position++];
                                switch (ch)
                                {
                                    case '0':
                                        sb.Write('\u0000');
                                        break;
                                    case '\'':
                                        sb.Write('\'');
                                        break;
                                    case '"':
                                        sb.Write('"');
                                        break;
                                    case '\\':
                                        sb.Write('\\');
                                        break;
                                    case '/':
                                        sb.Write('/');
                                        break;
                                    case 'r':
                                        sb.Write('\r');
                                        break;
                                    case 'n':
                                        sb.Write('\n');
                                        break;
                                    case 't':
                                        sb.Write('\t');
                                        break;
                                    case 'v':
                                        sb.Write('\v');
                                        break;
                                    case 'b':
                                        sb.Write('\b');
                                        break;
                                    case 'f':
                                        sb.Write('\f');
                                        break;
                                    case 'u':
                                        sb.Write(GetUnicode());
                                        break;
                                    default:
                                        throw new NotSupportedException("\\" + ch);
                                }
                                if (_position == _length)
                                    throw new FormatException("EOF");
                                ch = _value[_position];
                                if (ch == '\\')
                                {
                                    _position++;
                                }
                                else
                                {
                                    start = _position;
                                    _position++;
                                    break;
                                }
                            }
                            for (; ; )
                            {
                                if (ch == '"')
                                {
                                    sb.Write(new ReadOnlySpan<char>(_value + start, _position - start - 1));
                                    property = sb.ToString();
                                    for (; ; )
                                    {
                                        if (_position == _length)
                                            throw new FormatException("EOF");
                                        ch = _value[_position++];
                                        switch (ch)
                                        {
                                            case ':':
                                                _isGet = true;
                                                return;
                                            case ' ':
                                            case '\t':
                                            case '\r':
                                            case '\n':
                                            case '\v':
                                            case '\f':
                                            case '\u0000':
                                            case '\u0085':
                                            case '\u2028':
                                            case '\u2029':
                                            case '\u00A0':
                                            case '\uFEFF':
                                                continue;
                                            default:
                                                throw new FormatException($"Unexpected character '{ch}'");
                                        }
                                    }
                                }
                                if (_position == _length)
                                    throw new FormatException("EOF");
                                ch = _value[_position++];
                                if (ch == '\\')
                                {
                                    break;
                                }
                            }
                        }
                    }
                    finally
                    {
                        disposable.Dispose();
                    }
                }
            }
            private unsafe void SkipString()
            {
                Debug.Assert(!_isGet);
                for (; ; )
                {
                    if (_position == _length)
                        throw new FormatException("EOF");
                    var ch = _value[_position++];
                    if (ch == '\\')
                    {
                        if (_position == _length)
                            throw new FormatException("EOF");
                        ch = _value[_position++];
                        switch (ch)
                        {
                            case '0':
                            case '\'':
                            case '"':
                            case '\\':
                            case '/':
                            case 'r':
                            case 'n':
                            case 't':
                            case 'v':
                            case 'b':
                            case 'f':
                                break;
                            case 'u':
                                _position += 4;
                                break;
                            default:
                                throw new NotSupportedException("\\" + ch);
                        }
                    }
                    else if (ch == '"')
                    {
                        _isGet = true;
                        return;
                    }
                }
            }
            public override string GetString()
            {
                if (_token == Token.Null)
                    return null;
                if (_token != Token.String || _isGet)
                    throw new InvalidOperationException(nameof(GetString));

                unsafe
                {
                    var start = _position;
                    char ch;
                    for (; ; )
                    {
                        if (_position == _length)
                            throw new FormatException("EOF");
                        ch = _value[_position++];
                        if (ch == '\\')
                        {
                            break;
                        }
                        else if (ch == '"')
                        {
                            _isGet = true;
                            return new string(_value, start, _position - start - 1);
                        }
                    }
                    var sb = StringExtensions.ThreadRent(out var disposable);
                    try
                    {
                        for (; ; )
                        {
                            sb.Write(new ReadOnlySpan<char>(_value + start, _position - start - 1));
                            for (; ; )
                            {
                                if (_position == _length)
                                    throw new FormatException("EOF");
                                ch = _value[_position++];
                                switch (ch)
                                {
                                    case '0':
                                        sb.Write('\u0000');
                                        break;
                                    case '\'':
                                        sb.Write('\'');
                                        break;
                                    case '"':
                                        sb.Write('"');
                                        break;
                                    case '\\':
                                        sb.Write('\\');
                                        break;
                                    case '/':
                                        sb.Write('/');
                                        break;
                                    case 'r':
                                        sb.Write('\r');
                                        break;
                                    case 'n':
                                        sb.Write('\n');
                                        break;
                                    case 't':
                                        sb.Write('\t');
                                        break;
                                    case 'v':
                                        sb.Write('\v');
                                        break;
                                    case 'b':
                                        sb.Write('\b');
                                        break;
                                    case 'f':
                                        sb.Write('\f');
                                        break;
                                    case 'u':
                                        sb.Write(GetUnicode());
                                        break;
                                    default:
                                        throw new NotSupportedException("\\" + ch);
                                }
                                if (_position == _length)
                                    throw new FormatException("EOF");
                                ch = _value[_position];
                                if (ch == '\\')
                                {
                                    _position++;
                                }
                                else
                                {
                                    start = _position;
                                    _position++;
                                    break;
                                }
                            }
                            for (; ; )
                            {
                                if (ch == '"')
                                {
                                    sb.Write(new ReadOnlySpan<char>(_value + start, _position - start - 1));
                                    _isGet = true;
                                    return sb.ToString();
                                }
                                if (_position == _length)
                                    throw new FormatException("EOF");
                                ch = _value[_position++];
                                if (ch == '\\')
                                {
                                    break;
                                }
                            }
                        }
                    }
                    finally
                    {
                        disposable.Dispose();
                    }
                }
            }
            public override void GetString(out ReadOnlySpan<char> value)
            {
                if (_token == Token.Null)
                {
                    value = null;
                    return;
                }
                if (_token != Token.String || _isGet)
                    throw new InvalidOperationException(nameof(GetString));

                unsafe
                {
                    var start = _position;
                    char ch;
                    for (; ; )
                    {
                        if (_position == _length)
                            throw new FormatException("EOF");
                        ch = _value[_position++];
                        if (ch == '\\')
                        {
                            break;
                        }
                        else if (ch == '"')
                        {
                            _isGet = true;
                            value = new ReadOnlySpan<char>(_value + start, _position - start - 1);
                            return;
                        }
                    }
                    var sb = StringExtensions.ThreadRent(out var disposable);
                    try
                    {
                        for (; ; )
                        {
                            sb.Write(new ReadOnlySpan<char>(_value + start, _position - start - 1));
                            for (; ; )
                            {
                                if (_position == _length)
                                    throw new FormatException("EOF");
                                ch = _value[_position++];
                                switch (ch)
                                {
                                    case '0':
                                        sb.Write('\u0000');
                                        break;
                                    case '\'':
                                        sb.Write('\'');
                                        break;
                                    case '"':
                                        sb.Write('"');
                                        break;
                                    case '\\':
                                        sb.Write('\\');
                                        break;
                                    case '/':
                                        sb.Write('/');
                                        break;
                                    case 'r':
                                        sb.Write('\r');
                                        break;
                                    case 'n':
                                        sb.Write('\n');
                                        break;
                                    case 't':
                                        sb.Write('\t');
                                        break;
                                    case 'v':
                                        sb.Write('\v');
                                        break;
                                    case 'b':
                                        sb.Write('\b');
                                        break;
                                    case 'f':
                                        sb.Write('\f');
                                        break;
                                    case 'u':
                                        sb.Write(GetUnicode());
                                        break;
                                    default:
                                        throw new NotSupportedException("\\" + ch);
                                }
                                if (_position == _length)
                                    throw new FormatException("EOF");
                                ch = _value[_position];
                                if (ch == '\\')
                                {
                                    _position++;
                                }
                                else
                                {
                                    start = _position;
                                    _position++;
                                    break;
                                }
                            }
                            for (; ; )
                            {
                                if (ch == '"')
                                {
                                    sb.Write(new ReadOnlySpan<char>(_value + start, _position - start - 1));
                                    _isGet = true;
                                    value = sb.ToString();
                                    return;
                                }
                                if (_position == _length)
                                    throw new FormatException("EOF");
                                ch = _value[_position++];
                                if (ch == '\\')
                                {
                                    break;
                                }
                            }
                        }
                    }
                    finally
                    {
                        disposable.Dispose();
                    }
                }
            }
            public override byte GetByte()
            {
                GetNumber(out var number);
                return byte.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override sbyte GetSByte()
            {
                GetNumber(out var number);
                return sbyte.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override short GetInt16()
            {
                GetNumber(out var number);
                return short.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override ushort GetUInt16()
            {
                GetNumber(out var number);
                return ushort.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override int GetInt32()
            {
                GetNumber(out var number);
                return int.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override uint GetUInt32()
            {
                GetNumber(out var number);
                return uint.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override long GetInt64()
            {
                GetNumber(out var number);
                return long.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override ulong GetUInt64()
            {
                GetNumber(out var number);
                return ulong.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override float GetSingle()
            {
                GetNumber(out var number);
                return float.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override double GetDouble()
            {
                GetNumber(out var number);
                return double.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override decimal GetDecimal()
            {
                GetNumber(out var number);
                return decimal.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            private unsafe void SkipNumber()
            {
                Debug.Assert(!_isGet);
                _position++;
                for (; ; )
                {
                    if (_position == _length)
                    {
                        _isGet = true;
                        return;
                    }
                    var ch = _value[_position];
                    switch (ch)
                    {
                        case ',':
                        case '}':
                        case ']':
                            _isGet = true;
                            return;
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                        case '\v':
                        case '\f':
                        case '\u0000':
                        case '\u0085':
                        case '\u2028':
                        case '\u2029':
                        case '\u00A0':
                        case '\uFEFF':
                            _isGet = true;
                            _position += 1;
                            return;
                        default:
                            _position += 1;
                            continue;
                    }
                }
            }
            public override void GetNumber(out ReadOnlySpan<char> value)
            {
                if (_token != Token.Number || _isGet)
                    throw new InvalidOperationException(nameof(GetNumber));

                unsafe
                {
                    var start = _position++;
                    for (; ; )
                    {
                        if (_position == _length)
                        {
                            _isGet = true;
                            value = new ReadOnlySpan<char>(_value + start, _length - start);
                            return;
                        }
                        var ch = _value[_position];
                        switch (ch)
                        {
                            case ',':
                            case '}':
                            case ']':
                                _isGet = true;
                                value = new ReadOnlySpan<char>(_value + start, _position - start);
                                return;
                            case ' ':
                            case '\t':
                            case '\r':
                            case '\n':
                            case '\v':
                            case '\f':
                            case '\u0000':
                            case '\u0085':
                            case '\u2028':
                            case '\u2029':
                            case '\u00A0':
                            case '\uFEFF':
                                _isGet = true;
                                value = new ReadOnlySpan<char>(_value + start, _position - start);
                                _position += 1;
                                return;
                            default:
                                _position += 1;
                                continue;
                        }
                    }
                }
            }
            public override bool GetBoolean()
            {
                if (_token != Token.Boolean)
                    throw new InvalidOperationException(nameof(GetBoolean));

                return _boolean;
            }
            public override void Skip()
            {
                switch (_token)
                {
                    case Token.None:
                        if (Read())
                        {
                            Skip();
                        }
                        return;
                    case Token.StartArray:
                        for (; ; )
                        {
                            if (!Read())
                                throw new FormatException("EOF");

                            if (_token == Token.EndArray)
                                return;

                            Skip();
                        }
                    case Token.StartObject:
                        for (; ; )
                        {
                            if (!Read())
                                throw new FormatException("EOF");

                            if (_token == Token.EndObject)
                                return;

                            Skip();
                        }
                    case Token.Property:
                        {
                            if (!Read())
                                throw new FormatException("EOF");

                            Skip();
                            return;
                        }
                    default:
                        return;
                }
            }
        }
        private class SequenceJsonReader : JsonReader, IDisposable
        {
            private enum Token : byte
            {
                None,
                StartArray,
                EndArray,
                StartObject,
                EndObject,
                Property,
                String,
                Number,
                Boolean,
                Null
            }
            private static int _MaxDepth = 64;
            #region private
            private ReadOnlySequence<char> _value;
            private SequencePosition _next;
            private int _position;//-1=end
            private int _length;
            private unsafe char* _segment;
            private MemoryHandle _memoryHandle;
            private int _depth = -1;
            private bool[] _isObject;
            private Token _token;
            private bool _isGet;
            private bool _boolean;
            #endregion
            public SequenceJsonReader(ReadOnlySequence<char> value)
            {
                _value = value;
                _next = _value.Start;
                _token = Token.None;
                _isObject = new bool[_MaxDepth];
                if (_value.TryGet(ref _next, out var segment))
                {
                    _length = segment.Length;
                    _memoryHandle = segment.Pin();
                    unsafe { _segment = (char*)_memoryHandle.Pointer; }
                }
                else
                {
                    _position = -1;
                }
            }
            public void Set(ReadOnlySequence<char> value)
            {
                _value = value;
                _next = _value.Start;
                if (_value.TryGet(ref _next, out var segment))
                {
                    _length = segment.Length;
                    _memoryHandle = segment.Pin();
                    unsafe { _segment = (char*)_memoryHandle.Pointer; }
                }
                else
                {
                    _position = -1;
                }
            }
            public void Reset()
            {
                _token = Token.None;
                _value = default;
                _next = default;
                _length = 0;
                _position = 0;
                _depth = -1;
            }
            private int ReadChar()
            {
                if (_position == -1)
                    return -1;

                if (_position == _length)
                {
                    _memoryHandle.Dispose();
                    if (_value.TryGet(ref _next, out var segment))
                    {
                        _length = segment.Length;
                        _memoryHandle = segment.Pin();
                        unsafe { _segment = (char*)_memoryHandle.Pointer; }
                        _position = 0;
                    }
                    else
                    {
                        _position = -1;
                        return -1;
                    }
                }
                unsafe { return _segment[_position++]; }
            }
            private int PeekChar()
            {
                if (_position == -1)
                    return -1;

                if (_position == _length)
                {
                    _memoryHandle.Dispose();
                    if (_value.TryGet(ref _next, out var segment))
                    {
                        _length = segment.Length;
                        _memoryHandle = segment.Pin();
                        unsafe { _segment = (char*)_memoryHandle.Pointer; }
                        _position = 0;
                    }
                    else
                    {
                        _position = -1;
                        return -1;
                    }
                }
                unsafe { return _segment[_position]; }
            }
            private char GetUnicode()
            {
                var unicode = 0;
                for (var i = 1; ;)
                {
                    var ch = ReadChar();
                    switch (ch)
                    {
                        case -1:
                            throw new FormatException("\\uxxxx");
                        case '0':
                            break;
                        case '1':
                            unicode += 1;
                            break;
                        case '2':
                            unicode += 2;
                            break;
                        case '3':
                            unicode += 3;
                            break;
                        case '4':
                            unicode += 4;
                            break;
                        case '5':
                            unicode += 5;
                            break;
                        case '6':
                            unicode += 6;
                            break;
                        case '7':
                            unicode += 7;
                            break;
                        case '8':
                            unicode += 8;
                            break;
                        case '9':
                            unicode += 9;
                            break;
                        case 'a':
                        case 'A':
                            unicode += 10;
                            break;
                        case 'b':
                        case 'B':
                            unicode += 11;
                            break;
                        case 'c':
                        case 'C':
                            unicode += 12;
                            break;
                        case 'd':
                        case 'D':
                            unicode += 13;
                            break;
                        case 'e':
                        case 'E':
                            unicode += 14;
                            break;
                        case 'f':
                        case 'F':
                            unicode += 15;
                            break;
                        default:
                            throw new FormatException("\\uxxxx");
                    }
                    if (i == 4)
                        return (char)unicode;
                    i += 1;
                    unicode <<= 4;
                }
            }
            public override bool Read()
            {
                #region function
                bool Next()
                {
                    if (_depth == -1)
                        return NextValue();

                    if (_isObject[_depth])
                    {
                        for (; ; )
                        {
                            var ch = ReadChar();
                            switch (ch)
                            {
                                case -1:
                                    return false;
                                case ',':
                                    return NextProperty();
                                case '}':
                                    if (!_isObject[_depth--])
                                        throw new FormatException($"Unexpected character '{(char)ch}'");
                                    _token = Token.EndObject;
                                    return true;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{(char)ch}'");
                            }
                        }
                    }
                    else
                    {
                        for (; ; )
                        {
                            var ch = ReadChar();
                            switch (ch)
                            {
                                case -1:
                                    return false;
                                case ',':
                                    return NextValue();
                                case ']':
                                    if (_isObject[_depth--])
                                        throw new FormatException($"Unexpected character '{(char)ch}'");
                                    _token = Token.EndArray;
                                    return true;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{(char)ch}'");
                            }
                        }
                    }
                }
                bool NextProperty()
                {
                    for (; ; )
                    {
                        var ch = ReadChar();
                        switch (ch)
                        {
                            case -1:
                                return false;
                            case '"':
                                _isGet = false;
                                _token = Token.Property;
                                return true;
                            case ' ':
                            case '\t':
                            case '\r':
                            case '\n':
                            case '\v':
                            case '\f':
                            case '\u0000':
                            case '\u0085':
                            case '\u2028':
                            case '\u2029':
                            case '\u00A0':
                            case '\uFEFF':
                                continue;
                            case '}':
                                if (_token != Token.StartObject || !_isObject[_depth--])
                                    throw new FormatException($"Unexpected character '{(char)ch}'");
                                _token = Token.EndObject;
                                return true;
                            default:
                                throw new FormatException($"Unexpected character '{(char)ch}'");
                        }
                    }
                }
                bool NextValue()
                {
                    for (; ; )
                    {
                        var ch = PeekChar();
                        switch (ch)
                        {
                            case -1:
                                return false;
                            case '{':
                                _position += 1;
                                _depth += 1;
                                _isObject[_depth] = true;
                                _token = Token.StartObject;
                                return true;
                            case '[':
                                _position += 1;
                                _depth += 1;
                                _isObject[_depth] = false;
                                _token = Token.StartArray;
                                return true;
                            case '"':
                                _position += 1;
                                _isGet = false;
                                _token = Token.String;
                                return true;
                            case 'n':
                                _position += 1;
                                if (ReadChar() != 'u' || ReadChar() != 'l' || ReadChar() != 'l')
                                    throw new FormatException("Unexpected character");
                                _token = Token.Null;
                                return true;
                            case 't':
                                _position += 1;
                                if (ReadChar() != 'r' || ReadChar() != 'u' || ReadChar() != 'e')
                                    throw new FormatException("Unexpected character");
                                _boolean = true;
                                _token = Token.Boolean;
                                return true;
                            case 'f':
                                _position += 1;
                                if (ReadChar() != 'a' || ReadChar() != 'l' || ReadChar() != 's' || ReadChar() != 'e')
                                    throw new FormatException("Unexpected character");
                                _boolean = false;
                                _token = Token.Boolean;
                                return true;
                            case '0':
                            case '1':
                            case '2':
                            case '3':
                            case '4':
                            case '5':
                            case '6':
                            case '7':
                            case '8':
                            case '9':
                            case '+':
                            case '-':
                            case '.':
                            case 'N':
                            case 'I':
                                _isGet = false;
                                _token = Token.Number;
                                return true;
                            case ' ':
                            case '\t':
                            case '\r':
                            case '\n':
                            case '\v':
                            case '\f':
                            case '\u0000':
                            case '\u0085':
                            case '\u2028':
                            case '\u2029':
                            case '\u00A0':
                            case '\uFEFF':
                                _position += 1;
                                continue;
                            case ']':
                                if (_token != Token.StartArray || _isObject[_depth--])
                                    throw new FormatException($"Unexpected character '{(char)ch}'");
                                _position += 1;
                                _token = Token.EndArray;
                                return true;
                            default:
                                throw new FormatException($"Unexpected character '{(char)ch}'");
                        }
                    }
                }
                #endregion
                switch (_token)
                {
                    case Token.None:
                        return NextValue();
                    case Token.StartObject:
                        return NextProperty();
                    case Token.Property:
                        if (!_isGet)
                            SkipProperty();
                        return NextValue();
                    case Token.String:
                        if (!_isGet)
                            SkipString();
                        return Next();
                    case Token.Number:
                        if (!_isGet)
                            SkipNumber();
                        return Next();
                    case Token.Boolean:
                    case Token.Null:
                        return Next();
                    case Token.StartArray:
                        return NextValue();
                    case Token.EndObject:
                    case Token.EndArray:
                        return Next();
                    default:
                        return false;
                }
            }
            public override bool IsStartArray => _token == Token.StartArray;
            public override bool IsEndArray => _token == Token.EndArray;
            public override bool IsStartObject => _token == Token.StartObject;
            public override bool IsEndObject => _token == Token.EndObject;
            public override bool IsProperty => _token == Token.Property;
            public override bool IsString => _token == Token.String;
            public override bool IsNumber => _token == Token.Number;
            public override bool IsBoolean => _token == Token.Boolean;
            public override bool IsNull => _token == Token.Null;
            private void SkipProperty()
            {
                Debug.Assert(!_isGet);
                for (; ; )
                {
                    var ch = ReadChar();
                    switch (ch)
                    {
                        case -1:
                            throw new FormatException("EOF");
                        case '\\':
                            ch = ReadChar();
                            switch (ch)
                            {
                                case -1:
                                    throw new FormatException("EOF");
                                case '0':
                                case '\'':
                                case '"':
                                case '\\':
                                case '/':
                                case 'r':
                                case 'n':
                                case 't':
                                case 'v':
                                case 'b':
                                case 'f':
                                    break;
                                case 'u':
                                    if (ReadChar() == -1 || ReadChar() == -1 || ReadChar() == -1 || ReadChar() == -1)
                                        throw new FormatException("EOF");
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + (char)ch);
                            }
                            break;
                        case '"':
                            for (; ; )
                            {
                                ch = ReadChar();
                                switch (ch)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case ':':
                                        _isGet = true;
                                        return;
                                    case ' ':
                                    case '\t':
                                    case '\r':
                                    case '\n':
                                    case '\v':
                                    case '\f':
                                    case '\u0000':
                                    case '\u0085':
                                    case '\u2028':
                                    case '\u2029':
                                    case '\u00A0':
                                    case '\uFEFF':
                                        continue;
                                    default:
                                        throw new FormatException($"Unexpected character '{(char)ch}'");
                                }
                            }
                        default:
                            break;
                    }
                }
            }
            public override string GetProperty()
            {
                if (_token != Token.Property || _isGet)
                    throw new InvalidOperationException(nameof(GetProperty));

                unsafe
                {
                    var @char = PeekChar();
                    if (@char == -1)
                        throw new FormatException("EOF");
                    var ch = (char)@char;
                    var start = _position++;
                    for (; ; )
                    {
                        if (ch == '\\')
                        {
                            break;
                        }
                        else if (ch == '"')
                        {
                            var property = new string(_segment, start, _position - start - 1);
                            for (; ; )
                            {
                                @char = ReadChar();
                                switch (@char)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case ':':
                                        _isGet = true;
                                        return property;
                                    case ' ':
                                    case '\t':
                                    case '\r':
                                    case '\n':
                                    case '\v':
                                    case '\f':
                                    case '\u0000':
                                    case '\u0085':
                                    case '\u2028':
                                    case '\u2029':
                                    case '\u00A0':
                                    case '\uFEFF':
                                        continue;
                                    default:
                                        throw new FormatException($"Unexpected character '{(char)@char}'");
                                }
                            }
                        }
                        if (_position == _length)
                            break;
                        ch = _segment[_position++];
                    }
                    var sb = StringExtensions.ThreadRent(out var disposable);
                    try
                    {
                        for (; ; )
                        {
                            if (ch == '\\')
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start - 1));
                                for (; ; )
                                {
                                    @char = ReadChar();
                                    switch (@char)
                                    {
                                        case -1:
                                            throw new FormatException("EOF");
                                        case '0':
                                            sb.Write('\u0000');
                                            break;
                                        case '\'':
                                            sb.Write('\'');
                                            break;
                                        case '"':
                                            sb.Write('"');
                                            break;
                                        case '\\':
                                            sb.Write('\\');
                                            break;
                                        case '/':
                                            sb.Write('/');
                                            break;
                                        case 'r':
                                            sb.Write('\r');
                                            break;
                                        case 'n':
                                            sb.Write('\n');
                                            break;
                                        case 't':
                                            sb.Write('\t');
                                            break;
                                        case 'v':
                                            sb.Write('\v');
                                            break;
                                        case 'b':
                                            sb.Write('\b');
                                            break;
                                        case 'f':
                                            sb.Write('\f');
                                            break;
                                        case 'u':
                                            sb.Write(GetUnicode());
                                            break;
                                        default:
                                            throw new NotSupportedException("\\" + (char)@char);
                                    }
                                    @char = PeekChar();
                                    if (@char == -1)
                                        throw new FormatException("EOF");
                                    if (@char == '\\')
                                    {
                                        _position++;
                                    }
                                    else
                                    {
                                        ch = (char)@char;
                                        start = _position;
                                        _position++;
                                        break;
                                    }
                                }
                            }
                            if (ch == '"')
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start - 1));
                                for (; ; )
                                {
                                    @char = ReadChar();
                                    switch (@char)
                                    {
                                        case -1:
                                            throw new FormatException("EOF");
                                        case ':':
                                            _isGet = true;
                                            return sb.ToString();
                                        case ' ':
                                        case '\t':
                                        case '\r':
                                        case '\n':
                                        case '\v':
                                        case '\f':
                                        case '\u0000':
                                        case '\u0085':
                                        case '\u2028':
                                        case '\u2029':
                                        case '\u00A0':
                                        case '\uFEFF':
                                            continue;
                                        default:
                                            throw new FormatException($"Unexpected character '{ch}'");
                                    }
                                }
                            }
                            if (_position == _length)
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start));
                                start = 0;
                                @char = ReadChar();
                                if (@char == -1)
                                    throw new FormatException("EOF");
                                ch = (char)@char;
                            }
                            else
                            {
                                ch = _segment[_position++];
                            }
                        }
                    }
                    finally
                    {
                        disposable.Dispose();
                    }
                }
            }
            public override void GetProperty(out ReadOnlySpan<char> property)
            {
                if (_token != Token.Property || _isGet)
                    throw new InvalidOperationException(nameof(GetProperty));

                unsafe
                {
                    var @char = PeekChar();
                    if (@char == -1)
                        throw new FormatException("EOF");
                    var ch = (char)@char;
                    var start = _position++;
                    for (; ; )
                    {
                        if (ch == '\\')
                        {
                            break;
                        }
                        else if (ch == '"')
                        {
                            property = new ReadOnlySpan<char>(_segment + start, _position - start - 1);
                            for (; ; )
                            {
                                @char = ReadChar();
                                switch (@char)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case ':':
                                        _isGet = true;
                                        return;
                                    case ' ':
                                    case '\t':
                                    case '\r':
                                    case '\n':
                                    case '\v':
                                    case '\f':
                                    case '\u0000':
                                    case '\u0085':
                                    case '\u2028':
                                    case '\u2029':
                                    case '\u00A0':
                                    case '\uFEFF':
                                        continue;
                                    default:
                                        throw new FormatException($"Unexpected character '{(char)@char}'");
                                }
                            }
                        }
                        if (_position == _length)
                            break;
                        ch = _segment[_position++];
                    }
                    var sb = StringExtensions.ThreadRent(out var disposable);
                    try
                    {
                        for (; ; )
                        {
                            if (ch == '\\')
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start - 1));
                                for (; ; )
                                {
                                    @char = ReadChar();
                                    switch (@char)
                                    {
                                        case -1:
                                            throw new FormatException("EOF");
                                        case '0':
                                            sb.Write('\u0000');
                                            break;
                                        case '\'':
                                            sb.Write('\'');
                                            break;
                                        case '"':
                                            sb.Write('"');
                                            break;
                                        case '\\':
                                            sb.Write('\\');
                                            break;
                                        case '/':
                                            sb.Write('/');
                                            break;
                                        case 'r':
                                            sb.Write('\r');
                                            break;
                                        case 'n':
                                            sb.Write('\n');
                                            break;
                                        case 't':
                                            sb.Write('\t');
                                            break;
                                        case 'v':
                                            sb.Write('\v');
                                            break;
                                        case 'b':
                                            sb.Write('\b');
                                            break;
                                        case 'f':
                                            sb.Write('\f');
                                            break;
                                        case 'u':
                                            sb.Write(GetUnicode());
                                            break;
                                        default:
                                            throw new NotSupportedException("\\" + (char)@char);
                                    }
                                    @char = PeekChar();
                                    if (@char == -1)
                                        throw new FormatException("EOF");
                                    if (@char == '\\')
                                    {
                                        _position++;
                                    }
                                    else
                                    {
                                        ch = (char)@char;
                                        start = _position;
                                        _position++;
                                        break;
                                    }
                                }
                            }
                            if (ch == '"')
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start - 1));
                                property = sb.ToString();
                                for (; ; )
                                {
                                    @char = ReadChar();
                                    switch (@char)
                                    {
                                        case -1:
                                            throw new FormatException("EOF");
                                        case ':':
                                            _isGet = true;
                                            return;
                                        case ' ':
                                        case '\t':
                                        case '\r':
                                        case '\n':
                                        case '\v':
                                        case '\f':
                                        case '\u0000':
                                        case '\u0085':
                                        case '\u2028':
                                        case '\u2029':
                                        case '\u00A0':
                                        case '\uFEFF':
                                            continue;
                                        default:
                                            throw new FormatException($"Unexpected character '{ch}'");
                                    }
                                }
                            }
                            if (_position == _length)
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start));
                                start = 0;
                                @char = ReadChar();
                                if (@char == -1)
                                    throw new FormatException("EOF");
                                ch = (char)@char;
                            }
                            else
                            {
                                ch = _segment[_position++];
                            }
                        }
                    }
                    finally
                    {
                        disposable.Dispose();
                    }
                }
            }
            private void SkipString()
            {
                Debug.Assert(!_isGet);
                for (; ; )
                {
                    var ch = ReadChar();
                    switch (ch)
                    {
                        case -1:
                            throw new FormatException("EOF");
                        case '\\':
                            ch = ReadChar();
                            switch (ch)
                            {
                                case -1:
                                    throw new FormatException("EOF");
                                case '0':
                                case '\'':
                                case '"':
                                case '\\':
                                case '/':
                                case 'r':
                                case 'n':
                                case 't':
                                case 'v':
                                case 'b':
                                case 'f':
                                    break;
                                case 'u':
                                    if (ReadChar() == -1 || ReadChar() == -1 || ReadChar() == -1 || ReadChar() == -1)
                                        throw new FormatException("EOF");
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + (char)ch);
                            }
                            break;
                        case '"':
                            _isGet = true;
                            return;
                        default:
                            break;
                    }
                }
            }
            public override string GetString()
            {
                if (_token == Token.Null)
                    return null;
                if (_token != Token.String || _isGet)
                    throw new InvalidOperationException(nameof(GetString));

                unsafe
                {
                    var @char = PeekChar();
                    if (@char == -1)
                        throw new FormatException("EOF");
                    var ch = (char)@char;
                    var start = _position++;
                    for (; ; )
                    {
                        if (ch == '\\')
                        {
                            break;
                        }
                        else if (ch == '"')
                        {
                            _isGet = true;
                            return new string(_segment, start, _position - start - 1);
                        }
                        if (_position == _length)
                            break;
                        ch = _segment[_position++];
                    }
                    var sb = StringExtensions.ThreadRent(out var disposable);
                    try
                    {
                        for (; ; )
                        {
                            if (ch == '\\')
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start - 1));
                                for (; ; )
                                {
                                    @char = ReadChar();
                                    switch (@char)
                                    {
                                        case -1:
                                            throw new FormatException("EOF");
                                        case '0':
                                            sb.Write('\u0000');
                                            break;
                                        case '\'':
                                            sb.Write('\'');
                                            break;
                                        case '"':
                                            sb.Write('"');
                                            break;
                                        case '\\':
                                            sb.Write('\\');
                                            break;
                                        case '/':
                                            sb.Write('/');
                                            break;
                                        case 'r':
                                            sb.Write('\r');
                                            break;
                                        case 'n':
                                            sb.Write('\n');
                                            break;
                                        case 't':
                                            sb.Write('\t');
                                            break;
                                        case 'v':
                                            sb.Write('\v');
                                            break;
                                        case 'b':
                                            sb.Write('\b');
                                            break;
                                        case 'f':
                                            sb.Write('\f');
                                            break;
                                        case 'u':
                                            sb.Write(GetUnicode());
                                            break;
                                        default:
                                            throw new NotSupportedException("\\" + (char)@char);
                                    }
                                    @char = PeekChar();
                                    if (@char == -1)
                                        throw new FormatException("EOF");
                                    if (@char == '\\')
                                    {
                                        _position++;
                                    }
                                    else
                                    {
                                        ch = (char)@char;
                                        start = _position;
                                        _position++;
                                        break;
                                    }
                                }
                            }
                            if (ch == '"')
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start - 1));
                                _isGet = true;
                                return sb.ToString();
                            }
                            if (_position == _length)
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start));
                                start = 0;
                                @char = ReadChar();
                                if (@char == -1)
                                    throw new FormatException("EOF");
                                ch = (char)@char;
                            }
                            else
                            {
                                ch = _segment[_position++];
                            }
                        }
                    }
                    finally
                    {
                        disposable.Dispose();
                    }
                }
            }
            public override void GetString(out ReadOnlySpan<char> value)
            {
                if (_token == Token.Null)
                {
                    value = null;
                    return;
                }
                if (_token != Token.String || _isGet)
                    throw new InvalidOperationException(nameof(GetString));

                unsafe
                {
                    var @char = PeekChar();
                    if (@char == -1)
                        throw new FormatException("EOF");
                    var ch = (char)@char;
                    var start = _position++;
                    for (; ; )
                    {
                        if (ch == '\\')
                        {
                            break;
                        }
                        else if (ch == '"')
                        {
                            _isGet = true;
                            value = new ReadOnlySpan<char>(_segment + start, _position - start - 1);
                            return;
                        }
                        if (_position == _length)
                            break;
                        ch = _segment[_position++];
                    }
                    var sb = StringExtensions.ThreadRent(out var disposable);
                    try
                    {
                        for (; ; )
                        {
                            if (ch == '\\')
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start - 1));
                                for (; ; )
                                {
                                    @char = ReadChar();
                                    switch (@char)
                                    {
                                        case -1:
                                            throw new FormatException("EOF");
                                        case '0':
                                            sb.Write('\u0000');
                                            break;
                                        case '\'':
                                            sb.Write('\'');
                                            break;
                                        case '"':
                                            sb.Write('"');
                                            break;
                                        case '\\':
                                            sb.Write('\\');
                                            break;
                                        case '/':
                                            sb.Write('/');
                                            break;
                                        case 'r':
                                            sb.Write('\r');
                                            break;
                                        case 'n':
                                            sb.Write('\n');
                                            break;
                                        case 't':
                                            sb.Write('\t');
                                            break;
                                        case 'v':
                                            sb.Write('\v');
                                            break;
                                        case 'b':
                                            sb.Write('\b');
                                            break;
                                        case 'f':
                                            sb.Write('\f');
                                            break;
                                        case 'u':
                                            sb.Write(GetUnicode());
                                            break;
                                        default:
                                            throw new NotSupportedException("\\" + (char)@char);
                                    }
                                    @char = PeekChar();
                                    if (@char == -1)
                                        throw new FormatException("EOF");
                                    if (@char == '\\')
                                    {
                                        _position++;
                                    }
                                    else
                                    {
                                        ch = (char)@char;
                                        start = _position;
                                        _position++;
                                        break;
                                    }
                                }
                            }
                            if (ch == '"')
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start - 1));
                                _isGet = true;
                                value = sb.ToString();
                                return;
                            }
                            if (_position == _length)
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start));
                                start = 0;
                                @char = ReadChar();
                                if (@char == -1)
                                    throw new FormatException("EOF");
                                ch = (char)@char;
                            }
                            else
                            {
                                ch = _segment[_position++];
                            }
                        }
                    }
                    finally
                    {
                        disposable.Dispose();
                    }
                }
            }
            public override byte GetByte()
            {
                GetNumber(out var number);
                return byte.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override sbyte GetSByte()
            {
                GetNumber(out var number);
                return sbyte.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override short GetInt16()
            {
                GetNumber(out var number);
                return short.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override ushort GetUInt16()
            {
                GetNumber(out var number);
                return ushort.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override int GetInt32()
            {
                GetNumber(out var number);
                return int.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override uint GetUInt32()
            {
                GetNumber(out var number);
                return uint.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override long GetInt64()
            {
                GetNumber(out var number);
                return long.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override ulong GetUInt64()
            {
                GetNumber(out var number);
                return ulong.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override float GetSingle()
            {
                GetNumber(out var number);
                return float.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override double GetDouble()
            {
                GetNumber(out var number);
                return double.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override decimal GetDecimal()
            {
                GetNumber(out var number);
                return decimal.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            private void SkipNumber()
            {
                Debug.Assert(!_isGet);
                _position++;
                for (; ; )
                {
                    var @char = PeekChar();
                    switch (@char)
                    {
                        case -1:
                        case ',':
                        case '}':
                        case ']':
                            _isGet = true;
                            return;
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                        case '\v':
                        case '\f':
                        case '\u0000':
                        case '\u0085':
                        case '\u2028':
                        case '\u2029':
                        case '\u00A0':
                        case '\uFEFF':
                            _isGet = true;
                            _position += 1;
                            return;
                        default:
                            _position += 1;
                            continue;
                    }
                }
            }
            public override void GetNumber(out ReadOnlySpan<char> value)
            {
                if (_token != Token.Number || _isGet)
                    throw new InvalidOperationException(nameof(GetNumber));

                unsafe
                {
                    var start = _position++;
                    while (_position < _length)
                    {
                        var ch = _segment[_position];
                        switch (ch)
                        {
                            case ',':
                            case '}':
                            case ']':
                                _isGet = true;
                                value = new ReadOnlySpan<char>(_segment + start, _position - start);
                                return;
                            case ' ':
                            case '\t':
                            case '\r':
                            case '\n':
                            case '\v':
                            case '\f':
                            case '\u0000':
                            case '\u0085':
                            case '\u2028':
                            case '\u2029':
                            case '\u00A0':
                            case '\uFEFF':
                                _isGet = true;
                                value = new ReadOnlySpan<char>(_segment + start, _position - start);
                                _position += 1;
                                return;
                            default:
                                _position += 1;
                                continue;
                        }
                    }
                    var sb = StringExtensions.ThreadRent(out var disposable);
                    try
                    {
                        sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start));
                        for (; ; )
                        {
                            var @char = PeekChar();
                            switch (@char)
                            {
                                case -1:
                                case ',':
                                case '}':
                                case ']':
                                    _isGet = true;
                                    value = sb.ToString();
                                    return;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    _position += 1;
                                    _isGet = true;
                                    value = sb.ToString();
                                    return;
                                default:
                                    _position += 1;
                                    sb.Write((char)@char);
                                    continue;
                            }
                        }
                    }
                    finally
                    {
                        disposable.Dispose();
                    }
                }
            }
            public override bool GetBoolean()
            {
                if (_token != Token.Boolean)
                    throw new InvalidOperationException(nameof(GetBoolean));

                return _boolean;
            }
            public override void Skip()
            {
                switch (_token)
                {
                    case Token.None:
                        if (Read())
                        {
                            Skip();
                        }
                        return;
                    case Token.StartArray:
                        for (; ; )
                        {
                            if (!Read())
                                throw new FormatException("EOF");

                            if (_token == Token.EndArray)
                                return;

                            Skip();
                        }
                    case Token.StartObject:
                        for (; ; )
                        {
                            if (!Read())
                                throw new FormatException("EOF");

                            if (_token == Token.EndObject)
                                return;

                            Skip();
                        }
                    case Token.Property:
                        {
                            if (!Read())
                                throw new FormatException("EOF");

                            Skip();
                            return;
                        }
                    default:
                        return;
                }
            }
            public void Dispose()
            {
                _memoryHandle.Dispose();
            }
        }
        private class TextJsonReader : JsonReader
        {
            private enum Token : byte
            {
                None,
                StartArray,
                EndArray,
                StartObject,
                EndObject,
                Property,
                String,
                Number,
                Boolean,
                Null
            }
            private static int _MaxDepth = 64;
            #region private
            private TextReader _reader;
            private char[] _buffer;
            private int _position;//-1=end
            private int _length;
            private int _depth = -1;
            private bool[] _isObject;
            private Token _token;
            private bool _isGet;
            private bool _boolean;
            #endregion
            public TextJsonReader(TextReader reader, int bufferSize)
            {
                _reader = reader;
                _token = Token.None;
                _isObject = new bool[_MaxDepth];
                _buffer = new char[bufferSize];

                _length = _reader.Read(_buffer, 0, _buffer.Length);
                _position = _length == 0 ? -1 : 0;
            }
            public void Set(TextReader value)
            {
                _reader = value;
                _length = _reader.Read(_buffer, 0, _buffer.Length);
                _position = _length == 0 ? -1 : 0;
            }
            public void Reset()
            {
                _token = Token.None;
                _reader = null;
                _depth = -1;
            }
            private int ReadChar()
            {
                if (_position == -1)
                    return -1;

                if (_position == _length)
                {
                    _length = _reader.Read(_buffer, 0, _buffer.Length);
                    if (_length == 0)
                    {
                        _position = -1;
                        return -1;
                    }
                    _position = 0;
                }
                return _buffer[_position++];
            }
            private int PeekChar()
            {
                if (_position == -1)
                    return -1;

                if (_position == _length)
                {
                    _length = _reader.Read(_buffer, 0, _buffer.Length);
                    if (_length == 0)
                    {
                        _position = -1;
                        return -1;
                    }
                    _position = 0;
                }
                return _buffer[_position];
            }
            private char GetUnicode()
            {
                var unicode = 0;
                for (var i = 1; ;)
                {
                    var ch = ReadChar();
                    switch (ch)
                    {
                        case -1:
                            throw new FormatException("\\uxxxx");
                        case '0':
                            break;
                        case '1':
                            unicode += 1;
                            break;
                        case '2':
                            unicode += 2;
                            break;
                        case '3':
                            unicode += 3;
                            break;
                        case '4':
                            unicode += 4;
                            break;
                        case '5':
                            unicode += 5;
                            break;
                        case '6':
                            unicode += 6;
                            break;
                        case '7':
                            unicode += 7;
                            break;
                        case '8':
                            unicode += 8;
                            break;
                        case '9':
                            unicode += 9;
                            break;
                        case 'a':
                        case 'A':
                            unicode += 10;
                            break;
                        case 'b':
                        case 'B':
                            unicode += 11;
                            break;
                        case 'c':
                        case 'C':
                            unicode += 12;
                            break;
                        case 'd':
                        case 'D':
                            unicode += 13;
                            break;
                        case 'e':
                        case 'E':
                            unicode += 14;
                            break;
                        case 'f':
                        case 'F':
                            unicode += 15;
                            break;
                        default:
                            throw new FormatException("\\uxxxx");
                    }
                    if (i == 4)
                        return (char)unicode;
                    i += 1;
                    unicode <<= 4;
                }
            }
            public override bool Read()
            {
                #region function
                bool Next()
                {
                    if (_depth == -1)
                        return NextValue();

                    if (_isObject[_depth])
                    {
                        for (; ; )
                        {
                            var ch = ReadChar();
                            switch (ch)
                            {
                                case -1:
                                    return false;
                                case ',':
                                    return NextProperty();
                                case '}':
                                    if (!_isObject[_depth--])
                                        throw new FormatException($"Unexpected character '{(char)ch}'");
                                    _token = Token.EndObject;
                                    return true;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{(char)ch}'");
                            }
                        }
                    }
                    else
                    {
                        for (; ; )
                        {
                            var ch = ReadChar();
                            switch (ch)
                            {
                                case -1:
                                    return false;
                                case ',':
                                    return NextValue();
                                case ']':
                                    if (_isObject[_depth--])
                                        throw new FormatException($"Unexpected character '{(char)ch}'");
                                    _token = Token.EndArray;
                                    return true;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{(char)ch}'");
                            }
                        }
                    }
                }
                bool NextProperty()
                {
                    for (; ; )
                    {
                        var ch = ReadChar();
                        switch (ch)
                        {
                            case -1:
                                return false;
                            case '"':
                                _isGet = false;
                                _token = Token.Property;
                                return true;
                            case ' ':
                            case '\t':
                            case '\r':
                            case '\n':
                            case '\v':
                            case '\f':
                            case '\u0000':
                            case '\u0085':
                            case '\u2028':
                            case '\u2029':
                            case '\u00A0':
                            case '\uFEFF':
                                continue;
                            case '}':
                                if (_token != Token.StartObject || !_isObject[_depth--])
                                    throw new FormatException($"Unexpected character '{(char)ch}'");
                                _token = Token.EndObject;
                                return true;
                            default:
                                throw new FormatException($"Unexpected character '{(char)ch}'");
                        }
                    }
                }
                bool NextValue()
                {
                    for (; ; )
                    {
                        var ch = PeekChar();
                        switch (ch)
                        {
                            case -1:
                                return false;
                            case '{':
                                _position += 1;
                                _depth += 1;
                                _isObject[_depth] = true;
                                _token = Token.StartObject;
                                return true;
                            case '[':
                                _position += 1;
                                _depth += 1;
                                _isObject[_depth] = false;
                                _token = Token.StartArray;
                                return true;
                            case '"':
                                _position += 1;
                                _isGet = false;
                                _token = Token.String;
                                return true;
                            case 'n':
                                _position += 1;
                                if (ReadChar() != 'u' || ReadChar() != 'l' || ReadChar() != 'l')
                                    throw new FormatException("Unexpected character");
                                _token = Token.Null;
                                return true;
                            case 't':
                                _position += 1;
                                if (ReadChar() != 'r' || ReadChar() != 'u' || ReadChar() != 'e')
                                    throw new FormatException("Unexpected character");
                                _boolean = true;
                                _token = Token.Boolean;
                                return true;
                            case 'f':
                                _position += 1;
                                if (ReadChar() != 'a' || ReadChar() != 'l' || ReadChar() != 's' || ReadChar() != 'e')
                                    throw new FormatException("Unexpected character");
                                _boolean = false;
                                _token = Token.Boolean;
                                return true;
                            case '0':
                            case '1':
                            case '2':
                            case '3':
                            case '4':
                            case '5':
                            case '6':
                            case '7':
                            case '8':
                            case '9':
                            case '+':
                            case '-':
                            case '.':
                            case 'N':
                            case 'I':
                                _isGet = false;
                                _token = Token.Number;
                                return true;
                            case ' ':
                            case '\t':
                            case '\r':
                            case '\n':
                            case '\v':
                            case '\f':
                            case '\u0000':
                            case '\u0085':
                            case '\u2028':
                            case '\u2029':
                            case '\u00A0':
                            case '\uFEFF':
                                _position += 1;
                                continue;
                            case ']':
                                if (_token != Token.StartArray || _isObject[_depth--])
                                    throw new FormatException($"Unexpected character '{(char)ch}'");
                                _position += 1;
                                _token = Token.EndArray;
                                return true;
                            default:
                                throw new FormatException($"Unexpected character '{(char)ch}'");
                        }
                    }
                }
                #endregion
                switch (_token)
                {
                    case Token.None:
                        return NextValue();
                    case Token.StartObject:
                        return NextProperty();
                    case Token.Property:
                        if (!_isGet)
                            SkipProperty();
                        return NextValue();
                    case Token.String:
                        if (!_isGet)
                            SkipString();
                        return Next();
                    case Token.Number:
                        if (!_isGet)
                            SkipNumber();
                        return Next();
                    case Token.Boolean:
                    case Token.Null:
                        return Next();
                    case Token.StartArray:
                        return NextValue();
                    case Token.EndObject:
                    case Token.EndArray:
                        return Next();
                    default:
                        return false;
                }
            }
            public override bool IsStartArray => _token == Token.StartArray;
            public override bool IsEndArray => _token == Token.EndArray;
            public override bool IsStartObject => _token == Token.StartObject;
            public override bool IsEndObject => _token == Token.EndObject;
            public override bool IsProperty => _token == Token.Property;
            public override bool IsString => _token == Token.String;
            public override bool IsNumber => _token == Token.Number;
            public override bool IsBoolean => _token == Token.Boolean;
            public override bool IsNull => _token == Token.Null;
            private void SkipProperty()
            {
                Debug.Assert(!_isGet);
                for (; ; )
                {
                    var ch = ReadChar();
                    switch (ch)
                    {
                        case -1:
                            throw new FormatException("EOF");
                        case '\\':
                            ch = ReadChar();
                            switch (ch)
                            {
                                case -1:
                                    throw new FormatException("EOF");
                                case '0':
                                case '\'':
                                case '"':
                                case '\\':
                                case '/':
                                case 'r':
                                case 'n':
                                case 't':
                                case 'v':
                                case 'b':
                                case 'f':
                                    break;
                                case 'u':
                                    if (ReadChar() == -1 || ReadChar() == -1 || ReadChar() == -1 || ReadChar() == -1)
                                        throw new FormatException("EOF");
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + (char)ch);
                            }
                            break;
                        case '"':
                            for (; ; )
                            {
                                ch = ReadChar();
                                switch (ch)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case ':':
                                        _isGet = true;
                                        return;
                                    case ' ':
                                    case '\t':
                                    case '\r':
                                    case '\n':
                                    case '\v':
                                    case '\f':
                                    case '\u0000':
                                    case '\u0085':
                                    case '\u2028':
                                    case '\u2029':
                                    case '\u00A0':
                                    case '\uFEFF':
                                        continue;
                                    default:
                                        throw new FormatException($"Unexpected character '{(char)ch}'");
                                }
                            }
                        default:
                            break;
                    }
                }
            }
            public override string GetProperty()
            {
                if (_token != Token.Property || _isGet)
                    throw new InvalidOperationException(nameof(GetProperty));

                var @char = PeekChar();
                if (@char == -1)
                    throw new FormatException("EOF");
                var ch = (char)@char;
                var start = _position++;
                for (; ; )
                {
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '"')
                    {
                        var property = new string(_buffer, start, _position - start - 1);
                        for (; ; )
                        {
                            @char = ReadChar();
                            switch (@char)
                            {
                                case -1:
                                    throw new FormatException("EOF");
                                case ':':
                                    _isGet = true;
                                    return property;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{(char)@char}'");
                            }
                        }
                    }
                    if (_position == _length)
                        break;
                    ch = _buffer[_position++];
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        if (ch == '\\')
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start - 1));
                            for (; ; )
                            {
                                @char = ReadChar();
                                switch (@char)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case '0':
                                        sb.Write('\u0000');
                                        break;
                                    case '\'':
                                        sb.Write('\'');
                                        break;
                                    case '"':
                                        sb.Write('"');
                                        break;
                                    case '\\':
                                        sb.Write('\\');
                                        break;
                                    case '/':
                                        sb.Write('/');
                                        break;
                                    case 'r':
                                        sb.Write('\r');
                                        break;
                                    case 'n':
                                        sb.Write('\n');
                                        break;
                                    case 't':
                                        sb.Write('\t');
                                        break;
                                    case 'v':
                                        sb.Write('\v');
                                        break;
                                    case 'b':
                                        sb.Write('\b');
                                        break;
                                    case 'f':
                                        sb.Write('\f');
                                        break;
                                    case 'u':
                                        sb.Write(GetUnicode());
                                        break;
                                    default:
                                        throw new NotSupportedException("\\" + (char)@char);
                                }
                                @char = PeekChar();
                                if (@char == -1)
                                    throw new FormatException("EOF");
                                if (@char == '\\')
                                {
                                    _position++;
                                }
                                else
                                {
                                    ch = (char)@char;
                                    start = _position;
                                    _position++;
                                    break;
                                }
                            }
                        }
                        if (ch == '"')
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start - 1));
                            for (; ; )
                            {
                                @char = ReadChar();
                                switch (@char)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case ':':
                                        _isGet = true;
                                        return sb.ToString();
                                    case ' ':
                                    case '\t':
                                    case '\r':
                                    case '\n':
                                    case '\v':
                                    case '\f':
                                    case '\u0000':
                                    case '\u0085':
                                    case '\u2028':
                                    case '\u2029':
                                    case '\u00A0':
                                    case '\uFEFF':
                                        continue;
                                    default:
                                        throw new FormatException($"Unexpected character '{ch}'");
                                }
                            }
                        }
                        if (_position == _length)
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start));
                            start = 0;
                            @char = ReadChar();
                            if (@char == -1)
                                throw new FormatException("EOF");
                            ch = (char)@char;
                        }
                        else
                        {
                            ch = _buffer[_position++];
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override void GetProperty(out ReadOnlySpan<char> property)
            {
                if (_token != Token.Property || _isGet)
                    throw new InvalidOperationException(nameof(GetProperty));

                var @char = PeekChar();
                if (@char == -1)
                    throw new FormatException("EOF");
                var ch = (char)@char;
                var start = _position++;
                for (; ; )
                {
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '"')
                    {
                        property = new ReadOnlySpan<char>(_buffer, start, _position - start - 1);
                        for (; ; )
                        {
                            @char = ReadChar();
                            switch (@char)
                            {
                                case -1:
                                    throw new FormatException("EOF");
                                case ':':
                                    _isGet = true;
                                    return;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{(char)@char}'");
                            }
                        }
                    }
                    if (_position == _length)
                        break;
                    ch = _buffer[_position++];
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        if (ch == '\\')
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start - 1));
                            for (; ; )
                            {
                                @char = ReadChar();
                                switch (@char)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case '0':
                                        sb.Write('\u0000');
                                        break;
                                    case '\'':
                                        sb.Write('\'');
                                        break;
                                    case '"':
                                        sb.Write('"');
                                        break;
                                    case '\\':
                                        sb.Write('\\');
                                        break;
                                    case '/':
                                        sb.Write('/');
                                        break;
                                    case 'r':
                                        sb.Write('\r');
                                        break;
                                    case 'n':
                                        sb.Write('\n');
                                        break;
                                    case 't':
                                        sb.Write('\t');
                                        break;
                                    case 'v':
                                        sb.Write('\v');
                                        break;
                                    case 'b':
                                        sb.Write('\b');
                                        break;
                                    case 'f':
                                        sb.Write('\f');
                                        break;
                                    case 'u':
                                        sb.Write(GetUnicode());
                                        break;
                                    default:
                                        throw new NotSupportedException("\\" + (char)@char);
                                }
                                @char = PeekChar();
                                if (@char == -1)
                                    throw new FormatException("EOF");
                                if (@char == '\\')
                                {
                                    _position++;
                                }
                                else
                                {
                                    ch = (char)@char;
                                    start = _position;
                                    _position++;
                                    break;
                                }
                            }
                        }
                        if (ch == '"')
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start - 1));
                            property = sb.ToString();
                            for (; ; )
                            {
                                @char = ReadChar();
                                switch (@char)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case ':':
                                        _isGet = true;
                                        return;
                                    case ' ':
                                    case '\t':
                                    case '\r':
                                    case '\n':
                                    case '\v':
                                    case '\f':
                                    case '\u0000':
                                    case '\u0085':
                                    case '\u2028':
                                    case '\u2029':
                                    case '\u00A0':
                                    case '\uFEFF':
                                        continue;
                                    default:
                                        throw new FormatException($"Unexpected character '{ch}'");
                                }
                            }
                        }
                        if (_position == _length)
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start));
                            start = 0;
                            @char = ReadChar();
                            if (@char == -1)
                                throw new FormatException("EOF");
                            ch = (char)@char;
                        }
                        else
                        {
                            ch = _buffer[_position++];
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            private void SkipString()
            {
                Debug.Assert(!_isGet);
                for (; ; )
                {
                    var ch = ReadChar();
                    switch (ch)
                    {
                        case -1:
                            throw new FormatException("EOF");
                        case '\\':
                            ch = ReadChar();
                            switch (ch)
                            {
                                case -1:
                                    throw new FormatException("EOF");
                                case '0':
                                case '\'':
                                case '"':
                                case '\\':
                                case '/':
                                case 'r':
                                case 'n':
                                case 't':
                                case 'v':
                                case 'b':
                                case 'f':
                                    break;
                                case 'u':
                                    if (ReadChar() == -1 || ReadChar() == -1 || ReadChar() == -1 || ReadChar() == -1)
                                        throw new FormatException("EOF");
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + (char)ch);
                            }
                            break;
                        case '"':
                            _isGet = true;
                            return;
                        default:
                            break;
                    }
                }
            }
            public override string GetString()
            {
                if (_token == Token.Null)
                    return null;
                if (_token != Token.String || _isGet)
                    throw new InvalidOperationException(nameof(GetString));

                var @char = PeekChar();
                if (@char == -1)
                    throw new FormatException("EOF");
                var ch = (char)@char;
                var start = _position++;
                for (; ; )
                {
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '"')
                    {
                        _isGet = true;
                        return new string(_buffer, start, _position - start - 1);
                    }
                    if (_position == _length)
                        break;
                    ch = _buffer[_position++];
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        if (ch == '\\')
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start - 1));
                            for (; ; )
                            {
                                @char = ReadChar();
                                switch (@char)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case '0':
                                        sb.Write('\u0000');
                                        break;
                                    case '\'':
                                        sb.Write('\'');
                                        break;
                                    case '"':
                                        sb.Write('"');
                                        break;
                                    case '\\':
                                        sb.Write('\\');
                                        break;
                                    case '/':
                                        sb.Write('/');
                                        break;
                                    case 'r':
                                        sb.Write('\r');
                                        break;
                                    case 'n':
                                        sb.Write('\n');
                                        break;
                                    case 't':
                                        sb.Write('\t');
                                        break;
                                    case 'v':
                                        sb.Write('\v');
                                        break;
                                    case 'b':
                                        sb.Write('\b');
                                        break;
                                    case 'f':
                                        sb.Write('\f');
                                        break;
                                    case 'u':
                                        sb.Write(GetUnicode());
                                        break;
                                    default:
                                        throw new NotSupportedException("\\" + (char)@char);
                                }
                                @char = PeekChar();
                                if (@char == -1)
                                    throw new FormatException("EOF");
                                if (@char == '\\')
                                {
                                    _position++;
                                }
                                else
                                {
                                    ch = (char)@char;
                                    start = _position;
                                    _position++;
                                    break;
                                }
                            }
                        }
                        if (ch == '"')
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start - 1));
                            _isGet = true;
                            return sb.ToString();
                        }
                        if (_position == _length)
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start));
                            start = 0;
                            @char = ReadChar();
                            if (@char == -1)
                                throw new FormatException("EOF");
                            ch = (char)@char;
                        }
                        else
                        {
                            ch = _buffer[_position++];
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override void GetString(out ReadOnlySpan<char> value)
            {
                if (_token == Token.Null)
                {
                    value = null;
                    return;
                }
                if (_token != Token.String || _isGet)
                    throw new InvalidOperationException(nameof(GetString));

                var @char = PeekChar();
                if (@char == -1)
                    throw new FormatException("EOF");
                var ch = (char)@char;
                var start = _position++;
                for (; ; )
                {
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '"')
                    {
                        _isGet = true;
                        value = new ReadOnlySpan<char>(_buffer, start, _position - start - 1);
                        return;
                    }
                    if (_position == _length)
                        break;
                    ch = _buffer[_position++];
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        if (ch == '\\')
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start - 1));
                            for (; ; )
                            {
                                @char = ReadChar();
                                switch (@char)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case '0':
                                        sb.Write('\u0000');
                                        break;
                                    case '\'':
                                        sb.Write('\'');
                                        break;
                                    case '"':
                                        sb.Write('"');
                                        break;
                                    case '\\':
                                        sb.Write('\\');
                                        break;
                                    case '/':
                                        sb.Write('/');
                                        break;
                                    case 'r':
                                        sb.Write('\r');
                                        break;
                                    case 'n':
                                        sb.Write('\n');
                                        break;
                                    case 't':
                                        sb.Write('\t');
                                        break;
                                    case 'v':
                                        sb.Write('\v');
                                        break;
                                    case 'b':
                                        sb.Write('\b');
                                        break;
                                    case 'f':
                                        sb.Write('\f');
                                        break;
                                    case 'u':
                                        sb.Write(GetUnicode());
                                        break;
                                    default:
                                        throw new NotSupportedException("\\" + (char)@char);
                                }
                                @char = PeekChar();
                                if (@char == -1)
                                    throw new FormatException("EOF");
                                if (@char == '\\')
                                {
                                    _position++;
                                }
                                else
                                {
                                    ch = (char)@char;
                                    start = _position;
                                    _position++;
                                    break;
                                }
                            }
                        }
                        if (ch == '"')
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start - 1));
                            _isGet = true;
                            value = sb.ToString();
                            return;
                        }
                        if (_position == _length)
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start));
                            start = 0;
                            @char = ReadChar();
                            if (@char == -1)
                                throw new FormatException("EOF");
                            ch = (char)@char;
                        }
                        else
                        {
                            ch = _buffer[_position++];
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override byte GetByte()
            {
                GetNumber(out var number);
                return byte.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override sbyte GetSByte()
            {
                GetNumber(out var number);
                return sbyte.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override short GetInt16()
            {
                GetNumber(out var number);
                return short.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override ushort GetUInt16()
            {
                GetNumber(out var number);
                return ushort.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override int GetInt32()
            {
                GetNumber(out var number);
                return int.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override uint GetUInt32()
            {
                GetNumber(out var number);
                return uint.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override long GetInt64()
            {
                GetNumber(out var number);
                return long.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override ulong GetUInt64()
            {
                GetNumber(out var number);
                return ulong.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override float GetSingle()
            {
                GetNumber(out var number);
                return float.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override double GetDouble()
            {
                GetNumber(out var number);
                return double.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override decimal GetDecimal()
            {
                GetNumber(out var number);
                return decimal.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            private void SkipNumber()
            {
                Debug.Assert(!_isGet);
                _position++;
                for (; ; )
                {
                    var @char = PeekChar();
                    switch (@char)
                    {
                        case -1:
                        case ',':
                        case '}':
                        case ']':
                            _isGet = true;
                            return;
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                        case '\v':
                        case '\f':
                        case '\u0000':
                        case '\u0085':
                        case '\u2028':
                        case '\u2029':
                        case '\u00A0':
                        case '\uFEFF':
                            _isGet = true;
                            _position += 1;
                            return;
                        default:
                            _position += 1;
                            continue;
                    }
                }
            }
            public override void GetNumber(out ReadOnlySpan<char> value)
            {
                if (_token != Token.Number || _isGet)
                    throw new InvalidOperationException(nameof(GetNumber));

                var start = _position++;
                while (_position < _length)
                {
                    var ch = _buffer[_position];
                    switch (ch)
                    {
                        case ',':
                        case '}':
                        case ']':
                            _isGet = true;
                            value = new ReadOnlySpan<char>(_buffer, start, _position - start);
                            return;
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                        case '\v':
                        case '\f':
                        case '\u0000':
                        case '\u0085':
                        case '\u2028':
                        case '\u2029':
                        case '\u00A0':
                        case '\uFEFF':
                            _isGet = true;
                            value = new ReadOnlySpan<char>(_buffer, start, _position - start);
                            _position += 1;
                            return;
                        default:
                            _position += 1;
                            continue;
                    }
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start));
                    for (; ; )
                    {
                        var @char = PeekChar();
                        switch (@char)
                        {
                            case -1:
                            case ',':
                            case '}':
                            case ']':
                                _isGet = true;
                                value = sb.ToString();
                                return;
                            case ' ':
                            case '\t':
                            case '\r':
                            case '\n':
                            case '\v':
                            case '\f':
                            case '\u0000':
                            case '\u0085':
                            case '\u2028':
                            case '\u2029':
                            case '\u00A0':
                            case '\uFEFF':
                                _position += 1;
                                _isGet = true;
                                value = sb.ToString();
                                return;
                            default:
                                _position += 1;
                                sb.Write((char)@char);
                                continue;
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override bool GetBoolean()
            {
                if (_token != Token.Boolean)
                    throw new InvalidOperationException(nameof(GetBoolean));

                return _boolean;
            }
            public override void Skip()
            {
                switch (_token)
                {
                    case Token.None:
                        if (Read())
                        {
                            Skip();
                        }
                        return;
                    case Token.StartArray:
                        for (; ; )
                        {
                            if (!Read())
                                throw new FormatException("EOF");

                            if (_token == Token.EndArray)
                                return;

                            Skip();
                        }
                    case Token.StartObject:
                        for (; ; )
                        {
                            if (!Read())
                                throw new FormatException("EOF");

                            if (_token == Token.EndObject)
                                return;

                            Skip();
                        }
                    case Token.Property:
                        {
                            if (!Read())
                                throw new FormatException("EOF");

                            Skip();
                            return;
                        }
                    default:
                        return;
                }
            }
        }
        private class StringJson5Reader : JsonReader
        {
            private enum Token : byte
            {
                None,
                StartArray,
                EndArray,
                StartObject,
                EndObject,
                PropertyQuote,
                PropertyDoubleQuote,
                PropertyWithoutQuote,
                StringQuote,
                StringDoubleQuote,
                Number,
                Boolean,
                Null
            }
            private static int _MaxDepth = 64;
            #region private
            private string _value;
            private int _position;
            private int _depth = -1;
            private bool[] _isObject;
            private Token _token;
            private bool _isGet;
            private bool _boolean;
            #endregion
            public StringJson5Reader(string value)
            {
                _value = value;
                _token = Token.None;
                _isObject = new bool[_MaxDepth];
            }
            public void Set(string value) => _value = value;
            public void Reset()
            {
                _token = Token.None;
                _value = null;
                _position = 0;
                _depth = -1;
            }
            private char GetUnicode()
            {
                if (_position + 4 > _value.Length)
                    throw new FormatException("\\uxxxx");

                var unicode = 0;
                for (var i = 1; ;)
                {
                    var ch = _value[_position++];
                    switch (ch)
                    {
                        case '0':
                            break;
                        case '1':
                            unicode += 1;
                            break;
                        case '2':
                            unicode += 2;
                            break;
                        case '3':
                            unicode += 3;
                            break;
                        case '4':
                            unicode += 4;
                            break;
                        case '5':
                            unicode += 5;
                            break;
                        case '6':
                            unicode += 6;
                            break;
                        case '7':
                            unicode += 7;
                            break;
                        case '8':
                            unicode += 8;
                            break;
                        case '9':
                            unicode += 9;
                            break;
                        case 'a':
                        case 'A':
                            unicode += 10;
                            break;
                        case 'b':
                        case 'B':
                            unicode += 11;
                            break;
                        case 'c':
                        case 'C':
                            unicode += 12;
                            break;
                        case 'd':
                        case 'D':
                            unicode += 13;
                            break;
                        case 'e':
                        case 'E':
                            unicode += 14;
                            break;
                        case 'f':
                        case 'F':
                            unicode += 15;
                            break;
                        default:
                            throw new FormatException("\\uxxxx");
                    }
                    if (i == 4)
                        return (char)unicode;
                    i += 1;
                    unicode <<= 4;
                }
            }
            public override bool Read()
            {
                #region function
                bool Next()
                {
                    if (_depth == -1)
                        return NextValue();

                    if (_isObject[_depth])
                    {
                        while (_position < _value.Length)
                        {
                            var ch = _value[_position++];
                            switch (ch)
                            {
                                case ',':
                                    return NextProperty();
                                case '}':
                                    if (!_isObject[_depth--])
                                        throw new FormatException($"Unexpected character '{ch}'");
                                    _token = Token.EndObject;
                                    return true;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                case '/':
                                    SkipComment();
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{ch}'");
                            }
                        }
                        return false;
                    }
                    else
                    {
                        while (_position < _value.Length)
                        {
                            var ch = _value[_position++];
                            switch (ch)
                            {
                                case ',':
                                    return NextValue();
                                case ']':
                                    if (_isObject[_depth--])
                                        throw new FormatException($"Unexpected character '{ch}'");
                                    _token = Token.EndArray;
                                    return true;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                case '/':
                                    SkipComment();
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{ch}'");
                            }
                        }
                        return false;
                    }
                }
                bool NextProperty()
                {
                    while (_position < _value.Length)
                    {
                        var ch = _value[_position];
                        switch (ch)
                        {
                            case '\'':
                                _position += 1;
                                _isGet = false;
                                _token = Token.PropertyQuote;
                                return true;
                            case '"':
                                _position += 1;
                                _isGet = false;
                                _token = Token.PropertyDoubleQuote;
                                return true;
                            case '}':
                                if (!_isObject[_depth--])
                                    throw new FormatException($"Unexpected character '{ch}'");
                                _position += 1;
                                _token = Token.EndObject;
                                return true;
                            case ' ':
                            case '\t':
                            case '\r':
                            case '\n':
                            case '\v':
                            case '\f':
                            case '\u0000':
                            case '\u0085':
                            case '\u2028':
                            case '\u2029':
                            case '\u00A0':
                            case '\uFEFF':
                                _position += 1;
                                continue;
                            case '/':
                                _position += 1;
                                SkipComment();
                                continue;
                            case '{':
                            case '[':
                            case ']':
                            case ',':
                            case ':':
                                throw new FormatException($"Unexpected character '{ch}'");
                            default:
                                _isGet = false;
                                _token = Token.PropertyWithoutQuote;
                                return true;
                        }
                    }
                    return false;
                }
                bool NextValue()
                {
                    while (_position < _value.Length)
                    {
                        var ch = _value[_position];
                        switch (ch)
                        {
                            case '{':
                                _position += 1;
                                _depth += 1;
                                _isObject[_depth] = true;
                                _token = Token.StartObject;
                                return true;
                            case '[':
                                _position += 1;
                                _depth += 1;
                                _isObject[_depth] = false;
                                _token = Token.StartArray;
                                return true;
                            case '\'':
                                _position += 1;
                                _isGet = false;
                                _token = Token.StringQuote;
                                return true;
                            case '"':
                                _position += 1;
                                _isGet = false;
                                _token = Token.StringDoubleQuote;
                                return true;
                            case 'n':
                                if (_value.Length - _position < 4 || _value[_position + 1] != 'u' ||
                                    _value[_position + 2] != 'l' || _value[_position + 3] != 'l')
                                    throw new FormatException("Unexpected character");
                                _position += 4;
                                _token = Token.Null;
                                return true;
                            case 't':
                                if (_value.Length - _position < 4 || _value[_position + 1] != 'r' ||
                                     _value[_position + 2] != 'u' || _value[_position + 3] != 'e')
                                    throw new FormatException("Unexpected character");
                                _position += 4;
                                _boolean = true;
                                _token = Token.Boolean;
                                return true;
                            case 'f':
                                if (_value.Length - _position < 5 || _value[_position + 1] != 'a' ||
                                    _value[_position + 2] != 'l' || _value[_position + 3] != 's' || _value[_position + 4] != 'e')
                                    throw new FormatException("Unexpected character");
                                _position += 5;
                                _boolean = false;
                                _token = Token.Boolean;
                                return true;
                            case '0':
                            case '1':
                            case '2':
                            case '3':
                            case '4':
                            case '5':
                            case '6':
                            case '7':
                            case '8':
                            case '9':
                            case '+':
                            case '-':
                            case '.':
                            case 'N':
                            case 'I':
                                _isGet = false;
                                _token = Token.Number;
                                return true;
                            case ' ':
                            case '\t':
                            case '\r':
                            case '\n':
                            case '\v':
                            case '\f':
                            case '\u0000':
                            case '\u0085':
                            case '\u2028':
                            case '\u2029':
                            case '\u00A0':
                            case '\uFEFF':
                                _position += 1;
                                continue;
                            case '/':
                                _position += 1;
                                SkipComment();
                                continue;
                            case ']':
                                if (_isObject[_depth--])
                                    throw new FormatException($"Unexpected character '{ch}'");
                                _position += 1;
                                _token = Token.EndArray;
                                return true;
                            default:
                                throw new FormatException($"Unexpected character '{ch}'");
                        }
                    }
                    return false;
                }
                #endregion
                switch (_token)
                {
                    case Token.None:
                        return NextValue();
                    case Token.StartObject:
                        return NextProperty();
                    case Token.PropertyQuote:
                        if (!_isGet)
                            SkipPropertyQuote();
                        return NextValue();
                    case Token.PropertyDoubleQuote:
                        if (!_isGet)
                            SkipPropertyDoubleQuote();
                        return NextValue();
                    case Token.PropertyWithoutQuote:
                        if (!_isGet)
                            SkipPropertyWithoutQuote();
                        return NextValue();
                    case Token.StringQuote:
                        if (!_isGet)
                            SkipStringQuote();
                        return Next();
                    case Token.StringDoubleQuote:
                        if (!_isGet)
                            SkipStringDoubleQuote();
                        return Next();
                    case Token.Number:
                        if (!_isGet)
                            SkipNumber();
                        return Next();
                    case Token.Boolean:
                    case Token.Null:
                        return Next();
                    case Token.StartArray:
                        return NextValue();
                    case Token.EndObject:
                    case Token.EndArray:
                        return Next();
                    default:
                        return false;
                }
            }
            public override bool IsStartArray => _token == Token.StartArray;
            public override bool IsEndArray => _token == Token.EndArray;
            public override bool IsStartObject => _token == Token.StartObject;
            public override bool IsEndObject => _token == Token.EndObject;
            public override bool IsProperty => _token == Token.PropertyQuote || _token == Token.PropertyDoubleQuote || _token == Token.PropertyWithoutQuote;
            public override bool IsString => _token == Token.StringQuote || _token == Token.StringDoubleQuote;
            public override bool IsNumber => _token == Token.Number;
            public override bool IsBoolean => _token == Token.Boolean;
            public override bool IsNull => _token == Token.Null;
            private void SkipComment()
            {
                if (_position < _value.Length)
                {
                    var ch = _value[_position++];
                    switch (ch)
                    {
                        case '*':
                            while (_position < _value.Length)
                            {
                                ch = _value[_position++];
                                if (ch == '*')
                                {
                                    while (_position < _value.Length)
                                    {
                                        ch = _value[_position++];
                                        switch (ch)
                                        {
                                            case '*':
                                                continue;
                                            case '/':
                                                return;
                                            default:
                                                break;
                                        }
                                        break;
                                    }
                                }
                            }
                            break;
                        case '/':
                            while (_position < _value.Length)
                            {
                                ch = _value[_position++];
                                switch (ch)
                                {
                                    case '\r':
                                    case '\n':
                                    case '\u0085':
                                    case '\u2028':
                                    case '\u2029':
                                        return;
                                    default:
                                        continue;
                                }
                            }
                            return;
                        default:
                            throw new FormatException($"Unexpected character '{ch}'");
                    }
                }
                throw new FormatException("EOF");
            }
            private void SkipPropertyQuote()
            {
                Debug.Assert(_token == Token.PropertyQuote);
                Debug.Assert(!_isGet);
                for (; ; )
                {
                    if (_position == _value.Length)
                        throw new FormatException("EOF");
                    var ch = _value[_position++];
                    if (ch == '\\')
                    {
                        if (_position == _value.Length)
                            throw new FormatException("EOF");
                        ch = _value[_position++];
                        switch (ch)
                        {
                            case '0':
                            case '\'':
                            case '"':
                            case '\\':
                            case '/':
                            case 'r':
                            case 'n':
                            case 't':
                            case 'v':
                            case 'b':
                            case 'f':
                                break;
                            case 'u':
                                _position += 4;
                                break;
                            default:
                                throw new NotSupportedException("\\" + ch);
                        }
                    }
                    else if (ch == '\'')
                    {
                        for (; ; )
                        {
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case ':':
                                    _isGet = true;
                                    return;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                case '/':
                                    SkipComment();
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{ch}'");
                            }
                        }
                    }
                }
            }
            private string GetPropertyQuote()
            {
                Debug.Assert(_token == Token.PropertyQuote);
                Debug.Assert(!_isGet);
                var start = _position;
                char ch;
                for (; ; )
                {
                    if (_position == _value.Length)
                        throw new FormatException("EOF");
                    ch = _value[_position++];
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '\'')
                    {
                        var property = _value.Substring(start, _position - start - 1);
                        for (; ; )
                        {
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case ':':
                                    _isGet = true;
                                    return property;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                case '/':
                                    SkipComment();
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{ch}'");
                            }
                        }
                    }
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        sb.Write(_value.AsSpan(start, _position - start - 1));
                        for (; ; )
                        {
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case '0':
                                    sb.Write('\u0000');
                                    break;
                                case '\'':
                                    sb.Write('\'');
                                    break;
                                case '"':
                                    sb.Write('"');
                                    break;
                                case '\\':
                                    sb.Write('\\');
                                    break;
                                case '/':
                                    sb.Write('/');
                                    break;
                                case 'r':
                                    sb.Write('\r');
                                    break;
                                case 'n':
                                    sb.Write('\n');
                                    break;
                                case 't':
                                    sb.Write('\t');
                                    break;
                                case 'v':
                                    sb.Write('\v');
                                    break;
                                case 'b':
                                    sb.Write('\b');
                                    break;
                                case 'f':
                                    sb.Write('\f');
                                    break;
                                case 'u':
                                    sb.Write(GetUnicode());
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + ch);
                            }
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position];
                            if (ch == '\\')
                            {
                                _position++;
                            }
                            else
                            {
                                start = _position;
                                _position++;
                                break;
                            }
                        }
                        for (; ; )
                        {
                            if (ch == '\'')
                            {
                                sb.Write(_value.AsSpan(start, _position - start - 1));
                                for (; ; )
                                {
                                    if (_position == _value.Length)
                                        throw new FormatException("EOF");
                                    ch = _value[_position++];
                                    switch (ch)
                                    {
                                        case ':':
                                            _isGet = true;
                                            return sb.ToString();
                                        case ' ':
                                        case '\t':
                                        case '\r':
                                        case '\n':
                                        case '\v':
                                        case '\f':
                                        case '\u0000':
                                        case '\u0085':
                                        case '\u2028':
                                        case '\u2029':
                                        case '\u00A0':
                                        case '\uFEFF':
                                            continue;
                                        case '/':
                                            SkipComment();
                                            continue;
                                        default:
                                            throw new FormatException($"Unexpected character '{ch}'");
                                    }
                                }
                            }
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            if (ch == '\\')
                            {
                                break;
                            }
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            private void GetPropertyQuote(out ReadOnlySpan<char> property)
            {
                Debug.Assert(_token == Token.PropertyQuote);
                Debug.Assert(!_isGet);
                var start = _position;
                char ch;
                for (; ; )
                {
                    if (_position == _value.Length)
                        throw new FormatException("EOF");
                    ch = _value[_position++];
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '\'')
                    {
                        property = _value.AsSpan(start, _position - start - 1);
                        for (; ; )
                        {
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case ':':
                                    _isGet = true;
                                    return;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                case '/':
                                    SkipComment();
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{ch}'");
                            }
                        }
                    }
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        sb.Write(_value.AsSpan(start, _position - start - 1));
                        for (; ; )
                        {
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case '0':
                                    sb.Write('\u0000');
                                    break;
                                case '\'':
                                    sb.Write('\'');
                                    break;
                                case '"':
                                    sb.Write('"');
                                    break;
                                case '\\':
                                    sb.Write('\\');
                                    break;
                                case '/':
                                    sb.Write('/');
                                    break;
                                case 'r':
                                    sb.Write('\r');
                                    break;
                                case 'n':
                                    sb.Write('\n');
                                    break;
                                case 't':
                                    sb.Write('\t');
                                    break;
                                case 'v':
                                    sb.Write('\v');
                                    break;
                                case 'b':
                                    sb.Write('\b');
                                    break;
                                case 'f':
                                    sb.Write('\f');
                                    break;
                                case 'u':
                                    sb.Write(GetUnicode());
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + ch);
                            }
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position];
                            if (ch == '\\')
                            {
                                _position++;
                            }
                            else
                            {
                                start = _position;
                                _position++;
                                break;
                            }
                        }
                        for (; ; )
                        {
                            if (ch == '\'')
                            {
                                sb.Write(_value.AsSpan(start, _position - start - 1));
                                property = sb.ToString();
                                for (; ; )
                                {
                                    if (_position == _value.Length)
                                        throw new FormatException("EOF");
                                    ch = _value[_position++];
                                    switch (ch)
                                    {
                                        case ':':
                                            _isGet = true;
                                            return;
                                        case ' ':
                                        case '\t':
                                        case '\r':
                                        case '\n':
                                        case '\v':
                                        case '\f':
                                        case '\u0000':
                                        case '\u0085':
                                        case '\u2028':
                                        case '\u2029':
                                        case '\u00A0':
                                        case '\uFEFF':
                                            continue;
                                        case '/':
                                            SkipComment();
                                            continue;
                                        default:
                                            throw new FormatException($"Unexpected character '{ch}'");
                                    }
                                }
                            }
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            if (ch == '\\')
                            {
                                break;
                            }
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            private void SkipPropertyDoubleQuote()
            {
                Debug.Assert(_token == Token.PropertyDoubleQuote);
                Debug.Assert(!_isGet);
                for (; ; )
                {
                    if (_position == _value.Length)
                        throw new FormatException("EOF");
                    var ch = _value[_position++];
                    if (ch == '\\')
                    {
                        if (_position == _value.Length)
                            throw new FormatException("EOF");
                        ch = _value[_position++];
                        switch (ch)
                        {
                            case '0':
                            case '\'':
                            case '"':
                            case '\\':
                            case '/':
                            case 'r':
                            case 'n':
                            case 't':
                            case 'v':
                            case 'b':
                            case 'f':
                                break;
                            case 'u':
                                _position += 4;
                                break;
                            default:
                                throw new NotSupportedException("\\" + ch);
                        }
                    }
                    else if (ch == '"')
                    {
                        for (; ; )
                        {
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case ':':
                                    _isGet = true;
                                    return;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                case '/':
                                    SkipComment();
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{ch}'");
                            }
                        }
                    }
                }
            }
            private string GetPropertyDoubleQuote()
            {
                Debug.Assert(_token == Token.PropertyDoubleQuote);
                Debug.Assert(!_isGet);
                var start = _position;
                char ch;
                for (; ; )
                {
                    if (_position == _value.Length)
                        throw new FormatException("EOF");
                    ch = _value[_position++];
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '"')
                    {
                        var property = _value.Substring(start, _position - start - 1);
                        for (; ; )
                        {
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case ':':
                                    _isGet = true;
                                    return property;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                case '/':
                                    SkipComment();
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{ch}'");
                            }
                        }
                    }
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        sb.Write(_value.AsSpan(start, _position - start - 1));
                        for (; ; )
                        {
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case '0':
                                    sb.Write('\u0000');
                                    break;
                                case '\'':
                                    sb.Write('\'');
                                    break;
                                case '"':
                                    sb.Write('"');
                                    break;
                                case '\\':
                                    sb.Write('\\');
                                    break;
                                case '/':
                                    sb.Write('/');
                                    break;
                                case 'r':
                                    sb.Write('\r');
                                    break;
                                case 'n':
                                    sb.Write('\n');
                                    break;
                                case 't':
                                    sb.Write('\t');
                                    break;
                                case 'v':
                                    sb.Write('\v');
                                    break;
                                case 'b':
                                    sb.Write('\b');
                                    break;
                                case 'f':
                                    sb.Write('\f');
                                    break;
                                case 'u':
                                    sb.Write(GetUnicode());
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + ch);
                            }
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position];
                            if (ch == '\\')
                            {
                                _position++;
                            }
                            else
                            {
                                start = _position;
                                _position++;
                                break;
                            }
                        }
                        for (; ; )
                        {
                            if (ch == '"')
                            {
                                sb.Write(_value.AsSpan(start, _position - start - 1));
                                for (; ; )
                                {
                                    if (_position == _value.Length)
                                        throw new FormatException("EOF");
                                    ch = _value[_position++];
                                    switch (ch)
                                    {
                                        case ':':
                                            _isGet = true;
                                            return sb.ToString();
                                        case ' ':
                                        case '\t':
                                        case '\r':
                                        case '\n':
                                        case '\v':
                                        case '\f':
                                        case '\u0000':
                                        case '\u0085':
                                        case '\u2028':
                                        case '\u2029':
                                        case '\u00A0':
                                        case '\uFEFF':
                                            continue;
                                        case '/':
                                            SkipComment();
                                            continue;
                                        default:
                                            throw new FormatException($"Unexpected character '{ch}'");
                                    }
                                }
                            }
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            if (ch == '\\')
                            {
                                break;
                            }
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            private void GetPropertyDoubleQuote(out ReadOnlySpan<char> property)
            {
                Debug.Assert(_token == Token.PropertyDoubleQuote);
                Debug.Assert(!_isGet);
                var start = _position;
                char ch;
                for (; ; )
                {
                    if (_position == _value.Length)
                        throw new FormatException("EOF");
                    ch = _value[_position++];
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '"')
                    {
                        property = _value.AsSpan(start, _position - start - 1);
                        for (; ; )
                        {
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case ':':
                                    _isGet = true;
                                    return;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                case '/':
                                    SkipComment();
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{ch}'");
                            }
                        }
                    }
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        sb.Write(_value.AsSpan(start, _position - start - 1));
                        for (; ; )
                        {
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case '0':
                                    sb.Write('\u0000');
                                    break;
                                case '\'':
                                    sb.Write('\'');
                                    break;
                                case '"':
                                    sb.Write('"');
                                    break;
                                case '\\':
                                    sb.Write('\\');
                                    break;
                                case '/':
                                    sb.Write('/');
                                    break;
                                case 'r':
                                    sb.Write('\r');
                                    break;
                                case 'n':
                                    sb.Write('\n');
                                    break;
                                case 't':
                                    sb.Write('\t');
                                    break;
                                case 'v':
                                    sb.Write('\v');
                                    break;
                                case 'b':
                                    sb.Write('\b');
                                    break;
                                case 'f':
                                    sb.Write('\f');
                                    break;
                                case 'u':
                                    sb.Write(GetUnicode());
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + ch);
                            }
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position];
                            if (ch == '\\')
                            {
                                _position++;
                            }
                            else
                            {
                                start = _position;
                                _position++;
                                break;
                            }
                        }
                        for (; ; )
                        {
                            if (ch == '"')
                            {
                                sb.Write(_value.AsSpan(start, _position - start - 1));
                                property = sb.ToString();
                                for (; ; )
                                {
                                    if (_position == _value.Length)
                                        throw new FormatException("EOF");
                                    ch = _value[_position++];
                                    switch (ch)
                                    {
                                        case ':':
                                            _isGet = true;
                                            return;
                                        case ' ':
                                        case '\t':
                                        case '\r':
                                        case '\n':
                                        case '\v':
                                        case '\f':
                                        case '\u0000':
                                        case '\u0085':
                                        case '\u2028':
                                        case '\u2029':
                                        case '\u00A0':
                                        case '\uFEFF':
                                            continue;
                                        case '/':
                                            SkipComment();
                                            continue;
                                        default:
                                            throw new FormatException($"Unexpected character '{ch}'");
                                    }
                                }
                            }
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            if (ch == '\\')
                            {
                                break;
                            }
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            private void SkipPropertyWithoutQuote()
            {
                Debug.Assert(_token == Token.PropertyWithoutQuote);
                Debug.Assert(!_isGet);
                _position += 1;
                for (; ; )
                {
                    if (_position == _value.Length)
                        throw new FormatException("EOF");
                    var ch = _value[_position++];
                    switch (ch)
                    {
                        case ':':
                            _isGet = true;
                            return;
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                        case '\v':
                        case '\f':
                        case '\u0000':
                        case '\u0085':
                        case '\u2028':
                        case '\u2029':
                        case '\u00A0':
                        case '\uFEFF':
                            for (; ; )
                            {
                                if (_position == _value.Length)
                                    throw new FormatException("EOF");
                                ch = _value[_position++];
                                switch (ch)
                                {
                                    case ':':
                                        _isGet = true;
                                        return;
                                    case ' ':
                                    case '\t':
                                    case '\r':
                                    case '\n':
                                    case '\v':
                                    case '\f':
                                    case '\u0000':
                                    case '\u0085':
                                    case '\u2028':
                                    case '\u2029':
                                    case '\u00A0':
                                    case '\uFEFF':
                                        continue;
                                    case '/':
                                        SkipComment();
                                        continue;
                                    default:
                                        throw new FormatException($"Unexpected character '{ch}'");
                                }
                            }
                        default:
                            break;
                    }
                }
            }
            private string GetPropertyWithoutQuote()
            {
                Debug.Assert(_token == Token.PropertyWithoutQuote);
                Debug.Assert(!_isGet);
                var start = _position++;
                for (; ; )
                {
                    if (_position == _value.Length)
                        throw new FormatException("EOF");
                    var ch = _value[_position++];
                    switch (ch)
                    {
                        case ':':
                            {
                                var property = _value.Substring(start, _position - start - 1);
                                _isGet = true;
                                return property;
                            }
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                        case '\v':
                        case '\f':
                        case '\u0000':
                        case '\u0085':
                        case '\u2028':
                        case '\u2029':
                        case '\u00A0':
                        case '\uFEFF':
                            {
                                var property = _value.Substring(start, _position - start - 1);
                                for (; ; )
                                {
                                    if (_position == _value.Length)
                                        throw new FormatException("EOF");
                                    ch = _value[_position++];
                                    switch (ch)
                                    {
                                        case ':':
                                            _isGet = true;
                                            return property;
                                        case ' ':
                                        case '\t':
                                        case '\r':
                                        case '\n':
                                        case '\v':
                                        case '\f':
                                        case '\u0000':
                                        case '\u0085':
                                        case '\u2028':
                                        case '\u2029':
                                        case '\u00A0':
                                        case '\uFEFF':
                                            continue;
                                        case '/':
                                            SkipComment();
                                            continue;
                                        default:
                                            throw new FormatException($"Unexpected character '{ch}'");
                                    }
                                }
                            }
                        default:
                            break;
                    }
                }
            }
            private void GetPropertyWithoutQuote(out ReadOnlySpan<char> property)
            {
                Debug.Assert(_token == Token.PropertyWithoutQuote);
                Debug.Assert(!_isGet);
                var start = _position++;
                for (; ; )
                {
                    if (_position == _value.Length)
                        throw new FormatException("EOF");
                    var ch = _value[_position++];
                    switch (ch)
                    {
                        case ':':
                            {
                                property = _value.Substring(start, _position - start - 1);
                                _isGet = true;
                                return;
                            }
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                        case '\v':
                        case '\f':
                        case '\u0000':
                        case '\u0085':
                        case '\u2028':
                        case '\u2029':
                        case '\u00A0':
                        case '\uFEFF':
                            {
                                property = _value.Substring(start, _position - start - 1);
                                for (; ; )
                                {
                                    if (_position == _value.Length)
                                        throw new FormatException("EOF");
                                    ch = _value[_position++];
                                    switch (ch)
                                    {
                                        case ':':
                                            _isGet = true;
                                            return;
                                        case ' ':
                                        case '\t':
                                        case '\r':
                                        case '\n':
                                        case '\v':
                                        case '\f':
                                        case '\u0000':
                                        case '\u0085':
                                        case '\u2028':
                                        case '\u2029':
                                        case '\u00A0':
                                        case '\uFEFF':
                                            continue;
                                        case '/':
                                            SkipComment();
                                            continue;
                                        default:
                                            throw new FormatException($"Unexpected character '{ch}'");
                                    }
                                }
                            }
                        default:
                            break;
                    }
                }
            }
            public override string GetProperty()
            {
                if (_isGet)
                    throw new InvalidOperationException(nameof(GetProperty));

                switch (_token)
                {
                    case Token.PropertyQuote:
                        return GetPropertyQuote();
                    case Token.PropertyDoubleQuote:
                        return GetPropertyDoubleQuote();
                    case Token.PropertyWithoutQuote:
                        return GetPropertyWithoutQuote();
                    default:
                        throw new InvalidOperationException(nameof(GetProperty));
                }
            }
            public override void GetProperty(out ReadOnlySpan<char> property)
            {
                if (_isGet)
                    throw new InvalidOperationException(nameof(GetProperty));

                switch (_token)
                {
                    case Token.PropertyQuote:
                        GetPropertyQuote(out property);
                        break;
                    case Token.PropertyDoubleQuote:
                        GetPropertyDoubleQuote(out property);
                        break;
                    case Token.PropertyWithoutQuote:
                        GetPropertyWithoutQuote(out property);
                        break;
                    default:
                        throw new InvalidOperationException(nameof(GetProperty));
                }
            }
            private void SkipStringQuote()
            {
                Debug.Assert(_token == Token.StringQuote);
                Debug.Assert(!_isGet);
                for (; ; )
                {
                    if (_position == _value.Length)
                        throw new FormatException("EOF");
                    var ch = _value[_position++];
                    if (ch == '\\')
                    {
                        if (_position == _value.Length)
                            throw new FormatException("EOF");
                        ch = _value[_position++];
                        switch (ch)
                        {
                            case '0':
                            case '\'':
                            case '"':
                            case '\\':
                            case '/':
                            case 'r':
                            case 'n':
                            case 't':
                            case 'v':
                            case 'b':
                            case 'f':
                                break;
                            case 'u':
                                _position += 4;
                                break;
                            default:
                                throw new NotSupportedException("\\" + ch);
                        }
                    }
                    else if (ch == '\'')
                    {
                        _isGet = true;
                        return;
                    }
                }
            }
            private string GetStringQuote()
            {
                Debug.Assert(_token == Token.StringQuote);
                Debug.Assert(!_isGet);
                var start = _position;
                char ch;
                for (; ; )
                {
                    if (_position == _value.Length)
                        throw new FormatException("EOF");
                    ch = _value[_position++];
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '\'')
                    {
                        _isGet = true;
                        return _value.Substring(start, _position - start - 1);
                    }
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        sb.Write(_value.AsSpan(start, _position - start - 1));
                        for (; ; )
                        {
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case '0':
                                    sb.Write('\u0000');
                                    break;
                                case '\'':
                                    sb.Write('\'');
                                    break;
                                case '"':
                                    sb.Write('"');
                                    break;
                                case '\\':
                                    sb.Write('\\');
                                    break;
                                case '/':
                                    sb.Write('/');
                                    break;
                                case 'r':
                                    sb.Write('\r');
                                    break;
                                case 'n':
                                    sb.Write('\n');
                                    break;
                                case 't':
                                    sb.Write('\t');
                                    break;
                                case 'v':
                                    sb.Write('\v');
                                    break;
                                case 'b':
                                    sb.Write('\b');
                                    break;
                                case 'f':
                                    sb.Write('\f');
                                    break;
                                case 'u':
                                    sb.Write(GetUnicode());
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + ch);
                            }
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position];
                            if (ch == '\\')
                            {
                                _position++;
                            }
                            else
                            {
                                start = _position;
                                _position++;
                                break;
                            }
                        }
                        for (; ; )
                        {
                            if (ch == '\'')
                            {
                                sb.Write(_value.AsSpan(start, _position - start - 1));
                                _isGet = true;
                                return sb.ToString();
                            }
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            if (ch == '\\')
                            {
                                break;
                            }
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            private void GetStringQuote(out ReadOnlySpan<char> value)
            {
                Debug.Assert(_token == Token.StringQuote);
                Debug.Assert(!_isGet);
                var start = _position;
                char ch;
                for (; ; )
                {
                    if (_position == _value.Length)
                        throw new FormatException("EOF");
                    ch = _value[_position++];
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '\'')
                    {
                        _isGet = true;
                        value = _value.AsSpan(start, _position - start - 1);
                        return;
                    }
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        sb.Write(_value.AsSpan(start, _position - start - 1));
                        for (; ; )
                        {
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case '0':
                                    sb.Write('\u0000');
                                    break;
                                case '\'':
                                    sb.Write('\'');
                                    break;
                                case '"':
                                    sb.Write('"');
                                    break;
                                case '\\':
                                    sb.Write('\\');
                                    break;
                                case '/':
                                    sb.Write('/');
                                    break;
                                case 'r':
                                    sb.Write('\r');
                                    break;
                                case 'n':
                                    sb.Write('\n');
                                    break;
                                case 't':
                                    sb.Write('\t');
                                    break;
                                case 'v':
                                    sb.Write('\v');
                                    break;
                                case 'b':
                                    sb.Write('\b');
                                    break;
                                case 'f':
                                    sb.Write('\f');
                                    break;
                                case 'u':
                                    sb.Write(GetUnicode());
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + ch);
                            }
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position];
                            if (ch == '\\')
                            {
                                _position++;
                            }
                            else
                            {
                                start = _position;
                                _position++;
                                break;
                            }
                        }
                        for (; ; )
                        {
                            if (ch == '\'')
                            {
                                sb.Write(_value.AsSpan(start, _position - start - 1));
                                _isGet = true;
                                value = sb.ToString();
                                return;
                            }
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            if (ch == '\\')
                            {
                                break;
                            }
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            private void SkipStringDoubleQuote()
            {
                Debug.Assert(_token == Token.StringDoubleQuote);
                Debug.Assert(!_isGet);
                for (; ; )
                {
                    if (_position == _value.Length)
                        throw new FormatException("EOF");
                    var ch = _value[_position++];
                    if (ch == '\\')
                    {
                        if (_position == _value.Length)
                            throw new FormatException("EOF");
                        ch = _value[_position++];
                        switch (ch)
                        {
                            case '0':
                            case '"':
                            case '\\':
                            case '/':
                            case 'r':
                            case 'n':
                            case 't':
                            case 'v':
                            case 'b':
                            case 'f':
                                break;
                            case 'u':
                                _position += 4;
                                break;
                            default:
                                throw new NotSupportedException("\\" + ch);
                        }
                    }
                    else if (ch == '"')
                    {
                        _isGet = true;
                        return;
                    }
                }
            }
            private string GetStringDoubleQuote()
            {
                Debug.Assert(_token == Token.StringDoubleQuote);
                Debug.Assert(!_isGet);
                var start = _position;
                char ch;
                for (; ; )
                {
                    if (_position == _value.Length)
                        throw new FormatException("EOF");
                    ch = _value[_position++];
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '"')
                    {
                        _isGet = true;
                        return _value.Substring(start, _position - start - 1);
                    }
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        sb.Write(_value.AsSpan(start, _position - start - 1));
                        for (; ; )
                        {
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case '0':
                                    sb.Write('\u0000');
                                    break;
                                case '\'':
                                    sb.Write('\'');
                                    break;
                                case '"':
                                    sb.Write('"');
                                    break;
                                case '\\':
                                    sb.Write('\\');
                                    break;
                                case '/':
                                    sb.Write('/');
                                    break;
                                case 'r':
                                    sb.Write('\r');
                                    break;
                                case 'n':
                                    sb.Write('\n');
                                    break;
                                case 't':
                                    sb.Write('\t');
                                    break;
                                case 'v':
                                    sb.Write('\v');
                                    break;
                                case 'b':
                                    sb.Write('\b');
                                    break;
                                case 'f':
                                    sb.Write('\f');
                                    break;
                                case 'u':
                                    sb.Write(GetUnicode());
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + ch);
                            }
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position];
                            if (ch == '\\')
                            {
                                _position++;
                            }
                            else
                            {
                                start = _position;
                                _position++;
                                break;
                            }
                        }
                        for (; ; )
                        {
                            if (ch == '"')
                            {
                                sb.Write(_value.AsSpan(start, _position - start - 1));
                                _isGet = true;
                                return sb.ToString();
                            }
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            if (ch == '\\')
                            {
                                break;
                            }
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            private void GetStringDoubleQuote(out ReadOnlySpan<char> value)
            {
                Debug.Assert(_token == Token.StringDoubleQuote);
                Debug.Assert(!_isGet);
                var start = _position;
                char ch;
                for (; ; )
                {
                    if (_position == _value.Length)
                        throw new FormatException("EOF");
                    ch = _value[_position++];
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '"')
                    {
                        _isGet = true;
                        value = _value.AsSpan(start, _position - start - 1);
                        return;
                    }
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        sb.Write(_value.AsSpan(start, _position - start - 1));
                        for (; ; )
                        {
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case '0':
                                    sb.Write('\u0000');
                                    break;
                                case '\'':
                                    sb.Write('\'');
                                    break;
                                case '"':
                                    sb.Write('"');
                                    break;
                                case '\\':
                                    sb.Write('\\');
                                    break;
                                case '/':
                                    sb.Write('/');
                                    break;
                                case 'r':
                                    sb.Write('\r');
                                    break;
                                case 'n':
                                    sb.Write('\n');
                                    break;
                                case 't':
                                    sb.Write('\t');
                                    break;
                                case 'v':
                                    sb.Write('\v');
                                    break;
                                case 'b':
                                    sb.Write('\b');
                                    break;
                                case 'f':
                                    sb.Write('\f');
                                    break;
                                case 'u':
                                    sb.Write(GetUnicode());
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + ch);
                            }
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position];
                            if (ch == '\\')
                            {
                                _position++;
                            }
                            else
                            {
                                start = _position;
                                _position++;
                                break;
                            }
                        }
                        for (; ; )
                        {
                            if (ch == '"')
                            {
                                sb.Write(_value.AsSpan(start, _position - start - 1));
                                _isGet = true;
                                value = sb.ToString();
                                return;
                            }
                            if (_position == _value.Length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            if (ch == '\\')
                            {
                                break;
                            }
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override string GetString()
            {
                if (_token == Token.Null)
                    return null;
                if (_isGet)
                    throw new InvalidOperationException(nameof(GetString));

                switch (_token)
                {
                    case Token.StringQuote:
                        return GetStringQuote();
                    case Token.StringDoubleQuote:
                        return GetStringDoubleQuote();
                    default:
                        throw new InvalidOperationException(nameof(GetString));
                }
            }
            public override void GetString(out ReadOnlySpan<char> value)
            {
                if (_token == Token.Null)
                {
                    value = null;
                    return;
                }
                if (_isGet)
                    throw new InvalidOperationException(nameof(GetString));

                switch (_token)
                {
                    case Token.StringQuote:
                        GetStringQuote(out value);
                        break;
                    case Token.StringDoubleQuote:
                        GetStringDoubleQuote(out value);
                        break;
                    default:
                        throw new InvalidOperationException(nameof(GetString));
                }
            }
            public override byte GetByte()
            {
                GetNumber(out var number);
                return byte.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override sbyte GetSByte()
            {
                GetNumber(out var number);
                return sbyte.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override short GetInt16()
            {
                GetNumber(out var number);
                return short.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override ushort GetUInt16()
            {
                GetNumber(out var number);
                return ushort.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override int GetInt32()
            {
                GetNumber(out var number);
                return int.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override uint GetUInt32()
            {
                GetNumber(out var number);
                return uint.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override long GetInt64()
            {
                GetNumber(out var number);
                return long.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override ulong GetUInt64()
            {
                GetNumber(out var number);
                return ulong.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override float GetSingle()
            {
                GetNumber(out var number);
                return float.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override double GetDouble()
            {
                GetNumber(out var number);
                return double.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override decimal GetDecimal()
            {
                GetNumber(out var number);
                return decimal.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            private void SkipNumber()
            {
                Debug.Assert(!_isGet);
                _position++;
                for (; ; )
                {
                    if (_position == _value.Length)
                    {
                        _isGet = true;
                        return;
                    }
                    var ch = _value[_position];
                    switch (ch)
                    {
                        case ',':
                        case '}':
                        case ']':
                            _isGet = true;
                            return;
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                        case '\v':
                        case '\f':
                        case '\u0000':
                        case '\u0085':
                        case '\u2028':
                        case '\u2029':
                        case '\u00A0':
                        case '\uFEFF':
                            _isGet = true;
                            _position += 1;
                            return;
                        default:
                            _position += 1;
                            continue;
                    }
                }
            }
            public override void GetNumber(out ReadOnlySpan<char> value)
            {
                if (_token != Token.Number || _isGet)
                    throw new InvalidOperationException(nameof(GetNumber));

                var start = _position++;
                for (; ; )
                {
                    if (_position == _value.Length)
                    {
                        _isGet = true;
                        value = _value.AsSpan(start);
                        return;
                    }
                    var ch = _value[_position];
                    switch (ch)
                    {
                        case ',':
                        case '}':
                        case ']':
                            _isGet = true;
                            value = _value.AsSpan(start, _position - start);
                            return;
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                        case '\v':
                        case '\f':
                        case '\u0000':
                        case '\u0085':
                        case '\u2028':
                        case '\u2029':
                        case '\u00A0':
                        case '\uFEFF':
                            _isGet = true;
                            value = _value.AsSpan(start, _position - start);
                            _position += 1;
                            return;
                        default:
                            _position += 1;
                            continue;
                    }
                }
            }
            public override bool GetBoolean()
            {
                if (_token != Token.Boolean)
                    throw new InvalidOperationException(nameof(GetBoolean));

                return _boolean;
            }
            public override void Skip()
            {
                switch (_token)
                {
                    case Token.None:
                        if (Read())
                        {
                            Skip();
                        }
                        return;
                    case Token.StartArray:
                        for (; ; )
                        {
                            if (!Read())
                                throw new FormatException("EOF");

                            if (_token == Token.EndArray)
                                return;

                            Skip();
                        }
                    case Token.StartObject:
                        for (; ; )
                        {
                            if (!Read())
                                throw new FormatException("EOF");

                            if (_token == Token.EndObject)
                                return;

                            Skip();
                        }
                    case Token.PropertyQuote:
                    case Token.PropertyDoubleQuote:
                    case Token.PropertyWithoutQuote:
                        {
                            if (!Read())
                                throw new FormatException("EOF");

                            Skip();
                            return;
                        }
                    default:
                        return;
                }
            }
        }
        private class MemoryJson5Reader : JsonReader
        {
            private enum Token : byte
            {
                None,
                StartArray,
                EndArray,
                StartObject,
                EndObject,
                PropertyQuote,
                PropertyDoubleQuote,
                PropertyWithoutQuote,
                StringQuote,
                StringDoubleQuote,
                Number,
                Boolean,
                Null
            }
            private static int _MaxDepth = 64;
            #region private
            private int _length;
            private unsafe char* _value;
            private int _position;
            private int _depth = -1;
            private bool[] _isObject;
            private Token _token;
            private bool _isGet;
            private bool _boolean;
            #endregion
            public unsafe MemoryJson5Reader(char* value, int length)
            {
                _value = value;
                _length = length;
                _token = Token.None;
                _isObject = new bool[_MaxDepth];
            }
            public unsafe void Set(char* value, int length)
            {
                _value = value;
                _length = length;
            }
            public void Reset()
            {
                _token = Token.None;
                _length = 0;
                _position = 0;
                _depth = -1;
            }
            private char GetUnicode()
            {
                if (_position + 4 > _length)
                    throw new FormatException("\\uxxxx");

                unsafe
                {
                    var unicode = 0;
                    for (var i = 1; ;)
                    {
                        var ch = _value[_position++];
                        switch (ch)
                        {
                            case '0':
                                break;
                            case '1':
                                unicode += 1;
                                break;
                            case '2':
                                unicode += 2;
                                break;
                            case '3':
                                unicode += 3;
                                break;
                            case '4':
                                unicode += 4;
                                break;
                            case '5':
                                unicode += 5;
                                break;
                            case '6':
                                unicode += 6;
                                break;
                            case '7':
                                unicode += 7;
                                break;
                            case '8':
                                unicode += 8;
                                break;
                            case '9':
                                unicode += 9;
                                break;
                            case 'a':
                            case 'A':
                                unicode += 10;
                                break;
                            case 'b':
                            case 'B':
                                unicode += 11;
                                break;
                            case 'c':
                            case 'C':
                                unicode += 12;
                                break;
                            case 'd':
                            case 'D':
                                unicode += 13;
                                break;
                            case 'e':
                            case 'E':
                                unicode += 14;
                                break;
                            case 'f':
                            case 'F':
                                unicode += 15;
                                break;
                            default:
                                throw new FormatException("\\uxxxx");
                        }
                        if (i == 4)
                            return (char)unicode;
                        i += 1;
                        unicode <<= 4;
                    }
                }
            }
            public override bool Read()
            {
                #region function
                unsafe bool Next()
                {
                    if (_depth == -1)
                        return NextValue();

                    if (_isObject[_depth])
                    {
                        while (_position < _length)
                        {
                            var ch = _value[_position++];
                            switch (ch)
                            {
                                case ',':
                                    return NextProperty();
                                case '}':
                                    if (!_isObject[_depth--])
                                        throw new FormatException($"Unexpected character '{ch}'");
                                    _token = Token.EndObject;
                                    return true;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{ch}'");
                            }
                        }
                        return false;
                    }
                    else
                    {
                        while (_position < _length)
                        {
                            var ch = _value[_position++];
                            switch (ch)
                            {
                                case ',':
                                    return NextValue();
                                case ']':
                                    if (_isObject[_depth--])
                                        throw new FormatException($"Unexpected character '{ch}'");
                                    _token = Token.EndArray;
                                    return true;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                case '/':
                                    SkipComment();
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{ch}'");
                            }
                        }
                        return false;
                    }
                }
                unsafe bool NextProperty()
                {
                    while (_position < _length)
                    {
                        var ch = _value[_position];
                        switch (ch)
                        {
                            case '\'':
                                _position += 1;
                                _isGet = false;
                                _token = Token.PropertyQuote;
                                return true;
                            case '"':
                                _position += 1;
                                _isGet = false;
                                _token = Token.PropertyDoubleQuote;
                                return true;
                            case '}':
                                if (!_isObject[_depth--])
                                    throw new FormatException($"Unexpected character '{ch}'");
                                _position += 1;
                                _token = Token.EndObject;
                                return true;
                            case ' ':
                            case '\t':
                            case '\r':
                            case '\n':
                            case '\v':
                            case '\f':
                            case '\u0000':
                            case '\u0085':
                            case '\u2028':
                            case '\u2029':
                            case '\u00A0':
                            case '\uFEFF':
                                _position += 1;
                                continue;
                            case '/':
                                _position += 1;
                                SkipComment();
                                continue;
                            case '{':
                            case '[':
                            case ']':
                            case ',':
                            case ':':
                                throw new FormatException($"Unexpected character '{ch}'");
                            default:
                                _isGet = false;
                                _token = Token.PropertyWithoutQuote;
                                return true;
                        }
                    }
                    return false;
                }
                unsafe bool NextValue()
                {
                    while (_position < _length)
                    {
                        var ch = _value[_position];
                        switch (ch)
                        {
                            case '{':
                                _position += 1;
                                _depth += 1;
                                _isObject[_depth] = true;
                                _token = Token.StartObject;
                                return true;
                            case '[':
                                _position += 1;
                                _depth += 1;
                                _isObject[_depth] = false;
                                _token = Token.StartArray;
                                return true;
                            case '\'':
                                _position += 1;
                                _isGet = false;
                                _token = Token.StringQuote;
                                return true;
                            case '"':
                                _position += 1;
                                _isGet = false;
                                _token = Token.StringDoubleQuote;
                                return true;
                            case 'n':
                                if (_length - _position < 4 || _value[_position + 1] != 'u' ||
                                    _value[_position + 2] != 'l' || _value[_position + 3] != 'l')
                                    throw new FormatException("Unexpected character");
                                _position += 4;
                                _token = Token.Null;
                                return true;
                            case 't':
                                if (_length - _position < 4 || _value[_position + 1] != 'r' ||
                                     _value[_position + 2] != 'u' || _value[_position + 3] != 'e')
                                    throw new FormatException("Unexpected character");
                                _position += 4;
                                _boolean = true;
                                _token = Token.Boolean;
                                return true;
                            case 'f':
                                if (_length - _position < 5 || _value[_position + 1] != 'a' ||
                                    _value[_position + 2] != 'l' || _value[_position + 3] != 's' || _value[_position + 4] != 'e')
                                    throw new FormatException("Unexpected character");
                                _position += 5;
                                _boolean = false;
                                _token = Token.Boolean;
                                return true;
                            case '0':
                            case '1':
                            case '2':
                            case '3':
                            case '4':
                            case '5':
                            case '6':
                            case '7':
                            case '8':
                            case '9':
                            case '+':
                            case '-':
                            case '.':
                            case 'N':
                            case 'I':
                                _isGet = false;
                                _token = Token.Number;
                                return true;
                            case ' ':
                            case '\t':
                            case '\r':
                            case '\n':
                            case '\v':
                            case '\f':
                            case '\u0000':
                            case '\u0085':
                            case '\u2028':
                            case '\u2029':
                            case '\u00A0':
                            case '\uFEFF':
                                _position += 1;
                                continue;
                            case '/':
                                _position += 1;
                                SkipComment();
                                continue;
                            case ']':
                                if (_isObject[_depth--])
                                    throw new FormatException($"Unexpected character '{ch}'");
                                _position += 1;
                                _token = Token.EndArray;
                                return true;
                            default:
                                throw new FormatException($"Unexpected character '{ch}'");
                        }
                    }
                    return false;
                }
                #endregion
                unsafe
                {
                    switch (_token)
                    {
                        case Token.None:
                            return NextValue();
                        case Token.StartObject:
                            return NextProperty();
                        case Token.PropertyQuote:
                            if (!_isGet)
                                SkipPropertyQuote();
                            return NextValue();
                        case Token.PropertyDoubleQuote:
                            if (!_isGet)
                                SkipPropertyDoubleQuote();
                            return NextValue();
                        case Token.PropertyWithoutQuote:
                            if (!_isGet)
                                SkipPropertyWithoutQuote();
                            return NextValue();
                        case Token.StringQuote:
                            if (!_isGet)
                                SkipStringQuote();
                            return Next();
                        case Token.StringDoubleQuote:
                            if (!_isGet)
                                SkipStringDoubleQuote();
                            return Next();
                        case Token.Number:
                            if (!_isGet)
                                SkipNumber();
                            return Next();
                        case Token.Boolean:
                        case Token.Null:
                            return Next();
                        case Token.StartArray:
                            return NextValue();
                        case Token.EndObject:
                        case Token.EndArray:
                            return Next();
                        default:
                            return false;
                    }
                }
            }
            public override bool IsStartArray => _token == Token.StartArray;
            public override bool IsEndArray => _token == Token.EndArray;
            public override bool IsStartObject => _token == Token.StartObject;
            public override bool IsEndObject => _token == Token.EndObject;
            public override bool IsProperty => _token == Token.PropertyQuote || _token == Token.PropertyDoubleQuote || _token == Token.PropertyWithoutQuote;
            public override bool IsString => _token == Token.StringQuote || _token == Token.StringDoubleQuote;
            public override bool IsNumber => _token == Token.Number;
            public override bool IsBoolean => _token == Token.Boolean;
            public override bool IsNull => _token == Token.Null;
            private unsafe void SkipComment()
            {
                if (_position < _length)
                {
                    var ch = _value[_position++];
                    switch (ch)
                    {
                        case '*':
                            while (_position < _length)
                            {
                                ch = _value[_position++];
                                if (ch == '*')
                                {
                                    while (_position < _length)
                                    {
                                        ch = _value[_position++];
                                        switch (ch)
                                        {
                                            case '*':
                                                continue;
                                            case '/':
                                                return;
                                            default:
                                                break;
                                        }
                                        break;
                                    }
                                }
                            }
                            break;
                        case '/':
                            while (_position < _length)
                            {
                                ch = _value[_position++];
                                switch (ch)
                                {
                                    case '\r':
                                    case '\n':
                                    case '\u0085':
                                    case '\u2028':
                                    case '\u2029':
                                        return;
                                    default:
                                        continue;
                                }
                            }
                            return;
                        default:
                            throw new FormatException($"Unexpected character '{ch}'");
                    }
                }
                throw new FormatException("EOF");
            }
            private unsafe void SkipPropertyQuote()
            {
                Debug.Assert(_token == Token.PropertyQuote);
                Debug.Assert(!_isGet);
                for (; ; )
                {
                    if (_position == _length)
                        throw new FormatException("EOF");
                    var ch = _value[_position++];
                    if (ch == '\\')
                    {
                        if (_position == _length)
                            throw new FormatException("EOF");
                        ch = _value[_position++];
                        switch (ch)
                        {
                            case '0':
                            case '\'':
                            case '"':
                            case '\\':
                            case '/':
                            case 'r':
                            case 'n':
                            case 't':
                            case 'v':
                            case 'b':
                            case 'f':
                                break;
                            case 'u':
                                _position += 4;
                                break;
                            default:
                                throw new NotSupportedException("\\" + ch);
                        }
                    }
                    else if (ch == '\'')
                    {
                        for (; ; )
                        {
                            if (_position == _length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case ':':
                                    _isGet = true;
                                    return;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                case '/':
                                    SkipComment();
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{ch}'");
                            }
                        }
                    }
                }
            }
            private unsafe string GetPropertyQuote()
            {
                Debug.Assert(_token == Token.PropertyQuote);
                Debug.Assert(!_isGet);
                var start = _position;
                char ch;
                for (; ; )
                {
                    if (_position == _length)
                        throw new FormatException("EOF");
                    ch = _value[_position++];
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '\'')
                    {
                        var property = new string(_value, start, _position - start - 1);
                        for (; ; )
                        {
                            if (_position == _length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case ':':
                                    _isGet = true;
                                    return property;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                case '/':
                                    SkipComment();
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{ch}'");
                            }
                        }
                    }
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        sb.Write(new ReadOnlySpan<char>(_value + start, _position - start - 1));
                        for (; ; )
                        {
                            if (_position == _length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case '0':
                                    sb.Write('\u0000');
                                    break;
                                case '\'':
                                    sb.Write('\'');
                                    break;
                                case '"':
                                    sb.Write('"');
                                    break;
                                case '\\':
                                    sb.Write('\\');
                                    break;
                                case '/':
                                    sb.Write('/');
                                    break;
                                case 'r':
                                    sb.Write('\r');
                                    break;
                                case 'n':
                                    sb.Write('\n');
                                    break;
                                case 't':
                                    sb.Write('\t');
                                    break;
                                case 'v':
                                    sb.Write('\v');
                                    break;
                                case 'b':
                                    sb.Write('\b');
                                    break;
                                case 'f':
                                    sb.Write('\f');
                                    break;
                                case 'u':
                                    sb.Write(GetUnicode());
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + ch);
                            }
                            if (_position == _length)
                                throw new FormatException("EOF");
                            ch = _value[_position];
                            if (ch == '\\')
                            {
                                _position++;
                            }
                            else
                            {
                                start = _position;
                                _position++;
                                break;
                            }
                        }
                        for (; ; )
                        {
                            if (ch == '\'')
                            {
                                sb.Write(new ReadOnlySpan<char>(_value + start, _position - start - 1));
                                for (; ; )
                                {
                                    if (_position == _length)
                                        throw new FormatException("EOF");
                                    ch = _value[_position++];
                                    switch (ch)
                                    {
                                        case ':':
                                            _isGet = true;
                                            return sb.ToString();
                                        case ' ':
                                        case '\t':
                                        case '\r':
                                        case '\n':
                                        case '\v':
                                        case '\f':
                                        case '\u0000':
                                        case '\u0085':
                                        case '\u2028':
                                        case '\u2029':
                                        case '\u00A0':
                                        case '\uFEFF':
                                            continue;
                                        case '/':
                                            SkipComment();
                                            continue;
                                        default:
                                            throw new FormatException($"Unexpected character '{ch}'");
                                    }
                                }
                            }
                            if (_position == _length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            if (ch == '\\')
                            {
                                break;
                            }
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            private unsafe void GetPropertyQuote(out ReadOnlySpan<char> property)
            {
                Debug.Assert(_token == Token.PropertyQuote);
                Debug.Assert(!_isGet);
                var start = _position;
                char ch;
                for (; ; )
                {
                    if (_position == _length)
                        throw new FormatException("EOF");
                    ch = _value[_position++];
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '\'')
                    {
                        property = new ReadOnlySpan<char>(_value + start, _position - start - 1);
                        for (; ; )
                        {
                            if (_position == _length)
                                throw new FormatException("EOF");

                            ch = _value[_position++];
                            switch (ch)
                            {
                                case ':':
                                    _isGet = true;
                                    return;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                case '/':
                                    SkipComment();
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{ch}'");
                            }
                        }
                    }
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        sb.Write(new ReadOnlySpan<char>(_value + start, _position - start - 1));
                        for (; ; )
                        {
                            if (_position == _length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case '0':
                                    sb.Write('\u0000');
                                    break;
                                case '\'':
                                    sb.Write('\'');
                                    break;
                                case '"':
                                    sb.Write('"');
                                    break;
                                case '\\':
                                    sb.Write('\\');
                                    break;
                                case '/':
                                    sb.Write('/');
                                    break;
                                case 'r':
                                    sb.Write('\r');
                                    break;
                                case 'n':
                                    sb.Write('\n');
                                    break;
                                case 't':
                                    sb.Write('\t');
                                    break;
                                case 'v':
                                    sb.Write('\v');
                                    break;
                                case 'b':
                                    sb.Write('\b');
                                    break;
                                case 'f':
                                    sb.Write('\f');
                                    break;
                                case 'u':
                                    sb.Write(GetUnicode());
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + ch);
                            }
                            if (_position == _length)
                                throw new FormatException("EOF");
                            ch = _value[_position];
                            if (ch == '\\')
                            {
                                _position++;
                            }
                            else
                            {
                                start = _position;
                                _position++;
                                break;
                            }
                        }
                        for (; ; )
                        {
                            if (ch == '\'')
                            {
                                sb.Write(new ReadOnlySpan<char>(_value + start, _position - start - 1));
                                property = sb.ToString();
                                for (; ; )
                                {
                                    if (_position == _length)
                                        throw new FormatException("EOF");
                                    ch = _value[_position++];
                                    switch (ch)
                                    {
                                        case ':':
                                            _isGet = true;
                                            return;
                                        case ' ':
                                        case '\t':
                                        case '\r':
                                        case '\n':
                                        case '\v':
                                        case '\f':
                                        case '\u0000':
                                        case '\u0085':
                                        case '\u2028':
                                        case '\u2029':
                                        case '\u00A0':
                                        case '\uFEFF':
                                            continue;
                                        case '/':
                                            SkipComment();
                                            continue;
                                        default:
                                            throw new FormatException($"Unexpected character '{ch}'");
                                    }
                                }
                            }
                            if (_position == _length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            if (ch == '\\')
                            {
                                break;
                            }
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            private unsafe void SkipPropertyDoubleQuote()
            {
                Debug.Assert(_token == Token.PropertyDoubleQuote);
                Debug.Assert(!_isGet);
                for (; ; )
                {
                    if (_position == _length)
                        throw new FormatException("EOF");
                    var ch = _value[_position++];
                    if (ch == '\\')
                    {
                        if (_position == _length)
                            throw new FormatException("EOF");
                        ch = _value[_position++];
                        switch (ch)
                        {
                            case '0':
                            case '\'':
                            case '"':
                            case '\\':
                            case '/':
                            case 'r':
                            case 'n':
                            case 't':
                            case 'v':
                            case 'b':
                            case 'f':
                                break;
                            case 'u':
                                _position += 4;
                                break;
                            default:
                                throw new NotSupportedException("\\" + ch);
                        }
                    }
                    else if (ch == '"')
                    {
                        for (; ; )
                        {
                            if (_position == _length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case ':':
                                    _isGet = true;
                                    return;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                case '/':
                                    SkipComment();
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{ch}'");
                            }
                        }
                    }
                }
            }
            private unsafe string GetPropertyDoubleQuote()
            {
                Debug.Assert(_token == Token.PropertyDoubleQuote);
                Debug.Assert(!_isGet);
                var start = _position;
                char ch;
                for (; ; )
                {
                    if (_position == _length)
                        throw new FormatException("EOF");
                    ch = _value[_position++];
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '"')
                    {
                        var property = new string(_value, start, _position - start - 1);
                        for (; ; )
                        {
                            if (_position == _length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case ':':
                                    _isGet = true;
                                    return property;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                case '/':
                                    SkipComment();
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{ch}'");
                            }
                        }
                    }
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        sb.Write(new ReadOnlySpan<char>(_value + start, _position - start - 1));
                        for (; ; )
                        {
                            if (_position == _length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case '0':
                                    sb.Write('\u0000');
                                    break;
                                case '\'':
                                    sb.Write('\'');
                                    break;
                                case '"':
                                    sb.Write('"');
                                    break;
                                case '\\':
                                    sb.Write('\\');
                                    break;
                                case '/':
                                    sb.Write('/');
                                    break;
                                case 'r':
                                    sb.Write('\r');
                                    break;
                                case 'n':
                                    sb.Write('\n');
                                    break;
                                case 't':
                                    sb.Write('\t');
                                    break;
                                case 'v':
                                    sb.Write('\v');
                                    break;
                                case 'b':
                                    sb.Write('\b');
                                    break;
                                case 'f':
                                    sb.Write('\f');
                                    break;
                                case 'u':
                                    sb.Write(GetUnicode());
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + ch);
                            }
                            if (_position == _length)
                                throw new FormatException("EOF");
                            ch = _value[_position];
                            if (ch == '\\')
                            {
                                _position++;
                            }
                            else
                            {
                                start = _position;
                                _position++;
                                break;
                            }
                        }
                        for (; ; )
                        {
                            if (ch == '"')
                            {
                                sb.Write(new ReadOnlySpan<char>(_value + start, _position - start - 1));
                                for (; ; )
                                {
                                    if (_position == _length)
                                        throw new FormatException("EOF");
                                    ch = _value[_position++];
                                    switch (ch)
                                    {
                                        case ':':
                                            _isGet = true;
                                            return sb.ToString();
                                        case ' ':
                                        case '\t':
                                        case '\r':
                                        case '\n':
                                        case '\v':
                                        case '\f':
                                        case '\u0000':
                                        case '\u0085':
                                        case '\u2028':
                                        case '\u2029':
                                        case '\u00A0':
                                        case '\uFEFF':
                                            continue;
                                        case '/':
                                            SkipComment();
                                            continue;
                                        default:
                                            throw new FormatException($"Unexpected character '{ch}'");
                                    }
                                }
                            }
                            if (_position == _length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            if (ch == '\\')
                            {
                                break;
                            }
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            private unsafe void GetPropertyDoubleQuote(out ReadOnlySpan<char> property)
            {
                Debug.Assert(_token == Token.PropertyDoubleQuote);
                Debug.Assert(!_isGet);
                var start = _position;
                char ch;
                for (; ; )
                {
                    if (_position == _length)
                        throw new FormatException("EOF");
                    ch = _value[_position++];
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '"')
                    {
                        property = new ReadOnlySpan<char>(_value + start, _position - start - 1);
                        for (; ; )
                        {
                            if (_position == _length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case ':':
                                    _isGet = true;
                                    return;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                case '/':
                                    SkipComment();
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{ch}'");
                            }
                        }
                    }
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        sb.Write(new ReadOnlySpan<char>(_value + start, _position - start - 1));
                        for (; ; )
                        {
                            if (_position == _length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case '0':
                                    sb.Write('\u0000');
                                    break;
                                case '\'':
                                    sb.Write('\'');
                                    break;
                                case '"':
                                    sb.Write('"');
                                    break;
                                case '\\':
                                    sb.Write('\\');
                                    break;
                                case '/':
                                    sb.Write('/');
                                    break;
                                case 'r':
                                    sb.Write('\r');
                                    break;
                                case 'n':
                                    sb.Write('\n');
                                    break;
                                case 't':
                                    sb.Write('\t');
                                    break;
                                case 'v':
                                    sb.Write('\v');
                                    break;
                                case 'b':
                                    sb.Write('\b');
                                    break;
                                case 'f':
                                    sb.Write('\f');
                                    break;
                                case 'u':
                                    sb.Write(GetUnicode());
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + ch);
                            }
                            if (_position == _length)
                                throw new FormatException("EOF");
                            ch = _value[_position];
                            if (ch == '\\')
                            {
                                _position++;
                            }
                            else
                            {
                                start = _position;
                                _position++;
                                break;
                            }
                        }
                        for (; ; )
                        {
                            if (ch == '"')
                            {
                                sb.Write(new ReadOnlySpan<char>(_value + start, _position - start - 1));
                                property = sb.ToString();
                                for (; ; )
                                {
                                    if (_position == _length)
                                        throw new FormatException("EOF");
                                    ch = _value[_position++];
                                    switch (ch)
                                    {
                                        case ':':
                                            _isGet = true;
                                            return;
                                        case ' ':
                                        case '\t':
                                        case '\r':
                                        case '\n':
                                        case '\v':
                                        case '\f':
                                        case '\u0000':
                                        case '\u0085':
                                        case '\u2028':
                                        case '\u2029':
                                        case '\u00A0':
                                        case '\uFEFF':
                                            continue;
                                        case '/':
                                            SkipComment();
                                            continue;
                                        default:
                                            throw new FormatException($"Unexpected character '{ch}'");
                                    }
                                }
                            }
                            if (_position == _length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            if (ch == '\\')
                            {
                                break;
                            }
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            private unsafe void SkipPropertyWithoutQuote()
            {
                Debug.Assert(_token == Token.PropertyWithoutQuote);
                Debug.Assert(!_isGet);
                _position += 1;
                for (; ; )
                {
                    if (_position == _length)
                        throw new FormatException("EOF");
                    var ch = _value[_position++];
                    switch (ch)
                    {
                        case ':':
                            _isGet = true;
                            return;
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                        case '\v':
                        case '\f':
                        case '\u0000':
                        case '\u0085':
                        case '\u2028':
                        case '\u2029':
                        case '\u00A0':
                        case '\uFEFF':
                            for (; ; )
                            {
                                if (_position == _length)
                                    throw new FormatException("EOF");
                                ch = _value[_position++];
                                switch (ch)
                                {
                                    case ':':
                                        _isGet = true;
                                        return;
                                    case ' ':
                                    case '\t':
                                    case '\r':
                                    case '\n':
                                    case '\v':
                                    case '\f':
                                    case '\u0000':
                                    case '\u0085':
                                    case '\u2028':
                                    case '\u2029':
                                    case '\u00A0':
                                    case '\uFEFF':
                                        continue;
                                    case '/':
                                        SkipComment();
                                        continue;
                                    default:
                                        throw new FormatException($"Unexpected character '{ch}'");
                                }
                            }
                        default:
                            break;
                    }
                }
            }
            private unsafe string GetPropertyWithoutQuote()
            {
                Debug.Assert(_token == Token.PropertyWithoutQuote);
                Debug.Assert(!_isGet);
                var start = _position++;
                for (; ; )
                {
                    if (_position == _length)
                        throw new FormatException("EOF");
                    var ch = _value[_position++];
                    switch (ch)
                    {
                        case ':':
                            {
                                var property = new string(_value, start, _position - start - 1);
                                _isGet = true;
                                return property;
                            }
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                        case '\v':
                        case '\f':
                        case '\u0000':
                        case '\u0085':
                        case '\u2028':
                        case '\u2029':
                        case '\u00A0':
                        case '\uFEFF':
                            {
                                var property = new string(_value, start, _position - start - 1);
                                for (; ; )
                                {
                                    if (_position == _length)
                                        throw new FormatException("EOF");
                                    ch = _value[_position++];
                                    switch (ch)
                                    {
                                        case ':':
                                            _isGet = true;
                                            return property;
                                        case ' ':
                                        case '\t':
                                        case '\r':
                                        case '\n':
                                        case '\v':
                                        case '\f':
                                        case '\u0000':
                                        case '\u0085':
                                        case '\u2028':
                                        case '\u2029':
                                        case '\u00A0':
                                        case '\uFEFF':
                                            continue;
                                        case '/':
                                            SkipComment();
                                            continue;
                                        default:
                                            throw new FormatException($"Unexpected character '{ch}'");
                                    }
                                }
                            }
                        default:
                            break;
                    }
                }
            }
            private unsafe void GetPropertyWithoutQuote(out ReadOnlySpan<char> property)
            {
                Debug.Assert(_token == Token.PropertyWithoutQuote);
                Debug.Assert(!_isGet);
                var start = _position++;
                for (; ; )
                {
                    if (_position == _length)
                        throw new FormatException("EOF");
                    var ch = _value[_position++];
                    switch (ch)
                    {
                        case ':':
                            {
                                property = new ReadOnlySpan<char>(_value + start, _position - start - 1);
                                _isGet = true;
                                return;
                            }
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                        case '\v':
                        case '\f':
                        case '\u0000':
                        case '\u0085':
                        case '\u2028':
                        case '\u2029':
                        case '\u00A0':
                        case '\uFEFF':
                            {
                                property = new ReadOnlySpan<char>(_value + start, _position - start - 1);
                                for (; ; )
                                {
                                    if (_position == _length)
                                        throw new FormatException("EOF");
                                    ch = _value[_position++];
                                    switch (ch)
                                    {
                                        case ':':
                                            _isGet = true;
                                            return;
                                        case ' ':
                                        case '\t':
                                        case '\r':
                                        case '\n':
                                        case '\v':
                                        case '\f':
                                        case '\u0000':
                                        case '\u0085':
                                        case '\u2028':
                                        case '\u2029':
                                        case '\u00A0':
                                        case '\uFEFF':
                                            continue;
                                        case '/':
                                            SkipComment();
                                            continue;
                                        default:
                                            throw new FormatException($"Unexpected character '{ch}'");
                                    }
                                }
                            }
                        default:
                            break;
                    }
                }
            }
            public override string GetProperty()
            {
                if (_isGet)
                    throw new InvalidOperationException(nameof(GetProperty));

                switch (_token)
                {
                    case Token.PropertyQuote:
                        return GetPropertyQuote();
                    case Token.PropertyDoubleQuote:
                        return GetPropertyDoubleQuote();
                    case Token.PropertyWithoutQuote:
                        return GetPropertyWithoutQuote();
                    default:
                        throw new InvalidOperationException(nameof(GetProperty));
                }
            }
            public override void GetProperty(out ReadOnlySpan<char> property)
            {
                if (_isGet)
                    throw new InvalidOperationException(nameof(GetProperty));

                switch (_token)
                {
                    case Token.PropertyQuote:
                        GetPropertyQuote(out property);
                        break;
                    case Token.PropertyDoubleQuote:
                        GetPropertyDoubleQuote(out property);
                        break;
                    case Token.PropertyWithoutQuote:
                        GetPropertyWithoutQuote(out property);
                        break;
                    default:
                        throw new InvalidOperationException(nameof(GetProperty));
                }
            }
            private unsafe void SkipStringQuote()
            {
                Debug.Assert(_token == Token.StringQuote);
                Debug.Assert(!_isGet);
                for (; ; )
                {
                    if (_position == _length)
                        throw new FormatException("EOF");
                    var ch = _value[_position++];
                    if (ch == '\\')
                    {
                        if (_position == _length)
                            throw new FormatException("EOF");
                        ch = _value[_position++];
                        switch (ch)
                        {
                            case '0':
                            case '\'':
                            case '"':
                            case '\\':
                            case '/':
                            case 'r':
                            case 'n':
                            case 't':
                            case 'v':
                            case 'b':
                            case 'f':
                                break;
                            case 'u':
                                _position += 4;
                                break;
                            default:
                                throw new NotSupportedException("\\" + ch);
                        }
                    }
                    else if (ch == '\'')
                    {
                        _isGet = true;
                        return;
                    }
                }
            }
            private unsafe string GetStringQuote()
            {
                Debug.Assert(_token == Token.StringQuote);
                Debug.Assert(!_isGet);
                var start = _position;
                char ch;
                for (; ; )
                {
                    if (_position == _length)
                        throw new FormatException("EOF");
                    ch = _value[_position++];
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '\'')
                    {
                        _isGet = true;
                        return new string(_value, start, _position - start - 1);
                    }
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        sb.Write(new ReadOnlySpan<char>(_value + start, _position - start - 1));
                        for (; ; )
                        {
                            if (_position == _length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case '0':
                                    sb.Write('\u0000');
                                    break;
                                case '\'':
                                    sb.Write('\'');
                                    break;
                                case '"':
                                    sb.Write('"');
                                    break;
                                case '\\':
                                    sb.Write('\\');
                                    break;
                                case '/':
                                    sb.Write('/');
                                    break;
                                case 'r':
                                    sb.Write('\r');
                                    break;
                                case 'n':
                                    sb.Write('\n');
                                    break;
                                case 't':
                                    sb.Write('\t');
                                    break;
                                case 'v':
                                    sb.Write('\v');
                                    break;
                                case 'b':
                                    sb.Write('\b');
                                    break;
                                case 'f':
                                    sb.Write('\f');
                                    break;
                                case 'u':
                                    sb.Write(GetUnicode());
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + ch);
                            }
                            if (_position == _length)
                                throw new FormatException("EOF");
                            ch = _value[_position];
                            if (ch == '\\')
                            {
                                _position++;
                            }
                            else
                            {
                                start = _position;
                                _position++;
                                break;
                            }
                        }
                        for (; ; )
                        {
                            if (ch == '\'')
                            {
                                sb.Write(new ReadOnlySpan<char>(_value + start, _position - start - 1));
                                _isGet = true;
                                return sb.ToString();
                            }
                            if (_position == _length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            if (ch == '\\')
                            {
                                break;
                            }
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            private unsafe void GetStringQuote(out ReadOnlySpan<char> value)
            {
                Debug.Assert(_token == Token.StringQuote);
                Debug.Assert(!_isGet);
                var start = _position;
                char ch;
                for (; ; )
                {
                    if (_position == _length)
                        throw new FormatException("EOF");
                    ch = _value[_position++];
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '\'')
                    {
                        _isGet = true;
                        value = new ReadOnlySpan<char>(_value + start, _position - start - 1);
                        return;
                    }
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        sb.Write(new ReadOnlySpan<char>(_value + start, _position - start - 1));
                        for (; ; )
                        {
                            if (_position == _length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case '0':
                                    sb.Write('\u0000');
                                    break;
                                case '\'':
                                    sb.Write('\'');
                                    break;
                                case '"':
                                    sb.Write('"');
                                    break;
                                case '\\':
                                    sb.Write('\\');
                                    break;
                                case '/':
                                    sb.Write('/');
                                    break;
                                case 'r':
                                    sb.Write('\r');
                                    break;
                                case 'n':
                                    sb.Write('\n');
                                    break;
                                case 't':
                                    sb.Write('\t');
                                    break;
                                case 'v':
                                    sb.Write('\v');
                                    break;
                                case 'b':
                                    sb.Write('\b');
                                    break;
                                case 'f':
                                    sb.Write('\f');
                                    break;
                                case 'u':
                                    sb.Write(GetUnicode());
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + ch);
                            }
                            if (_position == _length)
                                throw new FormatException("EOF");
                            ch = _value[_position];
                            if (ch == '\\')
                            {
                                _position++;
                            }
                            else
                            {
                                start = _position;
                                _position++;
                                break;
                            }
                        }
                        for (; ; )
                        {
                            if (ch == '\'')
                            {
                                sb.Write(new ReadOnlySpan<char>(_value + start, _position - start - 1));
                                _isGet = true;
                                value = sb.ToString();
                                return;
                            }
                            if (_position == _length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            if (ch == '\\')
                            {
                                break;
                            }
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            private unsafe void SkipStringDoubleQuote()
            {
                Debug.Assert(_token == Token.StringDoubleQuote);
                Debug.Assert(!_isGet);
                for (; ; )
                {
                    if (_position == _length)
                        throw new FormatException("EOF");
                    var ch = _value[_position++];
                    if (ch == '\\')
                    {
                        if (_position == _length)
                            throw new FormatException("EOF");
                        ch = _value[_position++];
                        switch (ch)
                        {
                            case '0':
                            case '"':
                            case '\\':
                            case '/':
                            case 'r':
                            case 'n':
                            case 't':
                            case 'v':
                            case 'b':
                            case 'f':
                                break;
                            case 'u':
                                _position += 4;
                                break;
                            default:
                                throw new NotSupportedException("\\" + ch);
                        }
                    }
                    else if (ch == '"')
                    {
                        _isGet = true;
                        return;
                    }
                }
            }
            private unsafe string GetStringDoubleQuote()
            {
                Debug.Assert(_token == Token.StringDoubleQuote);
                Debug.Assert(!_isGet);
                var start = _position;
                char ch;
                for (; ; )
                {
                    if (_position == _length)
                        throw new FormatException("EOF");
                    ch = _value[_position++];
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '"')
                    {
                        _isGet = true;
                        return new string(_value, start, _position - start - 1);
                    }
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        sb.Write(new ReadOnlySpan<char>(_value + start, _position - start - 1));
                        for (; ; )
                        {
                            if (_position == _length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case '0':
                                    sb.Write('\u0000');
                                    break;
                                case '\'':
                                    sb.Write('\'');
                                    break;
                                case '"':
                                    sb.Write('"');
                                    break;
                                case '\\':
                                    sb.Write('\\');
                                    break;
                                case '/':
                                    sb.Write('/');
                                    break;
                                case 'r':
                                    sb.Write('\r');
                                    break;
                                case 'n':
                                    sb.Write('\n');
                                    break;
                                case 't':
                                    sb.Write('\t');
                                    break;
                                case 'v':
                                    sb.Write('\v');
                                    break;
                                case 'b':
                                    sb.Write('\b');
                                    break;
                                case 'f':
                                    sb.Write('\f');
                                    break;
                                case 'u':
                                    sb.Write(GetUnicode());
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + ch);
                            }
                            if (_position == _length)
                                throw new FormatException("EOF");
                            ch = _value[_position];
                            if (ch == '\\')
                            {
                                _position++;
                            }
                            else
                            {
                                start = _position;
                                _position++;
                                break;
                            }
                        }
                        for (; ; )
                        {
                            if (ch == '"')
                            {
                                sb.Write(new ReadOnlySpan<char>(_value + start, _position - start - 1));
                                _isGet = true;
                                return sb.ToString();
                            }
                            if (_position == _length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            if (ch == '\\')
                            {
                                break;
                            }
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            private unsafe void GetStringDoubleQuote(out ReadOnlySpan<char> value)
            {
                Debug.Assert(_token == Token.StringDoubleQuote);
                Debug.Assert(!_isGet);
                var start = _position;
                char ch;
                for (; ; )
                {
                    if (_position == _length)
                        throw new FormatException("EOF");
                    ch = _value[_position++];
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '"')
                    {
                        _isGet = true;
                        value = new ReadOnlySpan<char>(_value + start, _position - start - 1);
                        return;
                    }
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        sb.Write(new ReadOnlySpan<char>(_value + start, _position - start - 1));
                        for (; ; )
                        {
                            if (_position == _length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            switch (ch)
                            {
                                case '0':
                                    sb.Write('\u0000');
                                    break;
                                case '\'':
                                    sb.Write('\'');
                                    break;
                                case '"':
                                    sb.Write('"');
                                    break;
                                case '\\':
                                    sb.Write('\\');
                                    break;
                                case '/':
                                    sb.Write('/');
                                    break;
                                case 'r':
                                    sb.Write('\r');
                                    break;
                                case 'n':
                                    sb.Write('\n');
                                    break;
                                case 't':
                                    sb.Write('\t');
                                    break;
                                case 'v':
                                    sb.Write('\v');
                                    break;
                                case 'b':
                                    sb.Write('\b');
                                    break;
                                case 'f':
                                    sb.Write('\f');
                                    break;
                                case 'u':
                                    sb.Write(GetUnicode());
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + ch);
                            }
                            if (_position == _length)
                                throw new FormatException("EOF");
                            ch = _value[_position];
                            if (ch == '\\')
                            {
                                _position++;
                            }
                            else
                            {
                                start = _position;
                                _position++;
                                break;
                            }
                        }
                        for (; ; )
                        {
                            if (ch == '"')
                            {
                                sb.Write(new ReadOnlySpan<char>(_value + start, _position - start - 1));
                                _isGet = true;
                                value = sb.ToString();
                                return;
                            }
                            if (_position == _length)
                                throw new FormatException("EOF");
                            ch = _value[_position++];
                            if (ch == '\\')
                            {
                                break;
                            }
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override string GetString()
            {
                if (_token == Token.Null)
                    return null;
                if (_isGet)
                    throw new InvalidOperationException(nameof(GetString));

                switch (_token)
                {
                    case Token.StringQuote:
                        return GetStringQuote();
                    case Token.StringDoubleQuote:
                        return GetStringDoubleQuote();
                    default:
                        throw new InvalidOperationException(nameof(GetString));
                }
            }
            public override void GetString(out ReadOnlySpan<char> value)
            {
                if (_token == Token.Null)
                {
                    value = null;
                    return;
                }
                if (_isGet)
                    throw new InvalidOperationException(nameof(GetString));

                switch (_token)
                {
                    case Token.StringQuote:
                        GetStringQuote(out value);
                        break;
                    case Token.StringDoubleQuote:
                        GetStringDoubleQuote(out value);
                        break;
                    default:
                        throw new InvalidOperationException(nameof(GetString));
                }
            }
            public override byte GetByte()
            {
                GetNumber(out var number);
                return byte.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override sbyte GetSByte()
            {
                GetNumber(out var number);
                return sbyte.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override short GetInt16()
            {
                GetNumber(out var number);
                return short.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override ushort GetUInt16()
            {
                GetNumber(out var number);
                return ushort.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override int GetInt32()
            {
                GetNumber(out var number);
                return int.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override uint GetUInt32()
            {
                GetNumber(out var number);
                return uint.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override long GetInt64()
            {
                GetNumber(out var number);
                return long.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override ulong GetUInt64()
            {
                GetNumber(out var number);
                return ulong.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override float GetSingle()
            {
                GetNumber(out var number);
                return float.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override double GetDouble()
            {
                GetNumber(out var number);
                return double.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override decimal GetDecimal()
            {
                GetNumber(out var number);
                return decimal.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            private unsafe void SkipNumber()
            {
                Debug.Assert(!_isGet);
                _position++;
                for (; ; )
                {
                    if (_position == _length)
                    {
                        _isGet = true;
                        return;
                    }
                    var ch = _value[_position];
                    switch (ch)
                    {
                        case ',':
                        case '}':
                        case ']':
                            _isGet = true;
                            return;
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                        case '\v':
                        case '\f':
                        case '\u0000':
                        case '\u0085':
                        case '\u2028':
                        case '\u2029':
                        case '\u00A0':
                        case '\uFEFF':
                            _isGet = true;
                            _position += 1;
                            return;
                        default:
                            _position += 1;
                            continue;
                    }
                }
            }
            public override void GetNumber(out ReadOnlySpan<char> value)
            {
                if (_token != Token.Number || _isGet)
                    throw new InvalidOperationException(nameof(GetNumber));

                unsafe
                {
                    var start = _position++;
                    for (; ; )
                    {
                        if (_position == _length)
                        {
                            _isGet = true;
                            value = new ReadOnlySpan<char>(_value + start, _length - start);
                            return;
                        }
                        var ch = _value[_position];
                        switch (ch)
                        {
                            case ',':
                            case '}':
                            case ']':
                                _isGet = true;
                                value = new ReadOnlySpan<char>(_value + start, _position - start);
                                return;
                            case ' ':
                            case '\t':
                            case '\r':
                            case '\n':
                            case '\v':
                            case '\f':
                            case '\u0000':
                            case '\u0085':
                            case '\u2028':
                            case '\u2029':
                            case '\u00A0':
                            case '\uFEFF':
                                _isGet = true;
                                value = new ReadOnlySpan<char>(_value + start, _position - start);
                                _position += 1;
                                return;
                            default:
                                _position += 1;
                                continue;
                        }
                    }
                }
            }
            public override bool GetBoolean()
            {
                if (_token != Token.Boolean)
                    throw new InvalidOperationException(nameof(GetBoolean));

                return _boolean;
            }
            public override void Skip()
            {
                switch (_token)
                {
                    case Token.None:
                        if (Read())
                        {
                            Skip();
                        }
                        return;
                    case Token.StartArray:
                        for (; ; )
                        {
                            if (!Read())
                                throw new FormatException("EOF");

                            if (_token == Token.EndArray)
                                return;

                            Skip();
                        }
                    case Token.StartObject:
                        for (; ; )
                        {
                            if (!Read())
                                throw new FormatException("EOF");

                            if (_token == Token.EndObject)
                                return;

                            Skip();
                        }
                    case Token.PropertyQuote:
                    case Token.PropertyDoubleQuote:
                    case Token.PropertyWithoutQuote:
                        {
                            if (!Read())
                                throw new FormatException("EOF");

                            Skip();
                            return;
                        }
                    default:
                        return;
                }
            }
        }
        private class SequenceJson5Reader : JsonReader, IDisposable
        {
            private enum Token : byte
            {
                None,
                StartArray,
                EndArray,
                StartObject,
                EndObject,
                PropertyQuote,
                PropertyDoubleQuote,
                PropertyWithoutQuote,
                StringQuote,
                StringDoubleQuote,
                Number,
                Boolean,
                Null
            }
            private static int _MaxDepth = 64;
            #region private
            private ReadOnlySequence<char> _value;
            private SequencePosition _next;
            private int _position;//-1=end
            private int _length;
            private unsafe char* _segment;
            private MemoryHandle _memoryHandle;
            private int _depth = -1;
            private bool[] _isObject;
            private Token _token;
            private bool _isGet;
            private bool _boolean;
            #endregion
            public SequenceJson5Reader(ReadOnlySequence<char> value)
            {
                _value = value;
                _next = _value.Start;
                _token = Token.None;
                _isObject = new bool[_MaxDepth];
                if (_value.TryGet(ref _next, out var segment))
                {
                    _length = segment.Length;
                    _memoryHandle = segment.Pin();
                    unsafe { _segment = (char*)_memoryHandle.Pointer; }
                }
                else
                {
                    _position = -1;
                }
            }
            public void Set(ReadOnlySequence<char> value)
            {
                _value = value;
                _next = _value.Start;
                if (_value.TryGet(ref _next, out var segment))
                {
                    _length = segment.Length;
                    _memoryHandle = segment.Pin();
                    unsafe { _segment = (char*)_memoryHandle.Pointer; }
                }
                else
                {
                    _position = -1;
                }
            }
            public void Reset()
            {
                _token = Token.None;
                _value = default;
                _next = default;
                _length = 0;
                _position = 0;
                _depth = -1;
            }
            private int ReadChar()
            {
                if (_position == -1)
                    return -1;

                if (_position == _length)
                {
                    _memoryHandle.Dispose();
                    if (_value.TryGet(ref _next, out var segment))
                    {
                        _length = segment.Length;
                        _memoryHandle = segment.Pin();
                        unsafe { _segment = (char*)_memoryHandle.Pointer; }
                        _position = 0;
                    }
                    else
                    {
                        _position = -1;
                        return -1;
                    }
                }
                unsafe { return _segment[_position++]; }
            }
            private int PeekChar()
            {
                if (_position == -1)
                    return -1;

                if (_position == _length)
                {
                    _memoryHandle.Dispose();
                    if (_value.TryGet(ref _next, out var segment))
                    {
                        _length = segment.Length;
                        _memoryHandle = segment.Pin();
                        unsafe { _segment = (char*)_memoryHandle.Pointer; }
                        _position = 0;
                    }
                    else
                    {
                        _position = -1;
                        return -1;
                    }
                }
                unsafe { return _segment[_position]; }
            }
            private char GetUnicode()
            {
                var unicode = 0;
                for (var i = 1; ;)
                {
                    var ch = ReadChar();
                    switch (ch)
                    {
                        case -1:
                            throw new FormatException("\\uxxxx");
                        case '0':
                            break;
                        case '1':
                            unicode += 1;
                            break;
                        case '2':
                            unicode += 2;
                            break;
                        case '3':
                            unicode += 3;
                            break;
                        case '4':
                            unicode += 4;
                            break;
                        case '5':
                            unicode += 5;
                            break;
                        case '6':
                            unicode += 6;
                            break;
                        case '7':
                            unicode += 7;
                            break;
                        case '8':
                            unicode += 8;
                            break;
                        case '9':
                            unicode += 9;
                            break;
                        case 'a':
                        case 'A':
                            unicode += 10;
                            break;
                        case 'b':
                        case 'B':
                            unicode += 11;
                            break;
                        case 'c':
                        case 'C':
                            unicode += 12;
                            break;
                        case 'd':
                        case 'D':
                            unicode += 13;
                            break;
                        case 'e':
                        case 'E':
                            unicode += 14;
                            break;
                        case 'f':
                        case 'F':
                            unicode += 15;
                            break;
                        default:
                            throw new FormatException("\\uxxxx");
                    }
                    if (i == 4)
                        return (char)unicode;
                    i += 1;
                    unicode <<= 4;
                }
            }
            public override bool Read()
            {
                #region function
                bool Next()
                {
                    if (_depth == -1)
                        return NextValue();

                    if (_isObject[_depth])
                    {
                        for (; ; )
                        {
                            var ch = ReadChar();
                            switch (ch)
                            {
                                case -1:
                                    return false;
                                case ',':
                                    return NextProperty();
                                case '}':
                                    if (!_isObject[_depth--])
                                        throw new FormatException($"Unexpected character '{(char)ch}'");
                                    _token = Token.EndObject;
                                    return true;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                case '/':
                                    SkipComment();
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{(char)ch}'");
                            }
                        }
                    }
                    else
                    {
                        for (; ; )
                        {
                            var ch = ReadChar();
                            switch (ch)
                            {
                                case -1:
                                    return false;
                                case ',':
                                    return NextValue();
                                case ']':
                                    if (_isObject[_depth--])
                                        throw new FormatException($"Unexpected character '{(char)ch}'");
                                    _token = Token.EndArray;
                                    return true;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                case '/':
                                    SkipComment();
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{(char)ch}'");
                            }
                        }
                    }
                }
                bool NextProperty()
                {
                    for (; ; )
                    {
                        var ch = PeekChar();
                        switch (ch)
                        {
                            case -1:
                                return false;
                            case '\'':
                                _position += 1;
                                _isGet = false;
                                _token = Token.PropertyQuote;
                                return true;
                            case '"':
                                _position += 1;
                                _isGet = false;
                                _token = Token.PropertyDoubleQuote;
                                return true;
                            case '}':
                                if (!_isObject[_depth--])
                                    throw new FormatException($"Unexpected character '{(char)ch}'");
                                _position += 1;
                                _token = Token.EndObject;
                                return true;
                            case ' ':
                            case '\t':
                            case '\r':
                            case '\n':
                            case '\v':
                            case '\f':
                            case '\u0000':
                            case '\u0085':
                            case '\u2028':
                            case '\u2029':
                            case '\u00A0':
                            case '\uFEFF':
                                _position += 1;
                                continue;
                            case '/':
                                _position += 1;
                                SkipComment();
                                continue;
                            case '{':
                            case '[':
                            case ']':
                            case ',':
                            case ':':
                                throw new FormatException($"Unexpected character '{(char)ch}'");
                            default:
                                _isGet = false;
                                _token = Token.PropertyWithoutQuote;
                                return true;
                        }
                    }
                }
                bool NextValue()
                {
                    for (; ; )
                    {
                        var ch = PeekChar();
                        switch (ch)
                        {
                            case -1:
                                return false;
                            case '{':
                                _position += 1;
                                _depth += 1;
                                _isObject[_depth] = true;
                                _token = Token.StartObject;
                                return true;
                            case '[':
                                _position += 1;
                                _depth += 1;
                                _isObject[_depth] = false;
                                _token = Token.StartArray;
                                return true;
                            case '\'':
                                _position += 1;
                                _isGet = false;
                                _token = Token.StringQuote;
                                return true;
                            case '"':
                                _position += 1;
                                _isGet = false;
                                _token = Token.StringDoubleQuote;
                                return true;
                            case 'n':
                                _position += 1;
                                if (ReadChar() != 'u' || ReadChar() != 'l' || ReadChar() != 'l')
                                    throw new FormatException("Unexpected character");
                                _token = Token.Null;
                                return true;
                            case 't':
                                _position += 1;
                                if (ReadChar() != 'r' || ReadChar() != 'u' || ReadChar() != 'e')
                                    throw new FormatException("Unexpected character");
                                _boolean = true;
                                _token = Token.Boolean;
                                return true;
                            case 'f':
                                _position += 1;
                                if (ReadChar() != 'a' || ReadChar() != 'l' || ReadChar() != 's' || ReadChar() != 'e')
                                    throw new FormatException("Unexpected character");
                                _boolean = false;
                                _token = Token.Boolean;
                                return true;
                            case '0':
                            case '1':
                            case '2':
                            case '3':
                            case '4':
                            case '5':
                            case '6':
                            case '7':
                            case '8':
                            case '9':
                            case '+':
                            case '-':
                            case '.':
                            case 'N':
                            case 'I':
                                _isGet = false;
                                _token = Token.Number;
                                return true;
                            case ' ':
                            case '\t':
                            case '\r':
                            case '\n':
                            case '\v':
                            case '\f':
                            case '\u0000':
                            case '\u0085':
                            case '\u2028':
                            case '\u2029':
                            case '\u00A0':
                            case '\uFEFF':
                                _position += 1;
                                continue;
                            case '/':
                                _position += 1;
                                SkipComment();
                                continue;
                            case ']':
                                if (_isObject[_depth--])
                                    throw new FormatException($"Unexpected character '{(char)ch}'");
                                _position += 1;
                                _token = Token.EndArray;
                                return true;
                            default:
                                throw new FormatException($"Unexpected character '{(char)ch}'");
                        }
                    }
                }
                #endregion
                switch (_token)
                {
                    case Token.None:
                        return NextValue();
                    case Token.StartObject:
                        return NextProperty();
                    case Token.PropertyQuote:
                        if (!_isGet)
                            SkipPropertyQuote();
                        return NextValue();
                    case Token.PropertyDoubleQuote:
                        if (!_isGet)
                            SkipPropertyDoubleQuote();
                        return NextValue();
                    case Token.PropertyWithoutQuote:
                        if (!_isGet)
                            SkipPropertyWithoutQuote();
                        return NextValue();
                    case Token.StringQuote:
                        if (!_isGet)
                            SkipStringQuote();
                        return Next();
                    case Token.StringDoubleQuote:
                        if (!_isGet)
                            SkipStringDoubleQuote();
                        return Next();
                    case Token.Number:
                        if (!_isGet)
                            SkipNumber();
                        return Next();
                    case Token.Boolean:
                    case Token.Null:
                        return Next();
                    case Token.StartArray:
                        return NextValue();
                    case Token.EndObject:
                    case Token.EndArray:
                        return Next();
                    default:
                        return false;
                }
            }
            public override bool IsStartArray => _token == Token.StartArray;
            public override bool IsEndArray => _token == Token.EndArray;
            public override bool IsStartObject => _token == Token.StartObject;
            public override bool IsEndObject => _token == Token.EndObject;
            public override bool IsProperty => _token == Token.PropertyQuote || _token == Token.PropertyDoubleQuote || _token == Token.PropertyWithoutQuote;
            public override bool IsString => _token == Token.StringQuote || _token == Token.StringDoubleQuote;
            public override bool IsNumber => _token == Token.Number;
            public override bool IsBoolean => _token == Token.Boolean;
            public override bool IsNull => _token == Token.Null;
            private void SkipComment()
            {
                var ch = ReadChar();
                switch (ch)
                {
                    case -1:
                        throw new FormatException("EOF");
                    case '*':
                        for (; ; )
                        {
                            ch = ReadChar();
                            if (ch == -1)
                                throw new FormatException("EOF");

                            if (ch == '*')
                            {
                                for (; ; )
                                {
                                    ch = ReadChar();
                                    switch (ch)
                                    {
                                        case -1:
                                            throw new FormatException("EOF");
                                        case '*':
                                            continue;
                                        case '/':
                                            return;
                                        default:
                                            break;
                                    }
                                    break;
                                }
                            }
                        }
                    case '/':
                        for (; ; )
                        {
                            ch = ReadChar();
                            switch (ch)
                            {
                                case -1:
                                case '\r':
                                case '\n':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                    return;
                                default:
                                    continue;
                            }
                        }
                    default:
                        throw new FormatException($"Unexpected character '{(char)ch}'");
                }
            }
            private void SkipPropertyQuote()
            {
                Debug.Assert(_token == Token.PropertyQuote);
                Debug.Assert(!_isGet);
                for (; ; )
                {
                    var ch = ReadChar();
                    switch (ch)
                    {
                        case -1:
                            throw new FormatException("EOF");
                        case '\\':
                            ch = ReadChar();
                            switch (ch)
                            {
                                case -1:
                                    throw new FormatException("EOF");
                                case '0':
                                case '\'':
                                case '"':
                                case '\\':
                                case '/':
                                case 'r':
                                case 'n':
                                case 't':
                                case 'v':
                                case 'b':
                                case 'f':
                                    break;
                                case 'u':
                                    if (ReadChar() == -1 || ReadChar() == -1 || ReadChar() == -1 || ReadChar() == -1)
                                        throw new FormatException("EOF");
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + (char)ch);
                            }
                            break;
                        case '\'':
                            for (; ; )
                            {
                                ch = ReadChar();
                                switch (ch)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case ':':
                                        _isGet = true;
                                        return;
                                    case ' ':
                                    case '\t':
                                    case '\r':
                                    case '\n':
                                    case '\v':
                                    case '\f':
                                    case '\u0000':
                                    case '\u0085':
                                    case '\u2028':
                                    case '\u2029':
                                    case '\u00A0':
                                    case '\uFEFF':
                                        continue;
                                    case '/':
                                        SkipComment();
                                        continue;
                                    default:
                                        throw new FormatException($"Unexpected character '{(char)ch}'");
                                }
                            }
                        default:
                            break;
                    }
                }
            }
            private string GetPropertyQuote()
            {
                Debug.Assert(_token == Token.PropertyQuote);
                Debug.Assert(!_isGet);
                unsafe
                {
                    var @char = PeekChar();
                    if (@char == -1)
                        throw new FormatException("EOF");
                    var ch = (char)@char;
                    var start = _position++;
                    for (; ; )
                    {
                        if (ch == '\\')
                        {
                            break;
                        }
                        else if (ch == '\'')
                        {
                            var property = new string(_segment, start, _position - start - 1);
                            for (; ; )
                            {
                                @char = ReadChar();
                                switch (@char)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case ':':
                                        _isGet = true;
                                        return property;
                                    case ' ':
                                    case '\t':
                                    case '\r':
                                    case '\n':
                                    case '\v':
                                    case '\f':
                                    case '\u0000':
                                    case '\u0085':
                                    case '\u2028':
                                    case '\u2029':
                                    case '\u00A0':
                                    case '\uFEFF':
                                        continue;
                                    case '/':
                                        SkipComment();
                                        continue;
                                    default:
                                        throw new FormatException($"Unexpected character '{(char)@char}'");
                                }
                            }
                        }
                        if (_position == _length)
                            break;
                        ch = _segment[_position++];
                    }
                    var sb = StringExtensions.ThreadRent(out var disposable);
                    try
                    {
                        for (; ; )
                        {
                            if (ch == '\\')
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start - 1));
                                for (; ; )
                                {
                                    @char = ReadChar();
                                    switch (@char)
                                    {
                                        case -1:
                                            throw new FormatException("EOF");
                                        case '0':
                                            sb.Write('\u0000');
                                            break;
                                        case '\'':
                                            sb.Write('\'');
                                            break;
                                        case '"':
                                            sb.Write('"');
                                            break;
                                        case '\\':
                                            sb.Write('\\');
                                            break;
                                        case '/':
                                            sb.Write('/');
                                            break;
                                        case 'r':
                                            sb.Write('\r');
                                            break;
                                        case 'n':
                                            sb.Write('\n');
                                            break;
                                        case 't':
                                            sb.Write('\t');
                                            break;
                                        case 'v':
                                            sb.Write('\v');
                                            break;
                                        case 'b':
                                            sb.Write('\b');
                                            break;
                                        case 'f':
                                            sb.Write('\f');
                                            break;
                                        case 'u':
                                            sb.Write(GetUnicode());
                                            break;
                                        default:
                                            throw new NotSupportedException("\\" + (char)@char);
                                    }
                                    @char = PeekChar();
                                    if (@char == -1)
                                        throw new FormatException("EOF");
                                    if (@char == '\\')
                                    {
                                        _position++;
                                    }
                                    else
                                    {
                                        ch = (char)@char;
                                        start = _position;
                                        _position++;
                                        break;
                                    }
                                }
                            }
                            if (ch == '\'')
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start - 1));
                                for (; ; )
                                {
                                    @char = ReadChar();
                                    switch (@char)
                                    {
                                        case -1:
                                            throw new FormatException("EOF");
                                        case ':':
                                            _isGet = true;
                                            return sb.ToString();
                                        case ' ':
                                        case '\t':
                                        case '\r':
                                        case '\n':
                                        case '\v':
                                        case '\f':
                                        case '\u0000':
                                        case '\u0085':
                                        case '\u2028':
                                        case '\u2029':
                                        case '\u00A0':
                                        case '\uFEFF':
                                            continue;
                                        case '/':
                                            SkipComment();
                                            continue;
                                        default:
                                            throw new FormatException($"Unexpected character '{ch}'");
                                    }
                                }
                            }
                            if (_position == _length)
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start));
                                start = 0;
                                @char = ReadChar();
                                if (@char == -1)
                                    throw new FormatException("EOF");
                                ch = (char)@char;
                            }
                            else
                            {
                                ch = _segment[_position++];
                            }
                        }
                    }
                    finally
                    {
                        disposable.Dispose();
                    }
                }
            }
            private void GetPropertyQuote(out ReadOnlySpan<char> property)
            {
                Debug.Assert(_token == Token.PropertyQuote);
                Debug.Assert(!_isGet);
                unsafe
                {
                    var @char = PeekChar();
                    if (@char == -1)
                        throw new FormatException("EOF");
                    var ch = (char)@char;
                    var start = _position++;
                    for (; ; )
                    {
                        if (ch == '\\')
                        {
                            break;
                        }
                        else if (ch == '\'')
                        {
                            property = new ReadOnlySpan<char>(_segment + start, _position - start - 1);
                            for (; ; )
                            {
                                @char = ReadChar();
                                switch (@char)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case ':':
                                        _isGet = true;
                                        return;
                                    case ' ':
                                    case '\t':
                                    case '\r':
                                    case '\n':
                                    case '\v':
                                    case '\f':
                                    case '\u0000':
                                    case '\u0085':
                                    case '\u2028':
                                    case '\u2029':
                                    case '\u00A0':
                                    case '\uFEFF':
                                        continue;
                                    case '/':
                                        SkipComment();
                                        continue;
                                    default:
                                        throw new FormatException($"Unexpected character '{(char)@char}'");
                                }
                            }
                        }
                        if (_position == _length)
                            break;
                        ch = _segment[_position++];
                    }
                    var sb = StringExtensions.ThreadRent(out var disposable);
                    try
                    {
                        for (; ; )
                        {
                            if (ch == '\\')
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start - 1));
                                for (; ; )
                                {
                                    @char = ReadChar();
                                    switch (@char)
                                    {
                                        case -1:
                                            throw new FormatException("EOF");
                                        case '0':
                                            sb.Write('\u0000');
                                            break;
                                        case '\'':
                                            sb.Write('\'');
                                            break;
                                        case '"':
                                            sb.Write('"');
                                            break;
                                        case '\\':
                                            sb.Write('\\');
                                            break;
                                        case '/':
                                            sb.Write('/');
                                            break;
                                        case 'r':
                                            sb.Write('\r');
                                            break;
                                        case 'n':
                                            sb.Write('\n');
                                            break;
                                        case 't':
                                            sb.Write('\t');
                                            break;
                                        case 'v':
                                            sb.Write('\v');
                                            break;
                                        case 'b':
                                            sb.Write('\b');
                                            break;
                                        case 'f':
                                            sb.Write('\f');
                                            break;
                                        case 'u':
                                            sb.Write(GetUnicode());
                                            break;
                                        default:
                                            throw new NotSupportedException("\\" + (char)@char);
                                    }
                                    @char = PeekChar();
                                    if (@char == -1)
                                        throw new FormatException("EOF");
                                    if (@char == '\\')
                                    {
                                        _position++;
                                    }
                                    else
                                    {
                                        ch = (char)@char;
                                        start = _position;
                                        _position++;
                                        break;
                                    }
                                }
                            }
                            if (ch == '\'')
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start - 1));
                                property = sb.ToString();
                                for (; ; )
                                {
                                    @char = ReadChar();
                                    switch (@char)
                                    {
                                        case -1:
                                            throw new FormatException("EOF");
                                        case ':':
                                            _isGet = true;
                                            return;
                                        case ' ':
                                        case '\t':
                                        case '\r':
                                        case '\n':
                                        case '\v':
                                        case '\f':
                                        case '\u0000':
                                        case '\u0085':
                                        case '\u2028':
                                        case '\u2029':
                                        case '\u00A0':
                                        case '\uFEFF':
                                            continue;
                                        case '/':
                                            SkipComment();
                                            continue;
                                        default:
                                            throw new FormatException($"Unexpected character '{ch}'");
                                    }
                                }
                            }
                            if (_position == _length)
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start));
                                start = 0;
                                @char = ReadChar();
                                if (@char == -1)
                                    throw new FormatException("EOF");
                                ch = (char)@char;
                            }
                            else
                            {
                                ch = _segment[_position++];
                            }
                        }
                    }
                    finally
                    {
                        disposable.Dispose();
                    }
                }
            }
            private void SkipPropertyDoubleQuote()
            {
                Debug.Assert(_token == Token.PropertyDoubleQuote);
                Debug.Assert(!_isGet);
                for (; ; )
                {
                    var ch = ReadChar();
                    switch (ch)
                    {
                        case -1:
                            throw new FormatException("EOF");
                        case '\\':
                            ch = ReadChar();
                            switch (ch)
                            {
                                case -1:
                                    throw new FormatException("EOF");
                                case '0':
                                case '\'':
                                case '"':
                                case '\\':
                                case '/':
                                case 'r':
                                case 'n':
                                case 't':
                                case 'v':
                                case 'b':
                                case 'f':
                                    break;
                                case 'u':
                                    if (ReadChar() == -1 || ReadChar() == -1 || ReadChar() == -1 || ReadChar() == -1)
                                        throw new FormatException("EOF");
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + (char)ch);
                            }
                            break;
                        case '"':
                            for (; ; )
                            {
                                ch = ReadChar();
                                switch (ch)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case ':':
                                        _isGet = true;
                                        return;
                                    case ' ':
                                    case '\t':
                                    case '\r':
                                    case '\n':
                                    case '\v':
                                    case '\f':
                                    case '\u0000':
                                    case '\u0085':
                                    case '\u2028':
                                    case '\u2029':
                                    case '\u00A0':
                                    case '\uFEFF':
                                        continue;
                                    case '/':
                                        SkipComment();
                                        continue;
                                    default:
                                        throw new FormatException($"Unexpected character '{(char)ch}'");
                                }
                            }
                        default:
                            break;
                    }
                }
            }
            private string GetPropertyDoubleQuote()
            {
                Debug.Assert(_token == Token.PropertyDoubleQuote);
                Debug.Assert(!_isGet);
                unsafe
                {
                    var @char = PeekChar();
                    if (@char == -1)
                        throw new FormatException("EOF");
                    var ch = (char)@char;
                    var start = _position++;
                    for (; ; )
                    {
                        if (ch == '\\')
                        {
                            break;
                        }
                        else if (ch == '"')
                        {
                            var property = new string(_segment, start, _position - start - 1);
                            for (; ; )
                            {
                                @char = ReadChar();
                                switch (@char)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case ':':
                                        _isGet = true;
                                        return property;
                                    case ' ':
                                    case '\t':
                                    case '\r':
                                    case '\n':
                                    case '\v':
                                    case '\f':
                                    case '\u0000':
                                    case '\u0085':
                                    case '\u2028':
                                    case '\u2029':
                                    case '\u00A0':
                                    case '\uFEFF':
                                        continue;
                                    case '/':
                                        SkipComment();
                                        continue;
                                    default:
                                        throw new FormatException($"Unexpected character '{(char)@char}'");
                                }
                            }
                        }
                        if (_position == _length)
                            break;
                        ch = _segment[_position++];
                    }
                    var sb = StringExtensions.ThreadRent(out var disposable);
                    try
                    {
                        for (; ; )
                        {
                            if (ch == '\\')
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start - 1));
                                for (; ; )
                                {
                                    @char = ReadChar();
                                    switch (@char)
                                    {
                                        case -1:
                                            throw new FormatException("EOF");
                                        case '0':
                                            sb.Write('\u0000');
                                            break;
                                        case '\'':
                                            sb.Write('\'');
                                            break;
                                        case '"':
                                            sb.Write('"');
                                            break;
                                        case '\\':
                                            sb.Write('\\');
                                            break;
                                        case '/':
                                            sb.Write('/');
                                            break;
                                        case 'r':
                                            sb.Write('\r');
                                            break;
                                        case 'n':
                                            sb.Write('\n');
                                            break;
                                        case 't':
                                            sb.Write('\t');
                                            break;
                                        case 'v':
                                            sb.Write('\v');
                                            break;
                                        case 'b':
                                            sb.Write('\b');
                                            break;
                                        case 'f':
                                            sb.Write('\f');
                                            break;
                                        case 'u':
                                            sb.Write(GetUnicode());
                                            break;
                                        default:
                                            throw new NotSupportedException("\\" + (char)@char);
                                    }
                                    @char = PeekChar();
                                    if (@char == -1)
                                        throw new FormatException("EOF");
                                    if (@char == '\\')
                                    {
                                        _position++;
                                    }
                                    else
                                    {
                                        ch = (char)@char;
                                        start = _position;
                                        _position++;
                                        break;
                                    }
                                }
                            }
                            if (ch == '"')
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start - 1));
                                for (; ; )
                                {
                                    @char = ReadChar();
                                    switch (@char)
                                    {
                                        case -1:
                                            throw new FormatException("EOF");
                                        case ':':
                                            _isGet = true;
                                            return sb.ToString();
                                        case ' ':
                                        case '\t':
                                        case '\r':
                                        case '\n':
                                        case '\v':
                                        case '\f':
                                        case '\u0000':
                                        case '\u0085':
                                        case '\u2028':
                                        case '\u2029':
                                        case '\u00A0':
                                        case '\uFEFF':
                                            continue;
                                        case '/':
                                            SkipComment();
                                            continue;
                                        default:
                                            throw new FormatException($"Unexpected character '{ch}'");
                                    }
                                }
                            }
                            if (_position == _length)
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start));
                                start = 0;
                                @char = ReadChar();
                                if (@char == -1)
                                    throw new FormatException("EOF");
                                ch = (char)@char;
                            }
                            else
                            {
                                ch = _segment[_position++];
                            }
                        }
                    }
                    finally
                    {
                        disposable.Dispose();
                    }
                }
            }
            private void GetPropertyDoubleQuote(out ReadOnlySpan<char> property)
            {
                Debug.Assert(_token == Token.PropertyDoubleQuote);
                Debug.Assert(!_isGet);
                unsafe
                {
                    var @char = PeekChar();
                    if (@char == -1)
                        throw new FormatException("EOF");
                    var ch = (char)@char;
                    var start = _position++;
                    for (; ; )
                    {
                        if (ch == '\\')
                        {
                            break;
                        }
                        else if (ch == '"')
                        {
                            property = new ReadOnlySpan<char>(_segment + start, _position - start - 1);
                            for (; ; )
                            {
                                @char = ReadChar();
                                switch (@char)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case ':':
                                        _isGet = true;
                                        return;
                                    case ' ':
                                    case '\t':
                                    case '\r':
                                    case '\n':
                                    case '\v':
                                    case '\f':
                                    case '\u0000':
                                    case '\u0085':
                                    case '\u2028':
                                    case '\u2029':
                                    case '\u00A0':
                                    case '\uFEFF':
                                        continue;
                                    case '/':
                                        SkipComment();
                                        continue;
                                    default:
                                        throw new FormatException($"Unexpected character '{(char)@char}'");
                                }
                            }
                        }
                        if (_position == _length)
                            break;
                        ch = _segment[_position++];
                    }
                    var sb = StringExtensions.ThreadRent(out var disposable);
                    try
                    {
                        for (; ; )
                        {
                            if (ch == '\\')
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start - 1));
                                for (; ; )
                                {
                                    @char = ReadChar();
                                    switch (@char)
                                    {
                                        case -1:
                                            throw new FormatException("EOF");
                                        case '0':
                                            sb.Write('\u0000');
                                            break;
                                        case '\'':
                                            sb.Write('\'');
                                            break;
                                        case '"':
                                            sb.Write('"');
                                            break;
                                        case '\\':
                                            sb.Write('\\');
                                            break;
                                        case '/':
                                            sb.Write('/');
                                            break;
                                        case 'r':
                                            sb.Write('\r');
                                            break;
                                        case 'n':
                                            sb.Write('\n');
                                            break;
                                        case 't':
                                            sb.Write('\t');
                                            break;
                                        case 'v':
                                            sb.Write('\v');
                                            break;
                                        case 'b':
                                            sb.Write('\b');
                                            break;
                                        case 'f':
                                            sb.Write('\f');
                                            break;
                                        case 'u':
                                            sb.Write(GetUnicode());
                                            break;
                                        default:
                                            throw new NotSupportedException("\\" + (char)@char);
                                    }
                                    @char = PeekChar();
                                    if (@char == -1)
                                        throw new FormatException("EOF");
                                    if (@char == '\\')
                                    {
                                        _position++;
                                    }
                                    else
                                    {
                                        ch = (char)@char;
                                        start = _position;
                                        _position++;
                                        break;
                                    }
                                }
                            }
                            if (ch == '"')
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start - 1));
                                property = sb.ToString();
                                for (; ; )
                                {
                                    @char = ReadChar();
                                    switch (@char)
                                    {
                                        case -1:
                                            throw new FormatException("EOF");
                                        case ':':
                                            _isGet = true;
                                            return;
                                        case ' ':
                                        case '\t':
                                        case '\r':
                                        case '\n':
                                        case '\v':
                                        case '\f':
                                        case '\u0000':
                                        case '\u0085':
                                        case '\u2028':
                                        case '\u2029':
                                        case '\u00A0':
                                        case '\uFEFF':
                                            continue;
                                        case '/':
                                            SkipComment();
                                            continue;
                                        default:
                                            throw new FormatException($"Unexpected character '{ch}'");
                                    }
                                }
                            }
                            if (_position == _length)
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start));
                                start = 0;
                                @char = ReadChar();
                                if (@char == -1)
                                    throw new FormatException("EOF");
                                ch = (char)@char;
                            }
                            else
                            {
                                ch = _segment[_position++];
                            }
                        }
                    }
                    finally
                    {
                        disposable.Dispose();
                    }
                }
            }
            private void SkipPropertyWithoutQuote()
            {
                Debug.Assert(_token == Token.PropertyWithoutQuote);
                Debug.Assert(!_isGet);
                _position += 1;
                for (; ; )
                {
                    var ch = ReadChar();
                    switch (ch)
                    {
                        case -1:
                            throw new FormatException("EOF");
                        case ':':
                            _isGet = true;
                            return;
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                        case '\v':
                        case '\f':
                        case '\u0000':
                        case '\u0085':
                        case '\u2028':
                        case '\u2029':
                        case '\u00A0':
                        case '\uFEFF':
                            for (; ; )
                            {
                                ch = ReadChar();
                                switch (ch)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case ':':
                                        _isGet = true;
                                        return;
                                    case ' ':
                                    case '\t':
                                    case '\r':
                                    case '\n':
                                    case '\v':
                                    case '\f':
                                    case '\u0000':
                                    case '\u0085':
                                    case '\u2028':
                                    case '\u2029':
                                    case '\u00A0':
                                    case '\uFEFF':
                                        continue;
                                    case '/':
                                        SkipComment();
                                        continue;
                                    default:
                                        throw new FormatException($"Unexpected character '{ch}'");
                                }
                            }
                        default:
                            break;
                    }
                }
            }
            private string GetPropertyWithoutQuote()
            {
                Debug.Assert(_token == Token.PropertyWithoutQuote);
                Debug.Assert(!_isGet);
                unsafe
                {
                    var @char = _segment[_position];
                    var start = _position++;
                    for (; ; )
                    {
                        if (_position == _length)
                            break;
                        switch (@char)
                        {
                            case ':':
                                _isGet = true;
                                return new string(_segment, start, _position - start - 1);
                            case ' ':
                            case '\t':
                            case '\r':
                            case '\n':
                            case '\v':
                            case '\f':
                            case '\u0000':
                            case '\u0085':
                            case '\u2028':
                            case '\u2029':
                            case '\u00A0':
                            case '\uFEFF':
                                {
                                    var property = new string(_segment, start, _position - start - 1);
                                    for (; ; )
                                    {
                                        var ch = ReadChar();
                                        switch (ch)
                                        {
                                            case -1:
                                                throw new FormatException("EOF");
                                            case ':':
                                                _isGet = true;
                                                return property;
                                            case ' ':
                                            case '\t':
                                            case '\r':
                                            case '\n':
                                            case '\v':
                                            case '\f':
                                            case '\u0000':
                                            case '\u0085':
                                            case '\u2028':
                                            case '\u2029':
                                            case '\u00A0':
                                            case '\uFEFF':
                                                continue;
                                            case '/':
                                                SkipComment();
                                                continue;
                                            default:
                                                throw new FormatException($"Unexpected character '{ch}'");
                                        }
                                    }
                                }
                            default:
                                break;
                        }
                    }
                    var sb = StringExtensions.ThreadRent(out var disposable);
                    try
                    {
                        for (; ; )
                        {
                            switch (@char)
                            {
                                case ':':
                                    sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start - 1));
                                    _isGet = true;
                                    return sb.ToString();
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    {
                                        sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start - 1));
                                        for (; ; )
                                        {
                                            var ch = ReadChar();
                                            switch (ch)
                                            {
                                                case -1:
                                                    throw new FormatException("EOF");
                                                case ':':
                                                    _isGet = true;
                                                    return sb.ToString();
                                                case ' ':
                                                case '\t':
                                                case '\r':
                                                case '\n':
                                                case '\v':
                                                case '\f':
                                                case '\u0000':
                                                case '\u0085':
                                                case '\u2028':
                                                case '\u2029':
                                                case '\u00A0':
                                                case '\uFEFF':
                                                    continue;
                                                case '/':
                                                    SkipComment();
                                                    continue;
                                                default:
                                                    throw new FormatException($"Unexpected character '{ch}'");
                                            }
                                        }
                                    }
                                default:
                                    break;
                            }
                            if (_position == _length)
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start));
                                start = 0;
                                var ch = ReadChar();
                                if (ch == -1)
                                    throw new FormatException("EOF");
                                @char = (char)ch;
                            }
                            else
                            {
                                @char = _segment[_position++];
                            }
                        }
                    }
                    finally
                    {
                        disposable.Dispose();
                    }
                }
            }
            private void GetPropertyWithoutQuote(out ReadOnlySpan<char> property)
            {
                Debug.Assert(_token == Token.PropertyWithoutQuote);
                Debug.Assert(!_isGet);
                unsafe
                {
                    var @char = _segment[_position];
                    var start = _position++;
                    for (; ; )
                    {
                        if (_position == _length)
                            break;
                        switch (@char)
                        {
                            case ':':
                                property = new ReadOnlySpan<char>(_segment + start, _position - start - 1);
                                _isGet = true;
                                return;
                            case ' ':
                            case '\t':
                            case '\r':
                            case '\n':
                            case '\v':
                            case '\f':
                            case '\u0000':
                            case '\u0085':
                            case '\u2028':
                            case '\u2029':
                            case '\u00A0':
                            case '\uFEFF':
                                {
                                    property = new ReadOnlySpan<char>(_segment + start, _position - start - 1);
                                    for (; ; )
                                    {
                                        var ch = ReadChar();
                                        switch (ch)
                                        {
                                            case -1:
                                                throw new FormatException("EOF");
                                            case ':':
                                                _isGet = true;
                                                return;
                                            case ' ':
                                            case '\t':
                                            case '\r':
                                            case '\n':
                                            case '\v':
                                            case '\f':
                                            case '\u0000':
                                            case '\u0085':
                                            case '\u2028':
                                            case '\u2029':
                                            case '\u00A0':
                                            case '\uFEFF':
                                                continue;
                                            case '/':
                                                SkipComment();
                                                continue;
                                            default:
                                                throw new FormatException($"Unexpected character '{ch}'");
                                        }
                                    }
                                }
                            default:
                                break;
                        }
                    }
                    var sb = StringExtensions.ThreadRent(out var disposable);
                    try
                    {
                        for (; ; )
                        {
                            switch (@char)
                            {
                                case ':':
                                    sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start - 1));
                                    _isGet = true;
                                    property = sb.ToString();
                                    return;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    {
                                        sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start - 1));
                                        for (; ; )
                                        {
                                            var ch = ReadChar();
                                            switch (ch)
                                            {
                                                case -1:
                                                    throw new FormatException("EOF");
                                                case ':':
                                                    _isGet = true;
                                                    property = sb.ToString();
                                                    return;
                                                case ' ':
                                                case '\t':
                                                case '\r':
                                                case '\n':
                                                case '\v':
                                                case '\f':
                                                case '\u0000':
                                                case '\u0085':
                                                case '\u2028':
                                                case '\u2029':
                                                case '\u00A0':
                                                case '\uFEFF':
                                                    continue;
                                                case '/':
                                                    SkipComment();
                                                    continue;
                                                default:
                                                    throw new FormatException($"Unexpected character '{ch}'");
                                            }
                                        }
                                    }
                                default:
                                    break;
                            }
                            if (_position == _length)
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start));
                                start = 0;
                                var ch = ReadChar();
                                if (ch == -1)
                                    throw new FormatException("EOF");
                                @char = (char)ch;
                            }
                            else
                            {
                                @char = _segment[_position++];
                            }
                        }
                    }
                    finally
                    {
                        disposable.Dispose();
                    }
                }
            }
            public override string GetProperty()
            {
                if (_isGet)
                    throw new InvalidOperationException(nameof(GetProperty));

                switch (_token)
                {
                    case Token.PropertyQuote:
                        return GetPropertyQuote();
                    case Token.PropertyDoubleQuote:
                        return GetPropertyDoubleQuote();
                    case Token.PropertyWithoutQuote:
                        return GetPropertyWithoutQuote();
                    default:
                        throw new InvalidOperationException(nameof(GetProperty));
                }
            }
            public override void GetProperty(out ReadOnlySpan<char> property)
            {
                if (_isGet)
                    throw new InvalidOperationException(nameof(GetProperty));

                switch (_token)
                {
                    case Token.PropertyQuote:
                        GetPropertyQuote(out property);
                        break;
                    case Token.PropertyDoubleQuote:
                        GetPropertyDoubleQuote(out property);
                        break;
                    case Token.PropertyWithoutQuote:
                        GetPropertyWithoutQuote(out property);
                        break;
                    default:
                        throw new InvalidOperationException(nameof(GetProperty));
                }
            }
            private void SkipStringQuote()
            {
                Debug.Assert(_token == Token.StringQuote);
                Debug.Assert(!_isGet);
                for (; ; )
                {
                    var ch = ReadChar();
                    switch (ch)
                    {
                        case -1:
                            throw new FormatException("EOF");
                        case '\\':
                            ch = ReadChar();
                            switch (ch)
                            {
                                case -1:
                                    throw new FormatException("EOF");
                                case '0':
                                case '\'':
                                case '"':
                                case '\\':
                                case '/':
                                case 'r':
                                case 'n':
                                case 't':
                                case 'v':
                                case 'b':
                                case 'f':
                                    break;
                                case 'u':
                                    if (ReadChar() == -1 || ReadChar() == -1 || ReadChar() == -1 || ReadChar() == -1)
                                        throw new FormatException("EOF");
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + (char)ch);
                            }
                            break;
                        case '\'':
                            _isGet = true;
                            return;
                        default:
                            break;
                    }
                }
            }
            private string GetStringQuote()
            {
                Debug.Assert(_token == Token.StringQuote);
                Debug.Assert(!_isGet);
                unsafe
                {
                    var @char = PeekChar();
                    if (@char == -1)
                        throw new FormatException("EOF");
                    var ch = (char)@char;
                    var start = _position++;
                    for (; ; )
                    {
                        if (ch == '\\')
                        {
                            break;
                        }
                        else if (ch == '\'')
                        {
                            var property = new string(_segment, start, _position - start - 1);
                            _isGet = true;
                            return property;
                        }
                        if (_position == _length)
                            break;
                        ch = _segment[_position++];
                    }
                    var sb = StringExtensions.ThreadRent(out var disposable);
                    try
                    {
                        for (; ; )
                        {
                            if (ch == '\\')
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start - 1));
                                for (; ; )
                                {
                                    @char = ReadChar();
                                    switch (@char)
                                    {
                                        case -1:
                                            throw new FormatException("EOF");
                                        case '0':
                                            sb.Write('\u0000');
                                            break;
                                        case '\'':
                                            sb.Write('\'');
                                            break;
                                        case '"':
                                            sb.Write('"');
                                            break;
                                        case '\\':
                                            sb.Write('\\');
                                            break;
                                        case '/':
                                            sb.Write('/');
                                            break;
                                        case 'r':
                                            sb.Write('\r');
                                            break;
                                        case 'n':
                                            sb.Write('\n');
                                            break;
                                        case 't':
                                            sb.Write('\t');
                                            break;
                                        case 'v':
                                            sb.Write('\v');
                                            break;
                                        case 'b':
                                            sb.Write('\b');
                                            break;
                                        case 'f':
                                            sb.Write('\f');
                                            break;
                                        case 'u':
                                            sb.Write(GetUnicode());
                                            break;
                                        default:
                                            throw new NotSupportedException("\\" + (char)@char);
                                    }
                                    @char = PeekChar();
                                    if (@char == -1)
                                        throw new FormatException("EOF");
                                    if (@char == '\\')
                                    {
                                        _position++;
                                    }
                                    else
                                    {
                                        ch = (char)@char;
                                        start = _position;
                                        _position++;
                                        break;
                                    }
                                }
                            }
                            if (ch == '\'')
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start - 1));
                                _isGet = true;
                                return sb.ToString();
                            }
                            if (_position == _length)
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start));
                                start = 0;
                                @char = ReadChar();
                                if (@char == -1)
                                    throw new FormatException("EOF");
                                ch = (char)@char;
                            }
                            else
                            {
                                ch = _segment[_position++];
                            }
                        }
                    }
                    finally
                    {
                        disposable.Dispose();
                    }
                }
            }
            private void GetStringQuote(out ReadOnlySpan<char> value)
            {
                Debug.Assert(_token == Token.StringQuote);
                Debug.Assert(!_isGet);
                unsafe
                {
                    var @char = PeekChar();
                    if (@char == -1)
                        throw new FormatException("EOF");
                    var ch = (char)@char;
                    var start = _position++;
                    for (; ; )
                    {
                        if (ch == '\\')
                        {
                            break;
                        }
                        else if (ch == '\'')
                        {
                            value = new ReadOnlySpan<char>(_segment + start, _position - start - 1);
                            _isGet = true;
                            return;
                        }
                        if (_position == _length)
                            break;
                        ch = _segment[_position++];
                    }
                    var sb = StringExtensions.ThreadRent(out var disposable);
                    try
                    {
                        for (; ; )
                        {
                            if (ch == '\\')
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start - 1));
                                for (; ; )
                                {
                                    @char = ReadChar();
                                    switch (@char)
                                    {
                                        case -1:
                                            throw new FormatException("EOF");
                                        case '0':
                                            sb.Write('\u0000');
                                            break;
                                        case '\'':
                                            sb.Write('\'');
                                            break;
                                        case '"':
                                            sb.Write('"');
                                            break;
                                        case '\\':
                                            sb.Write('\\');
                                            break;
                                        case '/':
                                            sb.Write('/');
                                            break;
                                        case 'r':
                                            sb.Write('\r');
                                            break;
                                        case 'n':
                                            sb.Write('\n');
                                            break;
                                        case 't':
                                            sb.Write('\t');
                                            break;
                                        case 'v':
                                            sb.Write('\v');
                                            break;
                                        case 'b':
                                            sb.Write('\b');
                                            break;
                                        case 'f':
                                            sb.Write('\f');
                                            break;
                                        case 'u':
                                            sb.Write(GetUnicode());
                                            break;
                                        default:
                                            throw new NotSupportedException("\\" + (char)@char);
                                    }
                                    @char = PeekChar();
                                    if (@char == -1)
                                        throw new FormatException("EOF");
                                    if (@char == '\\')
                                    {
                                        _position++;
                                    }
                                    else
                                    {
                                        ch = (char)@char;
                                        start = _position;
                                        _position++;
                                        break;
                                    }
                                }
                            }
                            if (ch == '\'')
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start - 1));
                                value = sb.ToString();
                                _isGet = true;
                                return;
                            }
                            if (_position == _length)
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start));
                                start = 0;
                                @char = ReadChar();
                                if (@char == -1)
                                    throw new FormatException("EOF");
                                ch = (char)@char;
                            }
                            else
                            {
                                ch = _segment[_position++];
                            }
                        }
                    }
                    finally
                    {
                        disposable.Dispose();
                    }
                }
            }
            private void SkipStringDoubleQuote()
            {
                Debug.Assert(_token == Token.StringDoubleQuote);
                Debug.Assert(!_isGet);
                for (; ; )
                {
                    var ch = ReadChar();
                    switch (ch)
                    {
                        case -1:
                            throw new FormatException("EOF");
                        case '\\':
                            ch = ReadChar();
                            switch (ch)
                            {
                                case -1:
                                    throw new FormatException("EOF");
                                case '0':
                                case '\'':
                                case '"':
                                case '\\':
                                case '/':
                                case 'r':
                                case 'n':
                                case 't':
                                case 'v':
                                case 'b':
                                case 'f':
                                    break;
                                case 'u':
                                    if (ReadChar() == -1 || ReadChar() == -1 || ReadChar() == -1 || ReadChar() == -1)
                                        throw new FormatException("EOF");
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + (char)ch);
                            }
                            break;
                        case '"':
                            _isGet = true;
                            return;
                        default:
                            break;
                    }
                }
            }
            private string GetStringDoubleQuote()
            {
                Debug.Assert(_token == Token.StringDoubleQuote);
                Debug.Assert(!_isGet);
                unsafe
                {
                    var @char = PeekChar();
                    if (@char == -1)
                        throw new FormatException("EOF");
                    var ch = (char)@char;
                    var start = _position++;
                    for (; ; )
                    {
                        if (ch == '\\')
                        {
                            break;
                        }
                        else if (ch == '"')
                        {
                            _isGet = true;
                            return new string(_segment, start, _position - start - 1);
                        }
                        if (_position == _length)
                            break;
                        ch = _segment[_position++];
                    }
                    var sb = StringExtensions.ThreadRent(out var disposable);
                    try
                    {
                        for (; ; )
                        {
                            if (ch == '\\')
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start - 1));
                                for (; ; )
                                {
                                    @char = ReadChar();
                                    switch (@char)
                                    {
                                        case -1:
                                            throw new FormatException("EOF");
                                        case '0':
                                            sb.Write('\u0000');
                                            break;
                                        case '\'':
                                            sb.Write('\'');
                                            break;
                                        case '"':
                                            sb.Write('"');
                                            break;
                                        case '\\':
                                            sb.Write('\\');
                                            break;
                                        case '/':
                                            sb.Write('/');
                                            break;
                                        case 'r':
                                            sb.Write('\r');
                                            break;
                                        case 'n':
                                            sb.Write('\n');
                                            break;
                                        case 't':
                                            sb.Write('\t');
                                            break;
                                        case 'v':
                                            sb.Write('\v');
                                            break;
                                        case 'b':
                                            sb.Write('\b');
                                            break;
                                        case 'f':
                                            sb.Write('\f');
                                            break;
                                        case 'u':
                                            sb.Write(GetUnicode());
                                            break;
                                        default:
                                            throw new NotSupportedException("\\" + (char)@char);
                                    }
                                    @char = PeekChar();
                                    if (@char == -1)
                                        throw new FormatException("EOF");
                                    if (@char == '\\')
                                    {
                                        _position++;
                                    }
                                    else
                                    {
                                        ch = (char)@char;
                                        start = _position;
                                        _position++;
                                        break;
                                    }
                                }
                            }
                            if (ch == '"')
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start - 1));
                                _isGet = true;
                                return sb.ToString();
                            }
                            if (_position == _length)
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start));
                                start = 0;
                                @char = ReadChar();
                                if (@char == -1)
                                    throw new FormatException("EOF");
                                ch = (char)@char;
                            }
                            else
                            {
                                ch = _segment[_position++];
                            }
                        }
                    }
                    finally
                    {
                        disposable.Dispose();
                    }
                }
            }
            private void GetStringDoubleQuote(out ReadOnlySpan<char> value)
            {
                Debug.Assert(_token == Token.StringDoubleQuote);
                Debug.Assert(!_isGet);
                unsafe
                {
                    var @char = PeekChar();
                    if (@char == -1)
                        throw new FormatException("EOF");
                    var ch = (char)@char;
                    var start = _position++;
                    for (; ; )
                    {
                        if (ch == '\\')
                        {
                            break;
                        }
                        else if (ch == '"')
                        {
                            value = new ReadOnlySpan<char>(_segment + start, _position - start - 1);
                            _isGet = true;
                            return;
                        }
                        if (_position == _length)
                            break;
                        ch = _segment[_position++];
                    }
                    var sb = StringExtensions.ThreadRent(out var disposable);
                    try
                    {
                        for (; ; )
                        {
                            if (ch == '\\')
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start - 1));
                                for (; ; )
                                {
                                    @char = ReadChar();
                                    switch (@char)
                                    {
                                        case -1:
                                            throw new FormatException("EOF");
                                        case '0':
                                            sb.Write('\u0000');
                                            break;
                                        case '\'':
                                            sb.Write('\'');
                                            break;
                                        case '"':
                                            sb.Write('"');
                                            break;
                                        case '\\':
                                            sb.Write('\\');
                                            break;
                                        case '/':
                                            sb.Write('/');
                                            break;
                                        case 'r':
                                            sb.Write('\r');
                                            break;
                                        case 'n':
                                            sb.Write('\n');
                                            break;
                                        case 't':
                                            sb.Write('\t');
                                            break;
                                        case 'v':
                                            sb.Write('\v');
                                            break;
                                        case 'b':
                                            sb.Write('\b');
                                            break;
                                        case 'f':
                                            sb.Write('\f');
                                            break;
                                        case 'u':
                                            sb.Write(GetUnicode());
                                            break;
                                        default:
                                            throw new NotSupportedException("\\" + (char)@char);
                                    }
                                    @char = PeekChar();
                                    if (@char == -1)
                                        throw new FormatException("EOF");
                                    if (@char == '\\')
                                    {
                                        _position++;
                                    }
                                    else
                                    {
                                        ch = (char)@char;
                                        start = _position;
                                        _position++;
                                        break;
                                    }
                                }
                            }
                            if (ch == '"')
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start - 1));
                                _isGet = true;
                                value = sb.ToString();
                                return;
                            }
                            if (_position == _length)
                            {
                                sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start));
                                start = 0;
                                @char = ReadChar();
                                if (@char == -1)
                                    throw new FormatException("EOF");
                                ch = (char)@char;
                            }
                            else
                            {
                                ch = _segment[_position++];
                            }
                        }
                    }
                    finally
                    {
                        disposable.Dispose();
                    }
                }
            }
            public override string GetString()
            {
                if (_token == Token.Null)
                    return null;
                if (_isGet)
                    throw new InvalidOperationException(nameof(GetString));

                switch (_token)
                {
                    case Token.StringQuote:
                        return GetStringQuote();
                    case Token.StringDoubleQuote:
                        return GetStringDoubleQuote();
                    default:
                        throw new InvalidOperationException(nameof(GetString));
                }
            }
            public override void GetString(out ReadOnlySpan<char> value)
            {
                if (_token == Token.Null)
                {
                    value = null;
                    return;
                }
                if (_isGet)
                    throw new InvalidOperationException(nameof(GetString));

                switch (_token)
                {
                    case Token.StringQuote:
                        GetStringQuote(out value);
                        break;
                    case Token.StringDoubleQuote:
                        GetStringDoubleQuote(out value);
                        break;
                    default:
                        throw new InvalidOperationException(nameof(GetString));
                }
            }
            public override byte GetByte()
            {
                GetNumber(out var number);
                return byte.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override sbyte GetSByte()
            {
                GetNumber(out var number);
                return sbyte.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override short GetInt16()
            {
                GetNumber(out var number);
                return short.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override ushort GetUInt16()
            {
                GetNumber(out var number);
                return ushort.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override int GetInt32()
            {
                GetNumber(out var number);
                return int.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override uint GetUInt32()
            {
                GetNumber(out var number);
                return uint.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override long GetInt64()
            {
                GetNumber(out var number);
                return long.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override ulong GetUInt64()
            {
                GetNumber(out var number);
                return ulong.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override float GetSingle()
            {
                GetNumber(out var number);
                return float.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override double GetDouble()
            {
                GetNumber(out var number);
                return double.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override decimal GetDecimal()
            {
                GetNumber(out var number);
                return decimal.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            private void SkipNumber()
            {
                Debug.Assert(!_isGet);
                _position++;
                for (; ; )
                {
                    var @char = PeekChar();
                    switch (@char)
                    {
                        case -1:
                        case ',':
                        case '}':
                        case ']':
                            _isGet = true;
                            return;
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                        case '\v':
                        case '\f':
                        case '\u0000':
                        case '\u0085':
                        case '\u2028':
                        case '\u2029':
                        case '\u00A0':
                        case '\uFEFF':
                            _isGet = true;
                            _position += 1;
                            return;
                        default:
                            _position += 1;
                            continue;
                    }
                }
            }
            public override void GetNumber(out ReadOnlySpan<char> value)
            {
                if (_token != Token.Number || _isGet)
                    throw new InvalidOperationException(nameof(GetNumber));

                unsafe
                {
                    var start = _position++;
                    while (_position < _length)
                    {
                        var ch = _segment[_position];
                        switch (ch)
                        {
                            case ',':
                            case '}':
                            case ']':
                                _isGet = true;
                                value = new ReadOnlySpan<char>(_segment + start, _position - start);
                                return;
                            case ' ':
                            case '\t':
                            case '\r':
                            case '\n':
                            case '\v':
                            case '\f':
                            case '\u0000':
                            case '\u0085':
                            case '\u2028':
                            case '\u2029':
                            case '\u00A0':
                            case '\uFEFF':
                                _isGet = true;
                                value = new ReadOnlySpan<char>(_segment + start, _position - start);
                                _position += 1;
                                return;
                            default:
                                _position += 1;
                                continue;
                        }
                    }
                    var sb = StringExtensions.ThreadRent(out var disposable);
                    try
                    {
                        sb.Write(new ReadOnlySpan<char>(_segment + start, _position - start));
                        for (; ; )
                        {
                            var @char = PeekChar();
                            switch (@char)
                            {
                                case -1:
                                case ',':
                                case '}':
                                case ']':
                                    _isGet = true;
                                    value = sb.ToString();
                                    return;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    _position += 1;
                                    _isGet = true;
                                    value = sb.ToString();
                                    return;
                                default:
                                    _position += 1;
                                    sb.Write((char)@char);
                                    continue;
                            }
                        }
                    }
                    finally
                    {
                        disposable.Dispose();
                    }
                }
            }
            public override bool GetBoolean()
            {
                if (_token != Token.Boolean)
                    throw new InvalidOperationException(nameof(GetBoolean));

                return _boolean;
            }
            public override void Skip()
            {
                switch (_token)
                {
                    case Token.None:
                        if (Read())
                        {
                            Skip();
                        }
                        return;
                    case Token.StartArray:
                        for (; ; )
                        {
                            if (!Read())
                                throw new FormatException("EOF");

                            if (_token == Token.EndArray)
                                return;

                            Skip();
                        }
                    case Token.StartObject:
                        for (; ; )
                        {
                            if (!Read())
                                throw new FormatException("EOF");

                            if (_token == Token.EndObject)
                                return;

                            Skip();
                        }
                    case Token.PropertyQuote:
                    case Token.PropertyDoubleQuote:
                    case Token.PropertyWithoutQuote:
                        {
                            if (!Read())
                                throw new FormatException("EOF");

                            Skip();
                            return;
                        }
                    default:
                        return;
                }
            }
            public void Dispose()
            {
                _memoryHandle.Dispose();
            }
        }
        private class TextJson5Reader : JsonReader
        {
            private enum Token : byte
            {
                None,
                StartArray,
                EndArray,
                StartObject,
                EndObject,
                PropertyQuote,
                PropertyDoubleQuote,
                PropertyWithoutQuote,
                StringQuote,
                StringDoubleQuote,
                Number,
                Boolean,
                Null
            }
            private static int _MaxDepth = 64;
            #region private
            private TextReader _reader;
            private char[] _buffer;
            private int _position;//-1=end
            private int _length;
            private int _depth = -1;
            private bool[] _isObject;
            private Token _token;
            private bool _isGet;
            private bool _boolean;
            #endregion
            public TextJson5Reader(TextReader reader, int bufferSize)
            {
                _reader = reader;
                _token = Token.None;
                _isObject = new bool[_MaxDepth];
                _buffer = new char[bufferSize];

                _length = _reader.Read(_buffer, 0, _buffer.Length);
                _position = _length == 0 ? -1 : 0;
            }
            public void Set(TextReader value)
            {
                _reader = value;
                _length = _reader.Read(_buffer, 0, _buffer.Length);
                _position = _length == 0 ? -1 : 0;
            }
            public void Reset()
            {
                _token = Token.None;
                _reader = null;
                _depth = -1;
            }
            private int ReadChar()
            {
                if (_position == -1)
                    return -1;

                if (_position == _length)
                {
                    _length = _reader.Read(_buffer, 0, _buffer.Length);
                    if (_length == 0)
                    {
                        _position = -1;
                        return -1;
                    }
                    _position = 0;
                }
                return _buffer[_position++];
            }
            private int PeekChar()
            {
                if (_position == -1)
                    return -1;

                if (_position == _length)
                {
                    _length = _reader.Read(_buffer, 0, _buffer.Length);
                    if (_length == 0)
                    {
                        _position = -1;
                        return -1;
                    }
                    _position = 0;
                }
                return _buffer[_position];
            }
            private char GetUnicode()
            {
                var unicode = 0;
                for (var i = 1; ;)
                {
                    var ch = ReadChar();
                    switch (ch)
                    {
                        case -1:
                            throw new FormatException("\\uxxxx");
                        case '0':
                            break;
                        case '1':
                            unicode += 1;
                            break;
                        case '2':
                            unicode += 2;
                            break;
                        case '3':
                            unicode += 3;
                            break;
                        case '4':
                            unicode += 4;
                            break;
                        case '5':
                            unicode += 5;
                            break;
                        case '6':
                            unicode += 6;
                            break;
                        case '7':
                            unicode += 7;
                            break;
                        case '8':
                            unicode += 8;
                            break;
                        case '9':
                            unicode += 9;
                            break;
                        case 'a':
                        case 'A':
                            unicode += 10;
                            break;
                        case 'b':
                        case 'B':
                            unicode += 11;
                            break;
                        case 'c':
                        case 'C':
                            unicode += 12;
                            break;
                        case 'd':
                        case 'D':
                            unicode += 13;
                            break;
                        case 'e':
                        case 'E':
                            unicode += 14;
                            break;
                        case 'f':
                        case 'F':
                            unicode += 15;
                            break;
                        default:
                            throw new FormatException("\\uxxxx");
                    }
                    if (i == 4)
                        return (char)unicode;
                    i += 1;
                    unicode <<= 4;
                }
            }
            public override bool Read()
            {
                #region function
                bool Next()
                {
                    if (_depth == -1)
                        return NextValue();

                    if (_isObject[_depth])
                    {
                        for (; ; )
                        {
                            var ch = ReadChar();
                            switch (ch)
                            {
                                case -1:
                                    return false;
                                case ',':
                                    return NextProperty();
                                case '}':
                                    if (!_isObject[_depth--])
                                        throw new FormatException($"Unexpected character '{(char)ch}'");
                                    _token = Token.EndObject;
                                    return true;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                case '/':
                                    SkipComment();
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{(char)ch}'");
                            }
                        }
                    }
                    else
                    {
                        for (; ; )
                        {
                            var ch = ReadChar();
                            switch (ch)
                            {
                                case -1:
                                    return false;
                                case ',':
                                    return NextValue();
                                case ']':
                                    if (_isObject[_depth--])
                                        throw new FormatException($"Unexpected character '{(char)ch}'");
                                    _token = Token.EndArray;
                                    return true;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                case '/':
                                    SkipComment();
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{(char)ch}'");
                            }
                        }
                    }
                }
                bool NextProperty()
                {
                    for (; ; )
                    {
                        var ch = PeekChar();
                        switch (ch)
                        {
                            case -1:
                                return false;
                            case '\'':
                                _position += 1;
                                _isGet = false;
                                _token = Token.PropertyQuote;
                                return true;
                            case '"':
                                _position += 1;
                                _isGet = false;
                                _token = Token.PropertyDoubleQuote;
                                return true;
                            case '}':
                                if (!_isObject[_depth--])
                                    throw new FormatException($"Unexpected character '{(char)ch}'");
                                _position += 1;
                                _token = Token.EndObject;
                                return true;
                            case ' ':
                            case '\t':
                            case '\r':
                            case '\n':
                            case '\v':
                            case '\f':
                            case '\u0000':
                            case '\u0085':
                            case '\u2028':
                            case '\u2029':
                            case '\u00A0':
                            case '\uFEFF':
                                _position += 1;
                                continue;
                            case '/':
                                _position += 1;
                                SkipComment();
                                continue;
                            case '{':
                            case '[':
                            case ']':
                            case ',':
                            case ':':
                                throw new FormatException($"Unexpected character '{(char)ch}'");
                            default:
                                _isGet = false;
                                _token = Token.PropertyWithoutQuote;
                                return true;
                        }
                    }
                }
                bool NextValue()
                {
                    for (; ; )
                    {
                        var ch = PeekChar();
                        switch (ch)
                        {
                            case -1:
                                return false;
                            case '{':
                                _position += 1;
                                _depth += 1;
                                _isObject[_depth] = true;
                                _token = Token.StartObject;
                                return true;
                            case '[':
                                _position += 1;
                                _depth += 1;
                                _isObject[_depth] = false;
                                _token = Token.StartArray;
                                return true;
                            case '\'':
                                _position += 1;
                                _isGet = false;
                                _token = Token.StringQuote;
                                return true;
                            case '"':
                                _position += 1;
                                _isGet = false;
                                _token = Token.StringDoubleQuote;
                                return true;
                            case 'n':
                                _position += 1;
                                if (ReadChar() != 'u' || ReadChar() != 'l' || ReadChar() != 'l')
                                    throw new FormatException("Unexpected character");
                                _token = Token.Null;
                                return true;
                            case 't':
                                _position += 1;
                                if (ReadChar() != 'r' || ReadChar() != 'u' || ReadChar() != 'e')
                                    throw new FormatException("Unexpected character");
                                _boolean = true;
                                _token = Token.Boolean;
                                return true;
                            case 'f':
                                _position += 1;
                                if (ReadChar() != 'a' || ReadChar() != 'l' || ReadChar() != 's' || ReadChar() != 'e')
                                    throw new FormatException("Unexpected character");
                                _boolean = false;
                                _token = Token.Boolean;
                                return true;
                            case '0':
                            case '1':
                            case '2':
                            case '3':
                            case '4':
                            case '5':
                            case '6':
                            case '7':
                            case '8':
                            case '9':
                            case '+':
                            case '-':
                            case '.':
                            case 'N':
                            case 'I':
                                _isGet = false;
                                _token = Token.Number;
                                return true;
                            case ' ':
                            case '\t':
                            case '\r':
                            case '\n':
                            case '\v':
                            case '\f':
                            case '\u0000':
                            case '\u0085':
                            case '\u2028':
                            case '\u2029':
                            case '\u00A0':
                            case '\uFEFF':
                                _position += 1;
                                continue;
                            case '/':
                                _position += 1;
                                SkipComment();
                                continue;
                            case ']':
                                if (_isObject[_depth--])
                                    throw new FormatException($"Unexpected character '{(char)ch}'");
                                _position += 1;
                                _token = Token.EndArray;
                                return true;
                            default:
                                throw new FormatException($"Unexpected character '{(char)ch}'");
                        }
                    }
                }
                #endregion
                switch (_token)
                {
                    case Token.None:
                        return NextValue();
                    case Token.StartObject:
                        return NextProperty();
                    case Token.PropertyQuote:
                        if (!_isGet)
                            SkipPropertyQuote();
                        return NextValue();
                    case Token.PropertyDoubleQuote:
                        if (!_isGet)
                            SkipPropertyDoubleQuote();
                        return NextValue();
                    case Token.PropertyWithoutQuote:
                        if (!_isGet)
                            SkipPropertyWithoutQuote();
                        return NextValue();
                    case Token.StringQuote:
                        if (!_isGet)
                            SkipStringQuote();
                        return Next();
                    case Token.StringDoubleQuote:
                        if (!_isGet)
                            SkipStringDoubleQuote();
                        return Next();
                    case Token.Number:
                        if (!_isGet)
                            SkipNumber();
                        return Next();
                    case Token.Boolean:
                    case Token.Null:
                        return Next();
                    case Token.StartArray:
                        return NextValue();
                    case Token.EndObject:
                    case Token.EndArray:
                        return Next();
                    default:
                        return false;
                }
            }
            public override bool IsStartArray => _token == Token.StartArray;
            public override bool IsEndArray => _token == Token.EndArray;
            public override bool IsStartObject => _token == Token.StartObject;
            public override bool IsEndObject => _token == Token.EndObject;
            public override bool IsProperty => _token == Token.PropertyQuote || _token == Token.PropertyDoubleQuote || _token == Token.PropertyWithoutQuote;
            public override bool IsString => _token == Token.StringQuote || _token == Token.StringDoubleQuote;
            public override bool IsNumber => _token == Token.Number;
            public override bool IsBoolean => _token == Token.Boolean;
            public override bool IsNull => _token == Token.Null;
            private void SkipComment()
            {
                var ch = ReadChar();
                switch (ch)
                {
                    case -1:
                        throw new FormatException("EOF");
                    case '*':
                        for (; ; )
                        {
                            ch = ReadChar();
                            if (ch == -1)
                                throw new FormatException("EOF");

                            if (ch == '*')
                            {
                                for (; ; )
                                {
                                    ch = ReadChar();
                                    switch (ch)
                                    {
                                        case -1:
                                            throw new FormatException("EOF");
                                        case '*':
                                            continue;
                                        case '/':
                                            return;
                                        default:
                                            break;
                                    }
                                    break;
                                }
                            }
                        }
                    case '/':
                        for (; ; )
                        {
                            ch = ReadChar();
                            switch (ch)
                            {
                                case -1:
                                case '\r':
                                case '\n':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                    return;
                                default:
                                    continue;
                            }
                        }
                    default:
                        throw new FormatException($"Unexpected character '{(char)ch}'");
                }
            }
            private void SkipPropertyQuote()
            {
                Debug.Assert(_token == Token.PropertyQuote);
                Debug.Assert(!_isGet);
                for (; ; )
                {
                    var ch = ReadChar();
                    switch (ch)
                    {
                        case -1:
                            throw new FormatException("EOF");
                        case '\\':
                            ch = ReadChar();
                            switch (ch)
                            {
                                case -1:
                                    throw new FormatException("EOF");
                                case '0':
                                case '\'':
                                case '"':
                                case '\\':
                                case '/':
                                case 'r':
                                case 'n':
                                case 't':
                                case 'v':
                                case 'b':
                                case 'f':
                                    break;
                                case 'u':
                                    if (ReadChar() == -1 || ReadChar() == -1 || ReadChar() == -1 || ReadChar() == -1)
                                        throw new FormatException("EOF");
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + (char)ch);
                            }
                            break;
                        case '\'':
                            for (; ; )
                            {
                                ch = ReadChar();
                                switch (ch)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case ':':
                                        _isGet = true;
                                        return;
                                    case ' ':
                                    case '\t':
                                    case '\r':
                                    case '\n':
                                    case '\v':
                                    case '\f':
                                    case '\u0000':
                                    case '\u0085':
                                    case '\u2028':
                                    case '\u2029':
                                    case '\u00A0':
                                    case '\uFEFF':
                                        continue;
                                    case '/':
                                        SkipComment();
                                        continue;
                                    default:
                                        throw new FormatException($"Unexpected character '{(char)ch}'");
                                }
                            }
                        default:
                            break;
                    }
                }
            }
            private string GetPropertyQuote()
            {
                Debug.Assert(_token == Token.PropertyQuote);
                Debug.Assert(!_isGet);
                var @char = PeekChar();
                if (@char == -1)
                    throw new FormatException("EOF");
                var ch = (char)@char;
                var start = _position++;
                for (; ; )
                {
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '\'')
                    {
                        var property = new string(_buffer, start, _position - start - 1);
                        for (; ; )
                        {
                            @char = ReadChar();
                            switch (@char)
                            {
                                case -1:
                                    throw new FormatException("EOF");
                                case ':':
                                    _isGet = true;
                                    return property;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                case '/':
                                    SkipComment();
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{(char)@char}'");
                            }
                        }
                    }
                    if (_position == _length)
                        break;
                    ch = _buffer[_position++];
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        if (ch == '\\')
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start - 1));
                            for (; ; )
                            {
                                @char = ReadChar();
                                switch (@char)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case '0':
                                        sb.Write('\u0000');
                                        break;
                                    case '\'':
                                        sb.Write('\'');
                                        break;
                                    case '"':
                                        sb.Write('"');
                                        break;
                                    case '\\':
                                        sb.Write('\\');
                                        break;
                                    case '/':
                                        sb.Write('/');
                                        break;
                                    case 'r':
                                        sb.Write('\r');
                                        break;
                                    case 'n':
                                        sb.Write('\n');
                                        break;
                                    case 't':
                                        sb.Write('\t');
                                        break;
                                    case 'v':
                                        sb.Write('\v');
                                        break;
                                    case 'b':
                                        sb.Write('\b');
                                        break;
                                    case 'f':
                                        sb.Write('\f');
                                        break;
                                    case 'u':
                                        sb.Write(GetUnicode());
                                        break;
                                    default:
                                        throw new NotSupportedException("\\" + (char)@char);
                                }
                                @char = PeekChar();
                                if (@char == -1)
                                    throw new FormatException("EOF");
                                if (@char == '\\')
                                {
                                    _position++;
                                }
                                else
                                {
                                    ch = (char)@char;
                                    start = _position;
                                    _position++;
                                    break;
                                }
                            }
                        }
                        if (ch == '\'')
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start - 1));
                            for (; ; )
                            {
                                @char = ReadChar();
                                switch (@char)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case ':':
                                        _isGet = true;
                                        return sb.ToString();
                                    case ' ':
                                    case '\t':
                                    case '\r':
                                    case '\n':
                                    case '\v':
                                    case '\f':
                                    case '\u0000':
                                    case '\u0085':
                                    case '\u2028':
                                    case '\u2029':
                                    case '\u00A0':
                                    case '\uFEFF':
                                        continue;
                                    case '/':
                                        SkipComment();
                                        continue;
                                    default:
                                        throw new FormatException($"Unexpected character '{ch}'");
                                }
                            }
                        }
                        if (_position == _length)
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start));
                            start = 0;
                            @char = ReadChar();
                            if (@char == -1)
                                throw new FormatException("EOF");
                            ch = (char)@char;
                        }
                        else
                        {
                            ch = _buffer[_position++];
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            private void GetPropertyQuote(out ReadOnlySpan<char> property)
            {
                Debug.Assert(_token == Token.PropertyQuote);
                Debug.Assert(!_isGet);
                unsafe
                {
                    var @char = PeekChar();
                    if (@char == -1)
                        throw new FormatException("EOF");
                    var ch = (char)@char;
                    var start = _position++;
                    for (; ; )
                    {
                        if (ch == '\\')
                        {
                            break;
                        }
                        else if (ch == '\'')
                        {
                            property = new ReadOnlySpan<char>(_buffer, start, _position - start - 1);
                            for (; ; )
                            {
                                @char = ReadChar();
                                switch (@char)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case ':':
                                        _isGet = true;
                                        return;
                                    case ' ':
                                    case '\t':
                                    case '\r':
                                    case '\n':
                                    case '\v':
                                    case '\f':
                                    case '\u0000':
                                    case '\u0085':
                                    case '\u2028':
                                    case '\u2029':
                                    case '\u00A0':
                                    case '\uFEFF':
                                        continue;
                                    case '/':
                                        SkipComment();
                                        continue;
                                    default:
                                        throw new FormatException($"Unexpected character '{(char)@char}'");
                                }
                            }
                        }
                        if (_position == _length)
                            break;
                        ch = _buffer[_position++];
                    }
                    var sb = StringExtensions.ThreadRent(out var disposable);
                    try
                    {
                        for (; ; )
                        {
                            if (ch == '\\')
                            {
                                sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start - 1));
                                for (; ; )
                                {
                                    @char = ReadChar();
                                    switch (@char)
                                    {
                                        case -1:
                                            throw new FormatException("EOF");
                                        case '0':
                                            sb.Write('\u0000');
                                            break;
                                        case '\'':
                                            sb.Write('\'');
                                            break;
                                        case '"':
                                            sb.Write('"');
                                            break;
                                        case '\\':
                                            sb.Write('\\');
                                            break;
                                        case '/':
                                            sb.Write('/');
                                            break;
                                        case 'r':
                                            sb.Write('\r');
                                            break;
                                        case 'n':
                                            sb.Write('\n');
                                            break;
                                        case 't':
                                            sb.Write('\t');
                                            break;
                                        case 'v':
                                            sb.Write('\v');
                                            break;
                                        case 'b':
                                            sb.Write('\b');
                                            break;
                                        case 'f':
                                            sb.Write('\f');
                                            break;
                                        case 'u':
                                            sb.Write(GetUnicode());
                                            break;
                                        default:
                                            throw new NotSupportedException("\\" + (char)@char);
                                    }
                                    @char = PeekChar();
                                    if (@char == -1)
                                        throw new FormatException("EOF");
                                    if (@char == '\\')
                                    {
                                        _position++;
                                    }
                                    else
                                    {
                                        ch = (char)@char;
                                        start = _position;
                                        _position++;
                                        break;
                                    }
                                }
                            }
                            if (ch == '\'')
                            {
                                sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start - 1));
                                property = sb.ToString();
                                for (; ; )
                                {
                                    @char = ReadChar();
                                    switch (@char)
                                    {
                                        case -1:
                                            throw new FormatException("EOF");
                                        case ':':
                                            _isGet = true;
                                            return;
                                        case ' ':
                                        case '\t':
                                        case '\r':
                                        case '\n':
                                        case '\v':
                                        case '\f':
                                        case '\u0000':
                                        case '\u0085':
                                        case '\u2028':
                                        case '\u2029':
                                        case '\u00A0':
                                        case '\uFEFF':
                                            continue;
                                        case '/':
                                            SkipComment();
                                            continue;
                                        default:
                                            throw new FormatException($"Unexpected character '{ch}'");
                                    }
                                }
                            }
                            if (_position == _length)
                            {
                                sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start));
                                start = 0;
                                @char = ReadChar();
                                if (@char == -1)
                                    throw new FormatException("EOF");
                                ch = (char)@char;
                            }
                            else
                            {
                                ch = _buffer[_position++];
                            }
                        }
                    }
                    finally
                    {
                        disposable.Dispose();
                    }
                }
            }
            private void SkipPropertyDoubleQuote()
            {
                Debug.Assert(_token == Token.PropertyDoubleQuote);
                Debug.Assert(!_isGet);
                for (; ; )
                {
                    var ch = ReadChar();
                    switch (ch)
                    {
                        case -1:
                            throw new FormatException("EOF");
                        case '\\':
                            ch = ReadChar();
                            switch (ch)
                            {
                                case -1:
                                    throw new FormatException("EOF");
                                case '0':
                                case '\'':
                                case '"':
                                case '\\':
                                case '/':
                                case 'r':
                                case 'n':
                                case 't':
                                case 'v':
                                case 'b':
                                case 'f':
                                    break;
                                case 'u':
                                    if (ReadChar() == -1 || ReadChar() == -1 || ReadChar() == -1 || ReadChar() == -1)
                                        throw new FormatException("EOF");
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + (char)ch);
                            }
                            break;
                        case '"':
                            for (; ; )
                            {
                                ch = ReadChar();
                                switch (ch)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case ':':
                                        _isGet = true;
                                        return;
                                    case ' ':
                                    case '\t':
                                    case '\r':
                                    case '\n':
                                    case '\v':
                                    case '\f':
                                    case '\u0000':
                                    case '\u0085':
                                    case '\u2028':
                                    case '\u2029':
                                    case '\u00A0':
                                    case '\uFEFF':
                                        continue;
                                    case '/':
                                        SkipComment();
                                        continue;
                                    default:
                                        throw new FormatException($"Unexpected character '{(char)ch}'");
                                }
                            }
                        default:
                            break;
                    }
                }
            }
            private string GetPropertyDoubleQuote()
            {
                Debug.Assert(_token == Token.PropertyDoubleQuote);
                Debug.Assert(!_isGet);
                var @char = PeekChar();
                if (@char == -1)
                    throw new FormatException("EOF");
                var ch = (char)@char;
                var start = _position++;
                for (; ; )
                {
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '"')
                    {
                        var property = new string(_buffer, start, _position - start - 1);
                        for (; ; )
                        {
                            @char = ReadChar();
                            switch (@char)
                            {
                                case -1:
                                    throw new FormatException("EOF");
                                case ':':
                                    _isGet = true;
                                    return property;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                case '/':
                                    SkipComment();
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{(char)@char}'");
                            }
                        }
                    }
                    if (_position == _length)
                        break;
                    ch = _buffer[_position++];
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        if (ch == '\\')
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start - 1));
                            for (; ; )
                            {
                                @char = ReadChar();
                                switch (@char)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case '0':
                                        sb.Write('\u0000');
                                        break;
                                    case '\'':
                                        sb.Write('\'');
                                        break;
                                    case '"':
                                        sb.Write('"');
                                        break;
                                    case '\\':
                                        sb.Write('\\');
                                        break;
                                    case '/':
                                        sb.Write('/');
                                        break;
                                    case 'r':
                                        sb.Write('\r');
                                        break;
                                    case 'n':
                                        sb.Write('\n');
                                        break;
                                    case 't':
                                        sb.Write('\t');
                                        break;
                                    case 'v':
                                        sb.Write('\v');
                                        break;
                                    case 'b':
                                        sb.Write('\b');
                                        break;
                                    case 'f':
                                        sb.Write('\f');
                                        break;
                                    case 'u':
                                        sb.Write(GetUnicode());
                                        break;
                                    default:
                                        throw new NotSupportedException("\\" + (char)@char);
                                }
                                @char = PeekChar();
                                if (@char == -1)
                                    throw new FormatException("EOF");
                                if (@char == '\\')
                                {
                                    _position++;
                                }
                                else
                                {
                                    ch = (char)@char;
                                    start = _position;
                                    _position++;
                                    break;
                                }
                            }
                        }
                        if (ch == '"')
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start - 1));
                            for (; ; )
                            {
                                @char = ReadChar();
                                switch (@char)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case ':':
                                        _isGet = true;
                                        return sb.ToString();
                                    case ' ':
                                    case '\t':
                                    case '\r':
                                    case '\n':
                                    case '\v':
                                    case '\f':
                                    case '\u0000':
                                    case '\u0085':
                                    case '\u2028':
                                    case '\u2029':
                                    case '\u00A0':
                                    case '\uFEFF':
                                        continue;
                                    case '/':
                                        SkipComment();
                                        continue;
                                    default:
                                        throw new FormatException($"Unexpected character '{ch}'");
                                }
                            }
                        }
                        if (_position == _length)
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start));
                            start = 0;
                            @char = ReadChar();
                            if (@char == -1)
                                throw new FormatException("EOF");
                            ch = (char)@char;
                        }
                        else
                        {
                            ch = _buffer[_position++];
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            private void GetPropertyDoubleQuote(out ReadOnlySpan<char> property)
            {
                Debug.Assert(_token == Token.PropertyDoubleQuote);
                Debug.Assert(!_isGet);
                var @char = PeekChar();
                if (@char == -1)
                    throw new FormatException("EOF");
                var ch = (char)@char;
                var start = _position++;
                for (; ; )
                {
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '"')
                    {
                        property = new ReadOnlySpan<char>(_buffer, start, _position - start - 1);
                        for (; ; )
                        {
                            @char = ReadChar();
                            switch (@char)
                            {
                                case -1:
                                    throw new FormatException("EOF");
                                case ':':
                                    _isGet = true;
                                    return;
                                case ' ':
                                case '\t':
                                case '\r':
                                case '\n':
                                case '\v':
                                case '\f':
                                case '\u0000':
                                case '\u0085':
                                case '\u2028':
                                case '\u2029':
                                case '\u00A0':
                                case '\uFEFF':
                                    continue;
                                case '/':
                                    SkipComment();
                                    continue;
                                default:
                                    throw new FormatException($"Unexpected character '{(char)@char}'");
                            }
                        }
                    }
                    if (_position == _length)
                        break;
                    ch = _buffer[_position++];
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        if (ch == '\\')
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start - 1));
                            for (; ; )
                            {
                                @char = ReadChar();
                                switch (@char)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case '0':
                                        sb.Write('\u0000');
                                        break;
                                    case '\'':
                                        sb.Write('\'');
                                        break;
                                    case '"':
                                        sb.Write('"');
                                        break;
                                    case '\\':
                                        sb.Write('\\');
                                        break;
                                    case '/':
                                        sb.Write('/');
                                        break;
                                    case 'r':
                                        sb.Write('\r');
                                        break;
                                    case 'n':
                                        sb.Write('\n');
                                        break;
                                    case 't':
                                        sb.Write('\t');
                                        break;
                                    case 'v':
                                        sb.Write('\v');
                                        break;
                                    case 'b':
                                        sb.Write('\b');
                                        break;
                                    case 'f':
                                        sb.Write('\f');
                                        break;
                                    case 'u':
                                        sb.Write(GetUnicode());
                                        break;
                                    default:
                                        throw new NotSupportedException("\\" + (char)@char);
                                }
                                @char = PeekChar();
                                if (@char == -1)
                                    throw new FormatException("EOF");
                                if (@char == '\\')
                                {
                                    _position++;
                                }
                                else
                                {
                                    ch = (char)@char;
                                    start = _position;
                                    _position++;
                                    break;
                                }
                            }
                        }
                        if (ch == '"')
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start - 1));
                            property = sb.ToString();
                            for (; ; )
                            {
                                @char = ReadChar();
                                switch (@char)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case ':':
                                        _isGet = true;
                                        return;
                                    case ' ':
                                    case '\t':
                                    case '\r':
                                    case '\n':
                                    case '\v':
                                    case '\f':
                                    case '\u0000':
                                    case '\u0085':
                                    case '\u2028':
                                    case '\u2029':
                                    case '\u00A0':
                                    case '\uFEFF':
                                        continue;
                                    case '/':
                                        SkipComment();
                                        continue;
                                    default:
                                        throw new FormatException($"Unexpected character '{ch}'");
                                }
                            }
                        }
                        if (_position == _length)
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start));
                            start = 0;
                            @char = ReadChar();
                            if (@char == -1)
                                throw new FormatException("EOF");
                            ch = (char)@char;
                        }
                        else
                        {
                            ch = _buffer[_position++];
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            private void SkipPropertyWithoutQuote()
            {
                Debug.Assert(_token == Token.PropertyWithoutQuote);
                Debug.Assert(!_isGet);
                _position += 1;
                for (; ; )
                {
                    var ch = ReadChar();
                    switch (ch)
                    {
                        case -1:
                            throw new FormatException("EOF");
                        case ':':
                            _isGet = true;
                            return;
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                        case '\v':
                        case '\f':
                        case '\u0000':
                        case '\u0085':
                        case '\u2028':
                        case '\u2029':
                        case '\u00A0':
                        case '\uFEFF':
                            for (; ; )
                            {
                                ch = ReadChar();
                                switch (ch)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case ':':
                                        _isGet = true;
                                        return;
                                    case ' ':
                                    case '\t':
                                    case '\r':
                                    case '\n':
                                    case '\v':
                                    case '\f':
                                    case '\u0000':
                                    case '\u0085':
                                    case '\u2028':
                                    case '\u2029':
                                    case '\u00A0':
                                    case '\uFEFF':
                                        continue;
                                    case '/':
                                        SkipComment();
                                        continue;
                                    default:
                                        throw new FormatException($"Unexpected character '{ch}'");
                                }
                            }
                        default:
                            break;
                    }
                }
            }
            private string GetPropertyWithoutQuote()
            {
                Debug.Assert(_token == Token.PropertyWithoutQuote);
                Debug.Assert(!_isGet);
                var @char = _buffer[_position];
                var start = _position++;
                for (; ; )
                {
                    if (_position == _length)
                        break;
                    switch (@char)
                    {
                        case ':':
                            _isGet = true;
                            return new string(_buffer, start, _position - start - 1);
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                        case '\v':
                        case '\f':
                        case '\u0000':
                        case '\u0085':
                        case '\u2028':
                        case '\u2029':
                        case '\u00A0':
                        case '\uFEFF':
                            {
                                var property = new string(_buffer, start, _position - start - 1);
                                for (; ; )
                                {
                                    var ch = ReadChar();
                                    switch (ch)
                                    {
                                        case -1:
                                            throw new FormatException("EOF");
                                        case ':':
                                            _isGet = true;
                                            return property;
                                        case ' ':
                                        case '\t':
                                        case '\r':
                                        case '\n':
                                        case '\v':
                                        case '\f':
                                        case '\u0000':
                                        case '\u0085':
                                        case '\u2028':
                                        case '\u2029':
                                        case '\u00A0':
                                        case '\uFEFF':
                                            continue;
                                        case '/':
                                            SkipComment();
                                            continue;
                                        default:
                                            throw new FormatException($"Unexpected character '{ch}'");
                                    }
                                }
                            }
                        default:
                            break;
                    }
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        switch (@char)
                        {
                            case ':':
                                _isGet = true;
                                sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start - 1));
                                return sb.ToString();
                            case ' ':
                            case '\t':
                            case '\r':
                            case '\n':
                            case '\v':
                            case '\f':
                            case '\u0000':
                            case '\u0085':
                            case '\u2028':
                            case '\u2029':
                            case '\u00A0':
                            case '\uFEFF':
                                {
                                    sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start - 1));
                                    for (; ; )
                                    {
                                        var ch = ReadChar();
                                        switch (ch)
                                        {
                                            case -1:
                                                throw new FormatException("EOF");
                                            case ':':
                                                _isGet = true;
                                                return sb.ToString();
                                            case ' ':
                                            case '\t':
                                            case '\r':
                                            case '\n':
                                            case '\v':
                                            case '\f':
                                            case '\u0000':
                                            case '\u0085':
                                            case '\u2028':
                                            case '\u2029':
                                            case '\u00A0':
                                            case '\uFEFF':
                                                continue;
                                            case '/':
                                                SkipComment();
                                                continue;
                                            default:
                                                throw new FormatException($"Unexpected character '{ch}'");
                                        }
                                    }
                                }
                            default:
                                break;
                        }
                        if (_position == _length)
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start));
                            start = 0;
                            var ch = ReadChar();
                            if (ch == -1)
                                throw new FormatException("EOF");
                            @char = (char)ch;
                        }
                        else
                        {
                            @char = _buffer[_position++];
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            private void GetPropertyWithoutQuote(out ReadOnlySpan<char> property)
            {
                Debug.Assert(_token == Token.PropertyWithoutQuote);
                Debug.Assert(!_isGet);
                var @char = _buffer[_position];
                var start = _position++;
                for (; ; )
                {
                    if (_position == _length)
                        break;
                    switch (@char)
                    {
                        case ':':
                            _isGet = true;
                            property = new ReadOnlySpan<char>(_buffer, start, _position - start - 1);
                            return;
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                        case '\v':
                        case '\f':
                        case '\u0000':
                        case '\u0085':
                        case '\u2028':
                        case '\u2029':
                        case '\u00A0':
                        case '\uFEFF':
                            {
                                property = new ReadOnlySpan<char>(_buffer, start, _position - start - 1);
                                for (; ; )
                                {
                                    var ch = ReadChar();
                                    switch (ch)
                                    {
                                        case -1:
                                            throw new FormatException("EOF");
                                        case ':':
                                            _isGet = true;
                                            return;
                                        case ' ':
                                        case '\t':
                                        case '\r':
                                        case '\n':
                                        case '\v':
                                        case '\f':
                                        case '\u0000':
                                        case '\u0085':
                                        case '\u2028':
                                        case '\u2029':
                                        case '\u00A0':
                                        case '\uFEFF':
                                            continue;
                                        case '/':
                                            SkipComment();
                                            continue;
                                        default:
                                            throw new FormatException($"Unexpected character '{ch}'");
                                    }
                                }
                            }
                        default:
                            break;
                    }
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        switch (@char)
                        {
                            case ':':
                                _isGet = true;
                                sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start - 1));
                                property = sb.ToString();
                                return;
                            case ' ':
                            case '\t':
                            case '\r':
                            case '\n':
                            case '\v':
                            case '\f':
                            case '\u0000':
                            case '\u0085':
                            case '\u2028':
                            case '\u2029':
                            case '\u00A0':
                            case '\uFEFF':
                                {
                                    _isGet = true;
                                    sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start - 1));
                                    for (; ; )
                                    {
                                        var ch = ReadChar();
                                        switch (ch)
                                        {
                                            case -1:
                                                throw new FormatException("EOF");
                                            case ':':
                                                property = sb.ToString();
                                                return;
                                            case ' ':
                                            case '\t':
                                            case '\r':
                                            case '\n':
                                            case '\v':
                                            case '\f':
                                            case '\u0000':
                                            case '\u0085':
                                            case '\u2028':
                                            case '\u2029':
                                            case '\u00A0':
                                            case '\uFEFF':
                                                continue;
                                            case '/':
                                                SkipComment();
                                                continue;
                                            default:
                                                throw new FormatException($"Unexpected character '{ch}'");
                                        }
                                    }
                                }
                            default:
                                break;
                        }
                        if (_position == _length)
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start));
                            start = 0;
                            var ch = ReadChar();
                            if (ch == -1)
                                throw new FormatException("EOF");
                            @char = (char)ch;
                        }
                        else
                        {
                            @char = _buffer[_position++];
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override string GetProperty()
            {
                if (_isGet)
                    throw new InvalidOperationException(nameof(GetProperty));

                switch (_token)
                {
                    case Token.PropertyQuote:
                        return GetPropertyQuote();
                    case Token.PropertyDoubleQuote:
                        return GetPropertyDoubleQuote();
                    case Token.PropertyWithoutQuote:
                        return GetPropertyWithoutQuote();
                    default:
                        throw new InvalidOperationException(nameof(GetProperty));
                }
            }
            public override void GetProperty(out ReadOnlySpan<char> property)
            {
                if (_isGet)
                    throw new InvalidOperationException(nameof(GetProperty));

                switch (_token)
                {
                    case Token.PropertyQuote:
                        GetPropertyQuote(out property);
                        break;
                    case Token.PropertyDoubleQuote:
                        GetPropertyDoubleQuote(out property);
                        break;
                    case Token.PropertyWithoutQuote:
                        GetPropertyWithoutQuote(out property);
                        break;
                    default:
                        throw new InvalidOperationException(nameof(GetProperty));
                }
            }
            private void SkipStringQuote()
            {
                Debug.Assert(_token == Token.StringQuote);
                Debug.Assert(!_isGet);
                for (; ; )
                {
                    var ch = ReadChar();
                    switch (ch)
                    {
                        case -1:
                            throw new FormatException("EOF");
                        case '\\':
                            ch = ReadChar();
                            switch (ch)
                            {
                                case -1:
                                    throw new FormatException("EOF");
                                case '0':
                                case '\'':
                                case '"':
                                case '\\':
                                case '/':
                                case 'r':
                                case 'n':
                                case 't':
                                case 'v':
                                case 'b':
                                case 'f':
                                    break;
                                case 'u':
                                    if (ReadChar() == -1 || ReadChar() == -1 || ReadChar() == -1 || ReadChar() == -1)
                                        throw new FormatException("EOF");
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + (char)ch);
                            }
                            break;
                        case '\'':
                            _isGet = true;
                            return;
                        default:
                            break;
                    }
                }
            }
            private string GetStringQuote()
            {
                Debug.Assert(_token == Token.StringQuote);
                Debug.Assert(!_isGet);
                var @char = PeekChar();
                if (@char == -1)
                    throw new FormatException("EOF");
                var ch = (char)@char;
                var start = _position++;
                for (; ; )
                {
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '\'')
                    {
                        var property = new string(_buffer, start, _position - start - 1);
                        _isGet = true;
                        return property;
                    }
                    if (_position == _length)
                        break;
                    ch = _buffer[_position++];
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        if (ch == '\\')
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start - 1));
                            for (; ; )
                            {
                                @char = ReadChar();
                                switch (@char)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case '0':
                                        sb.Write('\u0000');
                                        break;
                                    case '\'':
                                        sb.Write('\'');
                                        break;
                                    case '"':
                                        sb.Write('"');
                                        break;
                                    case '\\':
                                        sb.Write('\\');
                                        break;
                                    case '/':
                                        sb.Write('/');
                                        break;
                                    case 'r':
                                        sb.Write('\r');
                                        break;
                                    case 'n':
                                        sb.Write('\n');
                                        break;
                                    case 't':
                                        sb.Write('\t');
                                        break;
                                    case 'v':
                                        sb.Write('\v');
                                        break;
                                    case 'b':
                                        sb.Write('\b');
                                        break;
                                    case 'f':
                                        sb.Write('\f');
                                        break;
                                    case 'u':
                                        sb.Write(GetUnicode());
                                        break;
                                    default:
                                        throw new NotSupportedException("\\" + (char)@char);
                                }
                                @char = PeekChar();
                                if (@char == -1)
                                    throw new FormatException("EOF");
                                if (@char == '\\')
                                {
                                    _position++;
                                }
                                else
                                {
                                    ch = (char)@char;
                                    start = _position;
                                    _position++;
                                    break;
                                }
                            }
                        }
                        if (ch == '\'')
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start - 1));
                            _isGet = true;
                            return sb.ToString();
                        }
                        if (_position == _length)
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start));
                            start = 0;
                            @char = ReadChar();
                            if (@char == -1)
                                throw new FormatException("EOF");
                            ch = (char)@char;
                        }
                        else
                        {
                            ch = _buffer[_position++];
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            private void GetStringQuote(out ReadOnlySpan<char> value)
            {
                Debug.Assert(_token == Token.StringQuote);
                Debug.Assert(!_isGet);
                var @char = PeekChar();
                if (@char == -1)
                    throw new FormatException("EOF");
                var ch = (char)@char;
                var start = _position++;
                for (; ; )
                {
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '\'')
                    {
                        value = new ReadOnlySpan<char>(_buffer, start, _position - start - 1);
                        _isGet = true;
                        return;
                    }
                    if (_position == _length)
                        break;
                    ch = _buffer[_position++];
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        if (ch == '\\')
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start - 1));
                            for (; ; )
                            {
                                @char = ReadChar();
                                switch (@char)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case '0':
                                        sb.Write('\u0000');
                                        break;
                                    case '\'':
                                        sb.Write('\'');
                                        break;
                                    case '"':
                                        sb.Write('"');
                                        break;
                                    case '\\':
                                        sb.Write('\\');
                                        break;
                                    case '/':
                                        sb.Write('/');
                                        break;
                                    case 'r':
                                        sb.Write('\r');
                                        break;
                                    case 'n':
                                        sb.Write('\n');
                                        break;
                                    case 't':
                                        sb.Write('\t');
                                        break;
                                    case 'v':
                                        sb.Write('\v');
                                        break;
                                    case 'b':
                                        sb.Write('\b');
                                        break;
                                    case 'f':
                                        sb.Write('\f');
                                        break;
                                    case 'u':
                                        sb.Write(GetUnicode());
                                        break;
                                    default:
                                        throw new NotSupportedException("\\" + (char)@char);
                                }
                                @char = PeekChar();
                                if (@char == -1)
                                    throw new FormatException("EOF");
                                if (@char == '\\')
                                {
                                    _position++;
                                }
                                else
                                {
                                    ch = (char)@char;
                                    start = _position;
                                    _position++;
                                    break;
                                }
                            }
                        }
                        if (ch == '\'')
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start - 1));
                            value = sb.ToString();
                            _isGet = true;
                            return;
                        }
                        if (_position == _length)
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start));
                            start = 0;
                            @char = ReadChar();
                            if (@char == -1)
                                throw new FormatException("EOF");
                            ch = (char)@char;
                        }
                        else
                        {
                            ch = _buffer[_position++];
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            private void SkipStringDoubleQuote()
            {
                Debug.Assert(_token == Token.StringDoubleQuote);
                Debug.Assert(!_isGet);
                for (; ; )
                {
                    var ch = ReadChar();
                    switch (ch)
                    {
                        case -1:
                            throw new FormatException("EOF");
                        case '\\':
                            ch = ReadChar();
                            switch (ch)
                            {
                                case -1:
                                    throw new FormatException("EOF");
                                case '0':
                                case '\'':
                                case '"':
                                case '\\':
                                case '/':
                                case 'r':
                                case 'n':
                                case 't':
                                case 'v':
                                case 'b':
                                case 'f':
                                    break;
                                case 'u':
                                    if (ReadChar() == -1 || ReadChar() == -1 || ReadChar() == -1 || ReadChar() == -1)
                                        throw new FormatException("EOF");
                                    break;
                                default:
                                    throw new NotSupportedException("\\" + (char)ch);
                            }
                            break;
                        case '"':
                            _isGet = true;
                            return;
                        default:
                            break;
                    }
                }
            }
            private string GetStringDoubleQuote()
            {
                Debug.Assert(_token == Token.StringDoubleQuote);
                Debug.Assert(!_isGet);
                var @char = PeekChar();
                if (@char == -1)
                    throw new FormatException("EOF");
                var ch = (char)@char;
                var start = _position++;
                for (; ; )
                {
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '"')
                    {
                        _isGet = true;
                        return new string(_buffer, start, _position - start - 1);
                    }
                    if (_position == _length)
                        break;
                    ch = _buffer[_position++];
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        if (ch == '\\')
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start - 1));
                            for (; ; )
                            {
                                @char = ReadChar();
                                switch (@char)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case '0':
                                        sb.Write('\u0000');
                                        break;
                                    case '\'':
                                        sb.Write('\'');
                                        break;
                                    case '"':
                                        sb.Write('"');
                                        break;
                                    case '\\':
                                        sb.Write('\\');
                                        break;
                                    case '/':
                                        sb.Write('/');
                                        break;
                                    case 'r':
                                        sb.Write('\r');
                                        break;
                                    case 'n':
                                        sb.Write('\n');
                                        break;
                                    case 't':
                                        sb.Write('\t');
                                        break;
                                    case 'v':
                                        sb.Write('\v');
                                        break;
                                    case 'b':
                                        sb.Write('\b');
                                        break;
                                    case 'f':
                                        sb.Write('\f');
                                        break;
                                    case 'u':
                                        sb.Write(GetUnicode());
                                        break;
                                    default:
                                        throw new NotSupportedException("\\" + (char)@char);
                                }
                                @char = PeekChar();
                                if (@char == -1)
                                    throw new FormatException("EOF");
                                if (@char == '\\')
                                {
                                    _position++;
                                }
                                else
                                {
                                    ch = (char)@char;
                                    start = _position;
                                    _position++;
                                    break;
                                }
                            }
                        }
                        if (ch == '"')
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start - 1));
                            _isGet = true;
                            return sb.ToString();
                        }
                        if (_position == _length)
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start));
                            start = 0;
                            @char = ReadChar();
                            if (@char == -1)
                                throw new FormatException("EOF");
                            ch = (char)@char;
                        }
                        else
                        {
                            ch = _buffer[_position++];
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            private void GetStringDoubleQuote(out ReadOnlySpan<char> value)
            {
                Debug.Assert(_token == Token.StringDoubleQuote);
                Debug.Assert(!_isGet);
                var @char = PeekChar();
                if (@char == -1)
                    throw new FormatException("EOF");
                var ch = (char)@char;
                var start = _position++;
                for (; ; )
                {
                    if (ch == '\\')
                    {
                        break;
                    }
                    else if (ch == '"')
                    {
                        value = new ReadOnlySpan<char>(_buffer, start, _position - start - 1);
                        _isGet = true;
                        return;
                    }
                    if (_position == _length)
                        break;
                    ch = _buffer[_position++];
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    for (; ; )
                    {
                        if (ch == '\\')
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start - 1));
                            for (; ; )
                            {
                                @char = ReadChar();
                                switch (@char)
                                {
                                    case -1:
                                        throw new FormatException("EOF");
                                    case '0':
                                        sb.Write('\u0000');
                                        break;
                                    case '\'':
                                        sb.Write('\'');
                                        break;
                                    case '"':
                                        sb.Write('"');
                                        break;
                                    case '\\':
                                        sb.Write('\\');
                                        break;
                                    case '/':
                                        sb.Write('/');
                                        break;
                                    case 'r':
                                        sb.Write('\r');
                                        break;
                                    case 'n':
                                        sb.Write('\n');
                                        break;
                                    case 't':
                                        sb.Write('\t');
                                        break;
                                    case 'v':
                                        sb.Write('\v');
                                        break;
                                    case 'b':
                                        sb.Write('\b');
                                        break;
                                    case 'f':
                                        sb.Write('\f');
                                        break;
                                    case 'u':
                                        sb.Write(GetUnicode());
                                        break;
                                    default:
                                        throw new NotSupportedException("\\" + (char)@char);
                                }
                                @char = PeekChar();
                                if (@char == -1)
                                    throw new FormatException("EOF");
                                if (@char == '\\')
                                {
                                    _position++;
                                }
                                else
                                {
                                    ch = (char)@char;
                                    start = _position;
                                    _position++;
                                    break;
                                }
                            }
                        }
                        if (ch == '"')
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start - 1));
                            _isGet = true;
                            value = sb.ToString();
                            return;
                        }
                        if (_position == _length)
                        {
                            sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start));
                            start = 0;
                            @char = ReadChar();
                            if (@char == -1)
                                throw new FormatException("EOF");
                            ch = (char)@char;
                        }
                        else
                        {
                            ch = _buffer[_position++];
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override string GetString()
            {
                if (_token == Token.Null)
                    return null;
                if (_isGet)
                    throw new InvalidOperationException(nameof(GetString));

                switch (_token)
                {
                    case Token.StringQuote:
                        return GetStringQuote();
                    case Token.StringDoubleQuote:
                        return GetStringDoubleQuote();
                    default:
                        throw new InvalidOperationException(nameof(GetString));
                }
            }
            public override void GetString(out ReadOnlySpan<char> value)
            {
                if (_token == Token.Null)
                {
                    value = null;
                    return;
                }
                if (_isGet)
                    throw new InvalidOperationException(nameof(GetString));

                switch (_token)
                {
                    case Token.StringQuote:
                        GetStringQuote(out value);
                        break;
                    case Token.StringDoubleQuote:
                        GetStringDoubleQuote(out value);
                        break;
                    default:
                        throw new InvalidOperationException(nameof(GetString));
                }
            }
            public override byte GetByte()
            {
                GetNumber(out var number);
                return byte.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override sbyte GetSByte()
            {
                GetNumber(out var number);
                return sbyte.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override short GetInt16()
            {
                GetNumber(out var number);
                return short.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override ushort GetUInt16()
            {
                GetNumber(out var number);
                return ushort.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override int GetInt32()
            {
                GetNumber(out var number);
                return int.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override uint GetUInt32()
            {
                GetNumber(out var number);
                return uint.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override long GetInt64()
            {
                GetNumber(out var number);
                return long.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override ulong GetUInt64()
            {
                GetNumber(out var number);
                return ulong.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override float GetSingle()
            {
                GetNumber(out var number);
                return float.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override double GetDouble()
            {
                GetNumber(out var number);
                return double.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            public override decimal GetDecimal()
            {
                GetNumber(out var number);
                return decimal.Parse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo);
            }
            private void SkipNumber()
            {
                Debug.Assert(!_isGet);
                _position++;
                for (; ; )
                {
                    var @char = PeekChar();
                    switch (@char)
                    {
                        case -1:
                        case ',':
                        case '}':
                        case ']':
                            _isGet = true;
                            return;
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                        case '\v':
                        case '\f':
                        case '\u0000':
                        case '\u0085':
                        case '\u2028':
                        case '\u2029':
                        case '\u00A0':
                        case '\uFEFF':
                            _isGet = true;
                            _position += 1;
                            return;
                        default:
                            _position += 1;
                            continue;
                    }
                }
            }
            public override void GetNumber(out ReadOnlySpan<char> value)
            {
                if (_token != Token.Number || _isGet)
                    throw new InvalidOperationException(nameof(GetNumber));

                var start = _position++;
                while (_position < _length)
                {
                    var ch = _buffer[_position];
                    switch (ch)
                    {
                        case ',':
                        case '}':
                        case ']':
                            _isGet = true;
                            value = new ReadOnlySpan<char>(_buffer, start, _position - start);
                            return;
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                        case '\v':
                        case '\f':
                        case '\u0000':
                        case '\u0085':
                        case '\u2028':
                        case '\u2029':
                        case '\u00A0':
                        case '\uFEFF':
                            _isGet = true;
                            value = new ReadOnlySpan<char>(_buffer, start, _position - start);
                            _position += 1;
                            return;
                        default:
                            _position += 1;
                            continue;
                    }
                }
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    sb.Write(new ReadOnlySpan<char>(_buffer, start, _position - start));
                    for (; ; )
                    {
                        var @char = PeekChar();
                        switch (@char)
                        {
                            case -1:
                            case ',':
                            case '}':
                            case ']':
                                _isGet = true;
                                value = sb.ToString();
                                return;
                            case ' ':
                            case '\t':
                            case '\r':
                            case '\n':
                            case '\v':
                            case '\f':
                            case '\u0000':
                            case '\u0085':
                            case '\u2028':
                            case '\u2029':
                            case '\u00A0':
                            case '\uFEFF':
                                _position += 1;
                                _isGet = true;
                                value = sb.ToString();
                                return;
                            default:
                                _position += 1;
                                sb.Write((char)@char);
                                continue;
                        }
                    }
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            public override bool GetBoolean()
            {
                if (_token != Token.Boolean)
                    throw new InvalidOperationException(nameof(GetBoolean));

                return _boolean;
            }
            public override void Skip()
            {
                switch (_token)
                {
                    case Token.None:
                        if (Read())
                        {
                            Skip();
                        }
                        return;
                    case Token.StartArray:
                        for (; ; )
                        {
                            if (!Read())
                                throw new FormatException("EOF");

                            if (_token == Token.EndArray)
                                return;

                            Skip();
                        }
                    case Token.StartObject:
                        for (; ; )
                        {
                            if (!Read())
                                throw new FormatException("EOF");

                            if (_token == Token.EndObject)
                                return;

                            Skip();
                        }
                    case Token.PropertyQuote:
                    case Token.PropertyDoubleQuote:
                    case Token.PropertyWithoutQuote:
                        {
                            if (!Read())
                                throw new FormatException("EOF");

                            Skip();
                            return;
                        }
                    default:
                        return;
                }
            }
        }
        #endregion

        #region Dynamic
        private class DynamicValue : DynamicObject
        {
            public static DynamicValue Undefined = new DynamicValue(ValueType.Undefined, null);
            public static DynamicValue Null = new DynamicValue(ValueType.Null, null);

            private static object _True = true;
            private static object _False = false;
            private ValueType _valueType;
            private object _value;
            public enum ValueType : byte
            {
                Undefined,
                Array,
                Object,
                String,
                Number,
                Boolean,
                Null
            }
            public DynamicValue(List<DynamicValue> value)
               : this(ValueType.Array, value)
            { }
            public DynamicValue(Dictionary<string, DynamicValue> value)
                : this(ValueType.Object, value)
            { }
            public DynamicValue(string value)
                : this(ValueType.String, value)
            { }
            public DynamicValue(object number)
               : this(ValueType.Number, number)
            { }
            public DynamicValue(bool value)
                : this(ValueType.Boolean, value ? _True : _False)
            { }
            public DynamicValue(ValueType valueType, object value)
                : base()
            {
                _valueType = valueType;
                _value = value;
            }
            public override IEnumerable<string> GetDynamicMemberNames()
            {
                if (_valueType == ValueType.Object)
                    return ((Dictionary<string, DynamicValue>)_value).Keys;

                return Array.Empty<string>();
            }
            public override bool TryGetMember(GetMemberBinder binder, out object result)
            {
                if (_valueType == ValueType.Object)
                {
                    var value = (Dictionary<string, DynamicValue>)_value;
                    if (value.TryGetValue(binder.Name, out var item))
                    {
                        result = item;
                        return true;
                    }
                }
                result = Undefined;
                return true;
            }
            public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
            {
                if (indexes.Length == 1)
                {
                    var index = indexes[0];
                    if (index is int @int)
                    {
                        if (_valueType == ValueType.Array)
                        {
                            var value = (List<DynamicValue>)_value;
                            if (@int >= 0 && @int < value.Count)
                            {
                                result = value[@int];
                                return true;
                            }
                        }
                    }
                    else if (index is string @string)
                    {
                        if (_valueType == ValueType.Object)
                        {
                            var value = (Dictionary<string, DynamicValue>)_value;
                            if (value.TryGetValue(@string, out var item))
                            {
                                result = item;
                                return true;
                            }
                        }
                    }

                }
                result = Undefined;
                return true;
            }
            public override bool TryConvert(ConvertBinder binder, out object result)
            {
                var type = binder.Type;
                if (_valueType == ValueType.String
                    || _valueType == ValueType.Number
                    || _valueType == ValueType.Boolean)
                {
                    if (Converter.TryConvert(_value, type, out result))
                    {
                        return true;
                    }
                }
                result = type.IsValueType ? Activator.CreateInstance(type) : null;
                return true;
            }
            public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
            {
                switch (binder.Name)
                {
                    case "IsArray":
                        result = _valueType == ValueType.Array ? _True : _False;
                        return true;
                    case "IsObject":
                        result = _valueType == ValueType.Object ? _True : _False;
                        return true;
                    case "IsUndefined":
                        result = _valueType == ValueType.Undefined ? _True : _False;
                        return true;
                    case "IsNull":
                        result = _valueType == ValueType.Null ? _True : _False;
                        return true;
                    case "IsString":
                        result = _valueType == ValueType.String ? _True : _False;
                        return true;
                    case "IsBoolean":
                        result = _valueType == ValueType.Boolean ? _True : _False;
                        return true;
                    case "IsNumber":
                        result = _valueType == ValueType.Number ? _True : _False;
                        return true;
                    case "Count":
                        result = _valueType == ValueType.Array ? ((List<DynamicValue>)_value).Count :
                            _valueType == ValueType.Object ? ((Dictionary<string, DynamicValue>)_value).Count : 0;
                        return true;
                    case "Properties":
                        if (_valueType == ValueType.Object)
                        {
                            var keys = ((Dictionary<string, DynamicValue>)_value).Keys;
                            var properties = new string[keys.Count];
                            keys.CopyTo(properties, 0);
                            result = properties;
                        }
                        else
                        {
                            result = Array.Empty<string>();
                        }
                        return true;
                }
                return base.TryInvokeMember(binder, args, out result);
            }
            public void Write(JsonWriter writer)
            {
                switch (_valueType)
                {
                    case ValueType.Array:
                        {
                            var value = (List<DynamicValue>)_value;
                            writer.WriteStartArray();
                            foreach (var item in value)
                            {
                                item.Write(writer);
                            }
                            writer.WriteEndArray();
                            return;
                        }
                    case ValueType.Object:
                        {
                            var value = (Dictionary<string, DynamicValue>)_value;
                            writer.WriteStartObject();
                            foreach (var item in value)
                            {
                                writer.WriteProperty(item.Key);
                                item.Value.Write(writer);
                            }
                            writer.WriteEndObject();
                            return;
                        }
                    case ValueType.Null:
                        writer.WriteNull();
                        return;
                    case ValueType.String:
                        writer.WriteString((string)_value);
                        return;
                    case ValueType.Boolean:
                        writer.WriteBoolean(_value == _True ? true : false);
                        return;
                    case ValueType.Number:
                        writer.WriteNumber(Converter.Convert<object, string>(_value));
                        return;
                    case ValueType.Undefined:
                        return;
                }
            }
        }
        #endregion

        #region Register
        private static readonly object _Sync = new object();
        private static Stack<object> _Handlers;
        private static Func<PropertyInfo, string> _PropertyResolver;
        private static Stack<Func<Type, string, ParameterExpression, Expression>> _PropertyFormats;
        private static Stack<Func<PropertyInfo, ParameterExpression, ParameterExpression, Expression>> _PropertyHandlers;
        private static Dictionary<Type, Func<JsonReader, object>> _ObjHandlers;
        private static class Handler<T>
        {
            static Handler()
            {
                var reader = Expression.Parameter(typeof(JsonReader), "reader");
                Register(typeof(T), reader, out var expression, out var @delegate);
                if (expression != null)
                {
                    Value = @delegate == null
                      ? Expression.Lambda<Func<JsonReader, T>>(expression, reader).Compile()
                      : (Func<JsonReader, T>)@delegate;
                }
            }

            public static Func<JsonReader, T> Value;
        }
        static JsonReader()
        {
            _ObjHandlers = new Dictionary<Type, Func<JsonReader, object>>();
            _Handlers = new Stack<object>();
            _PropertyResolver = (property) => property.Name;
            _PropertyFormats = new Stack<Func<Type, string, ParameterExpression, Expression>>();
            _PropertyHandlers = new Stack<Func<PropertyInfo, ParameterExpression, ParameterExpression, Expression>>();

            var isStartArray = typeof(JsonReader).GetProperty("IsStartArray");
            var isEndArray = typeof(JsonReader).GetProperty("IsEndArray");
            var isStartObject = typeof(JsonReader).GetProperty("IsStartObject");
            var isEndObject = typeof(JsonReader).GetProperty("IsEndObject");
            var isProperty = typeof(JsonReader).GetProperty("IsProperty");
            var isNull = typeof(JsonReader).GetProperty("IsNull");
            var read = typeof(JsonReader).GetMethod("Read", Type.EmptyTypes);
            var skip = typeof(JsonReader).GetMethod("Skip", Type.EmptyTypes);
            var formatException = typeof(FormatException).GetConstructor(new[] { typeof(string) });
            //default
            var typeReference = new Stack<Type>();
            Register((type, value, reader) => {
                typeReference.Push(type);
                var name = Expression.Variable(typeof(ReadOnlySpan<char>), "name");
                var equals = typeof(StringExtensions).GetMethod("Equals", new[] { typeof(string), typeof(ReadOnlySpan<char>) });
                var returnLabel = Expression.Label(typeof(void));
                var variables = new List<ParameterExpression>() { name };
                var assigns = new List<Expression>();
                var propertyExprs = new Stack<(string, Expression)>();
                var extensionDataExprs = default(Expression);
                var requiredTest = default(Expression);
                var properties = type.GetGeneralProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var property in properties)
                {
                    if (property.IsDefined(typeof(IgnoreDataMemberAttribute)))
                        continue;
                    string propertyName = null;
                    var isRequired = false;
                    var dataMemberAttribute = property.GetCustomAttribute<DataMemberAttribute>();
                    if (dataMemberAttribute == null)
                    {
                        if (property.Name == "ExtensionData")
                        {
                            if (property.CanRead
                               && property.PropertyType.IsEnumerable(out _, out _, out var current)
                               && current.PropertyType.IsGenericType
                               && current.PropertyType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                            {
                                var extensionData = Expression.Variable(property.PropertyType, "extensionData");
                                variables.Add(extensionData);
                                assigns.Add(Expression.Assign(extensionData, Expression.Property(value, property)));
                                var eleKey = current.PropertyType.GetProperty("Key");
                                var eleValue = current.PropertyType.GetProperty("Value");
                                var itemKey = Expression.Variable(typeof(string), "itemKey");
                                Converter.Register(typeof(string), eleKey.PropertyType, itemKey, out var itemKeyExpr, out _);
                                if (itemKeyExpr == null)
                                    continue;
                                Register(eleValue.PropertyType, reader, out var itemValueExpr, out _);
                                if (itemValueExpr == null)
                                    continue;
                                var addExpr = default(Expression);
                                var addMethod = property.PropertyType.GetMethod("Add", new[] { current.PropertyType });
                                if (addMethod == null && property.PropertyType.IsInterface)
                                {
                                    var interfaces = property.PropertyType.GetInterfaces();
                                    foreach (var @interface in interfaces)
                                    {
                                        addMethod = @interface.GetMethod("Add", new[] { current.PropertyType });
                                        if (addMethod != null)
                                            break;
                                    }
                                }
                                if (addMethod != null)
                                {
                                    addExpr = Expression.Call(
                                        extensionData, addMethod,
                                        Expression.New(current.PropertyType.GetConstructor(new[] { eleKey.PropertyType, eleValue.PropertyType }), itemKeyExpr, itemValueExpr));
                                }
                                else
                                {
                                    addMethod = property.PropertyType.GetMethod("Add", new[] { eleKey.PropertyType, eleValue.PropertyType });
                                    if (addMethod == null && property.PropertyType.IsInterface)
                                    {
                                        var interfaces = property.PropertyType.GetInterfaces();
                                        foreach (var @interface in interfaces)
                                        {
                                            addMethod = @interface.GetMethod("Add", new[] { eleKey.PropertyType, eleValue.PropertyType });
                                            if (addMethod != null)
                                                break;
                                        }
                                    }
                                    if (addMethod != null)
                                    {
                                        addExpr = Expression.Call(extensionData, addMethod, itemKeyExpr, itemValueExpr);
                                    }
                                }
                                if (addExpr == null)
                                    continue;

                                if (property.CanWrite)
                                {
                                    Constructor.Register(property.PropertyType, out var ctor, out _);
                                    if (ctor != null)
                                    {
                                        extensionDataExprs = Expression.Block(new[] { itemKey },
                                            Expression.IfThen(
                                                Expression.Equal(extensionData, Expression.Constant(null)),
                                                Expression.Block(
                                                    Expression.Assign(extensionData, ctor),
                                                    Expression.Assign(Expression.Property(value, property), extensionData)
                                                    )
                                                ),
                                            Expression.Assign(itemKey, Expression.New(typeof(string).GetConstructor(new[] { typeof(ReadOnlySpan<char>) }), name)),
                                            Expression.Call(reader, read),
                                            addExpr
                                            );
                                        continue;
                                    }
                                }
                                extensionDataExprs = Expression.IfThenElse(
                                    Expression.NotEqual(extensionData, Expression.Constant(null)),
                                    Expression.Block(new[] { itemKey },
                                        Expression.Assign(itemKey, Expression.New(typeof(string).GetConstructor(new[] { typeof(ReadOnlySpan<char>) }), name)),
                                        Expression.Call(reader, read),
                                        addExpr
                                    ),
                                    Expression.Call(reader, skip));
                            }
                            continue;
                        }
                        propertyName = _PropertyResolver(property);
                    }
                    else
                    {
                        propertyName = dataMemberAttribute.Name == null ? _PropertyResolver(property) : dataMemberAttribute.Name;
                        isRequired = dataMemberAttribute.IsRequired;
                    }
                    if (propertyName == null)
                        continue;

                    RegisterProperty(property, value, reader, out var propertyExpression);
                    if (propertyExpression == null)
                    {
                        if (property.CanWrite)
                        {
                            Register(property.PropertyType, reader, out propertyExpression, out _);
                            if (propertyExpression == null)
                                continue;
                            propertyExpression = Expression.Assign(Expression.Property(value, property), propertyExpression);
                        }
                        else
                        {
                            if (!property.CanRead || property.PropertyType.IsValueType)
                                continue;
                            var propertyValue = Expression.Variable(property.PropertyType, $"value{property.Name}");
                            Register(property.PropertyType, propertyValue, reader, out propertyExpression);
                            if (propertyExpression == null)
                                continue;
                            propertyExpression = Expression.Block(new[] { propertyValue },
                                Expression.Assign(propertyValue, Expression.Property(value, property)),
                                Expression.IfThen(
                                    Expression.NotEqual(propertyValue, Expression.Constant(null)),
                                    propertyExpression
                                    )
                                );
                        }
                    }
                    if (isRequired)
                    {
                        var required = Expression.Variable(typeof(bool), $"isRequired{property.Name}");
                        variables.Add(required);
                        assigns.Add(Expression.Assign(required, Expression.Constant(false)));
                        if (requiredTest == null)
                            requiredTest = Expression.Not(required);
                        else
                            requiredTest = Expression.OrElse(requiredTest, Expression.Not(required));
                        propertyExprs.Push((propertyName,
                            Expression.Block(
                                Expression.Assign(required, Expression.Constant(true)),
                                Expression.Call(reader, read),
                                propertyExpression)));
                    }
                    else
                    {
                        propertyExprs.Push((propertyName,
                            Expression.Block(
                                Expression.Call(reader, read),
                                propertyExpression)));
                    }
                }

                var getProperty = typeof(JsonReader).GetMethod(nameof(GetProperty), new[] { typeof(ReadOnlySpan<char>).MakeByRefType() });
                var breakLabel = Expression.Label();
                Expression endObjectExprA;
                Expression endObjectExprB;
                if (requiredTest == null)
                {
                    endObjectExprA = Expression.IfThen(
                        Expression.Property(reader, isEndObject),
                        Expression.Return(returnLabel)
                        );
                    endObjectExprB = Expression.IfThen(
                        Expression.Not(Expression.Property(reader, isEndObject)),
                        Expression.Throw(Expression.New(formatException, Expression.Constant("Expected character }")))
                        );
                }
                else
                {
                    endObjectExprA = Expression.IfThen(
                        Expression.Property(reader, isEndObject),
                        Expression.Throw(Expression.New(formatException, Expression.Constant("Property required")))
                        );
                    endObjectExprB = Expression.IfThenElse(
                        requiredTest,
                        Expression.Throw(Expression.New(formatException, Expression.Constant("Property required"))),
                        Expression.IfThen(
                            Expression.Not(Expression.Property(reader, isEndObject)),
                            Expression.Throw(Expression.New(formatException, Expression.Constant("Expected character }"))))
                        );
                }
                Expression propertyBlock;
                if (propertyExprs.TryPop(out var propertyItem))
                {
                    (var propertyName, var propertyExpr) = propertyItem;
                    propertyBlock = Expression.IfThenElse(
                        Expression.Call(equals, Expression.Constant(propertyName), name),
                        propertyExpr,
                        extensionDataExprs ?? Expression.Call(reader, skip));
                    while (propertyExprs.TryPop(out propertyItem))
                    {
                        (propertyName, propertyExpr) = propertyItem;
                        propertyBlock = Expression.IfThenElse(
                            Expression.Call(equals, Expression.Constant(propertyName), name),
                            propertyExpr,
                            propertyBlock);
                    }
                }
                else
                {
                    propertyBlock = extensionDataExprs ?? Expression.Call(reader, skip);
                }
                typeReference.Pop();
                return Expression.Block(variables,
                    Expression.IfThen(
                        Expression.Property(reader, isNull),
                        Expression.Return(returnLabel)
                        ),
                    Expression.IfThen(
                        Expression.Not(Expression.Property(reader, isStartObject)),
                        Expression.Throw(Expression.New(formatException, Expression.Constant("Expected character {")))
                        ),
                    Expression.Block(assigns),
                    Expression.IfThen(
                        Expression.Not(Expression.Call(reader, read)),
                        Expression.Throw(Expression.New(formatException, Expression.Constant("EOF")))
                        ),
                    endObjectExprA,
                    Expression.Loop(
                        Expression.IfThenElse(
                            Expression.Property(reader, isProperty),
                            Expression.Block(
                                Expression.Call(reader, getProperty, name),
                                propertyBlock,
                                Expression.IfThen(
                                    Expression.Not(Expression.Call(reader, read)),
                                    Expression.Throw(Expression.New(formatException, Expression.Constant("EOF"))))
                                ),
                            Expression.Break(breakLabel)
                            )
                        , breakLabel),
                    endObjectExprB,
                    Expression.Label(returnLabel)
                    );
            });
            Register((type, reader) => {
                if (!typeReference.Contains(type))
                    return null;

                var register = typeof(JsonReader).GetMethod("Register", new[] { typeof(Func<,>).MakeGenericType(typeof(JsonReader), Type.MakeGenericMethodParameter(0)).MakeByRefType() });
                var handler = Expression.Variable(typeof(Func<,>).MakeGenericType(typeof(JsonReader), type), "handler");
                return Expression.Block(new[] { handler },
                    Expression.Call(register.MakeGenericMethod(type), handler),
                    Expression.Invoke(handler, reader));
            });
            //void Read(JsonReader)
            Register((type, value, reader) => {
                var read = type.GetMethod("Read", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(JsonReader) }, null);
                if (read == null)
                    return null;

                return Expression.Call(value, read, reader);
            });
            //IEnumerable<>
            Register((type, reader) => {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(IEnumerable<>))
                    return null;

                var eleType = type.GetGenericArguments()[0];
                Register(typeof(List<>).MakeGenericType(eleType), reader, out var expression, out _);
                return Expression.Convert(expression, type);
            });
            //ISet<>
            Register((type, value, reader) =>
            {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(ISet<>))
                    return null;

                var eleType = type.GetGenericArguments()[0];
                Register(eleType, reader, out var expression, out _);
                if (expression == null)
                    return Expression.Empty();
                var add = type.GetMethod("Add");
                var returnLabel = Expression.Label();
                var item = Expression.Variable(eleType, "item");
                return Expression.Block(
                    Expression.IfThen(
                        Expression.Not(Expression.Property(reader, isStartArray)),
                        Expression.Throw(Expression.New(formatException, Expression.Constant("Expected character [")))
                        ),
                    Expression.IfThen(
                        Expression.Not(Expression.Call(reader, read)),
                        Expression.Throw(Expression.New(formatException, Expression.Constant("EOF")))
                        ),
                    Expression.IfThen(
                        Expression.Property(reader, isEndArray),
                        Expression.Return(returnLabel)
                        ),
                    Expression.Loop(
                        Expression.Block(new[] { item },
                            Expression.Assign(item, expression),
                            Expression.Call(value, add, item),
                            Expression.IfThen(
                                Expression.Not(Expression.Call(reader, read)),
                                Expression.Throw(Expression.New(formatException, Expression.Constant("EOF")))
                                ),
                            Expression.IfThen(
                                Expression.Property(reader, isEndArray),
                                Expression.Return(returnLabel))
                    )),
                    Expression.Label(returnLabel)
                    );
            });
            //HashSet<>
            Register((type, value, reader) =>
            {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(HashSet<>))
                    return null;

                var eleType = type.GetGenericArguments()[0];
                Register(eleType, reader, out var expression, out _);
                if (expression == null)
                    return Expression.Empty();
                var add = type.GetMethod("Add");
                var returnLabel = Expression.Label();
                var item = Expression.Variable(eleType, "item");
                return Expression.Block(
                    Expression.IfThen(
                        Expression.Not(Expression.Property(reader, isStartArray)),
                        Expression.Throw(Expression.New(formatException, Expression.Constant("Expected character [")))
                        ),
                    Expression.IfThen(
                        Expression.Not(Expression.Call(reader, read)),
                        Expression.Throw(Expression.New(formatException, Expression.Constant("EOF")))
                        ),
                    Expression.IfThen(
                        Expression.Property(reader, isEndArray),
                        Expression.Return(returnLabel)
                        ),
                    Expression.Loop(
                        Expression.Block(new[] { item },
                            Expression.Assign(item, expression),
                            Expression.Call(value, add, item),
                            Expression.IfThen(
                                Expression.Not(Expression.Call(reader, read)),
                                Expression.Throw(Expression.New(formatException, Expression.Constant("EOF")))
                                ),
                            Expression.IfThen(
                                Expression.Property(reader, isEndArray),
                                Expression.Return(returnLabel))
                    )),
                    Expression.Label(returnLabel)
                    );
            });
            //IList<>
            Register((type, value, reader) =>
            {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(IList<>))
                    return null;

                var eleType = type.GetGenericArguments()[0];
                Register(eleType, reader, out var expression, out _);
                if (expression == null)
                    return Expression.Empty();
                var add = typeof(ICollection<>).MakeGenericType(eleType).GetMethod("Add");
                var returnLabel = Expression.Label();
                var item = Expression.Variable(eleType, "item");
                return Expression.Block(
                    Expression.IfThen(
                        Expression.Not(Expression.Property(reader, isStartArray)),
                        Expression.Throw(Expression.New(formatException, Expression.Constant("Expected character [")))
                        ),
                    Expression.IfThen(
                        Expression.Not(Expression.Call(reader, read)),
                        Expression.Throw(Expression.New(formatException, Expression.Constant("EOF")))
                        ),
                    Expression.IfThen(
                        Expression.Property(reader, isEndArray),
                        Expression.Return(returnLabel)
                        ),
                    Expression.Loop(
                        Expression.Block(new[] { item },
                            Expression.Assign(item, expression),
                            Expression.Call(value, add, item),
                            Expression.IfThen(
                                Expression.Not(Expression.Call(reader, read)),
                                Expression.Throw(Expression.New(formatException, Expression.Constant("EOF")))
                                ),
                            Expression.IfThen(
                                Expression.Property(reader, isEndArray),
                                Expression.Return(returnLabel))
                    )),
                    Expression.Label(returnLabel)
                    );
            });
            //List<>
            Register((type, value, reader) =>
            {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(List<>))
                    return null;

                var eleType = type.GetGenericArguments()[0];
                Register(eleType, reader, out var expression, out _);
                if (expression == null)
                    return Expression.Empty();
                var add = type.GetMethod("Add");
                var returnLabel = Expression.Label();
                var item = Expression.Variable(eleType, "item");
                return Expression.Block(
                    Expression.IfThen(
                        Expression.Not(Expression.Property(reader, "IsStartArray")),
                        Expression.Throw(Expression.New(formatException, Expression.Constant("Expected character [")))
                        ),
                    Expression.IfThen(
                        Expression.Not(Expression.Call(reader, read)),
                        Expression.Throw(Expression.New(formatException, Expression.Constant("EOF")))
                        ),
                    Expression.IfThen(
                        Expression.Property(reader, "IsEndArray"),
                        Expression.Return(returnLabel)
                        ),
                    Expression.Loop(
                        Expression.Block(new[] { item },
                            Expression.Assign(item, expression),
                            Expression.Call(value, add, item),
                            Expression.IfThen(
                                Expression.Not(Expression.Call(reader, read)),
                                Expression.Throw(Expression.New(formatException, Expression.Constant("EOF")))
                                ),
                            Expression.IfThen(
                                Expression.Property(reader, "IsEndArray"),
                                Expression.Return(returnLabel))
                    )),
                    Expression.Label(returnLabel)
                    );
            });
            //Queue<>
            Register((type, value, reader) =>
            {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Queue<>))
                    return null;

                var eleType = type.GetGenericArguments()[0];
                Register(eleType, reader, out var expression, out _);
                if (expression == null)
                    return Expression.Empty();
                var enqueue = type.GetMethod("Enqueue");
                var returnLabel = Expression.Label();
                var item = Expression.Variable(eleType, "item");
                return Expression.Block(
                    Expression.IfThen(
                        Expression.Not(Expression.Property(reader, isStartArray)),
                        Expression.Throw(Expression.New(formatException, Expression.Constant("Expected character [")))
                        ),
                    Expression.IfThen(
                        Expression.Not(Expression.Call(reader, read)),
                        Expression.Throw(Expression.New(formatException, Expression.Constant("EOF")))
                        ),
                    Expression.IfThen(
                        Expression.Property(reader, isEndArray),
                        Expression.Return(returnLabel)
                        ),
                    Expression.Loop(
                        Expression.Block(new[] { item },
                            Expression.Assign(item, expression),
                            Expression.Call(value, enqueue, item),
                            Expression.IfThen(
                                Expression.Not(Expression.Call(reader, read)),
                                Expression.Throw(Expression.New(formatException, Expression.Constant("EOF")))
                                ),
                            Expression.IfThen(
                                Expression.Property(reader, isEndArray),
                                Expression.Return(returnLabel))
                    )),
                    Expression.Label(returnLabel)
                    );
            });
            //Stack<>
            Register((type, value, reader) =>
            {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Stack<>))
                    return null;

                var eleType = type.GetGenericArguments()[0];
                Register(eleType, reader, out var expression, out _);
                if (expression == null)
                    return Expression.Empty();
                var push = type.GetMethod("Push");
                var returnLabel = Expression.Label();
                var item = Expression.Variable(eleType, "item");
                return Expression.Block(
                    Expression.IfThen(
                        Expression.Not(Expression.Property(reader, isStartArray)),
                        Expression.Throw(Expression.New(formatException, Expression.Constant("Expected character [")))
                        ),
                    Expression.IfThen(
                        Expression.Not(Expression.Call(reader, read)),
                        Expression.Throw(Expression.New(formatException, Expression.Constant("EOF")))
                        ),
                    Expression.IfThen(
                        Expression.Property(reader, isEndArray),
                        Expression.Return(returnLabel)
                        ),
                    Expression.Loop(
                        Expression.Block(new[] { item },
                            Expression.Assign(item, expression),
                            Expression.Call(value, push, item),
                            Expression.IfThen(
                                Expression.Not(Expression.Call(reader, read)),
                                Expression.Throw(Expression.New(formatException, Expression.Constant("EOF")))
                                ),
                            Expression.IfThen(
                                Expression.Property(reader, isEndArray),
                                Expression.Return(returnLabel))
                    )),
                    Expression.Label(returnLabel)
                    );
            });
            //IDictionary<,>
            Register((type, value, reader) => {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(IDictionary<,>))
                    return null;

                var typeArgs = type.GetGenericArguments();
                var itemKey = Expression.Variable(typeof(string), "itemKey");
                Converter.Register(typeof(string), typeArgs[0], itemKey, out var itemKeyExpr, out _);
                if (itemKeyExpr == null)
                    return Expression.Empty();
                Register(typeArgs[1], reader, out var itemValueExpr, out _);
                if (itemValueExpr == null)
                    return Expression.Empty();
                var getProperty = typeof(JsonReader).GetMethod("GetProperty", Type.EmptyTypes);
                var add = type.GetMethod("Add");
                var returnLabel = Expression.Label();
                var breakLabel = Expression.Label();
                return Expression.Block(
                    Expression.IfThen(
                        Expression.Not(Expression.Property(reader, isStartObject)),
                        Expression.Throw(Expression.New(formatException, Expression.Constant("reader")))
                        ),
                    Expression.IfThen(
                        Expression.Not(Expression.Call(reader, read)),
                        Expression.Throw(Expression.New(formatException, Expression.Constant("EOF")))
                        ),
                    Expression.IfThen(
                        Expression.Property(reader, isEndObject),
                        Expression.Return(returnLabel)
                        ),
                    Expression.Loop(
                        Expression.IfThenElse(
                            Expression.Property(reader, isProperty),
                            Expression.Block(new[] { itemKey },
                                Expression.Assign(itemKey, Expression.Call(reader, getProperty)),
                                Expression.IfThen(
                                    Expression.Not(Expression.Call(reader, read)),
                                    Expression.Throw(Expression.New(formatException, Expression.Constant("EOF")))),
                                Expression.Call(value, add, itemKeyExpr, itemValueExpr),
                                Expression.IfThen(
                                    Expression.Not(Expression.Call(reader, read)),
                                    Expression.Throw(Expression.New(formatException, Expression.Constant("EOF"))))
                            ),
                            Expression.Break(breakLabel)
                            ),
                        breakLabel
                        ),
                    Expression.IfThen(
                        Expression.Not(Expression.Property(reader, isEndObject)),
                        Expression.Throw(Expression.New(formatException, Expression.Constant("reader")))),
                    Expression.Label(returnLabel)
                    );
            });
            //Dictionary<,>
            Register((type, value, reader) => {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Dictionary<,>))
                    return null;

                var typeArgs = type.GetGenericArguments();
                var itemKey = Expression.Variable(typeof(string), "itemKey");
                Converter.Register(typeof(string), typeArgs[0], itemKey, out var itemKeyExpr, out _);
                if (itemKeyExpr == null)
                    return Expression.Empty();
                Register(typeArgs[1], reader, out var itemValueExpr, out _);
                if (itemValueExpr == null)
                    return Expression.Empty();
                var getProperty = typeof(JsonReader).GetMethod("GetProperty", Type.EmptyTypes);
                var add = type.GetMethod("Add");
                var returnLabel = Expression.Label();
                var breakLabel = Expression.Label();
                return Expression.Block(
                    Expression.IfThen(
                        Expression.Not(Expression.Property(reader, isStartObject)),
                        Expression.Throw(Expression.New(formatException, Expression.Constant("reader")))
                        ),
                    Expression.IfThen(
                        Expression.Not(Expression.Call(reader, read)),
                        Expression.Throw(Expression.New(formatException, Expression.Constant("EOF")))
                        ),
                    Expression.IfThen(
                        Expression.Property(reader, isEndObject),
                        Expression.Return(returnLabel)
                        ),
                    Expression.Loop(
                        Expression.IfThenElse(
                            Expression.Property(reader, isProperty),
                            Expression.Block(new[] { itemKey },
                                Expression.Assign(itemKey, Expression.Call(reader, getProperty)),
                                Expression.IfThen(
                                    Expression.Not(Expression.Call(reader, read)),
                                    Expression.Throw(Expression.New(formatException, Expression.Constant("EOF")))),
                                Expression.Call(value, add, itemKeyExpr, itemValueExpr),
                                Expression.IfThen(
                                    Expression.Not(Expression.Call(reader, read)),
                                    Expression.Throw(Expression.New(formatException, Expression.Constant("EOF"))))
                            ),
                            Expression.Break(breakLabel)
                            ),
                        breakLabel
                        ),
                    Expression.IfThen(
                        Expression.Not(Expression.Property(reader, "IsEndObject")),
                        Expression.Throw(Expression.New(formatException, Expression.Constant("reader")))),
                    Expression.Label(returnLabel)
                    );
            });
            //Nullable<>
            Register((type, reader) =>
            {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Nullable<>))
                    return null;

                var nullableType = type.GetGenericArguments()[0];
                Register(nullableType, reader, out var expression, out _);
                if (expression == null)
                    return Expression.Empty();
                return Expression.Condition(
                    Expression.Property(reader, isNull),
                    Expression.Constant(null, type),
                    Expression.New(type.GetConstructor(new[] { nullableType }), expression)
                    );
            });
            //ValueTuple
            Register((type, reader) =>
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
                    var ctor = type.GetConstructor(eleTypes);
                    var variables = new List<ParameterExpression>();
                    var exprs = new List<Expression>();
                    exprs.Add(Expression.IfThen(
                        Expression.Not(Expression.Property(reader, isStartArray)),
                        Expression.Throw(Expression.New(formatException, Expression.Constant("Expected character [")))));
                    var itemIndex = 1;
                    foreach (var eleType in eleTypes)
                    {
                        var item = Expression.Variable(eleType, $"item{itemIndex++}");
                        variables.Add(item);
                        Register(eleType, reader, out var expression, out _);
                        if (expression == null)
                            return Expression.Empty();
                        exprs.Add(Expression.IfThen(
                            Expression.Not(Expression.Call(reader, read)),
                            Expression.Throw(Expression.New(formatException, Expression.Constant("EOF")))));
                        exprs.Add(Expression.Assign(item, expression));
                    }
                    exprs.Add(Expression.IfThen(
                        Expression.Not(Expression.Call(reader, read)),
                        Expression.Throw(Expression.New(formatException, Expression.Constant("EOF")))));
                    exprs.Add(Expression.IfThen(
                        Expression.Not(Expression.Property(reader, isEndArray)),
                        Expression.Throw(Expression.New(formatException, Expression.Constant("Expected character ]")))
                        ));
                    exprs.Add(Expression.New(ctor, variables));
                    return Expression.Condition(
                        Expression.Property(reader, isNull),
                        Expression.Default(type),
                        Expression.Block(variables, exprs)
                        );
                }
                return null;
            });
            //Tuple
            Register((type, reader) =>
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
                    var ctor = type.GetConstructor(eleTypes);
                    var variables = new List<ParameterExpression>();
                    var exprs = new List<Expression>();
                    exprs.Add(Expression.IfThen(
                        Expression.Not(Expression.Property(reader, isStartArray)),
                        Expression.Throw(Expression.New(formatException, Expression.Constant("Expected character [")))));
                    var itemIndex = 1;
                    foreach (var eleType in eleTypes)
                    {
                        var item = Expression.Variable(eleType, $"item{itemIndex++}");
                        variables.Add(item);
                        Register(eleType, reader, out var expression, out _);
                        if (expression == null)
                            return Expression.Empty();
                        exprs.Add(Expression.IfThen(
                            Expression.Not(Expression.Call(reader, read)),
                            Expression.Throw(Expression.New(formatException, Expression.Constant("EOF")))));
                        exprs.Add(Expression.Assign(item, expression));
                    }
                    exprs.Add(Expression.IfThen(
                        Expression.Not(Expression.Call(reader, read)),
                        Expression.Throw(Expression.New(formatException, Expression.Constant("EOF")))));
                    exprs.Add(Expression.IfThen(
                        Expression.Not(Expression.Property(reader, isEndArray)),
                        Expression.Throw(Expression.New(formatException, Expression.Constant("Expected character ]")))
                        ));
                    exprs.Add(Expression.New(ctor, variables));
                    return Expression.Condition(
                        Expression.Property(reader, isNull),
                        Expression.Default(type),
                        Expression.Block(variables, exprs)
                        );
                }
                return null;
            });
            //KeyValuePair<,>
            Register((type, reader) =>
            {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(KeyValuePair<,>))
                    return null;

                var typeArgs = type.GetGenericArguments();
                Register(typeArgs[0], reader, out var pairKeyExpr, out _);
                Register(typeArgs[1], reader, out var pairValueExpr, out _);
                if (pairKeyExpr == null || pairValueExpr == null)
                    return Expression.Empty();
                var ctor = type.GetConstructor(typeArgs);
                var pairKey = Expression.Variable(typeArgs[0], "pairKey");
                var pairValue = Expression.Variable(typeArgs[1], "pairValue");
                return Expression.Condition(
                    Expression.Property(reader, isNull),
                    Expression.Default(type),
                    Expression.Block(new[] { pairKey, pairValue },
                        Expression.IfThen(
                            Expression.Not(Expression.Property(reader, isStartArray)),
                            Expression.Throw(Expression.New(formatException, Expression.Constant("Expected character [")))),
                        Expression.IfThen(
                            Expression.Not(Expression.Call(reader, read)),
                            Expression.Throw(Expression.New(formatException, Expression.Constant("EOF")))),
                        Expression.Assign(pairKey, pairKeyExpr),
                        Expression.IfThen(
                            Expression.Not(Expression.Call(reader, read)),
                            Expression.Throw(Expression.New(formatException, Expression.Constant("EOF")))),
                        Expression.Assign(pairValue, pairValueExpr),
                        Expression.IfThen(
                            Expression.Not(Expression.Call(reader, read)),
                            Expression.Throw(Expression.New(formatException, Expression.Constant("EOF")))),
                        Expression.IfThen(
                            Expression.Not(Expression.Property(reader, isEndArray)),
                            Expression.Throw(Expression.New(formatException, Expression.Constant("Expected character ]")))),
                        Expression.New(ctor, pairKey, pairValue)
                    ));
            });
            //Array
            Register((type, reader) => {
                if (!type.IsArray)
                    return null;

                var eleType = type.GetElementType();
                var listType = typeof(List<>).MakeGenericType(eleType);
                Register(listType, reader, out var expression, out _);
                if (expression == null)
                    return Expression.Empty();
                var list = Expression.Variable(listType, "list");
                return Expression.Block(new[] { list },
                    Expression.Assign(list, expression),
                    Expression.Condition(
                        Expression.Equal(list, Expression.Constant(null)),
                        Expression.Default(type),
                        Expression.Call(list, listType.GetMethod("ToArray", Type.EmptyTypes))
                        )
                    );
            });
            //Enum
            Register((type, reader) => {
                if (!type.IsEnum)
                    return null;

                var getInt32 = typeof(JsonReader).GetMethod("GetInt32");
                var getString = typeof(JsonReader).GetMethod("GetString", Type.EmptyTypes);
                var parse = typeof(Enum).GetMethod("Parse", 1, new[] { typeof(string) });
                return Expression.Condition(
                    Expression.Property(reader, "IsNumber"),
                    Expression.Convert(Expression.Call(reader, getInt32), type),
                    Expression.Call(parse.MakeGenericMethod(type), Expression.Call(reader, getString))
                    );
            });

            object GetNumber(JsonReader reader)
            {
                reader.GetNumber(out var number);
                //int.TryParse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo, out var @int)
                if (decimal.TryParse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo, out var @decimal))
                {
                    return @decimal;
                }
                else if (double.TryParse(number, (NumberStyles)164, NumberFormatInfo.InvariantInfo, out var @double))
                {
                    return @double;
                }
                else
                {
                    return new string(number);
                }
            }
            //Object
            object GetObject(JsonReader reader)
            {
                if (reader.IsNull)
                {
                    return null;
                }
                else if (reader.IsStartObject)
                {
                    var value = new Dictionary<string, object>();
                    reader.Read();
                    while (reader.IsProperty)
                    {
                        var propertyName = reader.GetProperty();
                        reader.Read();
                        var propertyValue = reader.IsNull ? null :
                        reader.IsString ? reader.GetString() :
                        reader.IsNumber ? GetNumber(reader) :
                        reader.IsBoolean ? reader.GetBoolean() :
                        reader.IsStartObject || reader.IsStartArray ? GetObject(reader) :
                        throw new FormatException(nameof(reader));
                        value.TryAdd(propertyName, propertyValue);
                        reader.Read();
                    }
                    if (!reader.IsEndObject)
                        throw new FormatException("Expected character }");
                    return value;
                }
                else if (reader.IsStartArray)
                {
                    var value = new List<object>();
                    reader.Read();
                    while (!reader.IsEndArray)
                    {
                        var item = reader.IsNull ? null :
                        reader.IsString ? reader.GetString() :
                        reader.IsNumber ? GetNumber(reader) :
                        reader.IsBoolean ? reader.GetBoolean() :
                        reader.IsStartObject || reader.IsStartArray ? GetObject(reader) :
                        throw new FormatException(nameof(reader));
                        value.Add(item);
                        reader.Read();
                    }
                    return value;
                }
                else
                {
                    if (reader.IsNull)
                        return null;
                    else if (reader.IsString)
                        return reader.GetString();
                    else if (reader.IsNumber)
                        return GetNumber(reader);
                    else if (reader.IsBoolean)
                        return reader.GetBoolean();
                    else
                        throw new FormatException(nameof(reader));
                }
            }
            Register((reader) => { return GetObject(reader); });
            //Dynamic
            DynamicValue GetDynamic(JsonReader reader)
            {
                if (reader.IsNull)
                {
                    return DynamicValue.Null;
                }
                else if (reader.IsStartObject)
                {
                    var value = new Dictionary<string, DynamicValue>();
                    reader.Read();
                    while (reader.IsProperty)
                    {
                        var propertyName = reader.GetProperty();
                        reader.Read();
                        var propertyValue = reader.IsNull ? DynamicValue.Null :
                        reader.IsString ? new DynamicValue(reader.GetString()) :
                        reader.IsNumber ? new DynamicValue(GetNumber(reader)) :
                        reader.IsBoolean ? new DynamicValue(reader.GetBoolean()) :
                        reader.IsStartObject || reader.IsStartArray ? GetDynamic(reader) :
                        throw new FormatException(nameof(reader));
                        value.TryAdd(propertyName, propertyValue);
                        reader.Read();
                    }
                    if (!reader.IsEndObject)
                        throw new FormatException("Expected character }");
                    return new DynamicValue(value);
                }
                else if (reader.IsStartArray)
                {
                    var value = new List<DynamicValue>();
                    reader.Read();
                    while (!reader.IsEndArray)
                    {
                        var item = reader.IsNull ? DynamicValue.Null :
                        reader.IsString ? new DynamicValue(reader.GetString()) :
                        reader.IsNumber ? new DynamicValue(GetNumber(reader)) :
                        reader.IsBoolean ? new DynamicValue(reader.GetBoolean()) :
                        reader.IsStartObject || reader.IsStartArray ? GetDynamic(reader) :
                        throw new FormatException(nameof(reader));
                        value.Add(item);
                        reader.Read();
                    }
                    return new DynamicValue(value);
                }
                else
                {
                    return reader.IsNull ? DynamicValue.Null :
                    reader.IsString ? new DynamicValue(reader.GetString()) :
                    reader.IsNumber ? new DynamicValue(GetNumber(reader)) :
                    reader.IsBoolean ? new DynamicValue(reader.GetBoolean()) :
                    throw new FormatException(nameof(reader));
                }
            }
            Register<DynamicObject>((reader) => { return GetDynamic(reader); });
            //char
            Register((reader) => {
                reader.GetString(out var @string);
                return @string[0];
            });
            Register(typeof(bool), (reader) => { return Expression.Call(reader, typeof(JsonReader).GetMethod("GetBoolean")); });
            Register(typeof(string), (reader) => { return Expression.Call(reader, typeof(JsonReader).GetMethod("GetString", Type.EmptyTypes)); });
            Register(typeof(byte), (reader) => { return Expression.Call(reader, typeof(JsonReader).GetMethod("GetByte")); });
            Register(typeof(sbyte), (reader) => { return Expression.Call(reader, typeof(JsonReader).GetMethod("GetSByte")); });
            Register(typeof(short), (reader) => { return Expression.Call(reader, typeof(JsonReader).GetMethod("GetInt16")); });
            Register(typeof(ushort), (reader) => { return Expression.Call(reader, typeof(JsonReader).GetMethod("GetUInt16")); });
            Register(typeof(int), (reader) => { return Expression.Call(reader, typeof(JsonReader).GetMethod("GetInt32")); });
            Register(typeof(uint), (reader) => { return Expression.Call(reader, typeof(JsonReader).GetMethod("GetUInt32")); });
            Register(typeof(long), (reader) => { return Expression.Call(reader, typeof(JsonReader).GetMethod("GetInt64")); });
            Register(typeof(ulong), (reader) => { return Expression.Call(reader, typeof(JsonReader).GetMethod("GetUInt64")); });
            Register(typeof(float), (reader) => { return Expression.Call(reader, typeof(JsonReader).GetMethod("GetSingle")); });
            Register(typeof(double), (reader) => { return Expression.Call(reader, typeof(JsonReader).GetMethod("GetDouble")); });
            Register(typeof(decimal), (reader) => { return Expression.Call(reader, typeof(JsonReader).GetMethod("GetDecimal")); });
            //char[]
            Register((reader) => {
                if (reader.IsNull)
                    return null;

                reader.GetString(out var @string);
                return @string.ToArray();
            });
            //byte[]
            Register((reader) => {
                if (reader.IsNull)
                    return null;

                return Convert.FromBase64String(reader.GetString());
            });
            //Uri
            Register((reader) => {
                if (reader.IsNull)
                    return null;

                return new Uri(reader.GetString());
            });
            //DateTime
            Register((reader) =>
            {
                reader.GetString(out var @string);
                return DateTime.Parse(@string);
            });
            //DateTimeOffset
            Register((reader) =>
            {
                reader.GetString(out var @string);
                return DateTimeOffset.Parse(@string);
            });
            //TimeSpan
            Register((reader) => new TimeSpan(reader.GetInt64()));
            //Guid
            Register((reader) =>
            {
                reader.GetString(out var @string);
                return Guid.Parse(@string);
            });
            //DataTable
            Register((reader) => {
                if (reader.IsNull)
                    return null;

                if (!reader.IsStartArray)
                    throw new FormatException(nameof(reader));

                var dt = new DataTable();
                reader.Read();
                while (!reader.IsEndArray)
                {
                    if (!reader.IsStartObject)
                        throw new FormatException(nameof(reader));

                    var dr = dt.NewRow();
                    reader.Read();
                    while (reader.IsProperty)
                    {
                        var columnName = reader.GetProperty();
                        if (dt.Columns[columnName] == null)
                            dt.Columns.Add(new DataColumn(columnName));
                        reader.Read();
                        var columnValue = reader.IsNull ? null :
                        reader.IsString ? reader.GetString() :
                        reader.IsNumber ? GetNumber(reader) :
                        reader.IsBoolean ? reader.GetBoolean() :
                        reader.IsStartObject || reader.IsStartArray ? GetObject(reader) :
                        throw new FormatException(nameof(reader));
                        dr[columnName] = columnValue;
                        reader.Read();
                    }
                    if (!reader.IsEndObject)
                        throw new FormatException(nameof(reader));

                    dt.Rows.Add(dr);
                    reader.Read();
                }
                return dt;
            });

            //format
            RegisterProperty((property, value, reader) => {
                if (!property.CanWrite)
                    return null;

                var dataFormatAttribute = property.GetCustomAttribute<DataFormatAttribute>();
                if (dataFormatAttribute == null)
                    return null;

                RegisterProperty(property.PropertyType, dataFormatAttribute.Format, reader, out var expression);
                if (expression == null)
                    return null;

                return Expression.Assign(Expression.Property(value, property), expression);
            });
            //void Read(JsonReader,string)
            RegisterProperty(propertyFormat: (type, format, reader) =>
            {
                var read = type.GetMethod("Read", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(JsonReader), typeof(string) }, null);
                if (read == null)
                    return null;

                Constructor.Register(type, out var constructor, out _);
                if (constructor == null)
                    throw new ArgumentNullException(nameof(Constructor));

                var value = Expression.Variable(type, "value");
                return Expression.Block(new[] { value },
                    Expression.Assign(value, constructor),
                    Expression.Call(value, read, reader, Expression.Constant(format, typeof(string))),
                    value);
            });
            //Nullable<>
            RegisterProperty(propertyFormat: (type, format, reader) =>
            {
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Nullable<>))
                    return null;

                var nullableType = type.GetGenericArguments()[0];
                RegisterProperty(nullableType, format, reader, out var expression);
                if (expression == null)
                    return Expression.Empty();
                return Expression.Condition(
                    Expression.Property(reader, isNull),
                    Expression.Constant(null, type),
                    Expression.New(type.GetConstructor(new[] { nullableType }), expression));
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
        public static void RegisterProperty<T>(Predicate<string> formatPredicate, Func<JsonReader, T> propertyFormat)
        {
            if (formatPredicate == null)
                throw new ArgumentNullException(nameof(formatPredicate));
            if (propertyFormat == null)
                throw new ArgumentNullException(nameof(propertyFormat));

            RegisterProperty(propertyFormat: (type, format, reader) => {
                if (type != typeof(T) || !formatPredicate(format))
                    return null;

                return Expression.Invoke(Expression.Constant(propertyFormat), reader);
            });
        }
        public static void RegisterProperty<T>(Func<string, JsonReader, T> propertyFormat)
        {
            if (propertyFormat == null)
                throw new ArgumentNullException(nameof(propertyFormat));

            RegisterProperty(propertyFormat: (type, format, reader) => {
                if (type != typeof(T))
                    return null;

                return Expression.Invoke(Expression.Constant(propertyFormat), Expression.Constant(format, typeof(string)), reader);
            });
        }
        public static void RegisterProperty(Func<Type, string, ParameterExpression, Expression> propertyFormat)
        {
            if (propertyFormat == null)
                throw new ArgumentNullException(nameof(propertyFormat));

            lock (_Sync)
            {
                _PropertyFormats.Push(propertyFormat);
            }
        }
        public static void RegisterProperty(Type type, string format, ParameterExpression reader, out Expression expression)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            expression = null;
            lock (_Sync)
            {
                foreach (var propertyFormat in _PropertyFormats)
                {
                    expression = propertyFormat(type, format, reader);
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
        public static void RegisterProperty(PropertyInfo property, ParameterExpression value, ParameterExpression reader, out Expression expression)
        {
            if (property == null)
                throw new ArgumentNullException(nameof(property));
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            expression = null;
            lock (_Sync)
            {
                foreach (var handler in _PropertyHandlers)
                {
                    expression = handler(property, value, reader);
                    if (expression != null)
                    {
                        if (expression.Type == typeof(void) && expression.NodeType == ExpressionType.Default)
                            expression = null;
                        return;
                    }
                }
            }
        }
        public static void Register<T>(Func<JsonReader, T> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_Sync)
            {
                _Handlers.Push(new Tuple<Type, Delegate>(typeof(T), handler));
            }
        }
        public static void Register(Type type, Func<ParameterExpression, Expression> handler)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Register((_type, reader) => {
                if (_type != type)
                    return null;

                return handler(reader);
            });
        }
        public static void Register(Func<Type, ParameterExpression, Expression> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_Sync)
            {
                _Handlers.Push(handler);
            }
        }
        public static void Register<T>(Action<T, JsonReader> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Register((type, value, reader) => {
                if (type != typeof(T))
                    return null;

                return Expression.Invoke(Expression.Constant(handler), value, reader);
            });
        }
        public static void Register(Type type, Func<ParameterExpression, ParameterExpression, Expression> handler)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Register((_type, value, reader) => {
                if (_type != type)
                    return null;

                return handler(value, reader);
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
        public static void Register(Type type, ParameterExpression value, ParameterExpression reader, out Expression expression)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            expression = null;
            lock (_Sync)
            {
                foreach (var handler in _Handlers)
                {
                    if (handler is Func<Type, ParameterExpression, Expression> exprHandler)
                    {
                        expression = exprHandler.Invoke(type, reader);
                        if (expression != null)
                        {
                            expression = null;
                            return;
                        }
                    }
                    else if (handler is Func<Type, ParameterExpression, ParameterExpression, Expression> exprValueHandler)
                    {
                        expression = exprValueHandler.Invoke(type, value, reader);
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
                            expression = null;
                            return;
                        }
                    }
                }
            }
        }
        public static void Register(Type type, ParameterExpression reader, out Expression expression, out Delegate @delegate)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            expression = null;
            @delegate = null;
            lock (_Sync)
            {
                foreach (var handler in _Handlers)
                {
                    if (handler is Func<Type, ParameterExpression, Expression> exprHandler)
                    {
                        expression = exprHandler.Invoke(type, reader);
                        if (expression != null)
                        {
                            if (expression.Type == typeof(void) && expression.NodeType == ExpressionType.Default)
                                expression = null;
                            return;
                        }
                    }
                    else if (handler is Func<Type, ParameterExpression, ParameterExpression, Expression> exprValueHandler)
                    {
                        var value = Expression.Variable(type, "value");
                        expression = exprValueHandler.Invoke(type, value, reader);
                        if (expression != null)
                        {
                            if (expression.Type == typeof(void) && expression.NodeType == ExpressionType.Default)
                            {
                                expression = null;
                            }
                            else
                            {
                                Constructor.Register(type, out var constructor, out _);
                                if (constructor == null)
                                {
                                    expression = null;//throw??
                                }
                                else
                                {
                                    expression = Expression.Condition(
                                        Expression.Property(reader, "IsNull"),
                                        Expression.Default(type),
                                        Expression.Block(new[] { value },
                                            Expression.Assign(value, constructor),
                                            expression,
                                            value));
                                }
                            }
                            return;
                        }
                    }
                    else
                    {
                        (var _type, var _delegate) = (Tuple<Type, Delegate>)handler;
                        if (_type == type)
                        {
                            expression = Expression.Invoke(Expression.Constant(_delegate), reader);
                            @delegate = _delegate;
                            return;
                        }
                    }
                }
            }

        }
        public static void Register<T>(out Func<JsonReader, T> handler)
        {
            handler = Handler<T>.Value;
        }
        public static void Register(Type type, out Func<JsonReader, object> handler)
        {
            if (!_ObjHandlers.TryGetValue(type, out handler))
            {
                lock (_Sync)
                {
                    if (!_ObjHandlers.TryGetValue(type, out handler))
                    {
                        var reader = Expression.Parameter(typeof(JsonReader), "reader");
                        Register(type, reader, out var expression, out _);
                        if (expression != null)
                        {
                            handler = Expression.Lambda<Func<JsonReader, object>>(Expression.Convert(expression, typeof(object)), reader).Compile();
                        }
                        var objHandlers = new Dictionary<Type, Func<JsonReader, object>>(_ObjHandlers);
                        objHandlers.Add(type, handler);
                        _ObjHandlers = objHandlers;
                    }
                }
            }
        }
        #endregion
    }
}
