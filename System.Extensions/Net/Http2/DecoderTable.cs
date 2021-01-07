
namespace System.Extensions.Net
{
    //TODO(BUG) MaxSize 自增长
    public class DecoderTable
    {
        //DecoderTable.ContentType;比较引用?
        private const int _StaticTableIndex = 61;
        private static (string name, string value)[] _StaticTable=new [] 
        {
            (null,null),
            (":authority", ""),
            (":method", "GET"),
            (":method", "POST"),
            (":path", "/"),
            (":path", "/index.html"),
            (":scheme", "http"),
            (":scheme", "https"),
            (":status", "200"),
            (":status", "204"),
            (":status", "206"),
            (":status", "304"),
            (":status", "400"),
            (":status", "404"),
            (":status", "500"),
            ("accept-charset", ""),
            ("accept-encoding", "gzip, deflate"),
            ("accept-language", ""),
            ("accept-ranges", ""),
            ("accept", ""),
            ("access-control-allow-origin", ""),
            ("age", ""),
            ("allow", ""),
            ("authorization", ""),
            ("cache-control", ""),
            ("content-disposition", ""),
            ("content-encoding", ""),
            ("content-language", ""),
            ("content-length", ""),
            ("content-location", ""),
            ("content-range", ""),
            ("content-type", ""),
            ("cookie", ""),
            ("date", ""),
            ("etag", ""),
            ("expect", ""),
            ("expires", ""),
            ("from", ""),
            ("host", ""),
            ("if-match", ""),
            ("if-modified-since", ""),
            ("if-none-match", ""),
            ("if-range", ""),
            ("if-unmodifiedsince", ""),
            ("last-modified", ""),
            ("link", ""),
            ("location", ""),
            ("max-forwards", ""),
            ("proxy-authenticate", ""),
            ("proxy-authorization", ""),
            ("range", ""),
            ("referer", ""),
            ("refresh", ""),
            ("retry-after", ""),
            ("server", ""),
            ("set-cookie", ""),
            ("strict-transport-security", ""),
            ("transfer-encoding", ""),
            ("user-agent", ""),
            ("vary", ""),
            ("via", ""),
            ("www-authenticate", "")
        };

        private int _maxSize;
        private int _size;
        private int _count;
        private int _head;
        private int _tail;
        private (string name, string value)[] _dynamicTable;
        public DecoderTable(int maxSize)
        {
            if (maxSize < 0)
                throw new ArgumentOutOfRangeException(nameof(maxSize));

            _dynamicTable = new (string, string)[maxSize / 32];
            _maxSize = maxSize;
        }
        public int MaxSize
        {
            get=>_maxSize;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(MaxSize));

                //值一样
                //if (value == _maxSize)
                //    return;

                if (value > _maxSize)
                {
                    var length = value / 32;
                    if (length > _dynamicTable.Length)
                    {
                        var dynamicTable = new (string, string)[length];
                        for (var i = 1; i <= _count; i++)
                        {
                            var index = _head - i;
                            dynamicTable[_count - i] = index < 0 ? _dynamicTable[index + _dynamicTable.Length]
                                : dynamicTable[_count - i] = _dynamicTable[index];
                        }
                        _tail = 0;
                        _head = _count;
                        _dynamicTable = dynamicTable;
                    }
                    _maxSize = value;
                }
                else
                {
                    _maxSize = value;
                    while (_count > 0 && _maxSize < _size)
                    {
                        (var tempName, var tempValue) = _dynamicTable[_tail];
                        _size -= tempName.Length + tempValue.Length + 32;
                        _count--;
                        _tail = (_tail + 1) % _dynamicTable.Length;
                    }
                }
            }
        }
        public int Size
        {
            get => _size;
            set
            {
                if (value < 0 || value > _maxSize)
                    throw new ArgumentOutOfRangeException(nameof(value));

                if (value == 0)
                {
                    _size = 0;
                    _count = 0;
                    _head = 0;
                    _tail = 0;
                    Array.Clear(_dynamicTable, 0, _dynamicTable.Length);
                    return;
                }

                while (_count > 0 && value < _size)//Test
                {
                    (var tempName, var tempValue) = _dynamicTable[_tail];
                    _size -= tempName.Length + tempValue.Length + 32;
                    _count--;
                    _tail = (_tail + 1) % _dynamicTable.Length;
                }
            }
        }
        public void Add(string name, string value)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            var size = name.Length + value.Length + 32;

            //Available
            while (_count > 0 && _maxSize - _size < size)
            {
                (var tempName, var tempValue) = _dynamicTable[_tail];
                _size -= tempName.Length + tempValue.Length + 32;
                _count--;
                _tail = (_tail + 1) % _dynamicTable.Length;
            }

            if (size > _maxSize)
                return;

            _dynamicTable[_head] = (name, value);
            _head = (_head + 1) % _dynamicTable.Length;
            _size += size;
            _count++;
        }
        public bool TryGetField(int index, out string name, out string value)
        {
            if (index <= 0)
            {
                name = null;
                value = null;
                return false;
            }

            if (index <= _StaticTableIndex)
            {
                (name, value) = _StaticTable[index];
                return true;
            }

            index -= _StaticTableIndex;

            if (index > _count)
            {
                name = null;
                value = null;
                return false;
            }

            index = _head - index;
            if (index < 0)
            {
                (name, value) = _dynamicTable[index + _dynamicTable.Length];
                return true;
            }
            else
            {
                (name, value) = _dynamicTable[index];
                return true;
            }
        }
    }
}
