
namespace System.Buffers
{
    using System.Diagnostics;
    public abstract class Buffer<T> : BufferWriter<T> where T : unmanaged
    {
        #region abstract
        public abstract long Length { get; }
        public abstract ReadOnlySequence<T> Sequence { get; }
        public abstract Memory<T> GetMemory(int sizeHint = 0);
        public abstract Span<T> GetSpan(int sizeHint = 0);
        public abstract void Advance(int count);
        public abstract void Write(T value);
        public abstract void Write(ReadOnlySpan<T> value);
        //public unsafe abstract void Write(T* pValue, int count);//TODO?? Remove
        public abstract void Clear();
        public abstract void CopyTo(Span<T> destination);
        public override string ToString()
        {
            if (typeof(T) == typeof(char))
            {
                var length = checked((int)Length);
                unsafe
                {
                    var value = new string('\0', length);
                    fixed (char* pDest = value)
                    {
                        CopyTo(new Span<T>(pDest, length));
                    }
                    return value;
                }
            }
            return base.ToString();
        }
        #endregion
        public static Buffer<T> Create(int bufferSize)
        {
            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));

            return new ArrayBuffer(bufferSize);
        }
        public static Buffer<T> Create(ArrayPool<T> pool, int minimumLength, out IDisposable disposable)
        {
            if (pool == null)
                throw new ArgumentNullException(nameof(pool));
            if (minimumLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(minimumLength));

            var buffer = new ArrayPoolBuffer(0, pool, minimumLength);
            disposable = buffer;
            return buffer;
        }
        public static Buffer<T> Create(int bufferSize, ArrayPool<T> pool, int minimumLength, out IDisposable disposable)
        {
            if (bufferSize < 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            if (pool == null)
                throw new ArgumentNullException(nameof(pool));
            if (minimumLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(minimumLength));

            var buffer = new ArrayPoolBuffer(bufferSize, pool, minimumLength);
            disposable = buffer;
            return buffer;
        }

        //public static Buffer<T> Create(Provider<Memory<T>> provider, int minimumLength, out IDisposable disposable)
        //{
        //    if (provider == null)
        //        throw new ArgumentNullException(nameof(provider));
        //    if (minimumLength <= 0)
        //        throw new ArgumentOutOfRangeException(nameof(minimumLength));

        //    var buffer = new ProviderBuffer(provider, minimumLength);
        //    disposable = buffer;
        //    return buffer;
        //}
        //TODO (int bufferSize,ArrayPool<T> pool, int minimumLength,)
        #region private
        private class ArrayBuffer : Buffer<T>
        {
            private T[] _buffer;
            private Segment _segment;
            private T[] _array;
            private int _available;
            private Segment _start;
            private Segment _end;
            public class Segment : ReadOnlySequenceSegment<T>
            {
                public void SetMemory(ReadOnlyMemory<T> memory) => Memory = memory;
                public void SetNext(ReadOnlySequenceSegment<T> next) => Next = next;
                public void SetRunningIndex(long runningIndex) => RunningIndex = runningIndex;
            }
            public ArrayBuffer(int bufferSize)
            {
                _buffer = new T[bufferSize];
                _segment = new Segment();

                _array = _buffer;
                _available = _array.Length;
                _start = _segment;
                _end = _segment;
            }
            private void TryAlloc(int size)
            {
                Debug.Assert(size > 0);
                if (_available > size)
                    return;

                var temp = _array.Length - _available;
                if (temp > 0)
                {
                    _end.SetMemory(_array.AsMemory(0, temp));
                    var segment = new Segment();
                    segment.SetRunningIndex(_end.RunningIndex + temp);
                    _end.SetNext(segment);
                    _end = segment;
                }
                var runningIndex = checked((int)_end.RunningIndex);
                var newSize = runningIndex > 4000 ? 4000 : 2 * runningIndex;
                _array = new T[newSize < size ? size : newSize];
                _available = _array.Length;
            }
            public override long Length => _end.RunningIndex + (_array.Length - _available);
            public override ReadOnlySequence<T> Sequence
            {
                get
                {
                    var temp = _array.Length - _available;
                    if (_end.RunningIndex + temp == 0)
                        return ReadOnlySequence<T>.Empty;
                    _end.SetMemory(_array.AsMemory(0, temp));
                    return new ReadOnlySequence<T>(_start, 0, _end, temp);//temp maybe 0
                }
            }
            public override void CopyTo(Span<T> destination)
            {
                var dest = destination;
                ReadOnlySequenceSegment<T> segment = _start;
                while (segment.Next != null)
                {
                    var memory = segment.Memory;
                    memory.Span.CopyTo(dest);
                    dest = dest.Slice(memory.Length);
                    segment = segment.Next;
                }
                var temp = _array.Length - _available;
                Debug.Assert(dest.Length == temp);
                if (temp > 0)
                {
                    _array.AsSpan(0, temp).CopyTo(dest);
                }
            }
            public override void Advance(int count)
            {
                if (count < 0 || _available < count)
                    throw new ArgumentOutOfRangeException(nameof(count));

                _available -= count;
            }
            public override Memory<T> GetMemory(int sizeHint = 0)
            {
                if (sizeHint <= 0)
                    sizeHint = 1;

                TryAlloc(sizeHint);
                return _array.AsMemory(_array.Length - _available);
            }
            public override Span<T> GetSpan(int sizeHint = 0)
            {
                if (sizeHint <= 0)
                    sizeHint = 1;

                TryAlloc(sizeHint);
                return _array.AsSpan(_array.Length - _available);
            }
            public override void Write(T value)
            {
                if (_available > 0)
                {
                    _array[_array.Length - _available] = value;
                    _available -= 1;
                }
                else 
                {
                    TryAlloc(1);
                    _array[_array.Length - _available] = value;
                    _available -= 1;
                }
            }
            public override void Write(ReadOnlySpan<T> value)
            {
                var length = value.Length;
                if (length == 0)
                    return;

                unsafe
                {
                    fixed (T* pValue = value)
                    {
                        var pData = pValue;
                        var tempCount = length;
                        do
                        {
                            TryAlloc(tempCount);
                            var charsToCopy = tempCount < _available ? tempCount : _available;
                            fixed (T* pDest = _array)
                            {
                                var toCopy = charsToCopy * BufferExtensions.SizeOf<T>();
                                Buffer.MemoryCopy(pData, pDest + (_array.Length - _available), toCopy, toCopy);
                            }
                            pData += charsToCopy;
                            tempCount -= charsToCopy;
                            _available -= charsToCopy;
                        }
                        while (tempCount > 0);
                    }
                }
            }
            //public override unsafe void Write(T* pValue, int count)
            //{
            //    if (count <= 0)
            //        return;

            //    var pData = pValue;
            //    var tempCount = count;
            //    do
            //    {
            //        TryAlloc(tempCount);
            //        var charsToCopy = tempCount < _available ? tempCount : _available;
            //        fixed (T* pDest = _array)
            //        {
            //            var toCopy = charsToCopy * BufferExtensions.SizeOf<T>();
            //            Buffer.MemoryCopy(pData, pDest + (_array.Length - _available), toCopy, toCopy);
            //        }
            //        pData += charsToCopy;
            //        tempCount -= charsToCopy;
            //        _available -= charsToCopy;
            //    }
            //    while (tempCount > 0);
            //}
            public override void Clear()
            {
                _array = _buffer;
                _available = _array.Length;
                _segment.SetMemory(ReadOnlyMemory<T>.Empty);
                _segment.SetNext(null);
                _segment.SetRunningIndex(0);
                _start = _segment;
                _end = _segment;
            }
        }
        private class ArrayPoolBuffer : Buffer<T>, IDisposable
        {
            private T[] _buffer;
            private ArrayPool<T> _pool;
            private int _minimumLength;
            private Segment _segment;
            private T[] _array;
            private int _available;
            private Segment _start;
            private Segment _end;
            public class Segment : ReadOnlySequenceSegment<T>
            {
                public T[] Array { get; set; }//Return
                public void SetMemory(ReadOnlyMemory<T> memory) => Memory = memory;
                public void SetNext(ReadOnlySequenceSegment<T> next) => Next = next;
                public void SetRunningIndex(long runningIndex) => RunningIndex = runningIndex;
            }
            public ArrayPoolBuffer(int bufferSize, ArrayPool<T> pool, int minimumLength)
            {
                _buffer = bufferSize == 0 ? Array.Empty<T>() : new T[bufferSize];
                _pool = pool;
                _minimumLength = minimumLength;
                _segment = new Segment();
                _array = _buffer;
                _available = _array.Length;
                _start = _segment;
                _end = _segment;
            }
            private void TryAlloc(int size)
            {
                Debug.Assert(size > 0);
                if (_available > size)
                    return;

                var temp = _array.Length - _available;
                if (temp > 0)
                {
                    _end.SetMemory(_array.AsMemory(0, temp));
                    var segment = new Segment();
                    segment.SetRunningIndex(_end.RunningIndex + temp);
                    _end.SetNext(segment);
                    _end = segment;
                }
                _array = _pool.Rent(size <= _minimumLength ? _minimumLength : size);
                //Console.WriteLine("Pool Rent:"+_array.Length);
                _available = _array.Length;
                _end.Array = _array;
            }
            public override long Length => _end.RunningIndex + (_array.Length - _available);
            public override ReadOnlySequence<T> Sequence
            {
                get
                {
                    var temp = _array.Length - _available;
                    if (_end.RunningIndex + temp == 0)
                        return ReadOnlySequence<T>.Empty;
                    _end.SetMemory(_array.AsMemory(0, temp));
                    return new ReadOnlySequence<T>(_start, 0, _end, temp);
                }
            }
            public override void Advance(int count)
            {
                if (count < 0 || _available < count)
                    throw new ArgumentOutOfRangeException(nameof(count));

                _available -= count;
            }
            public override void CopyTo(Span<T> destination)
            {
                var dest = destination;
                ReadOnlySequenceSegment<T> segment = _start;
                while (segment.Next != null)
                {
                    var memory = segment.Memory;
                    memory.Span.CopyTo(dest);
                    dest = dest.Slice(memory.Length);
                    segment = segment.Next;
                }
                var temp = _array.Length - _available;
                Debug.Assert(dest.Length == temp);
                if (temp > 0)
                {
                    _array.AsSpan(0, temp).CopyTo(dest);
                }
            }
            public override Memory<T> GetMemory(int sizeHint = 0)
            {
                if (sizeHint <= 0)
                    sizeHint = 1;

                TryAlloc(sizeHint);
                return _array.AsMemory(_array.Length - _available);
            }
            public override Span<T> GetSpan(int sizeHint = 0)
            {
                if (sizeHint <= 0)
                    sizeHint = 1;

                TryAlloc(sizeHint);
                return _array.AsSpan(_array.Length - _available);
            }
            public override void Write(T value)
            {
                if (_available > 0)
                {
                    _array[_array.Length - _available] = value;
                    _available -= 1;
                }
                else
                {
                    TryAlloc(1);
                    _array[_array.Length - _available] = value;
                    _available -= 1;
                }
            }
            public override void Write(ReadOnlySpan<T> value)
            {
                var length = value.Length;
                if (length == 0)
                    return;

                unsafe
                {
                    fixed (T* pValue = value)
                    {
                        var pData = pValue;
                        var tempCount = length;
                        do
                        {
                            TryAlloc(tempCount);
                            var charsToCopy = tempCount < _available ? tempCount : _available;
                            fixed (T* pDest = _array)
                            {
                                var toCopy = charsToCopy * BufferExtensions.SizeOf<T>();
                                Buffer.MemoryCopy(pData, pDest + (_array.Length - _available), toCopy, toCopy);
                            }
                            pData += charsToCopy;
                            tempCount -= charsToCopy;
                            _available -= charsToCopy;
                        }
                        while (tempCount > 0);
                    }
                }
            }
            //public override unsafe void Write(T* pValue, int count)
            //{
            //    if (count <= 0)
            //        return;

            //    var pData = pValue;
            //    var tempCount = count;
            //    do
            //    {
            //        TryAlloc(tempCount);
            //        var charsToCopy = tempCount < _available ? tempCount : _available;
            //        fixed (T* pDest = _array)
            //        {
            //            var toCopy = charsToCopy * BufferExtensions.SizeOf<T>();
            //            Buffer.MemoryCopy(pData, pDest + (_array.Length - _available), toCopy, toCopy);
            //        }
            //        pData += charsToCopy;
            //        tempCount -= charsToCopy;
            //        _available -= charsToCopy;
            //    }
            //    while (tempCount > 0);
            //}
            public override void Clear()
            {
                _array = _buffer;
                _available = _array.Length;
                for (var segm = _start; segm != null; segm = (Segment)segm.Next)
                {
                    if (segm.Array != null)
                    {
                        _pool.Return(segm.Array);
                        segm.Array = null;
                    }
                }
                _segment.SetMemory(ReadOnlyMemory<T>.Empty);
                _segment.SetNext(null);
                _segment.SetRunningIndex(0);
                _start = _segment;
                _end = _segment;
            }
            public void Dispose()
            {
                Clear();//if (_array != _buffer || _array.Length != _available)
            }
        }
        //private class ProviderBuffer : Buffer<T>, IDisposable
        //{
        //    private Provider<Memory<T>> _provider;
        //    private int _minimumLength;
        //    private Segment _segment;
        //    private Memory<T> _array;
        //    private int _available;
        //    private Segment _start;
        //    private Segment _end;
        //    public class Segment : ReadOnlySequenceSegment<T>
        //    {
        //        public IDisposable Disposable { get; set; }
        //        public void SetMemory(ReadOnlyMemory<T> memory) => Memory = memory;
        //        public void SetNext(ReadOnlySequenceSegment<T> next) => Next = next;
        //        public void SetRunningIndex(long runningIndex) => RunningIndex = runningIndex;
        //    }
        //    public ProviderBuffer(Provider<Memory<T>> provider, int minimumLength)
        //    {
        //        _provider = provider;
        //        _minimumLength = minimumLength;
        //        _segment = new Segment();
        //        _array = Memory<T>.Empty;
        //        _available = 0;
        //        _start = _segment;
        //        _end = _segment;
        //    }
        //    private void TryAlloc(int size)
        //    {
        //        Debug.Assert(size > 0);
        //        if (_available > size)
        //            return;

        //        var temp = _array.Length - _available;
        //        if (temp > 0)
        //        {
        //            _end.SetMemory(_array.Slice(0, temp));
        //            var segment = new Segment();
        //            segment.SetRunningIndex(_end.RunningIndex + temp);
        //            _end.SetNext(segment);
        //            _end = segment;
        //        }

        //        if (_provider.TryGetValue(out var buffer, out var disposable))
        //        {
        //            _array = buffer;
        //            _available = _array.Length;
        //            _end.Disposable = disposable;
        //            return;
        //        }
        //        _array = new T[size <= _minimumLength ? _minimumLength : size];
        //        _available = _array.Length;
        //    }
        //    public override long Length => _end.RunningIndex + (_array.Length - _available);
        //    public override ReadOnlySequence<T> Sequence
        //    {
        //        get
        //        {
        //            var temp = _array.Length - _available;
        //            if (_end.RunningIndex + temp == 0)
        //                return ReadOnlySequence<T>.Empty;
        //            _end.SetMemory(_array.Slice(0, temp));
        //            return new ReadOnlySequence<T>(_start, 0, _end, temp);
        //        }
        //    }
        //    public override void Advance(int count)
        //    {
        //        if (count < 0 || _available < count)
        //            throw new ArgumentOutOfRangeException(nameof(count));

        //        _available -= count;
        //    }
        //    public override void CopyTo(Span<T> destination)
        //    {
        //        var dest = destination;
        //        ReadOnlySequenceSegment<T> segment = _start;
        //        while (segment.Next != null)
        //        {
        //            var memory = segment.Memory;
        //            memory.Span.CopyTo(dest);
        //            dest = dest.Slice(memory.Length);
        //            segment = segment.Next;
        //        }
        //        var temp = _array.Length - _available;
        //        Debug.Assert(dest.Length == temp);
        //        if (temp > 0)
        //        {
        //            _array.Span.Slice(0, temp).CopyTo(dest);
        //        }
        //    }
        //    public override Memory<T> GetMemory(int sizeHint = 0)
        //    {
        //        if (sizeHint <= 0)
        //            sizeHint = 1;

        //        TryAlloc(sizeHint);
        //        return _array.Slice(_array.Length - _available);
        //    }
        //    public override Span<T> GetSpan(int sizeHint = 0)
        //    {
        //        if (sizeHint <= 0)
        //            sizeHint = 1;

        //        TryAlloc(sizeHint);
        //        return _array.Span.Slice(_array.Length - _available);
        //    }
        //    public override void Write(T value)
        //    {
        //        if (_available > 0)
        //        {
        //            _array.Span[_array.Length - _available] = value;
        //            _available -= 1;
        //        }
        //        else 
        //        {
        //            TryAlloc(1);
        //            _array.Span[_array.Length - _available] = value;
        //            _available -= 1;
        //        }
        //    }
        //    public override void Write(ReadOnlySpan<T> value)
        //    {
        //        var length = value.Length;
        //        if (length == 0)
        //            return;

        //        unsafe
        //        {
        //            fixed (T* pValue = value)
        //            {
        //                var pData = pValue;
        //                var tempCount = length;
        //                do
        //                {
        //                    TryAlloc(tempCount);
        //                    var charsToCopy = tempCount < _available ? tempCount : _available;
        //                    fixed (T* pDest = _array.Span)
        //                    {
        //                        var toCopy = charsToCopy * BufferExtensions.SizeOf<T>();
        //                        Buffer.MemoryCopy(pData, pDest + (_array.Length - _available), toCopy, toCopy);
        //                    }
        //                    pData += charsToCopy;
        //                    tempCount -= charsToCopy;
        //                    _available -= charsToCopy;
        //                }
        //                while (tempCount > 0);
        //            }
        //        }
        //    }
        //    public override unsafe void Write(T* pValue, int count)
        //    {
        //        if (count <= 0)
        //            return;

        //        var pData = pValue;
        //        var tempCount = count;
        //        do
        //        {
        //            TryAlloc(tempCount);
        //            var charsToCopy = tempCount < _available ? tempCount : _available;
        //            fixed (T* pDest = _array.Span)
        //            {
        //                var toCopy = charsToCopy * BufferExtensions.SizeOf<T>();
        //                Buffer.MemoryCopy(pData, pDest + (_array.Length - _available), toCopy, toCopy);
        //            }
        //            pData += charsToCopy;
        //            tempCount -= charsToCopy;
        //            _available -= charsToCopy;
        //        }
        //        while (tempCount > 0);
        //    }
        //    public override void Clear()
        //    {
        //        _array = Memory<T>.Empty;
        //        _available = 0;
        //        for (var segm = _start; segm != null; segm = (Segment)segm.Next)
        //        {
        //            if (segm.Disposable != null)
        //            {
        //                segm.Disposable.Dispose();
        //                segm.Disposable = null;
        //            }
        //        }
        //        _segment.SetMemory(ReadOnlyMemory<T>.Empty);
        //        _segment.SetNext(null);
        //        _segment.SetRunningIndex(0);
        //        _start = _segment;
        //        _end = _segment;
        //    }
        //    public void Dispose()
        //    {
        //        if (_array.Length > 0)
        //            Clear();
        //    }
        //}
        #endregion
    }
}
