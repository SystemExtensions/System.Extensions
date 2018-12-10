
namespace System.IO.Compression
{
    using System.Threading;
    using System.Threading.Tasks;
    public class GZipCodec
    {
        public class Params
        {
            internal static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            internal static readonly Text.Encoding Iso8859dash1 = Text.Encoding.GetEncoding("iso-8859-1");
            public Params() { }
            public Params(string fileName)
            {
                FileName = fileName;
            }
            public string FileName;
            public string Comment;
            public DateTime LastModified = UnixEpoch;//默认1970
            public byte OperatingSystem = 255;//默认255
        }
        public static Stream Compress(Stream stream, DeflateCodec.Deflater deflater)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (deflater == null)
                throw new ArgumentNullException(nameof(deflater));

            return new DeflateStream(stream, null, deflater);
        }
        public static Stream Compress(Stream stream, Params inParams, DeflateCodec.Deflater deflater)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (deflater == null)
                throw new ArgumentNullException(nameof(deflater));

            return new DeflateStream(stream, inParams, deflater);
        }
        public static Stream Decompress(Stream stream, DeflateCodec.Inflater inflater)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (inflater == null)
                throw new ArgumentNullException(nameof(inflater));
            if (inflater.AvailableOffset != 0)
                throw new ArgumentException(nameof(inflater.AvailableOffset));
            if (inflater.AvailableCount != 0)
                throw new ArgumentException(nameof(inflater.AvailableCount));

            return new InflateStream(stream, null, inflater);
        }
        public static Stream Decompress(Stream stream, Params outParams, DeflateCodec.Inflater inflater)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (inflater == null)
                throw new ArgumentNullException(nameof(inflater));
            if (inflater.AvailableOffset != 0)
                throw new ArgumentException(nameof(inflater.AvailableOffset));
            if (inflater.AvailableCount != 0)
                throw new ArgumentException(nameof(inflater.AvailableCount));

            return new InflateStream(stream, outParams, inflater);
        }
        private class Crc32Stream : Stream
        {
            #region CrcTable
            static readonly uint[] _CrcTable = {
            0x00000000, 0x77073096, 0xEE0E612C, 0x990951BA, 0x076DC419,
            0x706AF48F, 0xE963A535, 0x9E6495A3, 0x0EDB8832, 0x79DCB8A4,
            0xE0D5E91E, 0x97D2D988, 0x09B64C2B, 0x7EB17CBD, 0xE7B82D07,
            0x90BF1D91, 0x1DB71064, 0x6AB020F2, 0xF3B97148, 0x84BE41DE,
            0x1ADAD47D, 0x6DDDE4EB, 0xF4D4B551, 0x83D385C7, 0x136C9856,
            0x646BA8C0, 0xFD62F97A, 0x8A65C9EC, 0x14015C4F, 0x63066CD9,
            0xFA0F3D63, 0x8D080DF5, 0x3B6E20C8, 0x4C69105E, 0xD56041E4,
            0xA2677172, 0x3C03E4D1, 0x4B04D447, 0xD20D85FD, 0xA50AB56B,
            0x35B5A8FA, 0x42B2986C, 0xDBBBC9D6, 0xACBCF940, 0x32D86CE3,
            0x45DF5C75, 0xDCD60DCF, 0xABD13D59, 0x26D930AC, 0x51DE003A,
            0xC8D75180, 0xBFD06116, 0x21B4F4B5, 0x56B3C423, 0xCFBA9599,
            0xB8BDA50F, 0x2802B89E, 0x5F058808, 0xC60CD9B2, 0xB10BE924,
            0x2F6F7C87, 0x58684C11, 0xC1611DAB, 0xB6662D3D, 0x76DC4190,
            0x01DB7106, 0x98D220BC, 0xEFD5102A, 0x71B18589, 0x06B6B51F,
            0x9FBFE4A5, 0xE8B8D433, 0x7807C9A2, 0x0F00F934, 0x9609A88E,
            0xE10E9818, 0x7F6A0DBB, 0x086D3D2D, 0x91646C97, 0xE6635C01,
            0x6B6B51F4, 0x1C6C6162, 0x856530D8, 0xF262004E, 0x6C0695ED,
            0x1B01A57B, 0x8208F4C1, 0xF50FC457, 0x65B0D9C6, 0x12B7E950,
            0x8BBEB8EA, 0xFCB9887C, 0x62DD1DDF, 0x15DA2D49, 0x8CD37CF3,
            0xFBD44C65, 0x4DB26158, 0x3AB551CE, 0xA3BC0074, 0xD4BB30E2,
            0x4ADFA541, 0x3DD895D7, 0xA4D1C46D, 0xD3D6F4FB, 0x4369E96A,
            0x346ED9FC, 0xAD678846, 0xDA60B8D0, 0x44042D73, 0x33031DE5,
            0xAA0A4C5F, 0xDD0D7CC9, 0x5005713C, 0x270241AA, 0xBE0B1010,
            0xC90C2086, 0x5768B525, 0x206F85B3, 0xB966D409, 0xCE61E49F,
            0x5EDEF90E, 0x29D9C998, 0xB0D09822, 0xC7D7A8B4, 0x59B33D17,
            0x2EB40D81, 0xB7BD5C3B, 0xC0BA6CAD, 0xEDB88320, 0x9ABFB3B6,
            0x03B6E20C, 0x74B1D29A, 0xEAD54739, 0x9DD277AF, 0x04DB2615,
            0x73DC1683, 0xE3630B12, 0x94643B84, 0x0D6D6A3E, 0x7A6A5AA8,
            0xE40ECF0B, 0x9309FF9D, 0x0A00AE27, 0x7D079EB1, 0xF00F9344,
            0x8708A3D2, 0x1E01F268, 0x6906C2FE, 0xF762575D, 0x806567CB,
            0x196C3671, 0x6E6B06E7, 0xFED41B76, 0x89D32BE0, 0x10DA7A5A,
            0x67DD4ACC, 0xF9B9DF6F, 0x8EBEEFF9, 0x17B7BE43, 0x60B08ED5,
            0xD6D6A3E8, 0xA1D1937E, 0x38D8C2C4, 0x4FDFF252, 0xD1BB67F1,
            0xA6BC5767, 0x3FB506DD, 0x48B2364B, 0xD80D2BDA, 0xAF0A1B4C,
            0x36034AF6, 0x41047A60, 0xDF60EFC3, 0xA867DF55, 0x316E8EEF,
            0x4669BE79, 0xCB61B38C, 0xBC66831A, 0x256FD2A0, 0x5268E236,
            0xCC0C7795, 0xBB0B4703, 0x220216B9, 0x5505262F, 0xC5BA3BBE,
            0xB2BD0B28, 0x2BB45A92, 0x5CB36A04, 0xC2D7FFA7, 0xB5D0CF31,
            0x2CD99E8B, 0x5BDEAE1D, 0x9B64C2B0, 0xEC63F226, 0x756AA39C,
            0x026D930A, 0x9C0906A9, 0xEB0E363F, 0x72076785, 0x05005713,
            0x95BF4A82, 0xE2B87A14, 0x7BB12BAE, 0x0CB61B38, 0x92D28E9B,
            0xE5D5BE0D, 0x7CDCEFB7, 0x0BDBDF21, 0x86D3D2D4, 0xF1D4E242,
            0x68DDB3F8, 0x1FDA836E, 0x81BE16CD, 0xF6B9265B, 0x6FB077E1,
            0x18B74777, 0x88085AE6, 0xFF0F6A70, 0x66063BCA, 0x11010B5C,
            0x8F659EFF, 0xF862AE69, 0x616BFFD3, 0x166CCF45, 0xA00AE278,
            0xD70DD2EE, 0x4E048354, 0x3903B3C2, 0xA7672661, 0xD06016F7,
            0x4969474D, 0x3E6E77DB, 0xAED16A4A, 0xD9D65ADC, 0x40DF0B66,
            0x37D83BF0, 0xA9BCAE53, 0xDEBB9EC5, 0x47B2CF7F, 0x30B5FFE9,
            0xBDBDF21C, 0xCABAC28A, 0x53B39330, 0x24B4A3A6, 0xBAD03605,
            0xCDD70693, 0x54DE5729, 0x23D967BF, 0xB3667A2E, 0xC4614AB8,
            0x5D681B02, 0x2A6F2B94, 0xB40BBE37, 0xC30C8EA1, 0x5A05DF1B,
            0x2D02EF8D
        };
            #endregion
            public Crc32Stream(Stream stream)
            {
                _baseStream = stream;
                CrcValue = 0xFFFFFFFF;
            }

            private Stream _baseStream;
            public uint CrcValue;
            public uint CrcSize;
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _baseStream.Length;
            public override long Position
            {
                get
                {
                    return _baseStream.Position;
                }
                set
                {
                    _baseStream.Position = value;
                }
            }
            public override void Flush()
            {
                _baseStream.Flush();
            }
            public override int Read(byte[] buffer, int offset, int count)
            {
                var result = _baseStream.Read(buffer,offset,count);
                if (result > 0)
                {
                    CrcSize += (uint)result;
                    for (int i = 0; i < result; i++)//是否指针优化
                    {
                        CrcValue = _CrcTable[(CrcValue ^ buffer[i + offset]) & 0xFF] ^ (CrcValue >> 8);
                    }
                }
                return result;
            }
            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                var result = await _baseStream.ReadAsync(buffer, offset, count, cancellationToken);
                if (result > 0)
                {
                    CrcSize += (uint)result;
                    for (int i = 0; i < result; i++)//是否指针优化
                    {
                        CrcValue = _CrcTable[(CrcValue ^ buffer[i + offset]) & 0xFF] ^ (CrcValue >> 8);
                    }
                }
                return result;
            }
            protected override void Dispose(bool disposing)
            {
                if (_baseStream != null)
                {
                    _baseStream = null;
                }
            }
            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }
            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }
            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }
        private class DeflateStream : Stream
        {
            const int _TrailCount = 8;  
            static readonly byte[] _Head = { 0x1F, 0x8B, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF };
            public DeflateStream(Stream stream, Params inParams, DeflateCodec.Deflater deflater)
            {
                if (inParams == null)
                {
                    _head = _Head;
                }
                else
                {
                    var fileName = inParams.FileName;
                    var comment = inParams.Comment;
                    var fileNameLen = fileName == null ? 0 : fileName.Length + 1;
                    var commentLen = comment == null ? 0 : comment.Length + 1;
                    _head = new byte[10 + fileNameLen + commentLen];
                    _head[0] = 0x1F;
                    _head[1] = 0x8B;
                    _head[2] = 0x08;
                    byte flag = 0;
                    if (commentLen != 0) flag ^= 0x10;
                    if (fileNameLen != 0) flag ^= 0x8;
                    _head[3] = flag;
                    int mtime = (int)((inParams.LastModified - Params.UnixEpoch).TotalSeconds);
                    unchecked
                    {
                        _head[4] = (byte)mtime;
                        _head[5] = (byte)(mtime >> 8);
                        _head[6] = (byte)(mtime >> 16);
                        _head[7] = (byte)(mtime >> 24);
                    }
                    _head[8] = 0;
                    _head[9] = inParams.OperatingSystem;

                    if (fileNameLen != 0)
                    {
                        var fileBytes = Params.Iso8859dash1.GetBytes(fileName);
                        Array.Copy(fileBytes, 0, _head, 10, fileBytes.Length);
                        _head[9 + fileNameLen] = 0;
                        if (commentLen != 0)
                        {
                            var commentBytes = Params.Iso8859dash1.GetBytes(comment);
                            Array.Copy(commentBytes, 0, _head, 10 + fileNameLen, commentBytes.Length);
                            _head[9 + fileNameLen + commentLen] = 0;
                        }
                    }
                    else if (commentLen != 0)
                    {
                        var commentBytes = Params.Iso8859dash1.GetBytes(comment);
                        Array.Copy(commentBytes, 0, _head, 10, commentBytes.Length);
                        _head[9 + commentLen] = 0;
                    }
                }
                _crc32Stream = new Crc32Stream(stream);
                _baseStream = DeflateCodec.Compress(_crc32Stream, deflater);
            }
            private byte[] _head;
            private int _headOffset;
            private byte[] _trail;
            private int _trailOffset;
            private Crc32Stream _crc32Stream;
            private Stream _baseStream;
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length
            {
                get
                {
                    if (_baseStream == null)
                        throw new ObjectDisposedException(nameof(Stream));
                    return _baseStream.Length;
                }
            }
            public override long Position
            {
                get
                {
                    if (_baseStream == null)
                        throw new ObjectDisposedException(nameof(Stream));

                    return _baseStream.Position;
                }
                set
                {
                    throw new NotSupportedException();
                }
            }
            public override void Flush()
            {
                
            }
            private void CreateTrail()
            {
                var size = _crc32Stream.CrcSize & 0xFFFFFFFF;
                var crc = (_crc32Stream.CrcValue ^ 0xFFFFFFFF) & 0xFFFFFFFF;
                unchecked
                {
                    _trail = new byte[] {
                    (byte) crc, (byte) (crc >> 8),
                    (byte) (crc >> 16), (byte) (crc >> 24),

                    (byte) size, (byte) (size >> 8),
                    (byte) (size >> 16), (byte) (size >> 24)};
                }

                Console.WriteLine(BitConverter.ToString(_trail, 0, 8));
            }
            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_baseStream == null)
                    throw new ObjectDisposedException(nameof(Stream));
                if (buffer == null)
                    throw new ArgumentNullException(nameof(buffer));
                if (offset < 0 || offset >= buffer.Length)
                    throw new ArgumentOutOfRangeException(nameof(offset));
                if (count < 0 || count > buffer.Length - offset)
                    throw new ArgumentOutOfRangeException(nameof(count));

                if (_trailOffset == _TrailCount)//尾部已经输出
                    return 0;

                if (_trail == null)
                {
                    if (_headOffset >= _head.Length)//头部读完了
                    {
                        var remainingBytes = count;
                        while (true)
                        {
                            int bytesRead = _baseStream.Read(buffer, offset, remainingBytes);
                            if (bytesRead <= 0)//baseStream 被读完
                            {
                                CreateTrail();
                                if (remainingBytes > _TrailCount)
                                {
                                    Array.Copy(_trail, 0, buffer, offset, _TrailCount);
                                    _trailOffset = _TrailCount;//完成
                                    return count - remainingBytes + _TrailCount;
                                }
                                else
                                {
                                    Array.Copy(_trail, 0, buffer, offset, remainingBytes);
                                    _trailOffset += remainingBytes;
                                    return count;//全部填充满
                                }
                            }
                            else
                            {
                                offset += bytesRead;
                                remainingBytes -= bytesRead;

                                if (remainingBytes == 0)
                                    return count;//buffer被填充满
                            }
                        }
                    }
                    else //头部还没读取完
                    {
                        var headRemaining = _head.Length - _headOffset;
                        if (count > headRemaining)//buffer充足
                        {
                            Array.Copy(_head, _headOffset, buffer, offset, headRemaining);
                            _headOffset = _head.Length;
                            offset += headRemaining;
                            var remainingBytes = count - headRemaining;
                            while (true)
                            {
                                int bytesRead = _baseStream.Read(buffer, offset, remainingBytes);
                                if (bytesRead <= 0)//baseStream 被读完
                                {
                                    CreateTrail();
                                    if (remainingBytes > _TrailCount)
                                    {
                                        Array.Copy(_trail, 0, buffer, offset, _TrailCount);
                                        _trailOffset = _TrailCount;//完成
                                        return count - remainingBytes + _TrailCount;
                                    }
                                    else
                                    {
                                        Array.Copy(_trail, 0, buffer, offset, remainingBytes);
                                        _trailOffset += remainingBytes;
                                        return count;//全部填充满
                                    }
                                }
                                else
                                {
                                    offset += bytesRead;
                                    remainingBytes -= bytesRead;

                                    if (remainingBytes == 0)
                                        return count;//buffer被填充满
                                }
                            }
                        }
                        else
                        {
                            Array.Copy(_head, _headOffset, buffer, offset, count);
                            _headOffset += count;
                            return count;
                        }
                    }
                }
                else
                {
                    var trailRemaining = _TrailCount - _trailOffset;
                    if (count > trailRemaining)
                    {
                        Array.Copy(_trail, _trailOffset, buffer, offset, trailRemaining);
                        _trailOffset = _TrailCount;
                        return trailRemaining;
                    }
                    else
                    {
                        Array.Copy(_trail, _trailOffset, buffer, offset, count);
                        _trailOffset += count;
                        return count;
                    }
                }
            }
            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                if (_baseStream == null)
                    throw new ArgumentNullException(nameof(_baseStream));
                if (buffer == null)
                    throw new ArgumentNullException(nameof(buffer));
                if (offset < 0 || offset >= buffer.Length)
                    throw new ArgumentOutOfRangeException(nameof(offset));
                if (count < 0 || count > buffer.Length - offset)
                    throw new ArgumentOutOfRangeException(nameof(count));

                if (_trailOffset == _TrailCount)//尾部已经输出
                    return 0;

                if (_trail == null)
                {
                    if (_headOffset >= _head.Length)//头部读完了
                    {
                        var remainingBytes = count;
                        while (true)
                        {
                            int bytesRead = await _baseStream.ReadAsync(buffer, offset, remainingBytes);
                            if (bytesRead <= 0)//baseStream 被读完
                            {
                                CreateTrail();
                                if (remainingBytes > _TrailCount)
                                {
                                    Array.Copy(_trail, 0, buffer, offset, _TrailCount);
                                    _trailOffset = _TrailCount;//完成
                                    return count - remainingBytes + _TrailCount;
                                }
                                else
                                {
                                    Array.Copy(_trail, 0, buffer, offset, remainingBytes);
                                    _trailOffset += remainingBytes;
                                    return count;//全部填充满
                                }
                            }
                            else
                            {
                                offset += bytesRead;
                                remainingBytes -= bytesRead;

                                if (remainingBytes == 0)
                                    return count;//buffer被填充满
                            }
                        }
                    }
                    else //头部还没读取完
                    {
                        var headRemaining = _head.Length - _headOffset;
                        if (count > headRemaining)//buffer充足
                        {
                            Array.Copy(_head, _headOffset, buffer, offset, headRemaining);
                            _headOffset = _head.Length;
                            offset += headRemaining;
                            var remainingBytes = count - headRemaining;
                            while (true)
                            {
                                int bytesRead = await _baseStream.ReadAsync(buffer, offset, remainingBytes);
                                if (bytesRead <= 0)//baseStream 被读完
                                {
                                    CreateTrail();
                                    if (remainingBytes > _TrailCount)
                                    {
                                        Array.Copy(_trail, 0, buffer, offset, _TrailCount);
                                        _trailOffset = _TrailCount;//完成
                                        return count - remainingBytes + _TrailCount;
                                    }
                                    else
                                    {
                                        Array.Copy(_trail, 0, buffer, offset, remainingBytes);
                                        _trailOffset += remainingBytes;
                                        return count;//全部填充满
                                    }
                                }
                                else
                                {
                                    offset += bytesRead;
                                    remainingBytes -= bytesRead;

                                    if (remainingBytes == 0)
                                        return count;//buffer被填充满
                                }
                            }
                        }
                        else
                        {
                            Array.Copy(_head, _headOffset, buffer, offset, count);
                            _headOffset += count;
                            return count;
                        }
                    }
                }
                else
                {
                    var trailRemaining = _TrailCount - _trailOffset;
                    if (count > trailRemaining)
                    {
                        Array.Copy(_trail, _trailOffset, buffer, offset, trailRemaining);
                        _trailOffset = _TrailCount;
                        return trailRemaining;
                    }
                    else
                    {
                        Array.Copy(_trail, _trailOffset, buffer, offset, count);
                        _trailOffset += count;
                        return count;
                    }
                }
            }
            protected override void Dispose(bool disposing)
            {
                if (_baseStream != null)
                {
                    _baseStream.Dispose();
                    _baseStream = null;
                    _crc32Stream.Dispose();
                    _crc32Stream = null;
                    _head = null;
                    _trail = null;
                }
            }
            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }
            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }
            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }
        private class InflateStream : Stream
        {
            public InflateStream(Stream stream, Params outParams, DeflateCodec.Inflater inflater)
            {
                _stream = stream;
                _outParams = outParams;
                _inflater = inflater;
            }
            private Params _outParams;
            private Stream _stream;//==null 就是完成状态
            private Stream _baseStream;
            private Crc32Stream _crc32Stream;
            private DeflateCodec.Inflater _inflater;
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length
            {
                get
                {
                    if (_inflater == null)
                        throw new ObjectDisposedException(nameof(InflateStream));
                    if (_stream == null)
                        return _baseStream.Length;
                    throw new InvalidOperationException();
                }
            }
            public override long Position
            {
                get
                {
                    if (_inflater == null)
                        throw new ObjectDisposedException(nameof(InflateStream));
                    if (_baseStream != null)
                        return _baseStream.Length;
                    return 0;
                }
                set
                {
                    throw new NotSupportedException();
                }
            }
            public override void Flush()
            {

            }
            private void ReadHead(int headCount)
            {
                var headBuff = _inflater.Buffer;
                if (headCount < 10)
                    throw new InvalidDataException(nameof(headCount));

                if (headBuff[0] != 0x1F || headBuff[1] != 0x8B || headBuff[2] != 8)
                    throw new InvalidDataException("1F8B08");
                var pos = 10;
                if ((headBuff[3] & 0x04) == 0x04)//extra
                {
                    ushort extraLength = (ushort)(headBuff[10] + headBuff[11] * 256);
                    if (extraLength > 512)
                        throw new InvalidOperationException("extraLength max 512");
                    pos += extraLength + 2;
                }
                if ((headBuff[3] & 0x08) == 0x08)//FileName
                {
                    var complete = false;
                    for (int i = 0; i < 256; i++)
                    {
                        if (headBuff[pos + i] == 0)
                        {
                            if (_outParams != null)
                                _outParams.FileName = Params.Iso8859dash1.GetString(headBuff, pos, i);
                            complete = true;
                            pos = pos + i + 1;
                            break;
                        }
                    }
                    if (!complete)
                        throw new InvalidDataException("FileName Max 256");
                }
                if ((headBuff[3] & 0x10) == 0x010)//Comment
                {
                    var complete = false;
                    for (int i = 0; i < 256; i++)
                    {
                        if (headBuff[pos + i] == 0)
                        {
                            if (_outParams != null)
                                _outParams.Comment = Params.Iso8859dash1.GetString(headBuff, pos, i);
                            complete = true;
                            pos = pos + i + 1;
                            break;
                        }
                    }
                    if (!complete)
                        throw new InvalidDataException("Comment Max 256");
                }
                if ((headBuff[3] & 0x02) == 0x02)//CRC16
                {
                    pos += 2;
                }

                if (_outParams != null)
                {
                    int seconds = BitConverter.ToInt32(headBuff, 4);
                    _outParams.LastModified = Params.UnixEpoch.AddSeconds(seconds);
                    _outParams.OperatingSystem = headBuff[9];
                }

                _inflater.AvailableOffset = pos;
                _inflater.AvailableCount = headCount - pos;
            }
            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_inflater == null)
                    throw new ObjectDisposedException(nameof(InflateStream));
                if (buffer == null)
                    throw new ArgumentNullException(nameof(buffer));
                if (offset < 0 || offset >= buffer.Length)
                    throw new ArgumentOutOfRangeException(nameof(offset));
                if (count < 0 || count > buffer.Length - offset)
                    throw new ArgumentOutOfRangeException(nameof(count));

                if (_stream==null)
                    return 0;

                if (_baseStream == null)//头不还没读取
                {
                    var headBuff = _inflater.Buffer;
                    var headOffset = 0;
                    var headRemainingBytes = headBuff.Length;
                    while (true)
                    {
                        int bytesRead = _stream.Read(headBuff, headOffset, headRemainingBytes);
                        if (bytesRead <= 0)
                            break;
                        headOffset += bytesRead;
                        headRemainingBytes -= bytesRead;
                        if (headRemainingBytes == 0)
                            break;
                    }
                    var headCount = headBuff.Length - headRemainingBytes;
                    ReadHead(headCount);
                    _baseStream = DeflateCodec.Decompress(_stream, _inflater);
                    _crc32Stream = new Crc32Stream(_baseStream);
                }
                var result = _crc32Stream.Read(buffer, offset, count);
                if (result <= 0)
                {
                    var trailBuff = _inflater.Buffer;
                    var trailOffset = _inflater.AvailableOffset;
                    var trailCount = _inflater.AvailableCount;
                    var size = _crc32Stream.CrcSize & 0xFFFFFFFF;
                    var crc = (_crc32Stream.CrcValue ^ 0xFFFFFFFF) & 0xFFFFFFFF;
                    if (trailCount == 8)
                    {
                        if ((byte)crc != trailBuff[trailOffset] || (byte)(crc >> 8) != trailBuff[trailOffset + 1]
                            || (byte)(crc >> 16) != trailBuff[trailOffset + 2] || (byte)(crc >> 24) != trailBuff[trailOffset + 3])
                        {
                            throw new InvalidDataException(nameof(crc));
                        }
                        if ((byte)size != trailBuff[trailOffset + 4] || (byte)(size >> 8) != trailBuff[trailOffset + 5]
                           || (byte)(size >> 16) != trailBuff[trailOffset + 6] || (byte)(size >> 24) != trailBuff[trailOffset + 7])
                        {
                            throw new InvalidDataException(nameof(crc));
                        }
                        var trailResult = _stream.Read(trailBuff, 0, trailBuff.Length);//在读一次看看完没完
                        if (trailResult != 0)
                            throw new InvalidDataException("trailCount");
                        //尾部读取成功
                    }
                    else if (trailCount < 8)
                    {
                        Array.Copy(trailBuff, trailOffset, trailBuff, 0, trailCount);
                        var trailRemainingBytes = trailBuff.Length - trailCount;
                        var tempOffset = trailCount;
                        while (true)
                        {
                            var bytesRead = _stream.Read(trailBuff, tempOffset, trailRemainingBytes);
                            if (bytesRead <= 0)
                                break;
                            tempOffset += bytesRead;
                            trailRemainingBytes -= bytesRead;
                            if (trailRemainingBytes == 0)
                                break;
                        }
                        trailCount = trailBuff.Length - trailRemainingBytes;
                        if (trailCount != 8)
                            throw new InvalidDataException("trailCount");

                        if ((byte)crc != trailBuff[0] || (byte)(crc >> 8) != trailBuff[1]
                            || (byte)(crc >> 16) != trailBuff[2] || (byte)(crc >> 24) != trailBuff[3])
                        {
                            throw new InvalidDataException(nameof(crc));
                        }
                        if ((byte)size != trailBuff[4] || (byte)(size >> 8) != trailBuff[5]
                           || (byte)(size >> 16) != trailBuff[6] || (byte)(size >> 24) != trailBuff[7])
                        {
                            throw new InvalidDataException(nameof(crc));
                        }
                        //成功
                    }
                    else
                    {
                        throw new InvalidDataException("trailCount");
                    }

                    _stream = null;
                }
                return result;
            }
            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                if (_inflater == null)
                    throw new ObjectDisposedException(nameof(InflateStream));
                if (buffer == null)
                    throw new ArgumentNullException(nameof(buffer));
                if (offset < 0 || offset >= buffer.Length)
                    throw new ArgumentOutOfRangeException(nameof(offset));
                if (count < 0 || count > buffer.Length - offset)
                    throw new ArgumentOutOfRangeException(nameof(count));

                if (_stream == null)
                    return 0;

                if (_baseStream == null)//头不还没读取
                {
                    var headBuff = _inflater.Buffer;
                    var headOffset = 0;
                    var headRemainingBytes = headBuff.Length;
                    while (true)
                    {
                        int bytesRead = await _stream.ReadAsync(headBuff, headOffset, headRemainingBytes);
                        if (bytesRead <= 0)
                            break;
                        headOffset += bytesRead;
                        headRemainingBytes -= bytesRead;
                        if (headRemainingBytes == 0)
                            break;
                    }
                    var headCount = headBuff.Length - headRemainingBytes;
                    ReadHead(headCount);
                    _baseStream = DeflateCodec.Decompress(_stream, _inflater);
                    _crc32Stream = new Crc32Stream(_baseStream);
                }
                var result = await _crc32Stream.ReadAsync(buffer, offset, count);
                if (result <= 0)
                {
                    var trailBuff = _inflater.Buffer;
                    var trailOffset = _inflater.AvailableOffset;
                    var trailCount = _inflater.AvailableCount;
                    var size = _crc32Stream.CrcSize & 0xFFFFFFFF;
                    var crc = (_crc32Stream.CrcValue ^ 0xFFFFFFFF) & 0xFFFFFFFF;
                    if (trailCount == 8)
                    {
                        if ((byte)crc != trailBuff[trailOffset] || (byte)(crc >> 8) != trailBuff[trailOffset + 1]
                            || (byte)(crc >> 16) != trailBuff[trailOffset + 2] || (byte)(crc >> 24) != trailBuff[trailOffset + 3])
                        {
                            throw new InvalidDataException(nameof(crc));
                        }
                        if ((byte)size != trailBuff[trailOffset + 4] || (byte)(size >> 8) != trailBuff[trailOffset + 5]
                           || (byte)(size >> 16) != trailBuff[trailOffset + 6] || (byte)(size >> 24) != trailBuff[trailOffset + 7])
                        {
                            throw new InvalidDataException(nameof(crc));
                        }
                        var trailResult = _stream.Read(trailBuff, 0, trailBuff.Length);//在读一次看看完没完
                        if (trailResult != 0)
                            throw new InvalidDataException("trailCount");
                        //尾部读取成功
                    }
                    else if (trailCount < 8)
                    {
                        Array.Copy(trailBuff, trailOffset, trailBuff, 0, trailCount);
                        var trailRemainingBytes = trailBuff.Length - trailCount;
                        var tempOffset = trailCount;
                        while (true)
                        {
                            var bytesRead = await _stream.ReadAsync(trailBuff, tempOffset, trailRemainingBytes);
                            if (bytesRead <= 0)
                                break;
                            tempOffset += bytesRead;
                            trailRemainingBytes -= bytesRead;
                            if (trailRemainingBytes == 0)
                                break;
                        }
                        trailCount = trailBuff.Length - trailRemainingBytes;
                        if (trailCount != 8)
                            throw new InvalidDataException("trailCount");

                        if ((byte)crc != trailBuff[0] || (byte)(crc >> 8) != trailBuff[1]
                            || (byte)(crc >> 16) != trailBuff[2] || (byte)(crc >> 24) != trailBuff[3])
                        {
                            throw new InvalidDataException(nameof(crc));
                        }
                        if ((byte)size != trailBuff[4] || (byte)(size >> 8) != trailBuff[5]
                           || (byte)(size >> 16) != trailBuff[6] || (byte)(size >> 24) != trailBuff[7])
                        {
                            throw new InvalidDataException(nameof(crc));
                        }
                        //成功
                    }
                    else
                    {
                        throw new InvalidDataException("trailCount");
                    }

                    _stream = null;
                }
                return result;
            }
            protected override void Dispose(bool disposing)
            {
                if (_inflater != null)
                {

                }
                base.Dispose(disposing);
            }
            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }
            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }
            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }
    }
}
