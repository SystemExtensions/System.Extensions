
namespace System.IO.Compression
{
    using System.Threading;
    using System.Threading.Tasks;
    public class DeflateCodec
    {
        public static Stream Compress(Stream stream, Deflater deflater)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (deflater == null)
                throw new ArgumentNullException(nameof(deflater));

            deflater.Init();
            return new DeflateStream(stream, deflater);
        }
        //流可能没用完
        public static Stream Decompress(Stream stream, Inflater inflater)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (inflater == null)
                throw new ArgumentNullException(nameof(inflater));

            inflater.Init();
            return new InflateStream(stream, inflater);
        }
        private class DeflateStream : Stream
        {
            public DeflateStream(Stream stream, Deflater deflater)
            {
                _stream = stream;
                _deflater = deflater;
            }

            private Stream _stream;
            private Deflater _deflater;
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length
            {
                get
                {
                    if (_deflater == null)
                        throw new ObjectDisposedException(nameof(Stream));
                    if (_deflater.IsFinished)
                        return _deflater.TotalOut;

                    throw new InvalidOperationException();
                }
            }
            public override long Position
            {
                get
                {
                    if (_deflater == null)
                        throw new ObjectDisposedException(nameof(Stream));

                    return _deflater.TotalOut;//是否减1
                }
                set
                {
                    throw new NotSupportedException();
                }
            }
            public override void Flush()
            {

            }
            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_deflater == null)
                    throw new ObjectDisposedException(nameof(Stream));

                if (buffer == null)
                    throw new ArgumentNullException(nameof(buffer));
                if (offset < 0 || offset >= buffer.Length)
                    throw new ArgumentOutOfRangeException(nameof(offset));
                if (count < 0 || count > buffer.Length - offset)
                    throw new ArgumentOutOfRangeException(nameof(count));

                if (_deflater.IsFinished)
                    return 0;

                int remainingBytes = count;
                while (true)
                {
                    int bytesRead = _deflater.Deflate(buffer, offset, remainingBytes);
                    offset += bytesRead;
                    remainingBytes -= bytesRead;

                    if (remainingBytes == 0 || _deflater.IsFinished)
                    {
                        break;
                    }

                    if (_deflater.IsNeedingInput)
                    {
                        var rawData = _deflater.Buffer;
                        var rawLength = 0;
                        int toRead = rawData.Length;
                        var result = 0;
                        while (toRead > 0)
                        {
                            result = _stream.Read(rawData, rawLength, toRead);
                            if (result <= 0)
                            {
                                break;
                            }
                            rawLength += result;
                            toRead -= result;
                        }
                        _deflater.SetInput(rawData, 0, rawLength);

                        if (result == 0)
                            _deflater.Finish();
                    }
                    else if (bytesRead == 0)
                    {
                        throw new InvalidDataException("Dont know what to do");
                    }
                }
                return count - remainingBytes;
            }
            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                if (_deflater == null)
                    throw new ObjectDisposedException(nameof(Stream));

                if (buffer == null)
                    throw new ArgumentNullException(nameof(buffer));
                if (offset < 0 || offset >= buffer.Length)
                    throw new ArgumentOutOfRangeException(nameof(offset));
                if (count < 0 || count > buffer.Length - offset)
                    throw new ArgumentOutOfRangeException(nameof(count));

                if (_deflater.IsFinished)
                    return 0;

                int remainingBytes = count;
                while (true)
                {
                    int bytesRead = _deflater.Deflate(buffer, offset, remainingBytes);
                    offset += bytesRead;
                    remainingBytes -= bytesRead;

                    if (remainingBytes == 0 || _deflater.IsFinished)
                    {
                        break;
                    }

                    if (_deflater.IsNeedingInput)
                    {
                        var rawData = _deflater.Buffer;
                        var rawLength = 0;
                        int toRead = rawData.Length;
                        var result = 0;
                        while (toRead > 0)
                        {
                            result = await _stream.ReadAsync(rawData, rawLength, toRead);
                            if (result <= 0)
                            {
                                break;
                            }
                            rawLength += result;
                            toRead -= result;
                        }
                        _deflater.SetInput(rawData, 0, rawLength);

                        if (result == 0)
                            _deflater.Finish();
                    }
                    else if (bytesRead == 0)
                    {
                        throw new InvalidDataException("Dont know what to do");
                    }
                }
                return count - remainingBytes;
            }
            protected override void Dispose(bool disposing)
            {
                if (_deflater != null)
                {
                    _deflater.Reset();
                    _deflater = null;
                    _stream = null;
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
        /// <summary>
        /// This is the Deflater class.  The deflater class compresses input
        /// with the deflate algorithm described in RFC 1951.  It has several
        /// compression levels and three different strategies described below.
        ///
        /// This class is <i>not</i> thread safe.  This is inherent in the API, due
        /// to the split of deflate and setInput.
        /// 
        /// author of the original java version : Jochen Hoenicke
        /// </summary>
        public class Deflater
        {
#region Deflater Documentation
            /*
            * The Deflater can do the following state transitions:
            *
            * (1) -> INIT_STATE   ----> INIT_FINISHING_STATE ---.
            *        /  | (2)      (5)                          |
            *       /   v          (5)                          |
            *   (3)| SETDICT_STATE ---> SETDICT_FINISHING_STATE |(3)
            *       \   | (3)                 |        ,--------'
            *        |  |                     | (3)   /
            *        v  v          (5)        v      v
            * (1) -> BUSY_STATE   ----> FINISHING_STATE
            *                                | (6)
            *                                v
            *                           FINISHED_STATE
            *    \_____________________________________/
            *                    | (7)
            *                    v
            *               CLOSED_STATE
            *
            * (1) If we should produce a header we start in INIT_STATE, otherwise
            *     we start in BUSY_STATE.
            * (2) A dictionary may be set only when we are in INIT_STATE, then
            *     we change the state as indicated.
            * (3) Whether a dictionary is set or not, on the first call of deflate
            *     we change to BUSY_STATE.
            * (4) -- intentionally left blank -- :)
            * (5) FINISHING_STATE is entered, when flush() is called to indicate that
            *     there is no more INPUT.  There are also states indicating, that
            *     the header wasn't written yet.
            * (6) FINISHED_STATE is entered, when everything has been flushed to the
            *     internal pending output buffer.
            * (7) At any time (7)
            *
            */
#endregion

#region Local Constants
            private const int NO_COMPRESSION = 0;
            private const int IS_SETDICT = 0x01;
            private const int IS_FLUSHING = 0x04;
            private const int IS_FINISHING = 0x08;

            private const int INIT_STATE = 0x00;
            private const int SETDICT_STATE = 0x01;
            //		private static  int INIT_FINISHING_STATE    = 0x08;
            //		private static  int SETDICT_FINISHING_STATE = 0x09;
            private const int BUSY_STATE = 0x10;
            private const int FLUSHING_STATE = 0x14;
            private const int FINISHING_STATE = 0x1c;
            private const int FINISHED_STATE = 0x1e;
            private const int CLOSED_STATE = 0x7f;
            #endregion
            #region Constructors
            public Deflater() : this(6, 8192) { }
            public Deflater(int level) : this(level, 8192) { }
            public Deflater(int level, int bufferSize)
            {
                if (level < 0 || level > 9)
                {
                    throw new ArgumentOutOfRangeException(nameof(level));
                }
                if (bufferSize < 2048)
                    throw new ArgumentOutOfRangeException(nameof(bufferSize));

                pending = new DeflaterPending();
                engine = new DeflaterEngine(pending);
                Strategy = 0;
                Level = level;
                buffer = new byte[bufferSize];
                Reset();
            }
            #endregion

            /// <summary>
            /// Resets the deflater.  The deflater acts afterwards as if it was
            /// just created with the same compression level and strategy as it
            /// had before.
            /// </summary>
            internal void Reset()
            {
                dictionary = null;
                state = INIT_STATE;
                totalOut = 0;
                pending.Reset();
                engine.Reset();
            }

            internal void Init()
            {
                if (state != INIT_STATE)
                    throw new InvalidOperationException(nameof(state));

                if (dictionary != null)
                    engine.SetDictionary(dictionary, 0, dictionary.Length);
                state = BUSY_STATE | (state & (IS_FLUSHING | IS_FINISHING));
            }

            /// <summary>
            /// Gets the number of input bytes processed so far.
            /// </summary>
            public long TotalIn
            {
                get
                {
                    return engine.TotalIn;
                }
            }

            /// <summary>
            /// Gets the number of output bytes so far.
            /// </summary>
            public long TotalOut
            {
                get
                {
                    return totalOut;
                }
            }

            /// <summary>
            /// Flushes the current input block.  Further calls to deflate() will
            /// produce enough output to inflate everything in the current input
            /// block.  This is not part of Sun's JDK so I have made it package
            /// private.  It is used by DeflaterOutputStream to implement
            /// flush().
            /// </summary>
            internal void Flush()
            {
                state |= IS_FLUSHING;
            }

            /// <summary>
            /// Finishes the deflater with the current input block.  It is an error
            /// to give more input after this method was called.  This method must
            /// be called to force all bytes to be flushed.
            /// </summary>
            internal void Finish()
            {
                state |= (IS_FLUSHING | IS_FINISHING);
            }

            /// <summary>
            /// Returns true if the stream was finished and no more output bytes
            /// are available.
            /// </summary>
            internal bool IsFinished
            {
                get
                {
                    return (state == FINISHED_STATE) && pending.IsFlushed;
                }
            }

            /// <summary>
            /// Returns true, if the input buffer is empty.
            /// You should then call setInput(). 
            /// NOTE: This method can also return true when the stream
            /// was finished.
            /// </summary>
            internal bool IsNeedingInput
            {
                get
                {
                    return engine.NeedsInput();
                }
            }

            /// <summary>
            /// Sets the data which should be compressed next.  This should be
            /// only called when needsInput indicates that more input is needed.
            /// The given byte array should not be changed, before needsInput() returns
            /// true again.
            /// </summary>
            /// <param name="input">
            /// the buffer containing the input data.
            /// </param>
            /// <param name="offset">
            /// the start of the data.
            /// </param>
            /// <param name="count">
            /// the number of data bytes of input.
            /// </param>
            /// <exception cref="System.InvalidOperationException">
            /// if the buffer was Finish()ed or if previous input is still pending.
            /// </exception>
            internal void SetInput(byte[] input, int offset, int count)
            {
                if ((state & IS_FINISHING) != 0)
                {
                    throw new InvalidOperationException("Finish() already called");
                }
                engine.SetInput(input, offset, count);
            }

            public int Level
            {
                get { return level; }
                set
                {
                    if (state != INIT_STATE)
                        throw new InvalidOperationException();
                    if (value < 0 || value > 9)
                        throw new ArgumentOutOfRangeException(nameof(level));

                    if (this.level != value)
                    {
                        this.level = value;
                        engine.SetLevel(value);
                    }
                }
            }
            public int Strategy
            {
                get { return engine.Strategy; }
                set
                {
                    if (state != INIT_STATE)
                        throw new InvalidOperationException();

                    engine.Strategy = value;
                }
            }
            public byte[] Buffer
            {
                get { return buffer; }
                set
                {
                    if (state != INIT_STATE)
                        throw new InvalidOperationException();

                    buffer = value;
                }
            }
            public byte[] Dictionary
            {
                get { return dictionary; }
                set
                {
                    if (state != INIT_STATE)
                        throw new InvalidOperationException();

                    //state = SETDICT_STATE;//这个不要吧
                    dictionary = value;
                }
            }

            /// <summary>
            /// Deflates the current input block to the given array.
            /// </summary>
            /// <param name="output">
            /// Buffer to store the compressed data.
            /// </param>
            /// <param name="offset">
            /// Offset into the output array.
            /// </param>
            /// <param name="length">
            /// The maximum number of bytes that may be stored.
            /// </param>
            /// <returns>
            /// The number of compressed bytes added to the output, or 0 if either
            /// needsInput() or finished() returns true or length is zero.
            /// </returns>
            /// <exception cref="System.InvalidOperationException">
            /// If Finish() was previously called.
            /// </exception>
            /// <exception cref="System.ArgumentOutOfRangeException">
            /// If offset or length don't match the array length.
            /// </exception>
            internal int Deflate(byte[] output, int offset, int length)
            {
                int origLength = length;

                if (state == CLOSED_STATE)
                {
                    throw new InvalidOperationException("Deflater closed");
                }

                for (;;)
                {
                    int count = pending.Flush(output, offset, length);
                    offset += count;
                    totalOut += count;
                    length -= count;

                    if (length == 0 || state == FINISHED_STATE)
                    {
                        break;
                    }

                    if (!engine.Deflate((state & IS_FLUSHING) != 0, (state & IS_FINISHING) != 0))
                    {
                        switch (state)
                        {
                            case BUSY_STATE:
                                // We need more input now
                                return origLength - length;
                            case FLUSHING_STATE:
                                if (level != NO_COMPRESSION)
                                {
                                    /* We have to supply some lookahead.  8 bit lookahead
                                     * is needed by the zlib inflater, and we must fill
                                     * the next byte, so that all bits are flushed.
                                     */
                                    int neededbits = 8 + ((-pending.BitCount) & 7);
                                    while (neededbits > 0)
                                    {
                                        /* write a static tree block consisting solely of
                                         * an EOF:
                                         */
                                        pending.WriteBits(2, 10);
                                        neededbits -= 10;
                                    }
                                }
                                state = BUSY_STATE;
                                break;
                            case FINISHING_STATE:
                                pending.AlignToByte();

                                // Compressed data is complete.  Write footer information if required.
                                //if (!noZlibHeaderOrFooter) {
                                //	int adler = engine.Adler;
                                //	pending.WriteShortMSB(adler >> 16);
                                //	pending.WriteShortMSB(adler & 0xffff);
                                //}
                                state = FINISHED_STATE;
                                break;
                        }
                    }
                }
                return origLength - length;
            }


#region Instance Fields
            /// <summary>
            /// Compression level.
            /// </summary>
            int level;

            /// <summary>
            /// The current state.
            /// </summary>
            int state;

            /// <summary>
            /// The total bytes of output written.
            /// </summary>
            long totalOut;

            /// <summary>
            /// The pending output.
            /// </summary>
            DeflaterPending pending;

            /// <summary>
            /// The deflater engine.
            /// </summary>
            DeflaterEngine engine;

            /// <summary>
            /// Buffer
            /// </summary>
            byte[] buffer;

            /// <summary>
            /// Dictionary
            /// </summary>
            byte[] dictionary;
#endregion
        }
        /// <summary>
        /// This class contains constants used for deflation.
        /// </summary>
        private static class DeflaterConstants
        {
            /// <summary>
            /// Set to true to enable debugging
            /// </summary>
            public const bool DEBUGGING = false;

            /// <summary>
            /// Written to Zip file to identify a stored block
            /// </summary>
            public const int STORED_BLOCK = 0;

            /// <summary>
            /// Identifies static tree in Zip file
            /// </summary>
            public const int STATIC_TREES = 1;

            /// <summary>
            /// Identifies dynamic tree in Zip file
            /// </summary>
            public const int DYN_TREES = 2;

            /// <summary>
            /// Header flag indicating a preset dictionary for deflation
            /// </summary>
            public const int PRESET_DICT = 0x20;//Remove

            /// <summary>
            /// Sets internal buffer sizes for Huffman encoding
            /// </summary>
            public const int DEFAULT_MEM_LEVEL = 8;

            /// <summary>
            /// Internal compression engine constant
            /// </summary>		
            public const int MAX_MATCH = 258;

            /// <summary>
            /// Internal compression engine constant
            /// </summary>		
            public const int MIN_MATCH = 3;

            /// <summary>
            /// Internal compression engine constant
            /// </summary>		
            public const int MAX_WBITS = 15;

            /// <summary>
            /// Internal compression engine constant
            /// </summary>		
            public const int WSIZE = 1 << MAX_WBITS;

            /// <summary>
            /// Internal compression engine constant
            /// </summary>		
            public const int WMASK = WSIZE - 1;

            /// <summary>
            /// Internal compression engine constant
            /// </summary>		
            public const int HASH_BITS = DEFAULT_MEM_LEVEL + 7;

            /// <summary>
            /// Internal compression engine constant
            /// </summary>		
            public const int HASH_SIZE = 1 << HASH_BITS;

            /// <summary>
            /// Internal compression engine constant
            /// </summary>		
            public const int HASH_MASK = HASH_SIZE - 1;

            /// <summary>
            /// Internal compression engine constant
            /// </summary>		
            public const int HASH_SHIFT = (HASH_BITS + MIN_MATCH - 1) / MIN_MATCH;

            /// <summary>
            /// Internal compression engine constant
            /// </summary>		
            public const int MIN_LOOKAHEAD = MAX_MATCH + MIN_MATCH + 1;

            /// <summary>
            /// Internal compression engine constant
            /// </summary>		
            public const int MAX_DIST = WSIZE - MIN_LOOKAHEAD;

            /// <summary>
            /// Internal compression engine constant
            /// </summary>		
            public const int PENDING_BUF_SIZE = 1 << (DEFAULT_MEM_LEVEL + 8);

            /// <summary>
            /// Internal compression engine constant
            /// </summary>		
            public static int MAX_BLOCK_SIZE = Math.Min(65535, PENDING_BUF_SIZE - 5);

            /// <summary>
            /// Internal compression engine constant
            /// </summary>		
            public const int DEFLATE_STORED = 0;

            /// <summary>
            /// Internal compression engine constant
            /// </summary>		
            public const int DEFLATE_FAST = 1;

            /// <summary>
            /// Internal compression engine constant
            /// </summary>		
            public const int DEFLATE_SLOW = 2;

            /// <summary>
            /// Internal compression engine constant
            /// </summary>		
            public static int[] GOOD_LENGTH = { 0, 4, 4, 4, 4, 8, 8, 8, 32, 32 };

            /// <summary>
            /// Internal compression engine constant
            /// </summary>		
            public static int[] MAX_LAZY = { 0, 4, 5, 6, 4, 16, 16, 32, 128, 258 };

            /// <summary>
            /// Internal compression engine constant
            /// </summary>		
            public static int[] NICE_LENGTH = { 0, 8, 16, 32, 16, 32, 128, 128, 258, 258 };

            /// <summary>
            /// Internal compression engine constant
            /// </summary>		
            public static int[] MAX_CHAIN = { 0, 4, 8, 32, 16, 32, 128, 256, 1024, 4096 };

            /// <summary>
            /// Internal compression engine constant
            /// </summary>		
            public static int[] COMPR_FUNC = { 0, 1, 1, 1, 1, 2, 2, 2, 2, 2 };

        }
        /// <summary>
        /// Low level compression engine for deflate algorithm which uses a 32K sliding window
        /// with secondary compression from Huffman/Shannon-Fano codes.
        /// </summary>
        private class DeflaterEngine
        {
#region Constants
            const int TooFar = 4096;

            //DeflateStrategy
            /// <summary>
            /// This strategy will only allow longer string repetitions.  It is
            /// useful for random data with a small character set.
            /// </summary>
            const int Filtered = 1;

            /// <summary>
            /// This strategy will not look for string repetitions at all.  It
            /// only encodes with Huffman trees (which means, that more common
            /// characters get a smaller encoding.
            /// </summary>
            const int HuffmanOnly = 2;
#endregion

#region Constructors
            /// <summary>
            /// Construct instance with pending buffer
            /// </summary>
            /// <param name="pending">
            /// Pending buffer to use
            /// </param>>
            public DeflaterEngine(DeflaterPending pending)
            {
                this.pending = pending;
                huffman = new DeflaterHuffman(pending);

                window = new byte[2 * DeflaterConstants.WSIZE];
                head = new short[DeflaterConstants.HASH_SIZE];
                prev = new short[DeflaterConstants.WSIZE];

                // We start at index 1, to avoid an implementation deficiency, that
                // we cannot build a repeat pattern at index 0.
                blockStart = strstart = 1;
            }

#endregion

            /// <summary>
            /// Deflate drives actual compression of data
            /// </summary>
            /// <param name="flush">True to flush input buffers</param>
            /// <param name="finish">Finish deflation with the current input.</param>
            /// <returns>Returns true if progress has been made.</returns>
            public bool Deflate(bool flush, bool finish)
            {
                bool progress;
                do
                {
                    FillWindow();
                    bool canFlush = flush && (inputOff == inputEnd);

#if DebugDeflation
				if (DeflaterConstants.DEBUGGING) {
					Console.WriteLine("window: [" + blockStart + "," + strstart + ","
								+ lookahead + "], " + compressionFunction + "," + canFlush);
				}
#endif
                    switch (compressionFunction)
                    {
                        case DeflaterConstants.DEFLATE_STORED:
                            progress = DeflateStored(canFlush, finish);
                            break;
                        case DeflaterConstants.DEFLATE_FAST:
                            progress = DeflateFast(canFlush, finish);
                            break;
                        case DeflaterConstants.DEFLATE_SLOW:
                            progress = DeflateSlow(canFlush, finish);
                            break;
                        default:
                            throw new InvalidOperationException("unknown compressionFunction");
                    }
                } while (pending.IsFlushed && progress); // repeat while we have no pending output and progress was made
                return progress;
            }

            /// <summary>
            /// Sets input data to be deflated.  Should only be called when <code>NeedsInput()</code>
            /// returns true
            /// </summary>
            /// <param name="buffer">The buffer containing input data.</param>
            /// <param name="offset">The offset of the first byte of data.</param>
            /// <param name="count">The number of bytes of data to use as input.</param>
            public void SetInput(byte[] buffer, int offset, int count)
            {
                if (buffer == null)
                {
                    throw new ArgumentNullException(nameof(buffer));
                }

                if (offset < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(offset));
                }

                if (count < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(count));
                }

                if (inputOff < inputEnd)
                {
                    throw new InvalidOperationException("Old input was not completely processed");
                }

                int end = offset + count;

                /* We want to throw an ArrayIndexOutOfBoundsException early.  The
                * check is very tricky: it also handles integer wrap around.
                */
                if ((offset > end) || (end > buffer.Length))
                {
                    throw new ArgumentOutOfRangeException(nameof(count));
                }

                inputBuf = buffer;
                inputOff = offset;
                inputEnd = end;
            }

            /// <summary>
            /// Determines if more <see cref="SetInput">input</see> is needed.
            /// </summary>		
            /// <returns>Return true if input is needed via <see cref="SetInput">SetInput</see></returns>
            public bool NeedsInput()
            {
                return (inputEnd == inputOff);
            }

            /// <summary>
            /// Set compression dictionary
            /// </summary>
            /// <param name="buffer">The buffer containing the dictionary data</param>
            /// <param name="offset">The offset in the buffer for the first byte of data</param>
            /// <param name="length">The length of the dictionary data.</param>
            public void SetDictionary(byte[] buffer, int offset, int length)
            {
#if DebugDeflation
			if (DeflaterConstants.DEBUGGING && (strstart != 1) ) 
			{
				throw new InvalidOperationException("strstart not 1");
			}
#endif
                if (length < DeflaterConstants.MIN_MATCH)
                {
                    return;
                }

                if (length > DeflaterConstants.MAX_DIST)
                {
                    offset += length - DeflaterConstants.MAX_DIST;
                    length = DeflaterConstants.MAX_DIST;
                }
                Buffer.BlockCopy(buffer, offset, window, strstart, length);
                //System.Array.Copy(buffer, offset, window, strstart, length);

                UpdateHash();
                --length;
                while (--length > 0)
                {
                    InsertString();
                    strstart++;
                }
                strstart += 2;
                blockStart = strstart;
            }

            /// <summary>
            /// Reset internal state
            /// </summary>		
            public void Reset()
            {
                huffman.Reset();
                blockStart = strstart = 1;
                lookahead = 0;
                totalIn = 0;
                prevAvailable = false;
                matchLen = DeflaterConstants.MIN_MATCH - 1;

                for (int i = 0; i < DeflaterConstants.HASH_SIZE; i++)
                {
                    head[i] = 0;
                }

                for (int i = 0; i < DeflaterConstants.WSIZE; i++)
                {
                    prev[i] = 0;
                }
            }

            /// <summary>
            /// Total data processed
            /// </summary>		
            public long TotalIn
            {
                get
                {
                    return totalIn;
                }
            }

            /// <summary>
            /// Get/set the <see cref="DeflateStrategy">deflate strategy</see>
            /// </summary>		
            public int Strategy
            {
                get
                {
                    return strategy;
                }
                set
                {
                    strategy = value;
                }
            }

            /// <summary>
            /// Set the deflate level (0-9)
            /// </summary>
            /// <param name="level">The value to set the level to.</param>
            public void SetLevel(int level)
            {
                if ((level < 0) || (level > 9))
                {
                    throw new ArgumentOutOfRangeException(nameof(level));
                }

                goodLength = DeflaterConstants.GOOD_LENGTH[level];
                max_lazy = DeflaterConstants.MAX_LAZY[level];
                niceLength = DeflaterConstants.NICE_LENGTH[level];
                max_chain = DeflaterConstants.MAX_CHAIN[level];

                if (DeflaterConstants.COMPR_FUNC[level] != compressionFunction)
                {

#if DebugDeflation
				if (DeflaterConstants.DEBUGGING) {
				   Console.WriteLine("Change from " + compressionFunction + " to "
										  + DeflaterConstants.COMPR_FUNC[level]);
				}
#endif
                    switch (compressionFunction)
                    {
                        case DeflaterConstants.DEFLATE_STORED:
                            if (strstart > blockStart)
                            {
                                huffman.FlushStoredBlock(window, blockStart,
                                    strstart - blockStart, false);
                                blockStart = strstart;
                            }
                            UpdateHash();
                            break;

                        case DeflaterConstants.DEFLATE_FAST:
                            if (strstart > blockStart)
                            {
                                huffman.FlushBlock(window, blockStart, strstart - blockStart,
                                    false);
                                blockStart = strstart;
                            }
                            break;

                        case DeflaterConstants.DEFLATE_SLOW:
                            if (prevAvailable)
                            {
                                huffman.TallyLit(window[strstart - 1] & 0xff);
                            }
                            if (strstart > blockStart)
                            {
                                huffman.FlushBlock(window, blockStart, strstart - blockStart, false);
                                blockStart = strstart;
                            }
                            prevAvailable = false;
                            matchLen = DeflaterConstants.MIN_MATCH - 1;
                            break;
                    }
                    compressionFunction = DeflaterConstants.COMPR_FUNC[level];
                }
            }

            /// <summary>
            /// Fill the window
            /// </summary>
            public void FillWindow()
            {
                /* If the window is almost full and there is insufficient lookahead,
                 * move the upper half to the lower one to make room in the upper half.
                 */
                if (strstart >= DeflaterConstants.WSIZE + DeflaterConstants.MAX_DIST)
                {
                    SlideWindow();
                }

                /* If there is not enough lookahead, but still some input left,
                 * read in the input
                 */
                if (lookahead < DeflaterConstants.MIN_LOOKAHEAD && inputOff < inputEnd)
                {
                    int more = 2 * DeflaterConstants.WSIZE - lookahead - strstart;

                    if (more > inputEnd - inputOff)
                    {
                        more = inputEnd - inputOff;
                    }

                    System.Array.Copy(inputBuf, inputOff, window, strstart + lookahead, more);
                    //adler.Update(inputBuf, inputOff, more);

                    inputOff += more;
                    totalIn += more;
                    lookahead += more;
                }

                if (lookahead >= DeflaterConstants.MIN_MATCH)
                {
                    UpdateHash();
                }
            }

            void UpdateHash()
            {
                /*
                            if (DEBUGGING) {
                                Console.WriteLine("updateHash: "+strstart);
                            }
                */
                ins_h = (window[strstart] << DeflaterConstants.HASH_SHIFT) ^ window[strstart + 1];
            }

            /// <summary>
            /// Inserts the current string in the head hash and returns the previous
            /// value for this hash.
            /// </summary>
            /// <returns>The previous hash value</returns>
            int InsertString()
            {
                short match;
                int hash = ((ins_h << DeflaterConstants.HASH_SHIFT) ^ window[strstart + (DeflaterConstants.MIN_MATCH - 1)]) & DeflaterConstants.HASH_MASK;

#if DebugDeflation
			if (DeflaterConstants.DEBUGGING) 
			{
				if (hash != (((window[strstart] << (2*HASH_SHIFT)) ^ 
								  (window[strstart + 1] << HASH_SHIFT) ^ 
								  (window[strstart + 2])) & HASH_MASK)) {
						throw new InvalidDataException("hash inconsistent: " + hash + "/"
												+window[strstart] + ","
												+window[strstart + 1] + ","
												+window[strstart + 2] + "," + HASH_SHIFT);
					}
			}
#endif
                prev[strstart & DeflaterConstants.WMASK] = match = head[hash];
                head[hash] = unchecked((short)strstart);
                ins_h = hash;
                return match & 0xffff;
            }

            void SlideWindow()
            {
                Array.Copy(window, DeflaterConstants.WSIZE, window, 0, DeflaterConstants.WSIZE);
                matchStart -= DeflaterConstants.WSIZE;
                strstart -= DeflaterConstants.WSIZE;
                blockStart -= DeflaterConstants.WSIZE;

                // Slide the hash table (could be avoided with 32 bit values
                // at the expense of memory usage).
                for (int i = 0; i < DeflaterConstants.HASH_SIZE; ++i)
                {
                    int m = head[i] & 0xffff;
                    head[i] = (short)(m >= DeflaterConstants.WSIZE ? (m - DeflaterConstants.WSIZE) : 0);
                }

                // Slide the prev table.
                for (int i = 0; i < DeflaterConstants.WSIZE; i++)
                {
                    int m = prev[i] & 0xffff;
                    prev[i] = (short)(m >= DeflaterConstants.WSIZE ? (m - DeflaterConstants.WSIZE) : 0);
                }
            }

            /// <summary>
            /// Find the best (longest) string in the window matching the 
            /// string starting at strstart.
            ///
            /// Preconditions:
            /// <code>
            /// strstart + DeflaterConstants.MAX_MATCH &lt;= window.length.</code>
            /// </summary>
            /// <param name="curMatch"></param>
            /// <returns>True if a match greater than the minimum length is found</returns>
            bool FindLongestMatch(int curMatch)
            {
                int match;
                int scan = strstart;
                // scanMax is the highest position that we can look at
                int scanMax = scan + Math.Min(DeflaterConstants.MAX_MATCH, lookahead) - 1;
                int limit = Math.Max(scan - DeflaterConstants.MAX_DIST, 0);

                byte[] window = this.window;
                short[] prev = this.prev;
                int chainLength = this.max_chain;
                int niceLength = Math.Min(this.niceLength, lookahead);

                matchLen = Math.Max(matchLen, DeflaterConstants.MIN_MATCH - 1);

                if (scan + matchLen > scanMax) return false;

                byte scan_end1 = window[scan + matchLen - 1];
                byte scan_end = window[scan + matchLen];

                // Do not waste too much time if we already have a good match:
                if (matchLen >= this.goodLength) chainLength >>= 2;

                do
                {
                    match = curMatch;
                    scan = strstart;

                    if (window[match + matchLen] != scan_end
                     || window[match + matchLen - 1] != scan_end1
                     || window[match] != window[scan]
                     || window[++match] != window[++scan])
                    {
                        continue;
                    }

                    // scan is set to strstart+1 and the comparison passed, so
                    // scanMax - scan is the maximum number of bytes we can compare.
                    // below we compare 8 bytes at a time, so first we compare
                    // (scanMax - scan) % 8 bytes, so the remainder is a multiple of 8

                    switch ((scanMax - scan) % 8)
                    {
                        case 1:
                            if (window[++scan] == window[++match]) break;
                            break;
                        case 2:
                            if (window[++scan] == window[++match]
                      && window[++scan] == window[++match]) break;
                            break;
                        case 3:
                            if (window[++scan] == window[++match]
                      && window[++scan] == window[++match]
                      && window[++scan] == window[++match]) break;
                            break;
                        case 4:
                            if (window[++scan] == window[++match]
                      && window[++scan] == window[++match]
                      && window[++scan] == window[++match]
                      && window[++scan] == window[++match]) break;
                            break;
                        case 5:
                            if (window[++scan] == window[++match]
                      && window[++scan] == window[++match]
                      && window[++scan] == window[++match]
                      && window[++scan] == window[++match]
                      && window[++scan] == window[++match]) break;
                            break;
                        case 6:
                            if (window[++scan] == window[++match]
                      && window[++scan] == window[++match]
                      && window[++scan] == window[++match]
                      && window[++scan] == window[++match]
                      && window[++scan] == window[++match]
                      && window[++scan] == window[++match]) break;
                            break;
                        case 7:
                            if (window[++scan] == window[++match]
                      && window[++scan] == window[++match]
                      && window[++scan] == window[++match]
                      && window[++scan] == window[++match]
                      && window[++scan] == window[++match]
                      && window[++scan] == window[++match]
                      && window[++scan] == window[++match]) break;
                            break;
                    }

                    if (window[scan] == window[match])
                    {
                        /* We check for insufficient lookahead only every 8th comparison;
                         * the 256th check will be made at strstart + 258 unless lookahead is
                         * exhausted first.
                         */
                        do
                        {
                            if (scan == scanMax)
                            {
                                ++scan;     // advance to first position not matched
                                ++match;

                                break;
                            }
                        }
                        while (window[++scan] == window[++match]
                            && window[++scan] == window[++match]
                            && window[++scan] == window[++match]
                            && window[++scan] == window[++match]
                            && window[++scan] == window[++match]
                            && window[++scan] == window[++match]
                            && window[++scan] == window[++match]
                            && window[++scan] == window[++match]);
                    }

                    if (scan - strstart > matchLen)
                    {
#if DebugDeflation
              if (DeflaterConstants.DEBUGGING && (ins_h == 0) )
              Console.Error.WriteLine("Found match: " + curMatch + "-" + (scan - strstart));
#endif

                        matchStart = curMatch;
                        matchLen = scan - strstart;

                        if (matchLen >= niceLength)
                            break;

                        scan_end1 = window[scan - 1];
                        scan_end = window[scan];
                    }
                } while ((curMatch = (prev[curMatch & DeflaterConstants.WMASK] & 0xffff)) > limit && 0 != --chainLength);

                return matchLen >= DeflaterConstants.MIN_MATCH;
            }

            bool DeflateStored(bool flush, bool finish)
            {
                if (!flush && (lookahead == 0))
                {
                    return false;
                }

                strstart += lookahead;
                lookahead = 0;

                int storedLength = strstart - blockStart;

                if ((storedLength >= DeflaterConstants.MAX_BLOCK_SIZE) || // Block is full
                    (blockStart < DeflaterConstants.WSIZE && storedLength >= DeflaterConstants.MAX_DIST) ||   // Block may move out of window
                    flush)
                {
                    bool lastBlock = finish;
                    if (storedLength > DeflaterConstants.MAX_BLOCK_SIZE)
                    {
                        storedLength = DeflaterConstants.MAX_BLOCK_SIZE;
                        lastBlock = false;
                    }

#if DebugDeflation
				if (DeflaterConstants.DEBUGGING) 
				{
				   Console.WriteLine("storedBlock[" + storedLength + "," + lastBlock + "]");
				}
#endif

                    huffman.FlushStoredBlock(window, blockStart, storedLength, lastBlock);
                    blockStart += storedLength;
                    return !lastBlock;
                }
                return true;
            }

            bool DeflateFast(bool flush, bool finish)
            {
                if (lookahead < DeflaterConstants.MIN_LOOKAHEAD && !flush)
                {
                    return false;
                }

                while (lookahead >= DeflaterConstants.MIN_LOOKAHEAD || flush)
                {
                    if (lookahead == 0)
                    {
                        // We are flushing everything
                        huffman.FlushBlock(window, blockStart, strstart - blockStart, finish);
                        blockStart = strstart;
                        return false;
                    }

                    if (strstart > 2 * DeflaterConstants.WSIZE - DeflaterConstants.MIN_LOOKAHEAD)
                    {
                        /* slide window, as FindLongestMatch needs this.
                         * This should only happen when flushing and the window
                         * is almost full.
                         */
                        SlideWindow();
                    }

                    int hashHead;
                    if (lookahead >= DeflaterConstants.MIN_MATCH &&
                        (hashHead = InsertString()) != 0 &&
                        strategy != HuffmanOnly &&
                        strstart - hashHead <= DeflaterConstants.MAX_DIST &&
                        FindLongestMatch(hashHead))
                    {
                        // longestMatch sets matchStart and matchLen
#if DebugDeflation
					if (DeflaterConstants.DEBUGGING) 
					{
						for (int i = 0 ; i < matchLen; i++) {
							if (window[strstart + i] != window[matchStart + i]) {
								throw new InvalidDataException("Match failure");
							}
						}
					}
#endif

                        bool full = huffman.TallyDist(strstart - matchStart, matchLen);

                        lookahead -= matchLen;
                        if (matchLen <= max_lazy && lookahead >= DeflaterConstants.MIN_MATCH)
                        {
                            while (--matchLen > 0)
                            {
                                ++strstart;
                                InsertString();
                            }
                            ++strstart;
                        }
                        else {
                            strstart += matchLen;
                            if (lookahead >= DeflaterConstants.MIN_MATCH - 1)
                            {
                                UpdateHash();
                            }
                        }
                        matchLen = DeflaterConstants.MIN_MATCH - 1;
                        if (!full)
                        {
                            continue;
                        }
                    }
                    else {
                        // No match found
                        huffman.TallyLit(window[strstart] & 0xff);
                        ++strstart;
                        --lookahead;
                    }

                    if (huffman.IsFull())
                    {
                        bool lastBlock = finish && (lookahead == 0);
                        huffman.FlushBlock(window, blockStart, strstart - blockStart, lastBlock);
                        blockStart = strstart;
                        return !lastBlock;
                    }
                }
                return true;
            }

            bool DeflateSlow(bool flush, bool finish)
            {
                if (lookahead < DeflaterConstants.MIN_LOOKAHEAD && !flush)
                {
                    return false;
                }

                while (lookahead >= DeflaterConstants.MIN_LOOKAHEAD || flush)
                {
                    if (lookahead == 0)
                    {
                        if (prevAvailable)
                        {
                            huffman.TallyLit(window[strstart - 1] & 0xff);
                        }
                        prevAvailable = false;

                        // We are flushing everything
#if DebugDeflation
					if (DeflaterConstants.DEBUGGING && !flush) 
					{
						throw new InvalidDataException("Not flushing, but no lookahead");
					}
#endif
                        huffman.FlushBlock(window, blockStart, strstart - blockStart,
                            finish);
                        blockStart = strstart;
                        return false;
                    }

                    if (strstart >= 2 * DeflaterConstants.WSIZE - DeflaterConstants.MIN_LOOKAHEAD)
                    {
                        /* slide window, as FindLongestMatch needs this.
                         * This should only happen when flushing and the window
                         * is almost full.
                         */
                        SlideWindow();
                    }

                    int prevMatch = matchStart;
                    int prevLen = matchLen;
                    if (lookahead >= DeflaterConstants.MIN_MATCH)
                    {

                        int hashHead = InsertString();

                        if (strategy != HuffmanOnly &&
                            hashHead != 0 &&
                            strstart - hashHead <= DeflaterConstants.MAX_DIST &&
                            FindLongestMatch(hashHead))
                        {

                            // longestMatch sets matchStart and matchLen

                            // Discard match if too small and too far away
                            if (matchLen <= 5 && (strategy == Filtered || (matchLen == DeflaterConstants.MIN_MATCH && strstart - matchStart > TooFar)))
                            {
                                matchLen = DeflaterConstants.MIN_MATCH - 1;
                            }
                        }
                    }

                    // previous match was better
                    if ((prevLen >= DeflaterConstants.MIN_MATCH) && (matchLen <= prevLen))
                    {
#if DebugDeflation
					if (DeflaterConstants.DEBUGGING) 
					{
					   for (int i = 0 ; i < matchLen; i++) {
						  if (window[strstart-1+i] != window[prevMatch + i])
							 throw new InvalidDataException();
						}
					}
#endif
                        huffman.TallyDist(strstart - 1 - prevMatch, prevLen);
                        prevLen -= 2;
                        do
                        {
                            strstart++;
                            lookahead--;
                            if (lookahead >= DeflaterConstants.MIN_MATCH)
                            {
                                InsertString();
                            }
                        } while (--prevLen > 0);

                        strstart++;
                        lookahead--;
                        prevAvailable = false;
                        matchLen = DeflaterConstants.MIN_MATCH - 1;
                    }
                    else {
                        if (prevAvailable)
                        {
                            huffman.TallyLit(window[strstart - 1] & 0xff);
                        }
                        prevAvailable = true;
                        strstart++;
                        lookahead--;
                    }

                    if (huffman.IsFull())
                    {
                        int len = strstart - blockStart;
                        if (prevAvailable)
                        {
                            len--;
                        }
                        bool lastBlock = (finish && (lookahead == 0) && !prevAvailable);
                        huffman.FlushBlock(window, blockStart, len, lastBlock);
                        blockStart += len;
                        return !lastBlock;
                    }
                }
                return true;
            }

#region Instance Fields

            // Hash index of string to be inserted
            int ins_h;

            /// <summary>
            /// Hashtable, hashing three characters to an index for window, so
            /// that window[index]..window[index+2] have this hash code.  
            /// Note that the array should really be unsigned short, so you need
            /// to and the values with 0xffff.
            /// </summary>
            short[] head;

            /// <summary>
            /// <code>prev[index &amp; WMASK]</code> points to the previous index that has the
            /// same hash code as the string starting at index.  This way 
            /// entries with the same hash code are in a linked list.
            /// Note that the array should really be unsigned short, so you need
            /// to and the values with 0xffff.
            /// </summary>
            short[] prev;

            int matchStart;
            // Length of best match
            int matchLen;
            // Set if previous match exists
            bool prevAvailable;
            int blockStart;

            /// <summary>
            /// Points to the current character in the window.
            /// </summary>
            int strstart;

            /// <summary>
            /// lookahead is the number of characters starting at strstart in
            /// window that are valid.
            /// So window[strstart] until window[strstart+lookahead-1] are valid
            /// characters.
            /// </summary>
            int lookahead;

            /// <summary>
            /// This array contains the part of the uncompressed stream that 
            /// is of relevance.  The current character is indexed by strstart.
            /// </summary>
            byte[] window;

            int strategy;
            int max_chain, max_lazy, niceLength, goodLength;

            /// <summary>
            /// The current compression function.
            /// </summary>
            int compressionFunction;

            /// <summary>
            /// The input data for compression.
            /// </summary>
            byte[] inputBuf;

            /// <summary>
            /// The total bytes of input read.
            /// </summary>
            long totalIn;

            /// <summary>
            /// The offset into inputBuf, where input data starts.
            /// </summary>
            int inputOff;

            /// <summary>
            /// The end offset of the input data.
            /// </summary>
            int inputEnd;

            DeflaterPending pending;
            DeflaterHuffman huffman;

#endregion
        }
        /// <summary>
        /// This is the DeflaterHuffman class.
        /// 
        /// This class is <i>not</i> thread safe.  This is inherent in the API, due
        /// to the split of Deflate and SetInput.
        /// 
        /// author of the original java version : Jochen Hoenicke
        /// </summary>
        private class DeflaterHuffman
        {
            const int BUFSIZE = 1 << (DeflaterConstants.DEFAULT_MEM_LEVEL + 6);
            const int LITERAL_NUM = 286;

            // Number of distance codes
            const int DIST_NUM = 30;
            // Number of codes used to transfer bit lengths
            const int BITLEN_NUM = 19;

            // repeat previous bit length 3-6 times (2 bits of repeat count)
            const int REP_3_6 = 16;
            // repeat a zero length 3-10 times  (3 bits of repeat count)
            const int REP_3_10 = 17;
            // repeat a zero length 11-138 times  (7 bits of repeat count)
            const int REP_11_138 = 18;

            const int EOF_SYMBOL = 256;

            // The lengths of the bit length codes are sent in order of decreasing
            // probability, to avoid transmitting the lengths for unused bit length codes.
            static readonly int[] BL_ORDER = { 16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15 };

            static readonly byte[] bit4Reverse = {
            0,
            8,
            4,
            12,
            2,
            10,
            6,
            14,
            1,
            9,
            5,
            13,
            3,
            11,
            7,
            15
        };

            static short[] staticLCodes;
            static byte[] staticLLength;
            static short[] staticDCodes;
            static byte[] staticDLength;

            class Tree
            {
#region Instance Fields
                public short[] freqs;

                public byte[] length;

                public int minNumCodes;

                public int numCodes;

                short[] codes;
                readonly int[] bl_counts;
                readonly int maxLength;
                DeflaterHuffman dh;
#endregion

#region Constructors
                public Tree(DeflaterHuffman dh, int elems, int minCodes, int maxLength)
                {
                    this.dh = dh;
                    this.minNumCodes = minCodes;
                    this.maxLength = maxLength;
                    freqs = new short[elems];
                    bl_counts = new int[maxLength];
                }

#endregion

                /// <summary>
                /// Resets the internal state of the tree
                /// </summary>
                public void Reset()
                {
                    for (int i = 0; i < freqs.Length; i++)
                    {
                        freqs[i] = 0;
                    }
                    codes = null;
                    length = null;
                }

                public void WriteSymbol(int code)
                {
                    //				if (DeflaterConstants.DEBUGGING) {
                    //					freqs[code]--;
                    //					//  	  Console.Write("writeSymbol("+freqs.length+","+code+"): ");
                    //				}
                    dh.pending.WriteBits(codes[code] & 0xffff, length[code]);
                }

                /// <summary>
                /// Check that all frequencies are zero
                /// </summary>
                /// <exception cref="InvalidDataException">
                /// At least one frequency is non-zero
                /// </exception>
                public void CheckEmpty()
                {
                    bool empty = true;
                    for (int i = 0; i < freqs.Length; i++)
                    {
                        empty &= freqs[i] == 0;
                    }

                    if (!empty)
                    {
                        throw new InvalidDataException("!Empty");
                    }
                }

                /// <summary>
                /// Set static codes and length
                /// </summary>
                /// <param name="staticCodes">new codes</param>
                /// <param name="staticLengths">length for new codes</param>
                public void SetStaticCodes(short[] staticCodes, byte[] staticLengths)
                {
                    codes = staticCodes;
                    length = staticLengths;
                }

                /// <summary>
                /// Build dynamic codes and lengths
                /// </summary>
                public void BuildCodes()
                {
                    int numSymbols = freqs.Length;
                    int[] nextCode = new int[maxLength];
                    int code = 0;

                    codes = new short[freqs.Length];

                    //				if (DeflaterConstants.DEBUGGING) {
                    //					//Console.WriteLine("buildCodes: "+freqs.Length);
                    //				}

                    for (int bits = 0; bits < maxLength; bits++)
                    {
                        nextCode[bits] = code;
                        code += bl_counts[bits] << (15 - bits);

                        //					if (DeflaterConstants.DEBUGGING) {
                        //						//Console.WriteLine("bits: " + ( bits + 1) + " count: " + bl_counts[bits]
                        //						                  +" nextCode: "+code);
                        //					}
                    }

#if DebugDeflation
				if ( DeflaterConstants.DEBUGGING && (code != 65536) ) 
				{
					throw new InvalidDataException("Inconsistent bl_counts!");
				}
#endif
                    for (int i = 0; i < numCodes; i++)
                    {
                        int bits = length[i];
                        if (bits > 0)
                        {

                            //						if (DeflaterConstants.DEBUGGING) {
                            //								//Console.WriteLine("codes["+i+"] = rev(" + nextCode[bits-1]+"),
                            //								                  +bits);
                            //						}

                            codes[i] = BitReverse(nextCode[bits - 1]);
                            nextCode[bits - 1] += 1 << (16 - bits);
                        }
                    }
                }

                public void BuildTree()
                {
                    int numSymbols = freqs.Length;

                    /* heap is a priority queue, sorted by frequency, least frequent
                    * nodes first.  The heap is a binary tree, with the property, that
                    * the parent node is smaller than both child nodes.  This assures
                    * that the smallest node is the first parent.
                    *
                    * The binary tree is encoded in an array:  0 is root node and
                    * the nodes 2*n+1, 2*n+2 are the child nodes of node n.
                    */
                    int[] heap = new int[numSymbols];
                    int heapLen = 0;
                    int maxCode = 0;
                    for (int n = 0; n < numSymbols; n++)
                    {
                        int freq = freqs[n];
                        if (freq != 0)
                        {
                            // Insert n into heap
                            int pos = heapLen++;
                            int ppos;
                            while (pos > 0 && freqs[heap[ppos = (pos - 1) / 2]] > freq)
                            {
                                heap[pos] = heap[ppos];
                                pos = ppos;
                            }
                            heap[pos] = n;

                            maxCode = n;
                        }
                    }

                    /* We could encode a single literal with 0 bits but then we
                    * don't see the literals.  Therefore we force at least two
                    * literals to avoid this case.  We don't care about order in
                    * this case, both literals get a 1 bit code.
                    */
                    while (heapLen < 2)
                    {
                        int node = maxCode < 2 ? ++maxCode : 0;
                        heap[heapLen++] = node;
                    }

                    numCodes = Math.Max(maxCode + 1, minNumCodes);

                    int numLeafs = heapLen;
                    int[] childs = new int[4 * heapLen - 2];
                    int[] values = new int[2 * heapLen - 1];
                    int numNodes = numLeafs;
                    for (int i = 0; i < heapLen; i++)
                    {
                        int node = heap[i];
                        childs[2 * i] = node;
                        childs[2 * i + 1] = -1;
                        values[i] = freqs[node] << 8;
                        heap[i] = i;
                    }

                    /* Construct the Huffman tree by repeatedly combining the least two
                    * frequent nodes.
                    */
                    do
                    {
                        int first = heap[0];
                        int last = heap[--heapLen];

                        // Propagate the hole to the leafs of the heap
                        int ppos = 0;
                        int path = 1;

                        while (path < heapLen)
                        {
                            if (path + 1 < heapLen && values[heap[path]] > values[heap[path + 1]])
                            {
                                path++;
                            }

                            heap[ppos] = heap[path];
                            ppos = path;
                            path = path * 2 + 1;
                        }

                        /* Now propagate the last element down along path.  Normally
                        * it shouldn't go too deep.
                        */
                        int lastVal = values[last];
                        while ((path = ppos) > 0 && values[heap[ppos = (path - 1) / 2]] > lastVal)
                        {
                            heap[path] = heap[ppos];
                        }
                        heap[path] = last;


                        int second = heap[0];

                        // Create a new node father of first and second
                        last = numNodes++;
                        childs[2 * last] = first;
                        childs[2 * last + 1] = second;
                        int mindepth = Math.Min(values[first] & 0xff, values[second] & 0xff);
                        values[last] = lastVal = values[first] + values[second] - mindepth + 1;

                        // Again, propagate the hole to the leafs
                        ppos = 0;
                        path = 1;

                        while (path < heapLen)
                        {
                            if (path + 1 < heapLen && values[heap[path]] > values[heap[path + 1]])
                            {
                                path++;
                            }

                            heap[ppos] = heap[path];
                            ppos = path;
                            path = ppos * 2 + 1;
                        }

                        // Now propagate the new element down along path
                        while ((path = ppos) > 0 && values[heap[ppos = (path - 1) / 2]] > lastVal)
                        {
                            heap[path] = heap[ppos];
                        }
                        heap[path] = last;
                    } while (heapLen > 1);

                    if (heap[0] != childs.Length / 2 - 1)
                    {
                        throw new InvalidDataException("Heap invariant violated");
                    }

                    BuildLength(childs);
                }

                /// <summary>
                /// Get encoded length
                /// </summary>
                /// <returns>Encoded length, the sum of frequencies * lengths</returns>
                public int GetEncodedLength()
                {
                    int len = 0;
                    for (int i = 0; i < freqs.Length; i++)
                    {
                        len += freqs[i] * length[i];
                    }
                    return len;
                }

                /// <summary>
                /// Scan a literal or distance tree to determine the frequencies of the codes
                /// in the bit length tree.
                /// </summary>
                public void CalcBLFreq(Tree blTree)
                {
                    int max_count;               /* max repeat count */
                    int min_count;               /* min repeat count */
                    int count;                   /* repeat count of the current code */
                    int curlen = -1;             /* length of current code */

                    int i = 0;
                    while (i < numCodes)
                    {
                        count = 1;
                        int nextlen = length[i];
                        if (nextlen == 0)
                        {
                            max_count = 138;
                            min_count = 3;
                        }
                        else {
                            max_count = 6;
                            min_count = 3;
                            if (curlen != nextlen)
                            {
                                blTree.freqs[nextlen]++;
                                count = 0;
                            }
                        }
                        curlen = nextlen;
                        i++;

                        while (i < numCodes && curlen == length[i])
                        {
                            i++;
                            if (++count >= max_count)
                            {
                                break;
                            }
                        }

                        if (count < min_count)
                        {
                            blTree.freqs[curlen] += (short)count;
                        }
                        else if (curlen != 0)
                        {
                            blTree.freqs[REP_3_6]++;
                        }
                        else if (count <= 10)
                        {
                            blTree.freqs[REP_3_10]++;
                        }
                        else {
                            blTree.freqs[REP_11_138]++;
                        }
                    }
                }

                /// <summary>
                /// Write tree values
                /// </summary>
                /// <param name="blTree">Tree to write</param>
                public void WriteTree(Tree blTree)
                {
                    int max_count;               // max repeat count
                    int min_count;               // min repeat count
                    int count;                   // repeat count of the current code
                    int curlen = -1;             // length of current code

                    int i = 0;
                    while (i < numCodes)
                    {
                        count = 1;
                        int nextlen = length[i];
                        if (nextlen == 0)
                        {
                            max_count = 138;
                            min_count = 3;
                        }
                        else {
                            max_count = 6;
                            min_count = 3;
                            if (curlen != nextlen)
                            {
                                blTree.WriteSymbol(nextlen);
                                count = 0;
                            }
                        }
                        curlen = nextlen;
                        i++;

                        while (i < numCodes && curlen == length[i])
                        {
                            i++;
                            if (++count >= max_count)
                            {
                                break;
                            }
                        }

                        if (count < min_count)
                        {
                            while (count-- > 0)
                            {
                                blTree.WriteSymbol(curlen);
                            }
                        }
                        else if (curlen != 0)
                        {
                            blTree.WriteSymbol(REP_3_6);
                            dh.pending.WriteBits(count - 3, 2);
                        }
                        else if (count <= 10)
                        {
                            blTree.WriteSymbol(REP_3_10);
                            dh.pending.WriteBits(count - 3, 3);
                        }
                        else {
                            blTree.WriteSymbol(REP_11_138);
                            dh.pending.WriteBits(count - 11, 7);
                        }
                    }
                }

                void BuildLength(int[] childs)
                {
                    this.length = new byte[freqs.Length];
                    int numNodes = childs.Length / 2;
                    int numLeafs = (numNodes + 1) / 2;
                    int overflow = 0;

                    for (int i = 0; i < maxLength; i++)
                    {
                        bl_counts[i] = 0;
                    }

                    // First calculate optimal bit lengths
                    int[] lengths = new int[numNodes];
                    lengths[numNodes - 1] = 0;

                    for (int i = numNodes - 1; i >= 0; i--)
                    {
                        if (childs[2 * i + 1] != -1)
                        {
                            int bitLength = lengths[i] + 1;
                            if (bitLength > maxLength)
                            {
                                bitLength = maxLength;
                                overflow++;
                            }
                            lengths[childs[2 * i]] = lengths[childs[2 * i + 1]] = bitLength;
                        }
                        else {
                            // A leaf node
                            int bitLength = lengths[i];
                            bl_counts[bitLength - 1]++;
                            this.length[childs[2 * i]] = (byte)lengths[i];
                        }
                    }

                    //				if (DeflaterConstants.DEBUGGING) {
                    //					//Console.WriteLine("Tree "+freqs.Length+" lengths:");
                    //					for (int i=0; i < numLeafs; i++) {
                    //						//Console.WriteLine("Node "+childs[2*i]+" freq: "+freqs[childs[2*i]]
                    //						                  + " len: "+length[childs[2*i]]);
                    //					}
                    //				}

                    if (overflow == 0)
                    {
                        return;
                    }

                    int incrBitLen = maxLength - 1;
                    do
                    {
                        // Find the first bit length which could increase:
                        while (bl_counts[--incrBitLen] == 0)
                        {
                        }

                        // Move this node one down and remove a corresponding
                        // number of overflow nodes.
                        do
                        {
                            bl_counts[incrBitLen]--;
                            bl_counts[++incrBitLen]++;
                            overflow -= 1 << (maxLength - 1 - incrBitLen);
                        } while (overflow > 0 && incrBitLen < maxLength - 1);
                    } while (overflow > 0);

                    /* We may have overshot above.  Move some nodes from maxLength to
                    * maxLength-1 in that case.
                    */
                    bl_counts[maxLength - 1] += overflow;
                    bl_counts[maxLength - 2] -= overflow;

                    /* Now recompute all bit lengths, scanning in increasing
                    * frequency.  It is simpler to reconstruct all lengths instead of
                    * fixing only the wrong ones. This idea is taken from 'ar'
                    * written by Haruhiko Okumura.
                    *
                    * The nodes were inserted with decreasing frequency into the childs
                    * array.
                    */
                    int nodePtr = 2 * numLeafs;
                    for (int bits = maxLength; bits != 0; bits--)
                    {
                        int n = bl_counts[bits - 1];
                        while (n > 0)
                        {
                            int childPtr = 2 * childs[nodePtr++];
                            if (childs[childPtr + 1] == -1)
                            {
                                // We found another leaf
                                length[childs[childPtr]] = (byte)bits;
                                n--;
                            }
                        }
                    }
                    //				if (DeflaterConstants.DEBUGGING) {
                    //					//Console.WriteLine("*** After overflow elimination. ***");
                    //					for (int i=0; i < numLeafs; i++) {
                    //						//Console.WriteLine("Node "+childs[2*i]+" freq: "+freqs[childs[2*i]]
                    //						                  + " len: "+length[childs[2*i]]);
                    //					}
                    //				}
                }

            }

#region Instance Fields
            /// <summary>
            /// Pending buffer to use
            /// </summary>
            public DeflaterPending pending;

            Tree literalTree;
            Tree distTree;
            Tree blTree;

            // Buffer for distances
            short[] d_buf;
            byte[] l_buf;
            int last_lit;
            int extra_bits;
#endregion

            static DeflaterHuffman()
            {
                // See RFC 1951 3.2.6
                // Literal codes
                staticLCodes = new short[LITERAL_NUM];
                staticLLength = new byte[LITERAL_NUM];

                int i = 0;
                while (i < 144)
                {
                    staticLCodes[i] = BitReverse((0x030 + i) << 8);
                    staticLLength[i++] = 8;
                }

                while (i < 256)
                {
                    staticLCodes[i] = BitReverse((0x190 - 144 + i) << 7);
                    staticLLength[i++] = 9;
                }

                while (i < 280)
                {
                    staticLCodes[i] = BitReverse((0x000 - 256 + i) << 9);
                    staticLLength[i++] = 7;
                }

                while (i < LITERAL_NUM)
                {
                    staticLCodes[i] = BitReverse((0x0c0 - 280 + i) << 8);
                    staticLLength[i++] = 8;
                }

                // Distance codes
                staticDCodes = new short[DIST_NUM];
                staticDLength = new byte[DIST_NUM];
                for (i = 0; i < DIST_NUM; i++)
                {
                    staticDCodes[i] = BitReverse(i << 11);
                    staticDLength[i] = 5;
                }
            }

            /// <summary>
            /// Construct instance with pending buffer
            /// </summary>
            /// <param name="pending">Pending buffer to use</param>
            public DeflaterHuffman(DeflaterPending pending)
            {
                this.pending = pending;

                literalTree = new Tree(this, LITERAL_NUM, 257, 15);
                distTree = new Tree(this, DIST_NUM, 1, 15);
                blTree = new Tree(this, BITLEN_NUM, 4, 7);

                d_buf = new short[BUFSIZE];
                l_buf = new byte[BUFSIZE];
            }

            /// <summary>
            /// Reset internal state
            /// </summary>		
            public void Reset()
            {
                last_lit = 0;
                extra_bits = 0;
                literalTree.Reset();
                distTree.Reset();
                blTree.Reset();
            }

            /// <summary>
            /// Write all trees to pending buffer
            /// </summary>
            /// <param name="blTreeCodes">The number/rank of treecodes to send.</param>
            public void SendAllTrees(int blTreeCodes)
            {
                blTree.BuildCodes();
                literalTree.BuildCodes();
                distTree.BuildCodes();
                pending.WriteBits(literalTree.numCodes - 257, 5);
                pending.WriteBits(distTree.numCodes - 1, 5);
                pending.WriteBits(blTreeCodes - 4, 4);
                for (int rank = 0; rank < blTreeCodes; rank++)
                {
                    pending.WriteBits(blTree.length[BL_ORDER[rank]], 3);
                }
                literalTree.WriteTree(blTree);
                distTree.WriteTree(blTree);

#if DebugDeflation
			if (DeflaterConstants.DEBUGGING) {
				blTree.CheckEmpty();
			}
#endif
            }

            /// <summary>
            /// Compress current buffer writing data to pending buffer
            /// </summary>
            public void CompressBlock()
            {
                for (int i = 0; i < last_lit; i++)
                {
                    int litlen = l_buf[i] & 0xff;
                    int dist = d_buf[i];
                    if (dist-- != 0)
                    {
                        //					if (DeflaterConstants.DEBUGGING) {
                        //						Console.Write("["+(dist+1)+","+(litlen+3)+"]: ");
                        //					}

                        int lc = Lcode(litlen);
                        literalTree.WriteSymbol(lc);

                        int bits = (lc - 261) / 4;
                        if (bits > 0 && bits <= 5)
                        {
                            pending.WriteBits(litlen & ((1 << bits) - 1), bits);
                        }

                        int dc = Dcode(dist);
                        distTree.WriteSymbol(dc);

                        bits = dc / 2 - 1;
                        if (bits > 0)
                        {
                            pending.WriteBits(dist & ((1 << bits) - 1), bits);
                        }
                    }
                    else {
                        //					if (DeflaterConstants.DEBUGGING) {
                        //						if (litlen > 32 && litlen < 127) {
                        //							Console.Write("("+(char)litlen+"): ");
                        //						} else {
                        //							Console.Write("{"+litlen+"}: ");
                        //						}
                        //					}
                        literalTree.WriteSymbol(litlen);
                    }
                }

#if DebugDeflation
			if (DeflaterConstants.DEBUGGING) {
				Console.Write("EOF: ");
			}
#endif
                literalTree.WriteSymbol(EOF_SYMBOL);

#if DebugDeflation
			if (DeflaterConstants.DEBUGGING) {
				literalTree.CheckEmpty();
				distTree.CheckEmpty();
			}
#endif
            }

            /// <summary>
            /// Flush block to output with no compression
            /// </summary>
            /// <param name="stored">Data to write</param>
            /// <param name="storedOffset">Index of first byte to write</param>
            /// <param name="storedLength">Count of bytes to write</param>
            /// <param name="lastBlock">True if this is the last block</param>
            public void FlushStoredBlock(byte[] stored, int storedOffset, int storedLength, bool lastBlock)
            {
#if DebugDeflation
			//			if (DeflaterConstants.DEBUGGING) {
			//				//Console.WriteLine("Flushing stored block "+ storedLength);
			//			}
#endif
                pending.WriteBits((DeflaterConstants.STORED_BLOCK << 1) + (lastBlock ? 1 : 0), 3);
                pending.AlignToByte();
                pending.WriteShort(storedLength);
                pending.WriteShort(~storedLength);
                pending.WriteBlock(stored, storedOffset, storedLength);
                Reset();
            }

            /// <summary>
            /// Flush block to output with compression
            /// </summary>		
            /// <param name="stored">Data to flush</param>
            /// <param name="storedOffset">Index of first byte to flush</param>
            /// <param name="storedLength">Count of bytes to flush</param>
            /// <param name="lastBlock">True if this is the last block</param>
            public void FlushBlock(byte[] stored, int storedOffset, int storedLength, bool lastBlock)
            {
                literalTree.freqs[EOF_SYMBOL]++;

                // Build trees
                literalTree.BuildTree();
                distTree.BuildTree();

                // Calculate bitlen frequency
                literalTree.CalcBLFreq(blTree);
                distTree.CalcBLFreq(blTree);

                // Build bitlen tree
                blTree.BuildTree();

                int blTreeCodes = 4;
                for (int i = 18; i > blTreeCodes; i--)
                {
                    if (blTree.length[BL_ORDER[i]] > 0)
                    {
                        blTreeCodes = i + 1;
                    }
                }
                int opt_len = 14 + blTreeCodes * 3 + blTree.GetEncodedLength() +
                    literalTree.GetEncodedLength() + distTree.GetEncodedLength() +
                    extra_bits;

                int static_len = extra_bits;
                for (int i = 0; i < LITERAL_NUM; i++)
                {
                    static_len += literalTree.freqs[i] * staticLLength[i];
                }
                for (int i = 0; i < DIST_NUM; i++)
                {
                    static_len += distTree.freqs[i] * staticDLength[i];
                }
                if (opt_len >= static_len)
                {
                    // Force static trees
                    opt_len = static_len;
                }

                if (storedOffset >= 0 && storedLength + 4 < opt_len >> 3)
                {
                    // Store Block

                    //				if (DeflaterConstants.DEBUGGING) {
                    //					//Console.WriteLine("Storing, since " + storedLength + " < " + opt_len
                    //					                  + " <= " + static_len);
                    //				}
                    FlushStoredBlock(stored, storedOffset, storedLength, lastBlock);
                }
                else if (opt_len == static_len)
                {
                    // Encode with static tree
                    pending.WriteBits((DeflaterConstants.STATIC_TREES << 1) + (lastBlock ? 1 : 0), 3);
                    literalTree.SetStaticCodes(staticLCodes, staticLLength);
                    distTree.SetStaticCodes(staticDCodes, staticDLength);
                    CompressBlock();
                    Reset();
                }
                else {
                    // Encode with dynamic tree
                    pending.WriteBits((DeflaterConstants.DYN_TREES << 1) + (lastBlock ? 1 : 0), 3);
                    SendAllTrees(blTreeCodes);
                    CompressBlock();
                    Reset();
                }
            }

            /// <summary>
            /// Get value indicating if internal buffer is full
            /// </summary>
            /// <returns>true if buffer is full</returns>
            public bool IsFull()
            {
                return last_lit >= BUFSIZE;
            }

            /// <summary>
            /// Add literal to buffer
            /// </summary>
            /// <param name="literal">Literal value to add to buffer.</param>
            /// <returns>Value indicating internal buffer is full</returns>
            public bool TallyLit(int literal)
            {
                //			if (DeflaterConstants.DEBUGGING) {
                //				if (lit > 32 && lit < 127) {
                //					//Console.WriteLine("("+(char)lit+")");
                //				} else {
                //					//Console.WriteLine("{"+lit+"}");
                //				}
                //			}
                d_buf[last_lit] = 0;
                l_buf[last_lit++] = (byte)literal;
                literalTree.freqs[literal]++;
                return IsFull();
            }

            /// <summary>
            /// Add distance code and length to literal and distance trees
            /// </summary>
            /// <param name="distance">Distance code</param>
            /// <param name="length">Length</param>
            /// <returns>Value indicating if internal buffer is full</returns>
            public bool TallyDist(int distance, int length)
            {
                //			if (DeflaterConstants.DEBUGGING) {
                //				//Console.WriteLine("[" + distance + "," + length + "]");
                //			}

                d_buf[last_lit] = (short)distance;
                l_buf[last_lit++] = (byte)(length - 3);

                int lc = Lcode(length - 3);
                literalTree.freqs[lc]++;
                if (lc >= 265 && lc < 285)
                {
                    extra_bits += (lc - 261) / 4;
                }

                int dc = Dcode(distance - 1);
                distTree.freqs[dc]++;
                if (dc >= 4)
                {
                    extra_bits += dc / 2 - 1;
                }
                return IsFull();
            }


            /// <summary>
            /// Reverse the bits of a 16 bit value.
            /// </summary>
            /// <param name="toReverse">Value to reverse bits</param>
            /// <returns>Value with bits reversed</returns>
            public static short BitReverse(int toReverse)
            {
                return (short)(bit4Reverse[toReverse & 0xF] << 12 |
                                bit4Reverse[(toReverse >> 4) & 0xF] << 8 |
                                bit4Reverse[(toReverse >> 8) & 0xF] << 4 |
                                bit4Reverse[toReverse >> 12]);
            }

            static int Lcode(int length)
            {
                if (length == 255)
                {
                    return 285;
                }

                int code = 257;
                while (length >= 8)
                {
                    code += 4;
                    length >>= 1;
                }
                return code + length;
            }

            static int Dcode(int distance)
            {
                int code = 0;
                while (distance >= 4)
                {
                    code += 2;
                    distance >>= 1;
                }
                return code + distance;
            }
        }
        /// <summary>
        /// This class is general purpose class for writing data to a buffer.
        /// 
        /// It allows you to write bits as well as bytes
        /// Based on DeflaterPending.java
        /// 
        /// author of the original java version : Jochen Hoenicke
        /// </summary>
        private class DeflaterPending
        {
#region Instance Fields
            /// <summary>
            /// Internal work buffer
            /// </summary>
            readonly byte[] buffer;

            int start;
            int end;

            uint bits;
            int bitCount;
#endregion

#region Constructors
            /// <summary>
            /// construct instance using default buffer size of 
            /// </summary>
            public DeflaterPending() : this(DeflaterConstants.PENDING_BUF_SIZE)
            {
            }

            /// <summary>
            /// construct instance using specified buffer size
            /// </summary>
            /// <param name="bufferSize">
            /// size to use for internal buffer
            /// </param>
            public DeflaterPending(int bufferSize)
            {
                buffer = new byte[bufferSize];
            }

#endregion

            /// <summary>
            /// Clear internal state/buffers
            /// </summary>
            public void Reset()
            {
                start = end = bitCount = 0;
            }

            /// <summary>
            /// Write a byte to buffer
            /// </summary>
            /// <param name="value">
            /// The value to write
            /// </param>
            public void WriteByte(int value)
            {
#if DebugDeflation
			if (DeflaterConstants.DEBUGGING && (start != 0) )
			{
				throw new InvalidDataException("Debug check: start != 0");
			}
#endif
                buffer[end++] = unchecked((byte)value);
            }

            /// <summary>
            /// Write a short value to buffer LSB first
            /// </summary>
            /// <param name="value">
            /// The value to write.
            /// </param>
            public void WriteShort(int value)
            {
#if DebugDeflation
			if (DeflaterConstants.DEBUGGING && (start != 0) )
			{
				throw new InvalidDataException("Debug check: start != 0");
			}
#endif
                buffer[end++] = unchecked((byte)value);
                buffer[end++] = unchecked((byte)(value >> 8));
            }

            /// <summary>
            /// write an integer LSB first
            /// </summary>
            /// <param name="value">The value to write.</param>
            public void WriteInt(int value)
            {
#if DebugDeflation
			if (DeflaterConstants.DEBUGGING && (start != 0) )
			{
				throw new InvalidDataException("Debug check: start != 0");
			}
#endif
                buffer[end++] = unchecked((byte)value);
                buffer[end++] = unchecked((byte)(value >> 8));
                buffer[end++] = unchecked((byte)(value >> 16));
                buffer[end++] = unchecked((byte)(value >> 24));
            }

            /// <summary>
            /// Write a block of data to buffer
            /// </summary>
            /// <param name="block">data to write</param>
            /// <param name="offset">offset of first byte to write</param>
            /// <param name="length">number of bytes to write</param>
            public void WriteBlock(byte[] block, int offset, int length)
            {
#if DebugDeflation
			if (DeflaterConstants.DEBUGGING && (start != 0) ) 
			{
				throw new InvalidDataException("Debug check: start != 0");
			}
#endif
                System.Array.Copy(block, offset, buffer, end, length);
                end += length;
            }

            /// <summary>
            /// The number of bits written to the buffer
            /// </summary>
            public int BitCount
            {
                get
                {
                    return bitCount;
                }
            }

            /// <summary>
            /// Align internal buffer on a byte boundary
            /// </summary>
            public void AlignToByte()
            {
#if DebugDeflation
			if (DeflaterConstants.DEBUGGING && (start != 0) ) 
			{
				throw new InvalidDataException("Debug check: start != 0");
			}
#endif
                if (bitCount > 0)
                {
                    buffer[end++] = unchecked((byte)bits);
                    if (bitCount > 8)
                    {
                        buffer[end++] = unchecked((byte)(bits >> 8));
                    }
                }
                bits = 0;
                bitCount = 0;
            }

            /// <summary>
            /// Write bits to internal buffer
            /// </summary>
            /// <param name="b">source of bits</param>
            /// <param name="count">number of bits to write</param>
            public void WriteBits(int b, int count)
            {
#if DebugDeflation
			if (DeflaterConstants.DEBUGGING && (start != 0) ) 
			{
				throw new InvalidDataException("Debug check: start != 0");
			}

			//			if (DeflaterConstants.DEBUGGING) {
			//				//Console.WriteLine("writeBits("+b+","+count+")");
			//			}
#endif
                bits |= (uint)(b << bitCount);
                bitCount += count;
                if (bitCount >= 16)
                {
                    buffer[end++] = unchecked((byte)bits);
                    buffer[end++] = unchecked((byte)(bits >> 8));
                    bits >>= 16;
                    bitCount -= 16;
                }
            }

            /// <summary>
            /// Write a short value to internal buffer most significant byte first
            /// </summary>
            /// <param name="s">value to write</param>
            public void WriteShortMSB(int s)
            {
#if DebugDeflation
			if (DeflaterConstants.DEBUGGING && (start != 0) ) 
			{
				throw new InvalidDataException("Debug check: start != 0");
			}
#endif
                buffer[end++] = unchecked((byte)(s >> 8));
                buffer[end++] = unchecked((byte)s);
            }

            /// <summary>
            /// Indicates if buffer has been flushed
            /// </summary>
            public bool IsFlushed
            {
                get
                {
                    return end == 0;
                }
            }

            /// <summary>
            /// Flushes the pending buffer into the given output array.  If the
            /// output array is to small, only a partial flush is done.
            /// </summary>
            /// <param name="output">The output array.</param>
            /// <param name="offset">The offset into output array.</param>
            /// <param name="length">The maximum number of bytes to store.</param>
            /// <returns>The number of bytes flushed.</returns>
            public int Flush(byte[] output, int offset, int length)
            {
                if (bitCount >= 8)
                {
                    buffer[end++] = unchecked((byte)bits);
                    bits >>= 8;
                    bitCount -= 8;
                }

                if (length > end - start)
                {
                    length = end - start;
                    System.Array.Copy(buffer, start, output, offset, length);
                    start = 0;
                    end = 0;
                }
                else {
                    System.Array.Copy(buffer, start, output, offset, length);
                    start += length;
                }
                return length;
            }

            /// <summary>
            /// Convert internal buffer to byte array.
            /// Buffer is empty on completion
            /// </summary>
            /// <returns>
            /// The internal buffer contents converted to a byte array.
            /// </returns>
            public byte[] ToByteArray()
            {
                AlignToByte();

                byte[] result = new byte[end - start];
                System.Array.Copy(buffer, start, result, 0, result.Length);
                start = 0;
                end = 0;
                return result;
            }
        }
        private class InflateStream : Stream
        {
            public InflateStream(Stream stream, Inflater inflater)
            {
                _stream = stream;
                _inflater = inflater;
            }
            private Stream _stream;
            private Inflater _inflater;
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length
            {
                get
                {
                    if (_inflater == null)
                        throw new ObjectDisposedException(nameof(InflateStream));
                    if (_inflater.IsFinished)
                        return _inflater.TotalOut;
                    throw new InvalidOperationException();
                }
            }
            public override long Position
            {
                get
                {
                    if (_inflater == null)
                        throw new ObjectDisposedException(nameof(InflateStream));

                    return _inflater.TotalOut;
                }
                set
                {
                    throw new NotSupportedException();
                }
            }
            public override void Flush()
            {
               
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

                if (_inflater.IsFinished)
                    return 0;

                int remainingBytes = count;
                while (true)
                {
                    int bytesRead = _inflater.Inflate(buffer, offset, remainingBytes);
                    offset += bytesRead;
                    remainingBytes -= bytesRead;

                    if (remainingBytes == 0)
                        break;
                    if (_inflater.IsFinished)
                    {
                        _inflater.SetAvailable();
                        break;
                    }

                    if (_inflater.IsNeedingInput)
                    {
                        var rawData = _inflater.Buffer;
                        var rawLength = 0;
                        int toRead = rawData.Length;
                        var result = 0;
                        while (toRead > 0)
                        {
                            result = _stream.Read(rawData, rawLength, toRead);
                            if (result <= 0)
                            {
                                break;
                            }
                            rawLength += result;
                            toRead -= result;
                        }

                        _inflater.SetInput(rawData, 0, rawLength);
                    }
                    else if (bytesRead == 0)
                    {
                        throw new InvalidDataException("Dont know what to do");
                    }
                }
                return count - remainingBytes;
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

                if (_inflater.IsFinished)
                    return 0;

                int remainingBytes = count;
                while (true)
                {
                    int bytesRead = _inflater.Inflate(buffer, offset, remainingBytes);
                    offset += bytesRead;
                    remainingBytes -= bytesRead;

                    if (remainingBytes == 0)
                        break;
                    if (_inflater.IsFinished)
                    {
                        _inflater.SetAvailable();
                        break;
                    }

                    if (_inflater.IsNeedingInput)
                    {
                        var rawData = _inflater.Buffer;
                        var rawLength = 0;
                        int toRead = rawData.Length;
                        var result = 0;
                        while (toRead > 0)
                        {
                            result = await _stream.ReadAsync(rawData, rawLength, toRead);
                            if (result <= 0)
                            {
                                break;
                            }
                            rawLength += result;
                            toRead -= result;
                        }

                        _inflater.SetInput(rawData, 0, rawLength);
                    }
                    else if (bytesRead == 0)
                    {
                        throw new InvalidDataException("Dont know what to do");
                    }
                }
                return count - remainingBytes;
            }
            protected override void Dispose(bool disposing)
            {
                if (_inflater != null)
                {
                    _inflater.Reset();
                    _inflater = null;
                    _stream = null;
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
        /// <summary>
        /// Inflater is used to decompress data that has been compressed according
        /// to the "deflate" standard described in rfc1951.
        /// 
        /// By default Zlib (rfc1950) headers and footers are expected in the input.
        /// You can use constructor <code> public Inflater(bool noHeader)</code> passing true
        /// if there is no Zlib header information
        ///
        /// The usage is as following.  First you have to set some input with
        /// <code>SetInput()</code>, then Inflate() it.  If inflate doesn't
        /// inflate any bytes there may be three reasons:
        /// <ul>
        /// <li>IsNeedingInput() returns true because the input buffer is empty.
        /// You have to provide more input with <code>SetInput()</code>.
        /// NOTE: IsNeedingInput() also returns true when, the stream is finished.
        /// </li>
        /// <li>IsNeedingDictionary() returns true, you have to provide a preset
        ///    dictionary with <code>SetDictionary()</code>.</li>
        /// <li>IsFinished returns true, the inflater has finished.</li>
        /// </ul>
        /// Once the first output byte is produced, a dictionary will not be
        /// needed at a later stage.
        ///
        /// author of the original java version : John Leuner, Jochen Hoenicke
        /// </summary>
        public class Inflater
        {
            #region Constants/Readonly
            /// <summary>
            /// Copy lengths for literal codes 257..285
            /// </summary>
            static readonly int[] CPLENS = {
                                  3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 19, 23, 27, 31,
                                  35, 43, 51, 59, 67, 83, 99, 115, 131, 163, 195, 227, 258
                              };

            /// <summary>
            /// Extra bits for literal codes 257..285
            /// </summary>
            static readonly int[] CPLEXT = {
                                  0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2,
                                  3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0
                              };

            /// <summary>
            /// Copy offsets for distance codes 0..29
            /// </summary>
            static readonly int[] CPDIST = {
                                1, 2, 3, 4, 5, 7, 9, 13, 17, 25, 33, 49, 65, 97, 129, 193,
                                257, 385, 513, 769, 1025, 1537, 2049, 3073, 4097, 6145,
                                8193, 12289, 16385, 24577
                              };

            /// <summary>
            /// Extra bits for distance codes
            /// </summary>
            static readonly int[] CPDEXT = {
                                0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6,
                                7, 7, 8, 8, 9, 9, 10, 10, 11, 11,
                                12, 12, 13, 13
                              };

            /// <summary>
            /// These are the possible states for an inflater
            /// </summary>
            const int DECODE_INIT = 0;
            //const int DECODE_HEADER = 0;
            //const int DECODE_DICT = 1;
            const int DECODE_BLOCKS = 2;
            const int DECODE_STORED_LEN1 = 3;
            const int DECODE_STORED_LEN2 = 4;
            const int DECODE_STORED = 5;
            const int DECODE_DYN_HEADER = 6;
            const int DECODE_HUFFMAN = 7;
            const int DECODE_HUFFMAN_LENBITS = 8;
            const int DECODE_HUFFMAN_DIST = 9;
            const int DECODE_HUFFMAN_DISTBITS = 10;
            //const int DECODE_CHKSUM = 11;
            const int FINISHED = 12;
            #endregion

            #region Instance Fields
            /// <summary>
            /// This variable contains the current state.
            /// </summary>
            int mode;

            /// <summary>
            /// The number of bits needed to complete the current state.  This
            /// is valid, if mode is DECODE_DICT, DECODE_CHKSUM,
            /// DECODE_HUFFMAN_LENBITS or DECODE_HUFFMAN_DISTBITS.
            /// </summary>
            int neededBits;
            int repLength;
            int repDist;
            int uncomprLen;

            /// <summary>
            /// True, if the last block flag was set in the last block of the
            /// inflated stream.  This means that the stream ends after the
            /// current block.
            /// </summary>
            bool isLastBlock;

            /// <summary>
            /// The total number of inflated bytes.
            /// </summary>
            long totalOut;

            /// <summary>
            /// The total number of bytes set with setInput().  This is not the
            /// value returned by the TotalIn property, since this also includes the
            /// unprocessed input.
            /// </summary>
            long totalIn;

            /// <summary>
            /// This variable stores the noHeader flag that was given to the constructor.
            /// True means, that the inflated stream doesn't contain a Zlib header or 
            /// footer.
            /// </summary>
            readonly StreamManipulator input;
            OutputWindow outputWindow;
            InflaterDynHeader dynHeader;
            InflaterHuffmanTree litlenTree, distTree;
            byte[] buffer;
            int availableOffset, availableCount;
            byte[] dictionary;
            #endregion
            public byte[] Dictionary
            {
                get { return dictionary; }
                set
                {
                    if (mode != DECODE_INIT)
                        throw new InvalidOperationException();

                    dictionary = value;
                }
            }
            public byte[] Buffer
            {
                get { return buffer; }
                set
                {
                    if (mode != DECODE_INIT)
                        throw new InvalidOperationException();

                    buffer = value;
                }
            }
            public int AvailableOffset
            {
                get { return availableOffset; }
                set
                {
                    if (mode != DECODE_INIT)
                        throw new InvalidOperationException();

                    availableOffset = value;
                }
            }
            public int AvailableCount
            {
                get { return availableCount; }
                set
                {
                    if (mode != DECODE_INIT)
                        throw new InvalidOperationException();

                    availableCount = value;
                }
            }

            #region Constructors

            public Inflater() : this(8192) { }
            public Inflater(int bufferSize)
            {
                if (bufferSize < 2048)
                    throw new ArgumentOutOfRangeException(nameof(bufferSize));

                buffer = new byte[bufferSize];
                input = new StreamManipulator();
                outputWindow = new OutputWindow();
                mode = DECODE_INIT;//ADD
            }
            #endregion

            internal void Init()
            {
                if (mode != DECODE_INIT)
                    throw new InvalidOperationException();

                if (dictionary != null)//设置字典
                    outputWindow.CopyDict(dictionary, 0, dictionary.Length);
                if (availableCount > 0)
                {
                    SetInput(buffer, availableOffset, availableCount);
                    availableOffset = 0;
                    availableCount = 0;
                }
                    
                mode = DECODE_BLOCKS;
            }
            /// <summary>
            /// Resets the inflater so that a new stream can be decompressed.  All
            /// pending input and output will be discarded.
            /// </summary>
            internal void Reset()
            {
                mode = DECODE_INIT;
                totalIn = 0;
                totalOut = 0;
                input.Reset();
                outputWindow.Reset();
                dynHeader = null;
                litlenTree = null;
                distTree = null;
                isLastBlock = false;
                dictionary = null;
                availableOffset = 0;
                availableCount = 0;
            }

            /// <summary>
            /// Decodes the huffman encoded symbols in the input stream.
            /// </summary>
            /// <returns>
            /// false if more input is needed, true if output window is
            /// full or the current block ends.
            /// </returns>
            /// <exception cref="InvalidDataException">
            /// if deflated stream is invalid.
            /// </exception>
            private bool DecodeHuffman()
            {
                int free = outputWindow.GetFreeSpace();
                while (free >= 258)
                {
                    int symbol;
                    switch (mode)
                    {
                        case DECODE_HUFFMAN:
                            // This is the inner loop so it is optimized a bit
                            while (((symbol = litlenTree.GetSymbol(input)) & ~0xff) == 0)
                            {
                                outputWindow.Write(symbol);
                                if (--free < 258)
                                {
                                    return true;
                                }
                            }

                            if (symbol < 257)
                            {
                                if (symbol < 0)
                                {
                                    return false;
                                }
                                else {
                                    // symbol == 256: end of block
                                    distTree = null;
                                    litlenTree = null;
                                    mode = DECODE_BLOCKS;
                                    return true;
                                }
                            }

                            try
                            {
                                repLength = CPLENS[symbol - 257];
                                neededBits = CPLEXT[symbol - 257];
                            }
                            catch (Exception)
                            {
                                throw new InvalidDataException("Illegal rep length code");
                            }
                            goto case DECODE_HUFFMAN_LENBITS; // fall through

                        case DECODE_HUFFMAN_LENBITS:
                            if (neededBits > 0)
                            {
                                mode = DECODE_HUFFMAN_LENBITS;
                                int i = input.PeekBits(neededBits);
                                if (i < 0)
                                {
                                    return false;
                                }
                                input.DropBits(neededBits);
                                repLength += i;
                            }
                            mode = DECODE_HUFFMAN_DIST;
                            goto case DECODE_HUFFMAN_DIST; // fall through

                        case DECODE_HUFFMAN_DIST:
                            symbol = distTree.GetSymbol(input);
                            if (symbol < 0)
                            {
                                return false;
                            }

                            try
                            {
                                repDist = CPDIST[symbol];
                                neededBits = CPDEXT[symbol];
                            }
                            catch (Exception)
                            {
                                throw new InvalidDataException("Illegal rep dist code");
                            }

                            goto case DECODE_HUFFMAN_DISTBITS; // fall through

                        case DECODE_HUFFMAN_DISTBITS:
                            if (neededBits > 0)
                            {
                                mode = DECODE_HUFFMAN_DISTBITS;
                                int i = input.PeekBits(neededBits);
                                if (i < 0)
                                {
                                    return false;
                                }
                                input.DropBits(neededBits);
                                repDist += i;
                            }

                            outputWindow.Repeat(repLength, repDist);
                            free -= repLength;
                            mode = DECODE_HUFFMAN;
                            break;

                        default:
                            throw new InvalidDataException("Inflater unknown mode");
                    }
                }
                return true;
            }

            /// <summary>
            /// Decodes the deflated stream.
            /// </summary>
            /// <returns>
            /// false if more input is needed, or if finished.
            /// </returns>
            /// <exception cref="InvalidDataException">
            /// if deflated stream is invalid.
            /// </exception>
            private bool Decode()
            {
                switch (mode)
                {
                    //case DECODE_HEADER:
                    //	return DecodeHeader();

                    //case DECODE_DICT:
                    //	return DecodeDict();

                    //case DECODE_CHKSUM:
                    //	return DecodeChksum();
                    case DECODE_BLOCKS:
                        if (isLastBlock)
                        {
                            mode = FINISHED;
                            return false;
                        }

                        int type = input.PeekBits(3);
                        if (type < 0)
                        {
                            return false;
                        }
                        input.DropBits(3);

                        isLastBlock |= (type & 1) != 0;
                        switch (type >> 1)
                        {
                            case DeflaterConstants.STORED_BLOCK:
                                input.SkipToByteBoundary();
                                mode = DECODE_STORED_LEN1;
                                break;
                            case DeflaterConstants.STATIC_TREES:
                                litlenTree = InflaterHuffmanTree.defLitLenTree;
                                distTree = InflaterHuffmanTree.defDistTree;
                                mode = DECODE_HUFFMAN;
                                break;
                            case DeflaterConstants.DYN_TREES:
                                dynHeader = new InflaterDynHeader();
                                mode = DECODE_DYN_HEADER;
                                break;
                            default:
                                throw new InvalidDataException("Unknown block type " + type);
                        }
                        return true;

                    case DECODE_STORED_LEN1:
                        {
                            if ((uncomprLen = input.PeekBits(16)) < 0)
                            {
                                return false;
                            }
                            input.DropBits(16);
                            mode = DECODE_STORED_LEN2;
                        }
                        goto case DECODE_STORED_LEN2; // fall through

                    case DECODE_STORED_LEN2:
                        {
                            int nlen = input.PeekBits(16);
                            if (nlen < 0)
                            {
                                return false;
                            }
                            input.DropBits(16);
                            if (nlen != (uncomprLen ^ 0xffff))
                            {
                                throw new InvalidDataException("broken uncompressed block");
                            }
                            mode = DECODE_STORED;
                        }
                        goto case DECODE_STORED; // fall through

                    case DECODE_STORED:
                        {
                            int more = outputWindow.CopyStored(input, uncomprLen);
                            uncomprLen -= more;
                            if (uncomprLen == 0)
                            {
                                mode = DECODE_BLOCKS;
                                return true;
                            }
                            return !input.IsNeedingInput;
                        }

                    case DECODE_DYN_HEADER:
                        if (!dynHeader.Decode(input))
                        {
                            return false;
                        }

                        litlenTree = dynHeader.BuildLitLenTree();
                        distTree = dynHeader.BuildDistTree();
                        mode = DECODE_HUFFMAN;
                        goto case DECODE_HUFFMAN; // fall through

                    case DECODE_HUFFMAN:
                    case DECODE_HUFFMAN_LENBITS:
                    case DECODE_HUFFMAN_DIST:
                    case DECODE_HUFFMAN_DISTBITS:
                        return DecodeHuffman();

                    case FINISHED:
                        return false;

                    default:
                        throw new InvalidDataException("Inflater.Decode unknown mode");
                }
            }

            /// <summary>
            /// Sets the input.  This should only be called, if needsInput()
            /// returns true.
            /// </summary>
            /// <param name="buffer">
            /// The source of input data
            /// </param>
            /// <param name="index">
            /// The index into buffer where the input starts.
            /// </param>
            /// <param name="count">
            /// The number of bytes of input to use.
            /// </param>
            /// <exception cref="System.InvalidOperationException">
            /// No input is needed.
            /// </exception>
            /// <exception cref="System.ArgumentOutOfRangeException">
            /// The index and/or count are wrong.
            /// </exception>
            internal void SetInput(byte[] buffer, int index, int count)
            {
                input.SetInput(buffer, index, count);
                totalIn += (long)count;
            }

            /// <summary>
            /// Inflates the compressed stream to the output buffer.  If this
            /// returns 0, you should check, whether needsDictionary(),
            /// needsInput() or finished() returns true, to determine why no
            /// further output is produced.
            /// </summary>
            /// <param name="buffer">
            /// the output buffer.
            /// </param>
            /// <param name="offset">
            /// the offset in buffer where storing starts.
            /// </param>
            /// <param name="count">
            /// the maximum number of bytes to output.
            /// </param>
            /// <returns>
            /// the number of bytes written to the buffer, 0 if no further output can be produced.
            /// </returns>
            /// <exception cref="System.ArgumentOutOfRangeException">
            /// if count is less than 0.
            /// </exception>
            /// <exception cref="System.ArgumentOutOfRangeException">
            /// if the index and / or count are wrong.
            /// </exception>
            /// <exception cref="System.FormatException">
            /// if deflated stream is invalid.
            /// </exception>
            internal int Inflate(byte[] buffer, int offset, int count)
            {
                if (buffer == null)
                {
                    throw new ArgumentNullException(nameof(buffer));
                }

                if (count < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(count), "count cannot be negative");
                }

                if (offset < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(offset), "offset cannot be negative");
                }

                if (offset + count > buffer.Length)
                {
                    throw new ArgumentException("count exceeds buffer bounds");
                }


                // Special case: count may be zero
                if (count == 0)
                {
                    if (!IsFinished)
                    { // -jr- 08-Nov-2003 INFLATE_BUG fix..
                        Decode();
                    }
                    return 0;
                }

                int bytesCopied = 0;

                do
                {
                    int more = outputWindow.CopyOutput(buffer, offset, count);
                    if (more > 0)
                    {
                        offset += more;
                        bytesCopied += more;
                        totalOut += (long)more;
                        count -= more;
                        if (count == 0)
                        {
                            return bytesCopied;
                        }
                    }
                } while (Decode() || (outputWindow.GetAvailable() > 0));
                return bytesCopied;
            }

            /// <summary>
            /// Returns true, if the input buffer is empty.
            /// You should then call setInput(). 
            /// NOTE: This method also returns true when the stream is finished.
            /// </summary>
            internal bool IsNeedingInput
            {
                get
                {
                    return input.IsNeedingInput;
                }
            }

            /// <summary>
            /// Returns true, if the inflater has finished.  This means, that no
            /// input is needed and no output can be produced.
            /// </summary>
            internal bool IsFinished
            {
                get
                {
                    return mode == FINISHED && outputWindow.GetAvailable() == 0;
                }
            }

            internal void SetAvailable()
            {
                availableOffset = input.End - input.AvailableBytes;
                availableCount = input.AvailableBytes;
            }

            /// <summary>
            /// Gets the total number of output bytes returned by Inflate().
            /// </summary>
            /// <returns>
            /// the total number of output bytes.
            /// </returns>
            internal long TotalOut
            {
                get
                {
                    return totalOut;
                }
            }

            /// <summary>
            /// Gets the total number of processed compressed input bytes.
            /// </summary>
            /// <returns>
            /// The total number of bytes of processed input bytes.
            /// </returns>
            internal long TotalIn
            {
                get
                {
                    return totalIn - (long)RemainingInput;
                }
            }

            /// <summary>
            /// Gets the number of unprocessed input bytes.  Useful, if the end of the
            /// stream is reached and you want to further process the bytes after
            /// the deflate stream.
            /// </summary>
            /// <returns>
            /// The number of bytes of the input which have not been processed.
            /// </returns>
            internal int RemainingInput
            {
                // TODO: This should be a long?
                get
                {
                    return input.AvailableBytes;
                }
            }
        }
        /// <summary>
        /// DynHeader
        /// </summary>
        private class InflaterDynHeader
        {
            #region Constants
            const int LNUM = 0;
            const int DNUM = 1;
            const int BLNUM = 2;
            const int BLLENS = 3;
            const int LENS = 4;
            const int REPS = 5;

            static readonly int[] repMin = { 3, 3, 11 };
            static readonly int[] repBits = { 2, 3, 7 };

            static readonly int[] BL_ORDER =
            { 16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15 };
            #endregion

            public bool Decode(StreamManipulator input)
            {
                decode_loop:
                for (;;)
                {
                    switch (mode)
                    {
                        case LNUM:
                            lnum = input.PeekBits(5);
                            if (lnum < 0)
                            {
                                return false;
                            }
                            lnum += 257;
                            input.DropBits(5);
                            //  	    System.err.println("LNUM: "+lnum);
                            mode = DNUM;
                            goto case DNUM; // fall through
                        case DNUM:
                            dnum = input.PeekBits(5);
                            if (dnum < 0)
                            {
                                return false;
                            }
                            dnum++;
                            input.DropBits(5);
                            //  	    System.err.println("DNUM: "+dnum);
                            num = lnum + dnum;
                            litdistLens = new byte[num];
                            mode = BLNUM;
                            goto case BLNUM; // fall through
                        case BLNUM:
                            blnum = input.PeekBits(4);
                            if (blnum < 0)
                            {
                                return false;
                            }
                            blnum += 4;
                            input.DropBits(4);
                            blLens = new byte[19];
                            ptr = 0;
                            //  	    System.err.println("BLNUM: "+blnum);
                            mode = BLLENS;
                            goto case BLLENS; // fall through
                        case BLLENS:
                            while (ptr < blnum)
                            {
                                int len = input.PeekBits(3);
                                if (len < 0)
                                {
                                    return false;
                                }
                                input.DropBits(3);
                                //  		System.err.println("blLens["+BL_ORDER[ptr]+"]: "+len);
                                blLens[BL_ORDER[ptr]] = (byte)len;
                                ptr++;
                            }
                            blTree = new InflaterHuffmanTree(blLens);
                            blLens = null;
                            ptr = 0;
                            mode = LENS;
                            goto case LENS; // fall through
                        case LENS:
                            {
                                int symbol;
                                while (((symbol = blTree.GetSymbol(input)) & ~15) == 0)
                                {
                                    /* Normal case: symbol in [0..15] */

                                    //  		  System.err.println("litdistLens["+ptr+"]: "+symbol);
                                    litdistLens[ptr++] = lastLen = (byte)symbol;

                                    if (ptr == num)
                                    {
                                        /* Finished */
                                        return true;
                                    }
                                }

                                /* need more input ? */
                                if (symbol < 0)
                                {
                                    return false;
                                }

                                /* otherwise repeat code */
                                if (symbol >= 17)
                                {
                                    /* repeat zero */
                                    //  		  System.err.println("repeating zero");
                                    lastLen = 0;
                                }
                                else {
                                    if (ptr == 0)
                                    {
                                        throw new InvalidDataException();
                                    }
                                }
                                repSymbol = symbol - 16;
                            }
                            mode = REPS;
                            goto case REPS; // fall through
                        case REPS:
                            {
                                int bits = repBits[repSymbol];
                                int count = input.PeekBits(bits);
                                if (count < 0)
                                {
                                    return false;
                                }
                                input.DropBits(bits);
                                count += repMin[repSymbol];
                                //  	      System.err.println("litdistLens repeated: "+count);

                                if (ptr + count > num)
                                {
                                    throw new InvalidDataException();
                                }
                                while (count-- > 0)
                                {
                                    litdistLens[ptr++] = lastLen;
                                }

                                if (ptr == num)
                                {
                                    /* Finished */
                                    return true;
                                }
                            }
                            mode = LENS;
                            goto decode_loop;
                    }
                }
            }

            public InflaterHuffmanTree BuildLitLenTree()
            {
                byte[] litlenLens = new byte[lnum];
                Array.Copy(litdistLens, 0, litlenLens, 0, lnum);
                return new InflaterHuffmanTree(litlenLens);
            }

            public InflaterHuffmanTree BuildDistTree()
            {
                byte[] distLens = new byte[dnum];
                Array.Copy(litdistLens, lnum, distLens, 0, dnum);
                return new InflaterHuffmanTree(distLens);
            }

            #region Instance Fields
            byte[] blLens;
            byte[] litdistLens;

            InflaterHuffmanTree blTree;

            /// <summary>
            /// The current decode mode
            /// </summary>
            int mode;
            int lnum, dnum, blnum, num;
            int repSymbol;
            byte lastLen;
            int ptr;
            #endregion

        }
        /// <summary>
        /// Huffman tree used for inflation
        /// </summary>
        private class InflaterHuffmanTree
        {
            #region Constants
            const int MAX_BITLEN = 15;
            #endregion

            #region Instance Fields
            short[] tree;
            #endregion

            /// <summary>
            /// Literal length tree
            /// </summary>
            public static InflaterHuffmanTree defLitLenTree;

            /// <summary>
            /// Distance tree
            /// </summary>
            public static InflaterHuffmanTree defDistTree;

            static InflaterHuffmanTree()
            {
                try
                {
                    byte[] codeLengths = new byte[288];
                    int i = 0;
                    while (i < 144)
                    {
                        codeLengths[i++] = 8;
                    }
                    while (i < 256)
                    {
                        codeLengths[i++] = 9;
                    }
                    while (i < 280)
                    {
                        codeLengths[i++] = 7;
                    }
                    while (i < 288)
                    {
                        codeLengths[i++] = 8;
                    }
                    defLitLenTree = new InflaterHuffmanTree(codeLengths);

                    codeLengths = new byte[32];
                    i = 0;
                    while (i < 32)
                    {
                        codeLengths[i++] = 5;
                    }
                    defDistTree = new InflaterHuffmanTree(codeLengths);
                }
                catch (Exception)
                {
                    throw new InvalidDataException("InflaterHuffmanTree: static tree length illegal");
                }
            }

            #region Constructors
            /// <summary>
            /// Constructs a Huffman tree from the array of code lengths.
            /// </summary>
            /// <param name = "codeLengths">
            /// the array of code lengths
            /// </param>
            public InflaterHuffmanTree(byte[] codeLengths)
            {
                BuildTree(codeLengths);
            }
            #endregion

            void BuildTree(byte[] codeLengths)
            {
                int[] blCount = new int[MAX_BITLEN + 1];
                int[] nextCode = new int[MAX_BITLEN + 1];

                for (int i = 0; i < codeLengths.Length; i++)
                {
                    int bits = codeLengths[i];
                    if (bits > 0)
                    {
                        blCount[bits]++;
                    }
                }

                int code = 0;
                int treeSize = 512;
                for (int bits = 1; bits <= MAX_BITLEN; bits++)
                {
                    nextCode[bits] = code;
                    code += blCount[bits] << (16 - bits);
                    if (bits >= 10)
                    {
                        /* We need an extra table for bit lengths >= 10. */
                        int start = nextCode[bits] & 0x1ff80;
                        int end = code & 0x1ff80;
                        treeSize += (end - start) >> (16 - bits);
                    }
                }

                /* -jr comment this out! doesnt work for dynamic trees and pkzip 2.04g
                            if (code != 65536) 
                            {
                                throw new InvalidDataException("Code lengths don't add up properly.");
                            }
                */
                /* Now create and fill the extra tables from longest to shortest
                * bit len.  This way the sub trees will be aligned.
                */
                tree = new short[treeSize];
                int treePtr = 512;
                for (int bits = MAX_BITLEN; bits >= 10; bits--)
                {
                    int end = code & 0x1ff80;
                    code -= blCount[bits] << (16 - bits);
                    int start = code & 0x1ff80;
                    for (int i = start; i < end; i += 1 << 7)
                    {
                        tree[DeflaterHuffman.BitReverse(i)] = (short)((-treePtr << 4) | bits);
                        treePtr += 1 << (bits - 9);
                    }
                }

                for (int i = 0; i < codeLengths.Length; i++)
                {
                    int bits = codeLengths[i];
                    if (bits == 0)
                    {
                        continue;
                    }
                    code = nextCode[bits];
                    int revcode = DeflaterHuffman.BitReverse(code);
                    if (bits <= 9)
                    {
                        do
                        {
                            tree[revcode] = (short)((i << 4) | bits);
                            revcode += 1 << bits;
                        } while (revcode < 512);
                    }
                    else {
                        int subTree = tree[revcode & 511];
                        int treeLen = 1 << (subTree & 15);
                        subTree = -(subTree >> 4);
                        do
                        {
                            tree[subTree | (revcode >> 9)] = (short)((i << 4) | bits);
                            revcode += 1 << bits;
                        } while (revcode < treeLen);
                    }
                    nextCode[bits] = code + (1 << (16 - bits));
                }

            }

            /// <summary>
            /// Reads the next symbol from input.  The symbol is encoded using the
            /// huffman tree.
            /// </summary>
            /// <param name="input">
            /// input the input source.
            /// </param>
            /// <returns>
            /// the next symbol, or -1 if not enough input is available.
            /// </returns>
            public int GetSymbol(StreamManipulator input)
            {
                int lookahead, symbol;
                if ((lookahead = input.PeekBits(9)) >= 0)
                {
                    if ((symbol = tree[lookahead]) >= 0)
                    {
                        input.DropBits(symbol & 15);
                        return symbol >> 4;
                    }
                    int subtree = -(symbol >> 4);
                    int bitlen = symbol & 15;
                    if ((lookahead = input.PeekBits(bitlen)) >= 0)
                    {
                        symbol = tree[subtree | (lookahead >> 9)];
                        input.DropBits(symbol & 15);
                        return symbol >> 4;
                    }
                    else {
                        int bits = input.AvailableBits;
                        lookahead = input.PeekBits(bits);
                        symbol = tree[subtree | (lookahead >> 9)];
                        if ((symbol & 15) <= bits)
                        {
                            input.DropBits(symbol & 15);
                            return symbol >> 4;
                        }
                        else {
                            return -1;
                        }
                    }
                }
                else {
                    int bits = input.AvailableBits;
                    lookahead = input.PeekBits(bits);
                    symbol = tree[lookahead];
                    if (symbol >= 0 && (symbol & 15) <= bits)
                    {
                        input.DropBits(symbol & 15);
                        return symbol >> 4;
                    }
                    else {
                        return -1;
                    }
                }
            }
        }
        /// <summary>
        /// Contains the output from the Inflation process.
        /// We need to have a window so that we can refer backwards into the output stream
        /// to repeat stuff.<br/>
        /// Author of the original java version : John Leuner
        /// </summary>
        private class OutputWindow
        {
            #region Constants
            const int WindowSize = 1 << 15;
            const int WindowMask = WindowSize - 1;
            #endregion

            #region Instance Fields
            byte[] window = new byte[WindowSize]; //The window is 2^15 bytes
            int windowEnd;
            int windowFilled;
            #endregion

            /// <summary>
            /// Write a byte to this output window
            /// </summary>
            /// <param name="value">value to write</param>
            /// <exception cref="InvalidOperationException">
            /// if window is full
            /// </exception>
            public void Write(int value)
            {
                if (windowFilled++ == WindowSize)
                {
                    throw new InvalidOperationException("Window full");
                }
                window[windowEnd++] = (byte)value;
                windowEnd &= WindowMask;
            }


            private void SlowRepeat(int repStart, int length, int distance)
            {
                while (length-- > 0)
                {
                    window[windowEnd++] = window[repStart++];
                    windowEnd &= WindowMask;
                    repStart &= WindowMask;
                }
            }

            /// <summary>
            /// Append a byte pattern already in the window itself
            /// </summary>
            /// <param name="length">length of pattern to copy</param>
            /// <param name="distance">distance from end of window pattern occurs</param>
            /// <exception cref="InvalidOperationException">
            /// If the repeated data overflows the window
            /// </exception>
            public void Repeat(int length, int distance)
            {
                if ((windowFilled += length) > WindowSize)
                {
                    throw new InvalidOperationException("Window full");
                }

                int repStart = (windowEnd - distance) & WindowMask;
                int border = WindowSize - length;
                if ((repStart <= border) && (windowEnd < border))
                {
                    if (length <= distance)
                    {
                        System.Array.Copy(window, repStart, window, windowEnd, length);
                        windowEnd += length;
                    }
                    else {
                        // We have to copy manually, since the repeat pattern overlaps.
                        while (length-- > 0)
                        {
                            window[windowEnd++] = window[repStart++];
                        }
                    }
                }
                else {
                    SlowRepeat(repStart, length, distance);
                }
            }

            /// <summary>
            /// Copy from input manipulator to internal window
            /// </summary>
            /// <param name="input">source of data</param>
            /// <param name="length">length of data to copy</param>
            /// <returns>the number of bytes copied</returns>
            public int CopyStored(StreamManipulator input, int length)
            {
                length = Math.Min(Math.Min(length, WindowSize - windowFilled), input.AvailableBytes);
                int copied;

                int tailLen = WindowSize - windowEnd;
                if (length > tailLen)
                {
                    copied = input.CopyBytes(window, windowEnd, tailLen);
                    if (copied == tailLen)
                    {
                        copied += input.CopyBytes(window, 0, length - tailLen);
                    }
                }
                else {
                    copied = input.CopyBytes(window, windowEnd, length);
                }

                windowEnd = (windowEnd + copied) & WindowMask;
                windowFilled += copied;
                return copied;
            }

            /// <summary>
            /// Copy dictionary to window
            /// </summary>
            /// <param name="dictionary">source dictionary</param>
            /// <param name="offset">offset of start in source dictionary</param>
            /// <param name="length">length of dictionary</param>
            /// <exception cref="InvalidOperationException">
            /// If window isnt empty
            /// </exception>
            public void CopyDict(byte[] dictionary, int offset, int length)
            {
                if (dictionary == null)
                {
                    throw new ArgumentNullException(nameof(dictionary));
                }

                if (windowFilled > 0)
                {
                    throw new InvalidOperationException();
                }

                if (length > WindowSize)
                {
                    offset += length - WindowSize;
                    length = WindowSize;
                }
                System.Array.Copy(dictionary, offset, window, 0, length);
                windowEnd = length & WindowMask;
            }

            /// <summary>
            /// Get remaining unfilled space in window
            /// </summary>
            /// <returns>Number of bytes left in window</returns>
            public int GetFreeSpace()
            {
                return WindowSize - windowFilled;
            }

            /// <summary>
            /// Get bytes available for output in window
            /// </summary>
            /// <returns>Number of bytes filled</returns>
            public int GetAvailable()
            {
                return windowFilled;
            }

            /// <summary>
            /// Copy contents of window to output
            /// </summary>
            /// <param name="output">buffer to copy to</param>
            /// <param name="offset">offset to start at</param>
            /// <param name="len">number of bytes to count</param>
            /// <returns>The number of bytes copied</returns>
            /// <exception cref="InvalidOperationException">
            /// If a window underflow occurs
            /// </exception>
            public int CopyOutput(byte[] output, int offset, int len)
            {
                int copyEnd = windowEnd;
                if (len > windowFilled)
                {
                    len = windowFilled;
                }
                else {
                    copyEnd = (windowEnd - windowFilled + len) & WindowMask;
                }

                int copied = len;
                int tailLen = len - copyEnd;

                if (tailLen > 0)
                {
                    System.Array.Copy(window, WindowSize - tailLen, output, offset, tailLen);
                    offset += tailLen;
                    len = copyEnd;
                }
                System.Array.Copy(window, copyEnd - len, output, offset, len);
                windowFilled -= copied;
                if (windowFilled < 0)
                {
                    throw new InvalidOperationException();
                }
                return copied;
            }

            /// <summary>
            /// Reset by clearing window so <see cref="GetAvailable">GetAvailable</see> returns 0
            /// </summary>
            public void Reset()
            {
                windowFilled = windowEnd = 0;
            }
        }
        /// <summary>
        /// This class allows us to retrieve a specified number of bits from
        /// the input buffer, as well as copy big byte blocks.
        ///
        /// It uses an int buffer to store up to 31 bits for direct
        /// manipulation.  This guarantees that we can get at least 16 bits,
        /// but we only need at most 15, so this is all safe.
        ///
        /// There are some optimizations in this class, for example, you must
        /// never peek more than 8 bits more than needed, and you must first
        /// peek bits before you may drop them.  This is not a general purpose
        /// class but optimized for the behaviour of the Inflater.
        ///
        /// authors of the original java version : John Leuner, Jochen Hoenicke
        /// </summary>
        private class StreamManipulator
        {
            /// <summary>
            /// Get the next sequence of bits but don't increase input pointer.  bitCount must be
            /// less or equal 16 and if this call succeeds, you must drop
            /// at least n - 8 bits in the next call.
            /// </summary>
            /// <param name="bitCount">The number of bits to peek.</param>
            /// <returns>
            /// the value of the bits, or -1 if not enough bits available.  */
            /// </returns>
            public int PeekBits(int bitCount)
            {
                if (bitsInBuffer_ < bitCount)
                {
                    if (windowStart_ == windowEnd_)
                    {
                        return -1; // ok
                    }
                    buffer_ |= (uint)((window_[windowStart_++] & 0xff |
                                     (window_[windowStart_++] & 0xff) << 8) << bitsInBuffer_);
                    bitsInBuffer_ += 16;
                }
                return (int)(buffer_ & ((1 << bitCount) - 1));
            }

            /// <summary>
            /// Drops the next n bits from the input.  You should have called PeekBits
            /// with a bigger or equal n before, to make sure that enough bits are in
            /// the bit buffer.
            /// </summary>
            /// <param name="bitCount">The number of bits to drop.</param>
            public void DropBits(int bitCount)
            {
                buffer_ >>= bitCount;
                bitsInBuffer_ -= bitCount;
            }

            /// <summary>
            /// Gets the next n bits and increases input pointer.  This is equivalent
            /// to <see cref="PeekBits"/> followed by <see cref="DropBits"/>, except for correct error handling.
            /// </summary>
            /// <param name="bitCount">The number of bits to retrieve.</param>
            /// <returns>
            /// the value of the bits, or -1 if not enough bits available.
            /// </returns>
            public int GetBits(int bitCount)
            {
                int bits = PeekBits(bitCount);
                if (bits >= 0)
                {
                    DropBits(bitCount);
                }
                return bits;
            }

            /// <summary>
            /// Gets the number of bits available in the bit buffer.  This must be
            /// only called when a previous PeekBits() returned -1.
            /// </summary>
            /// <returns>
            /// the number of bits available.
            /// </returns>
            public int AvailableBits
            {
                get
                {
                    return bitsInBuffer_;
                }
            }

            /// <summary>
            /// Gets the number of bytes available.
            /// </summary>
            /// <returns>
            /// The number of bytes available.
            /// </returns>
            public int AvailableBytes
            {
                get
                {
                    return windowEnd_ - windowStart_ + (bitsInBuffer_ >> 3);
                }
            }

            /// <summary>
            /// Skips to the next byte boundary.
            /// </summary>
            public void SkipToByteBoundary()
            {
                buffer_ >>= (bitsInBuffer_ & 7);
                bitsInBuffer_ &= ~7;
            }

            /// <summary>
            /// Returns true when SetInput can be called
            /// </summary>
            public bool IsNeedingInput
            {
                get
                {
                    return windowStart_ == windowEnd_;
                }
            }

            //ADD bitsInBuffer_不知道怎么用
            public int End => windowEnd_;

            /// <summary>
            /// Copies bytes from input buffer to output buffer starting
            /// at output[offset].  You have to make sure, that the buffer is
            /// byte aligned.  If not enough bytes are available, copies fewer
            /// bytes.
            /// </summary>
            /// <param name="output">
            /// The buffer to copy bytes to.
            /// </param>
            /// <param name="offset">
            /// The offset in the buffer at which copying starts
            /// </param>
            /// <param name="length">
            /// The length to copy, 0 is allowed.
            /// </param>
            /// <returns>
            /// The number of bytes copied, 0 if no bytes were available.
            /// </returns>
            /// <exception cref="ArgumentOutOfRangeException">
            /// Length is less than zero
            /// </exception>
            /// <exception cref="InvalidOperationException">
            /// Bit buffer isnt byte aligned
            /// </exception>
            public int CopyBytes(byte[] output, int offset, int length)
            {
                if (length < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(length));
                }

                if ((bitsInBuffer_ & 7) != 0)
                {
                    // bits_in_buffer may only be 0 or a multiple of 8
                    throw new InvalidOperationException("Bit buffer is not byte aligned!");
                }

                int count = 0;
                while ((bitsInBuffer_ > 0) && (length > 0))
                {
                    output[offset++] = (byte)buffer_;
                    buffer_ >>= 8;
                    bitsInBuffer_ -= 8;
                    length--;
                    count++;
                }

                if (length == 0)
                {
                    return count;
                }

                int avail = windowEnd_ - windowStart_;
                if (length > avail)
                {
                    length = avail;
                }
                System.Array.Copy(window_, windowStart_, output, offset, length);
                windowStart_ += length;

                if (((windowStart_ - windowEnd_) & 1) != 0)
                {
                    // We always want an even number of bytes in input, see peekBits
                    buffer_ = (uint)(window_[windowStart_++] & 0xff);
                    bitsInBuffer_ = 8;
                }
                return count + length;
            }

            /// <summary>
            /// Resets state and empties internal buffers
            /// </summary>
            public void Reset()
            {
                buffer_ = 0;
                windowStart_ = windowEnd_ = bitsInBuffer_ = 0;
            }

            /// <summary>
            /// Add more input for consumption.
            /// Only call when IsNeedingInput returns true
            /// </summary>
            /// <param name="buffer">data to be input</param>
            /// <param name="offset">offset of first byte of input</param>
            /// <param name="count">number of bytes of input to add.</param>
            public void SetInput(byte[] buffer, int offset, int count)
            {
                if (buffer == null)
                {
                    throw new ArgumentNullException(nameof(buffer));
                }

                if (offset < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(offset), "Cannot be negative");
                }

                if (count < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(count), "Cannot be negative");
                }

                if (windowStart_ < windowEnd_)
                {
                    throw new InvalidOperationException("Old input was not completely processed");
                }

                int end = offset + count;

                // We want to throw an ArrayIndexOutOfBoundsException early.
                // Note the check also handles integer wrap around.
                if ((offset > end) || (end > buffer.Length))
                {
                    throw new ArgumentOutOfRangeException(nameof(count));
                }

                if ((count & 1) != 0)
                {
                    // We always want an even number of bytes in input, see PeekBits
                    buffer_ |= (uint)((buffer[offset++] & 0xff) << bitsInBuffer_);
                    bitsInBuffer_ += 8;
                }

                window_ = buffer;
                windowStart_ = offset;
                windowEnd_ = end;
            }

            #region Instance Fields
            private byte[] window_;
            private int windowStart_;
            private int windowEnd_;

            private uint buffer_;
            private int bitsInBuffer_;
            #endregion
        }
    }
}
