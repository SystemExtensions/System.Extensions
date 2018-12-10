
namespace System.Text
{
    using System.Buffers;
    using System.Collections;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    public class StringBuffer//继承 IBufferWriter<char>
    {
        public StringBuffer()
            : this(16) { }
        public StringBuffer(int bufferSize)
        {
            if (bufferSize < 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));

            _segments = Array.Empty<ReadOnlyMemory<char>>();
            _buffer = bufferSize > 0 ? new char[bufferSize] : Array.Empty<char>();
            _chars = _buffer;
            _available = _chars.Length;
        }

        private int _length;
        private char[] _buffer;
        private int _pos = -1;
        private ReadOnlyMemory<char>[] _segments;
        private char[] _chars;
        private int _available;
        public int Length => _length + (_chars.Length - _available);
        private void TryAdd(int length = 16)
        {
            if (_available > 0)
                return;

            if (_chars.Length > 0)
            {
                _pos++;
                if (_pos == _segments.Length)
                {
                    int newLength = _segments.Length * 2;
                    var newSegments = new ReadOnlyMemory<char>[newLength > 4 ? newLength : 4];
                    Array.Copy(_segments, newSegments, _segments.Length);
                    _segments = newSegments;
                }
                _length += _chars.Length;
                _segments[_pos] = _chars.AsMemory();
            }
            _chars = new char[length > 16 ? length : 16];
            _available = _chars.Length;
        }
        private void Add(int length)
        {
            var temp = _chars.Length - _available;
            if (temp > 0)
            {
                _pos++;
                if (_pos == _segments.Length)
                {
                    int newLength = _segments.Length * 2;
                    var newSegments = new ReadOnlyMemory<char>[newLength > 4 ? newLength : 4];
                    Array.Copy(_segments, newSegments, _segments.Length);
                    _segments = newSegments;
                }
                _length += temp;
                _segments[_pos] = _chars.AsMemory(0, temp);
            }
            _chars = new char[length > 16 ? length : 16];
            _available = _chars.Length;
        }
        public void Append(char ch)
        {
            if (_available > 0)
            {
                _chars[_chars.Length - _available] = ch;
                _available -= 1;
                return;
            }

            TryAdd();
            _chars[_chars.Length - _available] = ch;
            _available -= 1;
        }
        public void Append(char ch, int repeatCount)
        {
            if (repeatCount <= 0)
                return;

            if (_available >= repeatCount)
            {
                var offset = _chars.Length - _available;
                var endOffset = offset + repeatCount;
                for (; offset < endOffset; offset++)
                {
                    _chars[offset] = ch;
                }

                _available -= repeatCount;
            }
            else
            {
                var tempCount = repeatCount;
                do
                {
                    TryAdd(repeatCount);
                    var charsToCopy = tempCount < _available ? tempCount : _available;
                    var offset = _chars.Length - _available;
                    var endOffset = offset + charsToCopy;
                    for (; offset < endOffset; offset++)
                    {
                        _chars[offset] = ch;
                    }
                    _available -= charsToCopy;
                    tempCount -= charsToCopy;
                } while (tempCount > 0);
            }
        }
        public void Append(long value)
        {
            const int maxSize = 20;
            if (_available >= maxSize)
            {
                value.TryFormat(_chars.AsSpan(_chars.Length - _available), out var charsWritten);
                _available -= charsWritten;
            }
            else
            {
                Span<char> dest = stackalloc char[maxSize];
                value.TryFormat(dest, out var charsWritten);
                Append(dest.Slice(0, charsWritten));
            }
        }
        public void Append(int value)
        {
            const int maxSize = 11;
            if (_available >= maxSize)
            {
                value.TryFormat(_chars.AsSpan(_chars.Length - _available), out var charsWritten);
                _available -= charsWritten;
            }
            else
            {
                Span<char> dest = stackalloc char[maxSize];
                value.TryFormat(dest, out var charsWritten);
                Append(dest.Slice(0, charsWritten));
            }
        }
        public void Append(ulong value)
        {
            const int maxSize = 20;
            if (_available >= maxSize)
            {
                value.TryFormat(_chars.AsSpan(_chars.Length - _available), out var charsWritten);
                _available -= charsWritten;
            }
            else
            {
                Span<char> dest = stackalloc char[maxSize];
                value.TryFormat(dest, out var charsWritten);
                Append(dest.Slice(0, charsWritten));
            }
        }
        public void Append(uint value)
        {
            const int maxSize = 10;
            if (_available >= maxSize)
            {
                value.TryFormat(_chars.AsSpan(_chars.Length - _available), out var charsWritten);
                _available -= charsWritten;
            }
            else
            {
                Span<char> dest = stackalloc char[maxSize];
                value.TryFormat(dest, out var charsWritten);
                Append(dest.Slice(0, charsWritten));
            }
        }
        public void Append(string value)
        {
            if (value == null || value.Length == 0)
                return;

            Append(value.AsSpan());
        }
        public void Append(char[] value)
        {
            if (value == null || value.Length == 0)
                return;

            Append(value.AsSpan());
        }
        public void Append(char[] value, int offset, int count)
        {
            if (value == null || count == 0)
                return;

            Append(value.AsSpan(offset, count));
        }
        public void Append(ReadOnlySpan<char> value)
        {
            if (value.IsEmpty)
                return;

            unsafe
            {

                fixed (char* pValue = &MemoryMarshal.GetReference(value))
                {
                    Append(pValue, value.Length);
                }
            }
        }
        public unsafe void Append(char* pValue, int count)
        {
            if (count <= 0)
                return;

            var pData = pValue;
            var tempCount = count;
            do
            {
                TryAdd(tempCount);
                var charsToCopy = tempCount < _available ? tempCount : _available;
                fixed (char* pDest = _chars)
                {
                    var toCopy = charsToCopy * 2;
                    Buffer.MemoryCopy(pData, pDest + (_chars.Length - _available), toCopy, toCopy);
                }
                pData += charsToCopy;
                tempCount -= charsToCopy;
                _available -= charsToCopy;
            }
            while (tempCount > 0);
        }
        public void Append(ReadOnlySpan<byte> value, Decoder decoder)
        {
            Append(value, true, decoder);
        }
        public void Append(ReadOnlySpan<byte> value, bool flush, Decoder decoder)
        {
            if (value.IsEmpty)
                return;

            unsafe
            {
                fixed (byte* pData = &MemoryMarshal.GetReference(value))
                {
                    Append(pData, value.Length, flush, decoder);
                }
            }
        }
        public void Append(byte[] value, Decoder decoder)
        {
            Append(value, true, decoder);
        }
        public void Append(byte[] value, bool flush, Decoder decoder)
        {
            if (value == null || value.Length == 0)
                return;

            unsafe
            {
                fixed (byte* pData = value)
                {
                    Append(pData, value.Length, flush, decoder);
                }
            }
        }
        public void Append(byte[] value, int offset, int count, Decoder decoder)
        {
            Append(value, offset, count, true, decoder);
        }
        public void Append(byte[] value, int offset, int count, bool flush, Decoder decoder)
        {
            if (count == 0)
                return;

            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (offset < 0 || offset >= value.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count <= 0 || count > value.Length - offset)
                throw new ArgumentOutOfRangeException(nameof(count));

            unsafe
            {
                fixed (byte* pData = value)
                {
                    Append(pData + offset, count, flush, decoder);
                }
            }
        }
        public unsafe void Append(byte* pValue, int count, bool flush, Decoder decoder)
        {
            if (decoder == null)
                throw new ArgumentNullException(nameof(decoder));

            if (count <= 0)
                return;

            var pData = pValue;
            var tempCount = count;
            bool completed; int charsUsed, bytesUsed;
            do
            {
                TryAdd(tempCount);
                fixed (char* pDest = _chars)
                {
                    decoder.Convert(pData, tempCount, pDest + (_chars.Length - _available), _available, flush, out bytesUsed, out charsUsed, out completed);
                    tempCount -= bytesUsed;
                    _available -= charsUsed;
                    pData += bytesUsed;
                }
            } while (tempCount > 0);
        }
        public void CopyTo(Span<char> dest)
        {
            var length = Length;
            if (length == 0)
                return;

            if (length > dest.Length)
                throw new InvalidOperationException(nameof(dest));

            unsafe
            {
                fixed (char* pDest = &MemoryMarshal.GetReference(dest))
                {
                    var offset = 0;
                    for (int i = 0; i <= _pos; i++)
                    {
                        var segment = _segments[i];
                        fixed (char* pSrc = &MemoryMarshal.GetReference(segment.Span))
                        {
                            var toCopy = segment.Length * 2;
                            Buffer.MemoryCopy(pSrc, pDest + offset, toCopy, toCopy);
                            offset += segment.Length;
                        }
                    }

                    var temp = _chars.Length - _available;
                    if (temp > 0)
                    {
                        fixed (char* pSrc = _chars)
                        {
                            var toCopy = temp * 2;
                            Buffer.MemoryCopy(pSrc, pDest + offset, toCopy, toCopy);
                        }
                    }
                }
            }


        }
        public void Clear()
        {
            if (_pos != -1)
            {
                _segments = Array.Empty<ReadOnlyMemory<char>>();
                _pos = -1;
            }
            _length = 0;
            _chars = _buffer;
            _available = _chars.Length;
        }
        public override string ToString()
        {
            var length = Length;
            if (length == 0)
                return string.Empty;

            var value = new string('\0', length);
            unsafe
            {
                fixed (char* pDest = value)
                {
                    var offset = 0;
                    for (int i = 0; i <= _pos; i++)
                    {
                        var segment = _segments[i];
                        fixed (char* pSrc = &MemoryMarshal.GetReference(segment.Span))
                        {
                            var toCopy = segment.Length * 2;
                            Buffer.MemoryCopy(pSrc, pDest + offset, toCopy, toCopy);
                            offset += segment.Length;
                        }
                    }

                    var temp = _chars.Length - _available;
                    if (temp > 0)
                    {
                        fixed (char* pSrc = _chars)
                        {
                            var toCopy = temp * 2;
                            Buffer.MemoryCopy(pSrc, pDest + offset, toCopy, toCopy);
                        }
                    }
                    return value;
                }
            }
        }
        public string ToString(int startIndex)
        {
            var length = Length;
            if (startIndex < 0 || startIndex >= length)
                throw new ArgumentOutOfRangeException(nameof(startIndex));

            var count = length - startIndex;
            if (count == 0)
                return string.Empty;
            var value = new string('\0', count);
            unsafe
            {
                fixed (char* pDest = value)
                {
                    var index = 0;
                    var tempCount = count;
                    for (int i = 0; i <= _pos; i++)
                    {
                        var segment = _segments[i];
                        var tempIndex = index + segment.Length;
                        if (tempIndex >= startIndex)
                        {
                            var toCopyX = index >= startIndex ? 0 : startIndex - index;
                            var toCopyCount = segment.Length - toCopyX;
                            var toCopyIndex = count - tempCount;
                            fixed (char* pSrc = &MemoryMarshal.GetReference(segment.Span))
                            {
                                var toCopy = toCopyCount * 2;
                                Buffer.MemoryCopy(pSrc + toCopyX, pDest + toCopyIndex, toCopy, toCopy);
                            }
                            tempCount -= toCopyCount;
                        }
                        index = tempIndex;
                    }
                    if (tempCount > 0)
                    {
                        var segLength = _chars.Length - _available;//最后一个段
                        var tempIndex = index + segLength;
                        var toCopyX = index >= startIndex ? 0 : startIndex - index;
                        var toCopyCount = segLength - toCopyX;
                        var toCopyIndex = count - tempCount;
                        fixed (char* pSrc = _chars)
                        {
                            var toCopy = toCopyCount * 2;
                            Buffer.MemoryCopy(pSrc + toCopyX, pDest + toCopyIndex, toCopy, toCopy);
                        }
                    }
                }
                return value;
            }

        }
        public string ToString(int startIndex, int count)
        {
            if (count == 0)
                return string.Empty;

            var length = Length;
            if (startIndex < 0 || startIndex >= length)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            if (count < 0 || count > length - startIndex)
                throw new ArgumentOutOfRangeException(nameof(count));

            var value = new string('\0', count);
            unsafe
            {
                fixed (char* pDest = value)
                {
                    var index = 0;
                    var tempCount = count;
                    var endIndex = startIndex + count;
                    for (int i = 0; i <= _pos; i++)
                    {
                        if (tempCount == 0)
                            return value;
                        var segment = _segments[i];
                        var tempIndex = index + segment.Length;
                        if (tempIndex >= startIndex)
                        {
                            var toCopyX = index >= startIndex ? 0 : startIndex - index;
                            var toCopyY = tempIndex <= endIndex ? 0 : tempIndex - endIndex;
                            var toCopyCount = segment.Length - toCopyX - toCopyY;
                            var toCopyIndex = count - tempCount;
                            fixed (char* pSrc = &MemoryMarshal.GetReference(segment.Span))
                            {
                                var toCopy = toCopyCount * 2;
                                Buffer.MemoryCopy(pSrc + toCopyX, pDest + toCopyIndex, toCopy, toCopy);
                            }
                            tempCount -= toCopyCount;
                        }
                        index = tempIndex;
                    }
                    if (tempCount > 0)
                    {
                        var segLength = _chars.Length - _available;//最后一个段
                        var tempIndex = index + segLength;
                        var toCopyX = index >= startIndex ? 0 : startIndex - index;
                        var toCopyY = tempIndex <= endIndex ? 0 : tempIndex - endIndex;
                        var toCopyCount = segLength - toCopyX - toCopyY;
                        var toCopyIndex = count - tempCount;
                        fixed (char* pSrc = _chars)
                        {
                            var toCopy = toCopyCount * 2;
                            Buffer.MemoryCopy(pSrc + toCopyX, pDest + toCopyIndex, toCopy, toCopy);
                        }
                    }
                }
                return value;
            }
        }
        public Writer GetWriter()
        {
            return new Writer(this);
        }
        public struct Writer : IBufferWriter<char>//TextWriter
        {
            public Writer(StringBuffer sb)
            {
                @this = sb;
            }

            private StringBuffer @this;
            public Memory<char> GetMemory(int sizeHint = 0)
            {
                if (sizeHint < 0)
                    throw new ArgumentOutOfRangeException(nameof(sizeHint));

                if (sizeHint == 0)
                {
                    @this.TryAdd();
                    return @this._chars.AsMemory(@this._chars.Length - @this._available, @this._available);
                }
                else
                {
                    if (@this._available < sizeHint)
                        @this.Add(sizeHint);
                    return @this._chars.AsMemory(@this._chars.Length - @this._available, @this._available);
                }
            }
            public Span<char> GetSpan(int sizeHint = 0)
            {
                return GetMemory(sizeHint).Span;
            }
            public void Advance(int count)
            {
                if (count <= 0)
                    return;
                if (@this._available < count)
                    throw new InvalidOperationException(nameof(count));

                @this._available -= count;
            }
        }
        public Enumerable GetEnumerable()
        {
            return new Enumerable(this);
        }
        public struct Enumerable:IEnumerable<ReadOnlyMemory<char>>
        {
            public Enumerable(StringBuffer sb)
            {
                _sb = sb;
            }

            private StringBuffer _sb;
            public ReadOnlyMemory<char> this[int index]
            {
                get
                {
                    if (index < 0)
                        throw new ArgumentOutOfRangeException(nameof(index));

                    if (index <= _sb._pos)
                        return _sb._segments[index];

                    if (index == _sb._pos + 1)
                    {
                        var temp = _sb._chars.Length - _sb._available;
                        if(temp>0)
                            return _sb._chars.AsMemory(0, temp);
                    }

                    throw new ArgumentOutOfRangeException(nameof(index));
                }
            }
            public int Count
            {
                get
                {
                    var temp = _sb._chars.Length - _sb._available;
                    if (temp > 0)
                        return _sb._pos + 2;
                    return _sb._pos + 1;
                }
            }
            public Enumerator GetEnumerator()
            {
                return new Enumerator(_sb);
            }
            IEnumerator<ReadOnlyMemory<char>> IEnumerable<ReadOnlyMemory<char>>.GetEnumerator()
            {
                return GetEnumerator();
            }
            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
        public struct Enumerator : IEnumerator<ReadOnlyMemory<char>>
        {
            public Enumerator(StringBuffer sb)
            {
                _sb = sb;
                _current = ReadOnlyMemory<char>.Empty;
                _index = -1;
            }

            private int _index;//-2=end -1=start
            private ReadOnlyMemory<char> _current;
            private StringBuffer _sb;
            public ReadOnlyMemory<char> Current => _current;
            object IEnumerator.Current => _current;
            public bool MoveNext()
            {
                if (_index == -2)
                    return false;

                _index++;
                if (_index <= _sb._pos)
                {
                    _current = _sb._segments[_index];
                    return true;
                }
                else
                {
                    _index = -2;
                    var temp = _sb._chars.Length - _sb._available;
                    if (temp <= 0)
                    {
                        return false;
                    }
                    else
                    {
                        _current = _sb._chars.AsMemory(0, temp);
                        return true;
                    }
                }
            }
            public void Reset()
            {
                _current = ReadOnlyMemory<char>.Empty;
                _index = -1;
            }
            public void Dispose()
            {

            }
        }
    }
}
