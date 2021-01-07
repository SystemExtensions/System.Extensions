
namespace System.Extensions.Http
{
    using System.Diagnostics;
    using System.Collections;
    using System.Collections.Generic;
    [DebuggerDisplay("Count = {Count}")]
    [DebuggerTypeProxy(typeof(DebugView))]
    public class HttpHeaders : IHttpHeaders
    {
        private static int _Capacity = 12;
        private static int _MaxCapacity = 100;
        private KeyValueCollection<string, string> _headers;
        public HttpHeaders()
        {
            _headers = new KeyValueCollection<string, string>(_Capacity, StringComparer.OrdinalIgnoreCase);
        }
        public HttpHeaders(int capacity)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            _headers = new KeyValueCollection<string, string>(capacity, StringComparer.OrdinalIgnoreCase);
        }
        public KeyValuePair<string, string> this[int index]
        {
            get => _headers[index];
            set
            {
                if (value.Key == null || value.Value == null)
                    throw new ArgumentException(nameof(value));

                for (int i = 0; i < value.Key.Length; i++)
                {
                    var temp = value.Key[i];
                    if (temp > 58 && temp <= byte.MaxValue)
                        continue;
                    if (temp >= 32 && temp < 58)
                        continue;
                    throw new ArgumentException($"Invalid HeaderName:{value.Key}");
                }
                for (int i = 0; i < value.Value.Length; i++)
                {
                    var temp = value.Value[i];
                    if (temp >= 32 && temp <= byte.MaxValue)
                        continue;
                    throw new ArgumentException($"Invalid HeaderValue:{value.Value}");
                }
                _headers[index] = value;
            }
        }
        public string this[string name]
        {
            get => _headers[name];
            set
            {
                if (name == null)
                    throw new ArgumentNullException(nameof(name));
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                for (int i = 0; i < name.Length; i++)
                {
                    var temp = name[i];
                    if (temp > 58 && temp <= byte.MaxValue)
                        continue;
                    if (temp >= 32 && temp < 58)
                        continue;
                    throw new ArgumentException($"Invalid HeaderName:{name}");
                }
                for (int i = 0; i < value.Length; i++)
                {
                    var temp = value[i];
                    if (temp >= 32 && temp <= byte.MaxValue)
                        continue;
                    throw new ArgumentException($"Invalid HeaderValue:{value}");
                }
                _headers[name] = value;

                if (_headers.Count > _MaxCapacity)
                    throw new InvalidOperationException(nameof(_MaxCapacity));
            }
        }
        public void Add(string name, string value)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            
            for (int i = 0; i < name.Length; i++)
            {
                var temp = name[i];//':'=58
                if (temp > 58 && temp <= byte.MaxValue)
                    continue;
                if (temp >= 32 && temp < 58)
                    continue;
                throw new ArgumentException($"Invalid HeaderName:{name}");
            }
            for (int i = 0; i < value.Length; i++)
            {
                var temp = value[i];
                if (temp >= 32 && temp <= byte.MaxValue)
                    continue;
                throw new ArgumentException($"Invalid HeaderValue:{value}");
            }
            _headers.Add(name, value);

            if (_headers.Count > _MaxCapacity)
                throw new InvalidOperationException(nameof(_MaxCapacity));
        }
        public int Remove(string name)
        {
            return _headers.Remove(name);
        }
        public void Clear() => _headers.Clear();
        public int Count => _headers.Count;
        public bool Contains(string name)
        {
            return _headers.ContainsKey(name);
        }
        public bool TryGetValue(string name,out string value)
        {
            return _headers.TryGetValue(name, out value);
        }
        public string[] GetValues(string name)
        {
            return _headers.GetValues(name);
        }
        public override string ToString()
        {
            if (_headers.Count == 0)
                return string.Empty;

            var sb = StringExtensions.ThreadRent(out var disposable);
            try
            {
                for (int i = 0; i < _headers.Count; i++)
                {
                    var item = _headers[i];
                    sb.Write(item.Key);
                    sb.Write(": ");
                    sb.Write(item.Value);
                    sb.Write("\r\n");
                }
                sb.Write("\r\n");
                return sb.ToString();
            }
            finally
            {
                disposable.Dispose();
            }
        }
        public Enumerator GetEnumerator() => new Enumerator(_headers);
        public struct Enumerator
        {
            internal Enumerator(KeyValueCollection<string,string> headers)
            {
                _headers = headers;
                _index = 0;
                _current = default;
            }

            private int _index;
            private KeyValuePair<string, string> _current;
            private KeyValueCollection<string, string> _headers;
            public KeyValuePair<string, string> Current => _current;
            public bool MoveNext()
            {
                if (_index < _headers.Count)
                {
                    _current = _headers[_index];
                    _index++;
                    return true;
                }
                return false;
            }
        }
        #region IEnumerable
        IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator()
        {
            return _headers.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _headers.GetEnumerator();
        }
        #endregion
        private class DebugView
        {
            public DebugView(HttpHeaders headers)
            {
                _headers = headers;
            }
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private HttpHeaders _headers;
            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            internal KeyValuePair<string, string>[] Items
            {
                get
                {
                    var items = new KeyValuePair<string, string>[_headers.Count];
                    for (int i = 0; i < items.Length; i++)
                    {
                        items[i] = _headers[i];
                    }
                    return items;
                }
            }
        }

        #region TryParse
        //TODO?? IList<KeyValuePair<string,string>> @params OR ReadOnlySpanAction<char,char>
        //OR bool TryParse(ref ReadOnlySpan<char> header, out string value, KeyValueCollection<string,string> headerParams) 
        //out bool paramName1Exist
        //{value}; {paramName}={paramValue}; {paramName}={paramValue},{value}; {paramName}={paramValue}; {paramName}={paramValue}
        public static bool TryParse(ReadOnlySpan<char> header, out ReadOnlySpan<char> value)
        {
            //if (TryParse(ref header, out value)) 
            //{
            //    if (header != null) 
            //    {
            //        //DO
            //        return false;//
            //    }
            //}
            return TryParse(ref header, out value);
        }
        public static bool TryParse(ReadOnlySpan<char> header, out ReadOnlySpan<char> value, string paramName1, out ReadOnlySpan<char> paramValue1)
        {
            return TryParse(ref header, out value, paramName1, out paramValue1);
        }
        public static bool TryParse(ReadOnlySpan<char> header, out ReadOnlySpan<char> value, string paramName1, out ReadOnlySpan<char> paramValue1, string paramName2, out ReadOnlySpan<char> paramValue2)
        {
            return TryParse(ref header, out value, paramName1, out paramValue1, paramName2, out paramValue2);
        }
        public static bool TryParse(ReadOnlySpan<char> header, out ReadOnlySpan<char> value, string paramName1, out ReadOnlySpan<char> paramValue1, string paramName2, out ReadOnlySpan<char> paramValue2, string paramName3, out ReadOnlySpan<char> paramValue3)
        {
            return TryParse(ref header, out value, paramName1, out paramValue1, paramName2, out paramValue2, paramName3, out paramValue3);
        }
        public static bool TryParse(ref ReadOnlySpan<char> header, out ReadOnlySpan<char> value)
        {
            if (header == null)
            {
                value = null;
                return false;
            }
            var span = header.TrimStart();
            if (span.IsEmpty)
            {
                header = null;
                value = string.Empty;
                return true;
            }
            //value
            if (span[0] == '"')
            {
                //QuotedString
                span = span.Slice(1);
                var index = span.IndexOf('"');
                if (index == -1)
                {
                    value = null;
                    return false;
                }
                value = span.Slice(0, index);
                span = span.Slice(index + 1);
                if (span.IsEmpty)
                {
                    header = null;
                    return true;
                }
                if (span[0] == ',')
                {
                    header = span.Slice(1);
                    return true;
                }
                if (span[0] != ';')
                {
                    value = null;
                    return false;
                }

                span = span.Slice(1);
            }
            else
            {
                for (var index = 0; ;)
                {
                    var temp = span[index++];
                    if (temp == ',')
                    {
                        value = span.Slice(0, index - 1);
                        header = span.Slice(index);
                        return true;
                    }
                    else if (temp == ';')
                    {
                        value = span.Slice(0, index - 1);
                        span = span.Slice(index);
                        break;
                    }
                    else if (index == span.Length)
                    {
                        value = span.Slice(0, index);
                        header = null;
                        return true;
                    }
                }
            }

            var tempOffset = 0;//-1==Reutrn 
            ReadOnlySpan<char> paramName = null;//On Set TrimStart
            ReadOnlySpan<char> paramValue = null;
            for (var index = 0; ;)
            {
                if (index == span.Length)
                {
                    if (paramName == null)
                    {
                        paramName = span.Slice(tempOffset, index - tempOffset).TrimStart();
                        paramValue = string.Empty;
                    }
                    else
                    {
                        paramValue = span.Slice(tempOffset, index - tempOffset);
                    }
                    tempOffset = -1;
                    header = null;
                    goto param;
                }
                switch (span[index++])
                {
                    case ';':
                        if (paramName == null)
                        {
                            paramName = span.Slice(tempOffset, index - tempOffset - 1).TrimStart();
                            paramValue = null;
                        }
                        else
                        {
                            paramValue = span.Slice(tempOffset, index - tempOffset - 1);
                        }
                        tempOffset = index;
                        goto param;
                    case '=':
                        if (paramName != null)
                            continue;
                        paramName = span.Slice(tempOffset, index - tempOffset - 1).TrimStart();
                        if (index == span.Length)
                        {
                            paramValue = string.Empty;
                            tempOffset = -1;
                            header = null;
                            goto param;
                        }
                        if (span[index] == '"')
                        {
                            var tempSpan = span.Slice(index + 1);
                            var tempIndex = tempSpan.IndexOf('"');
                            if (tempIndex == -1)
                                throw new FormatException();
                            paramValue = tempSpan.Slice(0, tempIndex);
                            index += tempIndex + 2;
                            if (index == span.Length)
                            {
                                tempOffset = -1;
                                header = null;
                            }
                            else if (span[index] == ';')
                            {
                                index += 1;
                                tempOffset = index;
                            }
                            else if (span[index] == ',')
                            {
                                header = span.Slice(index + 1);
                                tempOffset = -1;
                            }
                            else
                            {
                                return false;
                            }
                            goto param;
                        }
                        tempOffset = index;
                        continue;
                    case ',':
                        if (paramName == null)
                        {
                            paramName = span.Slice(tempOffset, index - tempOffset - 1);
                            paramValue = null;
                        }
                        else
                        {
                            paramValue = span.Slice(tempOffset, index - tempOffset - 1);
                        }
                        header = span.Slice(index);
                        tempOffset = -1;
                        goto param;
                    default:
                        continue;
                }
            param:
                //Debug.WriteLine("Name:" + new string(paramName));
                //Debug.WriteLine("Value:" + new string(paramValue));
                if (tempOffset == -1)
                    return true;
                paramName = null;
                paramValue = null;
            }
        }
        public static bool TryParse(ref ReadOnlySpan<char> header, out ReadOnlySpan<char> value, string paramName1, out ReadOnlySpan<char> paramValue1)
        {
            if (paramName1 == null)
                throw new ArgumentNullException(nameof(paramName1));

            paramValue1 = null;
            if (header == null)
            {
                value = null;
                return false;
            }
            var span = header.TrimStart();
            if (span.IsEmpty)
            {
                header = null;
                value = string.Empty;
                return true;
            }
            //value
            if (span[0] == '"')
            {
                //QuotedString
                span = span.Slice(1);
                var index = span.IndexOf('"');
                if (index == -1)
                {
                    value = null;
                    return false;
                }
                value = span.Slice(0, index);
                span = span.Slice(index + 1);
                if (span.IsEmpty)
                {
                    header = null;
                    return true;
                }
                if (span[0] == ',')
                {
                    header = span.Slice(1);
                    return true;
                }
                if (span[0] != ';')
                {
                    value = null;
                    return false;
                }

                span = span.Slice(1);
            }
            else
            {
                for (var index = 0; ;)
                {
                    var temp = span[index++];
                    if (temp == ',')
                    {
                        value = span.Slice(0, index - 1);
                        header = span.Slice(index);
                        return true;
                    }
                    else if (temp == ';')
                    {
                        value = span.Slice(0, index - 1);
                        span = span.Slice(index);
                        break;
                    }
                    else if (index == span.Length)
                    {
                        value = span.Slice(0, index);
                        header = null;
                        return true;
                    }
                }
            }

            var tempOffset = 0;
            ReadOnlySpan<char> paramName = null;
            ReadOnlySpan<char> paramValue = null;
            for (var index = 0; ;)
            {
                if (index == span.Length)
                {
                    if (paramName == null)
                    {
                        paramName = span.Slice(tempOffset, index - tempOffset).TrimStart();
                        paramValue = string.Empty;
                    }
                    else
                    {
                        paramValue = span.Slice(tempOffset, index - tempOffset);
                    }
                    tempOffset = -1;
                    header = null;
                    goto param;
                }
                switch (span[index++])
                {
                    case ';':
                        if (paramName == null)
                        {
                            paramName = span.Slice(tempOffset, index - tempOffset - 1).TrimStart();
                            paramValue = null;
                        }
                        else
                        {
                            paramValue = span.Slice(tempOffset, index - tempOffset - 1);
                        }
                        tempOffset = index;
                        goto param;
                    case '=':
                        if (paramName != null)
                            continue;
                        paramName = span.Slice(tempOffset, index - tempOffset - 1).TrimStart();
                        if (index == span.Length)
                        {
                            paramValue = string.Empty;
                            tempOffset = -1;
                            header = null;
                            goto param;
                        }
                        if (span[index] == '"')
                        {
                            var tempSpan = span.Slice(index + 1);
                            var tempIndex = tempSpan.IndexOf('"');
                            if (tempIndex == -1)
                                throw new FormatException();
                            paramValue = tempSpan.Slice(0, tempIndex);
                            index += tempIndex + 2;
                            if (index == span.Length)
                            {
                                tempOffset = -1;
                                header = null;
                            }
                            else if (span[index] == ';')
                            {
                                index += 1;
                                tempOffset = index;
                            }
                            else if (span[index] == ',')
                            {
                                header = span.Slice(index + 1);
                                tempOffset = -1;
                            }
                            else
                            {
                                return false;
                            }
                            goto param;
                        }
                        tempOffset = index;
                        continue;
                    case ',':
                        if (paramName == null)
                        {
                            paramName = span.Slice(tempOffset, index - tempOffset - 1);
                            paramValue = null;
                        }
                        else
                        {
                            paramValue = span.Slice(tempOffset, index - tempOffset - 1);
                        }
                        header = span.Slice(index);
                        tempOffset = -1;
                        goto param;
                    default:
                        continue;
                }
            param:
                //Debug.WriteLine("Name:" + new string(paramName));
                //Debug.WriteLine("Value:" + new string(paramValue));
                if (paramValue1 == null && paramName.EqualsIgnoreCase(paramName1))
                {
                    paramValue1 = paramValue;
                }

                if (tempOffset == -1)
                    return true;
                paramName = null;
                paramValue = null;
            }
        }
        public static bool TryParse(ref ReadOnlySpan<char> header, out ReadOnlySpan<char> value, string paramName1, out ReadOnlySpan<char> paramValue1, string paramName2, out ReadOnlySpan<char> paramValue2)
        {
            if (paramName1 == null)
                throw new ArgumentNullException(nameof(paramName1));
            if (paramName2 == null)
                throw new ArgumentNullException(nameof(paramName2));

            paramValue1 = null;
            paramValue2 = null;
            if (header == null)
            {
                value = null;
                return false;
            }
            var span = header.TrimStart();
            if (span.IsEmpty)
            {
                header = null;
                value = string.Empty;
                return true;
            }
            //value
            if (span[0] == '"')
            {
                //QuotedString
                span = span.Slice(1);
                var index = span.IndexOf('"');
                if (index == -1)
                {
                    value = null;
                    return false;
                }
                value = span.Slice(0, index);
                span = span.Slice(index + 1);
                if (span.IsEmpty)
                {
                    header = null;
                    return true;
                }
                if (span[0] == ',')
                {
                    header = span.Slice(1);
                    return true;
                }
                if (span[0] != ';')
                {
                    value = null;
                    return false;
                }

                span = span.Slice(1);
            }
            else
            {
                for (var index = 0; ;)
                {
                    var temp = span[index++];
                    if (temp == ',')
                    {
                        value = span.Slice(0, index - 1);
                        header = span.Slice(index);
                        return true;
                    }
                    else if (temp == ';')
                    {
                        value = span.Slice(0, index - 1);
                        span = span.Slice(index);
                        break;
                    }
                    else if (index == span.Length)
                    {
                        value = span.Slice(0, index);
                        header = null;
                        return true;
                    }
                }
            }

            var tempOffset = 0;
            ReadOnlySpan<char> paramName = null;
            ReadOnlySpan<char> paramValue = null;
            for (var index = 0; ;)
            {
                if (index == span.Length)
                {
                    if (paramName == null)
                    {
                        paramName = span.Slice(tempOffset, index - tempOffset).TrimStart();
                        paramValue = string.Empty;
                    }
                    else
                    {
                        paramValue = span.Slice(tempOffset, index - tempOffset);
                    }
                    tempOffset = -1;
                    header = null;
                    goto param;
                }
                switch (span[index++])
                {
                    case ';':
                        if (paramName == null)
                        {
                            paramName = span.Slice(tempOffset, index - tempOffset - 1).TrimStart();
                            paramValue = null;
                        }
                        else
                        {
                            paramValue = span.Slice(tempOffset, index - tempOffset - 1);
                        }
                        tempOffset = index;
                        goto param;
                    case '=':
                        if (paramName != null)
                            continue;
                        paramName = span.Slice(tempOffset, index - tempOffset - 1).TrimStart();
                        if (index == span.Length)
                        {
                            paramValue = string.Empty;
                            tempOffset = -1;
                            header = null;
                            goto param;
                        }
                        if (span[index] == '"')
                        {
                            var tempSpan = span.Slice(index + 1);
                            var tempIndex = tempSpan.IndexOf('"');
                            if (tempIndex == -1)
                                throw new FormatException();
                            paramValue = tempSpan.Slice(0, tempIndex);
                            index += tempIndex + 2;
                            if (index == span.Length)
                            {
                                tempOffset = -1;
                                header = null;
                            }
                            else if (span[index] == ';')
                            {
                                index += 1;
                                tempOffset = index;
                            }
                            else if (span[index] == ',')
                            {
                                header = span.Slice(index + 1);
                                tempOffset = -1;
                            }
                            else
                            {
                                return false;
                            }
                            goto param;
                        }
                        tempOffset = index;
                        continue;
                    case ',':
                        if (paramName == null)
                        {
                            paramName = span.Slice(tempOffset, index - tempOffset - 1);
                            paramValue = null;
                        }
                        else
                        {
                            paramValue = span.Slice(tempOffset, index - tempOffset - 1);
                        }
                        header = span.Slice(index);
                        tempOffset = -1;
                        goto param;
                    default:
                        continue;
                }
            param:
                //Debug.WriteLine("Name:" + new string(paramName));
                //Debug.WriteLine("Value:" + new string(paramValue));
                if (paramValue1 == null && paramName.EqualsIgnoreCase(paramName1))
                {
                    paramValue1 = paramValue;
                }
                else if (paramValue2 == null && paramName.EqualsIgnoreCase(paramName2))
                {
                    paramValue2 = paramValue;
                }

                if (tempOffset == -1)
                    return true;
                paramName = null;
                paramValue = null;
            }
        }
        public static bool TryParse(ref ReadOnlySpan<char> header, out ReadOnlySpan<char> value, string paramName1, out ReadOnlySpan<char> paramValue1, string paramName2, out ReadOnlySpan<char> paramValue2, string paramName3, out ReadOnlySpan<char> paramValue3)
        {
            if (paramName1 == null)
                throw new ArgumentNullException(nameof(paramName1));
            if (paramName2 == null)
                throw new ArgumentNullException(nameof(paramName2));
            if (paramName3 == null)
                throw new ArgumentNullException(nameof(paramName3));

            paramValue1 = null;
            paramValue2 = null;
            paramValue3 = null;
            if (header == null)
            {
                value = null;
                return false;
            }
            var span = header.TrimStart();
            if (span.IsEmpty)
            {
                header = null;
                value = string.Empty;
                return true;
            }
            //value
            if (span[0] == '"')
            {
                //QuotedString
                span = span.Slice(1);
                var index = span.IndexOf('"');
                if (index == -1)
                {
                    value = null;
                    return false;
                }
                value = span.Slice(0, index);
                span = span.Slice(index + 1);
                if (span.IsEmpty)
                {
                    header = null;
                    return true;
                }
                if (span[0] == ',')
                {
                    header = span.Slice(1);
                    return true;
                }
                if (span[0] != ';')
                {
                    value = null;
                    return false;
                }

                span = span.Slice(1);
            }
            else
            {
                for (var index = 0; ;)
                {
                    var temp = span[index++];
                    if (temp == ',')
                    {
                        value = span.Slice(0, index - 1);
                        header = span.Slice(index);
                        return true;
                    }
                    else if (temp == ';')
                    {
                        value = span.Slice(0, index - 1);
                        span = span.Slice(index);
                        break;
                    }
                    else if (index == span.Length)
                    {
                        value = span.Slice(0, index);
                        header = null;
                        return true;
                    }
                }
            }

            var tempOffset = 0;
            ReadOnlySpan<char> paramName = null;
            ReadOnlySpan<char> paramValue = null;
            for (var index = 0; ;)
            {
                if (index == span.Length)
                {
                    if (paramName == null)
                    {
                        paramName = span.Slice(tempOffset, index - tempOffset).TrimStart();
                        paramValue = string.Empty;
                    }
                    else
                    {
                        paramValue = span.Slice(tempOffset, index - tempOffset);
                    }
                    tempOffset = -1;
                    header = null;
                    goto param;
                }
                switch (span[index++])
                {
                    case ';':
                        if (paramName == null)
                        {
                            paramName = span.Slice(tempOffset, index - tempOffset - 1).TrimStart();
                            paramValue = null;
                        }
                        else
                        {
                            paramValue = span.Slice(tempOffset, index - tempOffset - 1);
                        }
                        tempOffset = index;
                        goto param;
                    case '=':
                        if (paramName != null)
                            continue;
                        paramName = span.Slice(tempOffset, index - tempOffset - 1).TrimStart();
                        if (index == span.Length)
                        {
                            paramValue = string.Empty;
                            tempOffset = -1;
                            header = null;
                            goto param;
                        }
                        if (span[index] == '"')
                        {
                            var tempSpan = span.Slice(index + 1);
                            var tempIndex = tempSpan.IndexOf('"');
                            if (tempIndex == -1)
                                throw new FormatException();
                            paramValue = tempSpan.Slice(0, tempIndex);
                            index += tempIndex + 2;
                            if (index == span.Length)
                            {
                                tempOffset = -1;
                                header = null;
                            }
                            else if (span[index] == ';')
                            {
                                index += 1;
                                tempOffset = index;
                            }
                            else if (span[index] == ',')
                            {
                                header = span.Slice(index + 1);
                                tempOffset = -1;
                            }
                            else
                            {
                                return false;
                            }
                            goto param;
                        }
                        tempOffset = index;
                        continue;
                    case ',':
                        if (paramName == null)
                        {
                            paramName = span.Slice(tempOffset, index - tempOffset - 1);
                            paramValue = null;
                        }
                        else
                        {
                            paramValue = span.Slice(tempOffset, index - tempOffset - 1);
                        }
                        header = span.Slice(index);
                        tempOffset = -1;
                        goto param;
                    default:
                        continue;
                }
            param:
                //Debug.WriteLine("Name:" + new string(paramName));
                //Debug.WriteLine("Value:" + new string(paramValue));
                if (paramValue1 == null && paramName.EqualsIgnoreCase(paramName1))
                {
                    paramValue1 = paramValue;
                }
                else if (paramValue2 == null && paramName.EqualsIgnoreCase(paramName2))
                {
                    paramValue2 = paramValue;
                }
                else if (paramValue3 == null && paramName.EqualsIgnoreCase(paramName3))
                {
                    paramValue3 = paramValue;
                }

                if (tempOffset == -1)
                    return true;
                paramName = null;
                paramValue = null;
            }
        }
        #endregion

        #region const
        public const string Accept = "Accept";
        public const string AcceptCharset = "Accept-Charset";
        public const string AcceptEncoding = "Accept-Encoding";
        public const string AcceptLanguage = "Accept-Language";
        //public const string AcceptRanges="Accept-Ranges";
        public const string Authorization = "Authorization";
        public const string CacheControl = "Cache-Control";
        public const string Connection = "Connection";
        public const string Cookie = "Cookie";
        public const string ContentLength = "Content-Length";
        public const string ContentType = "Content-Type";
        public const string Date = "Date";
        public const string Expect = "Expect";
        public const string From = "From";
        public const string Host = "Host";
        public const string IfMatch = "If-Match";
        public const string IfModifiedSince = "If-Modified-Since";
        public const string IfNoneMatch = "If-None-Match";
        public const string IfRange = "If-Range";
        public const string IfUnmodifiedSince = "If-Unmodified-Since";
        public const string MaxForwards = "Max-Forwards";
        public const string Pragma = "Pragma";
        public const string ProxyAuthorization = "Proxy-Authorization";
        public const string Range = "Range";
        public const string Referer = "Referer";
        public const string TE = "TE";
        public const string Upgrade = "Upgrade";
        public const string UserAgent = "User-Agent";
        public const string Via = "Via";
        public const string Warning = "Warning";
        public const string TransferEncoding = "Transfer-Encoding";
        public const string Origin = "Origin";
        public const string UpgradeInsecureRequests = "Upgrade-Insecure-Requests";


        public const string AcceptRanges = "Accept-Ranges";
        public const string Age = "Age";
        public const string Allow = "Allow";
        //public const string CacheControl = "Cache-Control";
        //public const string Connection = "Connection";
        public const string ContentEncoding = "Content-Encoding";
        public const string ContentLanguage = "Content-Language";
        //public const string ContentLength="Content-Length";
        public const string ContentLocation = "Content-Location";
        public const string ContentMD5 = "Content-MD5";
        public const string ContentRange = "Content-Range";
        //public const string ContentType="Content-Type";
        //public const string Date = "Date";
        public const string ETag = "ETag";
        public const string Expires = "Expires";
        public const string LastModified = "Last-Modified";
        public const string Location = "Location";
        //public const string Pragma = "Pragma";
        public const string ProxyAuthenticate = "Proxy-Authenticate";
        public const string Refresh = "Refresh";
        public const string RetryAfter = "Retry-After";
        public const string Server = "Server";
        public const string SetCookie = "Set-Cookie";
        public const string Trailer = "Trailer";
        //public const string TransferEncoding="Transfer-Encoding";
        public const string Vary = "Vary";
        //public const string Via = "Via";
        //public const string Warning = "Warning";
        public const string WwwAuthenticate = "WWW-Authenticate";
        //public const string Origin = "Origin";
        public const string ContentDisposition = "Content-Disposition";

        public const string AccessControlAllowOrigin = "Access-Control-Allow-Origin";
        public const string Link = "Link";
        public const string StrictTransportSecurity = "Strict-Transport-Security";

        //Content-Security-Policy: upgrade-insecure-requests
        #endregion
    }
}
