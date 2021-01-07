
namespace System.Extensions.Net
{
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Buffers;
    using System.Diagnostics;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Extensions.Http;
    using HttpVersion = Http.HttpVersion;
    public class Http2Service : IHttpService
    {
        #region const
        private const int _InitialWindowSize = 65535;
        private const int _HeaderTableSize = 4096, _MaxHeaderTableSize = 65536;
        private const int _MinFrameSize = 16384, _MaxFrameSize = 16777215;
        #endregion

        #region private
        private int _maxSettings;
        private int _maxConcurrentStreams;
        private int _initialWindowSize;
        private int _maxHeaderListSize;
        private byte[] _startupBytes;//Settings WindowUpdate
        private TaskTimeoutQueue<int> _keepAliveQueue;
        private TaskTimeoutQueue<int> _receiveQueue;
        private TaskTimeoutQueue _sendQueue;
        #endregion
        public Http2Service(
            int keepAliveTimeout = -60000,
            int receiveTimeout = -20000,
            int sendTimeout = -10000,
            int maxConcurrentStreams = 64,
            int initialWindowSize = 1024 * 1024,
            int maxHeaderListSize = 40 * 1024,
            int maxSettings = 1)
        {
            if (keepAliveTimeout != 0)
                _keepAliveQueue = new TaskTimeoutQueue<int>(keepAliveTimeout);
            if (receiveTimeout != 0)
                _receiveQueue = new TaskTimeoutQueue<int>(receiveTimeout);
            if (sendTimeout != 0)
                _sendQueue = new TaskTimeoutQueue(sendTimeout);

            _maxConcurrentStreams = maxConcurrentStreams > 0 ? maxConcurrentStreams : 64;
            _initialWindowSize = initialWindowSize > 0 ? initialWindowSize : 65535;
            _maxHeaderListSize = maxHeaderListSize > 0 ? maxHeaderListSize : 40 * 1024;
            _maxSettings = maxSettings > 0 ? maxSettings : 1;

            #region Settings WindowUpdate
            Span<byte> startupBytes = stackalloc byte[128];
            var offset = 3;
            startupBytes[offset++] = 0x4;
            startupBytes[offset++] = 0;
            startupBytes[offset++] = 0;
            startupBytes[offset++] = 0;
            startupBytes[offset++] = 0;
            startupBytes[offset++] = 0;
            //MaxConcurrentStreams
            startupBytes[offset++] = 0;
            startupBytes[offset++] = 0x3;
            startupBytes[offset++] = (byte)((_maxConcurrentStreams & 0xFF000000) >> 24);
            startupBytes[offset++] = (byte)((_maxConcurrentStreams & 0x00FF0000) >> 16);
            startupBytes[offset++] = (byte)((_maxConcurrentStreams & 0x0000FF00) >> 8);
            startupBytes[offset++] = (byte)(_maxConcurrentStreams & 0x000000FF);
            //InitialWindowSize
            if (_initialWindowSize != _InitialWindowSize)
            {
                startupBytes[offset++] = 0;
                startupBytes[offset++] = 0x4;
                startupBytes[offset++] = (byte)((_initialWindowSize & 0xFF000000) >> 24);
                startupBytes[offset++] = (byte)((_initialWindowSize & 0x00FF0000) >> 16);
                startupBytes[offset++] = (byte)((_initialWindowSize & 0x0000FF00) >> 8);
                startupBytes[offset++] = (byte)(_initialWindowSize & 0x000000FF);
            }
            //MaxHeaderListSize
            startupBytes[offset++] = 0;
            startupBytes[offset++] = 0x6;
            startupBytes[offset++] = (byte)((_maxHeaderListSize & 0xFF000000) >> 24);
            startupBytes[offset++] = (byte)((_maxHeaderListSize & 0x00FF0000) >> 16);
            startupBytes[offset++] = (byte)((_maxHeaderListSize & 0x0000FF00) >> 8);
            startupBytes[offset++] = (byte)(_maxHeaderListSize & 0x000000FF);

            startupBytes[0] = 0;
            startupBytes[1] = 0;
            startupBytes[2] = (byte)(offset - 9);

            var increment = _initialWindowSize - 65535;//if(increment<0) how?
            if (increment > 0)
            {
                startupBytes[offset++] = 0;
                startupBytes[offset++] = 0;
                startupBytes[offset++] = 4;
                startupBytes[offset++] = 0x8;
                startupBytes[offset++] = 0;
                startupBytes[offset++] = 0;
                startupBytes[offset++] = 0;
                startupBytes[offset++] = 0;
                startupBytes[offset++] = 0;
                startupBytes[offset++] = (byte)((increment & 0xFF000000) >> 24);
                startupBytes[offset++] = (byte)((increment & 0x00FF0000) >> 16);
                startupBytes[offset++] = (byte)((increment & 0x0000FF00) >> 8);
                startupBytes[offset++] = (byte)(increment & 0x000000FF);
            }
            _startupBytes = startupBytes.Slice(0, offset).ToArray();
            #endregion
        }
        public IHttpHandler Handler { get; set; }
        private class Connection : IConnection
        {
            #region const
            private static byte[] _Preface = Encoding.ASCII.GetBytes("PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n");
            private const int _MaxStreamId = int.MaxValue;//客户端最大流ID(这个Id不用)
            private static (State state, int prefix)[] _Ready;
            private enum State : byte { Ready, Indexed /*1*/, Indexing/*01*/, WithoutIndexing/*0000*/, NeverIndexed/*0001*/ , SizeUpdate/*001*/ }
            private static (uint code, int bitLength)[] _EncodingTable = new (uint code, int bitLength)[]
            {
                    /*    (  0)  |11111111|11000                      */     ( 0x1ff8, 13),
                    /*    (  1)  |11111111|11111111|1011000           */     ( 0x7fffd8, 23),
                    /*    (  2)  |11111111|11111111|11111110|0010     */    ( 0xfffffe2, 28),
                    /*    (  3)  |11111111|11111111|11111110|0011     */    ( 0xfffffe3, 28),
                    /*    (  4)  |11111111|11111111|11111110|0100     */    ( 0xfffffe4, 28),
                    /*    (  5)  |11111111|11111111|11111110|0101     */    ( 0xfffffe5, 28),
                    /*    (  6)  |11111111|11111111|11111110|0110     */    ( 0xfffffe6, 28),
                    /*    (  7)  |11111111|11111111|11111110|0111     */    ( 0xfffffe7, 28),
                    /*    (  8)  |11111111|11111111|11111110|1000     */    ( 0xfffffe8, 28),
                    /*    (  9)  |11111111|11111111|11101010          */     ( 0xffffea, 24),
                    /*    ( 10)  |11111111|11111111|11111111|111100   */   ( 0x3ffffffc, 30),
                    /*    ( 11)  |11111111|11111111|11111110|1001     */    ( 0xfffffe9, 28),
                    /*    ( 12)  |11111111|11111111|11111110|1010     */    ( 0xfffffea, 28),
                    /*    ( 13)  |11111111|11111111|11111111|111101   */   ( 0x3ffffffd, 30),
                    /*    ( 14)  |11111111|11111111|11111110|1011     */    ( 0xfffffeb, 28),
                    /*    ( 15)  |11111111|11111111|11111110|1100     */    ( 0xfffffec, 28),
                    /*    ( 16)  |11111111|11111111|11111110|1101     */    ( 0xfffffed, 28),
                    /*    ( 17)  |11111111|11111111|11111110|1110     */    ( 0xfffffee, 28),
                    /*    ( 18)  |11111111|11111111|11111110|1111     */    ( 0xfffffef, 28),
                    /*    ( 19)  |11111111|11111111|11111111|0000     */    ( 0xffffff0, 28),
                    /*    ( 20)  |11111111|11111111|11111111|0001     */    ( 0xffffff1, 28),
                    /*    ( 21)  |11111111|11111111|11111111|0010     */    ( 0xffffff2, 28),
                    /*    ( 22)  |11111111|11111111|11111111|111110   */   ( 0x3ffffffe, 30),
                    /*    ( 23)  |11111111|11111111|11111111|0011     */    ( 0xffffff3, 28),
                    /*    ( 24)  |11111111|11111111|11111111|0100     */    ( 0xffffff4, 28),
                    /*    ( 25)  |11111111|11111111|11111111|0101     */    ( 0xffffff5, 28),
                    /*    ( 26)  |11111111|11111111|11111111|0110     */    ( 0xffffff6, 28),
                    /*    ( 27)  |11111111|11111111|11111111|0111     */    ( 0xffffff7, 28),
                    /*    ( 28)  |11111111|11111111|11111111|1000     */    ( 0xffffff8, 28),
                    /*    ( 29)  |11111111|11111111|11111111|1001     */    ( 0xffffff9, 28),
                    /*    ( 30)  |11111111|11111111|11111111|1010     */    ( 0xffffffa, 28),
                    /*    ( 31)  |11111111|11111111|11111111|1011     */    ( 0xffffffb, 28),
                    /*' ' ( 32)  |010100                              */         ( 0x14, 6),
                    /*'!' ( 33)  |11111110|00                         */        ( 0x3f8, 10),
                    /*'"' ( 34)  |11111110|01                         */        ( 0x3f9, 10),
                    /*'#' ( 35)  |11111111|1010                       */        ( 0xffa, 12),
                    /*'$' ( 36)  |11111111|11001                      */      ( 0x1ff9, 13),
                    /*'%' ( 37)  |010101                              */         ( 0x15, 6),
                    /*'&' ( 38)  |11111000                            */         ( 0xf8, 8),
                    /*''' ( 39)  |11111111|010                        */       ( 0x7fa, 11),
                    /*'(' ( 40)  |11111110|10                         */       ( 0x3fa, 10),
                    /*')' ( 41)  |11111110|11                         */       ( 0x3fb, 10),
                    /*'*' ( 42)  |11111001                            */         ( 0xf9, 8),
                    /*'+' ( 43)  |11111111|011                        */       ( 0x7fb, 11),
                    /*',' ( 44)  |11111010                            */         ( 0xfa, 8),
                    /*'-' ( 45)  |010110                              */         ( 0x16, 6),
                    /*'.' ( 46)  |010111                              */         ( 0x17, 6),
                    /*'/' ( 47)  |011000                              */         ( 0x18, 6),
                    /*'0' ( 48)  |00000                               */          ( 0x0, 5),
                    /*'1' ( 49)  |00001                               */          ( 0x1, 5),
                    /*'2' ( 50)  |00010                               */          ( 0x2, 5),
                    /*'3' ( 51)  |011001                              */         ( 0x19, 6),
                    /*'4' ( 52)  |011010                              */         ( 0x1a, 6),
                    /*'5' ( 53)  |011011                              */         ( 0x1b, 6),
                    /*'6' ( 54)  |011100                              */         ( 0x1c, 6),
                    /*'7' ( 55)  |011101                              */         ( 0x1d, 6),
                    /*'8' ( 56)  |011110                              */         ( 0x1e, 6),
                    /*'9' ( 57)  |011111                              */         ( 0x1f, 6),
                    /*':' ( 58)  |1011100                             */         ( 0x5c, 7),
                    /*';' ( 59)  |11111011                            */         ( 0xfb, 8),
                    /*'<' ( 60)  |11111111|1111100                    */      ( 0x7ffc, 15),
                    /*'=' ( 61)  |100000                              */         ( 0x20, 6),
                    /*'>' ( 62)  |11111111|1011                       */       ( 0xffb, 12),
                    /*'?' ( 63)  |11111111|00                         */       ( 0x3fc, 10),
                    /*'@' ( 64)  |11111111|11010                      */      ( 0x1ffa, 13),
                    /*'A' ( 65)  |100001                              */         ( 0x21, 6),
                    /*'B' ( 66)  |1011101                             */         ( 0x5d, 7),
                    /*'C' ( 67)  |1011110                             */         ( 0x5e, 7),
                    /*'D' ( 68)  |1011111                             */         ( 0x5f, 7),
                    /*'E' ( 69)  |1100000                             */         ( 0x60, 7),
                    /*'F' ( 70)  |1100001                             */         ( 0x61, 7),
                    /*'G' ( 71)  |1100010                             */         ( 0x62, 7),
                    /*'H' ( 72)  |1100011                             */         ( 0x63, 7),
                    /*'I' ( 73)  |1100100                             */         ( 0x64, 7),
                    /*'J' ( 74)  |1100101                             */         ( 0x65, 7),
                    /*'K' ( 75)  |1100110                             */         ( 0x66, 7),
                    /*'L' ( 76)  |1100111                             */         ( 0x67, 7),
                    /*'M' ( 77)  |1101000                             */         ( 0x68, 7),
                    /*'N' ( 78)  |1101001                             */         ( 0x69, 7),
                    /*'O' ( 79)  |1101010                             */         ( 0x6a, 7),
                    /*'P' ( 80)  |1101011                             */         ( 0x6b, 7),
                    /*'Q' ( 81)  |1101100                             */         ( 0x6c, 7),
                    /*'R' ( 82)  |1101101                             */         ( 0x6d, 7),
                    /*'S' ( 83)  |1101110                             */         ( 0x6e, 7),
                    /*'T' ( 84)  |1101111                             */         ( 0x6f, 7),
                    /*'U' ( 85)  |1110000                             */         ( 0x70, 7),
                    /*'V' ( 86)  |1110001                             */         ( 0x71, 7),
                    /*'W' ( 87)  |1110010                             */         ( 0x72, 7),
                    /*'X' ( 88)  |11111100                            */         ( 0xfc, 8),
                    /*'Y' ( 89)  |1110011                             */         ( 0x73, 7),
                    /*'Z' ( 90)  |11111101                            */         ( 0xfd, 8),
                    /*'[' ( 91)  |11111111|11011                      */      ( 0x1ffb, 13),
                    /*'\' ( 92)  |11111111|11111110|000               */     ( 0x7fff0, 19),
                    /*']' ( 93)  |11111111|11100                      */      ( 0x1ffc, 13),
                    /*'^' ( 94)  |11111111|111100                     */      ( 0x3ffc, 14),
                    /*'_' ( 95)  |100010                              */         ( 0x22, 6),
                    /*'`' ( 96)  |11111111|1111101                    */      ( 0x7ffd, 15),
                    /*'a' ( 97)  |00011                               */          ( 0x3, 5),
                    /*'b' ( 98)  |100011                              */         ( 0x23, 6),
                    /*'c' ( 99)  |00100                               */          ( 0x4, 5),
                    /*'d' (100)  |100100                              */         ( 0x24, 6),
                    /*'e' (101)  |00101                               */          ( 0x5, 5),
                    /*'f' (102)  |100101                              */         ( 0x25, 6),
                    /*'g' (103)  |100110                              */         ( 0x26, 6),
                    /*'h' (104)  |100111                              */         ( 0x27, 6),
                    /*'i' (105)  |00110                               */          ( 0x6, 5),
                    /*'j' (106)  |1110100                             */         ( 0x74, 7),
                    /*'k' (107)  |1110101                             */         ( 0x75, 7),
                    /*'l' (108)  |101000                              */         ( 0x28, 6),
                    /*'m' (109)  |101001                              */         ( 0x29, 6),
                    /*'n' (110)  |101010                              */         ( 0x2a, 6),
                    /*'o' (111)  |00111                               */          ( 0x7, 5),
                    /*'p' (112)  |101011                              */         ( 0x2b, 6),
                    /*'q' (113)  |1110110                             */         ( 0x76, 7),
                    /*'r' (114)  |101100                              */         ( 0x2c, 6),
                    /*'s' (115)  |01000                               */          ( 0x8, 5),
                    /*'t' (116)  |01001                               */          ( 0x9, 5),
                    /*'u' (117)  |101101                              */         ( 0x2d, 6),
                    /*'v' (118)  |1110111                             */         ( 0x77, 7),
                    /*'w' (119)  |1111000                             */         ( 0x78, 7),
                    /*'x' (120)  |1111001                             */         ( 0x79, 7),
                    /*'y' (121)  |1111010                             */         ( 0x7a, 7),
                    /*'z' (122)  |1111011                             */         ( 0x7b, 7),
                    /*'(' (123)  |11111111|1111110                    */       ( 0x7ffe, 15),
                    /*'|' (124)  |11111111|100                        */        ( 0x7fc, 11),
                    /*')' (125)  |11111111|111101                     */       ( 0x3ffd, 14),
                    /*'~' (126)  |11111111|11101                      */       ( 0x1ffd, 13),
                    /*    (127)  |11111111|11111111|11111111|1100     */    ( 0xffffffc, 28),
                    /*    (128)  |11111111|11111110|0110              */      ( 0xfffe6, 20),
                    /*    (129)  |11111111|11111111|010010            */     ( 0x3fffd2, 22),
                    /*    (130)  |11111111|11111110|0111              */      ( 0xfffe7, 20),
                    /*    (131)  |11111111|11111110|1000              */      ( 0xfffe8, 20),
                    /*    (132)  |11111111|11111111|010011            */     ( 0x3fffd3, 22),
                    /*    (133)  |11111111|11111111|010100            */     ( 0x3fffd4, 22),
                    /*    (134)  |11111111|11111111|010101            */     ( 0x3fffd5, 22),
                    /*    (135)  |11111111|11111111|1011001           */     ( 0x7fffd9, 23),
                    /*    (136)  |11111111|11111111|010110            */     ( 0x3fffd6, 22),
                    /*    (137)  |11111111|11111111|1011010           */     ( 0x7fffda, 23),
                    /*    (138)  |11111111|11111111|1011011           */     ( 0x7fffdb, 23),
                    /*    (139)  |11111111|11111111|1011100           */     ( 0x7fffdc, 23),
                    /*    (140)  |11111111|11111111|1011101           */     ( 0x7fffdd, 23),
                    /*    (141)  |11111111|11111111|1011110           */     ( 0x7fffde, 23),
                    /*    (142)  |11111111|11111111|11101011          */     ( 0xffffeb, 24),
                    /*    (143)  |11111111|11111111|1011111           */     ( 0x7fffdf, 23),
                    /*    (144)  |11111111|11111111|11101100          */     ( 0xffffec, 24),
                    /*    (145)  |11111111|11111111|11101101          */     ( 0xffffed, 24),
                    /*    (146)  |11111111|11111111|010111            */     ( 0x3fffd7, 22),
                    /*    (147)  |11111111|11111111|1100000           */     ( 0x7fffe0, 23),
                    /*    (148)  |11111111|11111111|11101110          */     ( 0xffffee, 24),
                    /*    (149)  |11111111|11111111|1100001           */     ( 0x7fffe1, 23),
                    /*    (150)  |11111111|11111111|1100010           */     ( 0x7fffe2, 23),
                    /*    (151)  |11111111|11111111|1100011           */     ( 0x7fffe3, 23),
                    /*    (152)  |11111111|11111111|1100100           */     ( 0x7fffe4, 23),
                    /*    (153)  |11111111|11111110|11100             */     ( 0x1fffdc, 21),
                    /*    (154)  |11111111|11111111|011000            */     ( 0x3fffd8, 22),
                    /*    (155)  |11111111|11111111|1100101           */     ( 0x7fffe5, 23),
                    /*    (156)  |11111111|11111111|011001            */     ( 0x3fffd9, 22),
                    /*    (157)  |11111111|11111111|1100110           */     ( 0x7fffe6, 23),
                    /*    (158)  |11111111|11111111|1100111           */     ( 0x7fffe7, 23),
                    /*    (159)  |11111111|11111111|11101111          */     ( 0xffffef, 24),
                    /*    (160)  |11111111|11111111|011010            */     ( 0x3fffda, 22),
                    /*    (161)  |11111111|11111110|11101             */     ( 0x1fffdd, 21),
                    /*    (162)  |11111111|11111110|1001              */      ( 0xfffe9, 20),
                    /*    (163)  |11111111|11111111|011011            */     ( 0x3fffdb, 22),
                    /*    (164)  |11111111|11111111|011100            */     ( 0x3fffdc, 22),
                    /*    (165)  |11111111|11111111|1101000           */     ( 0x7fffe8, 23),
                    /*    (166)  |11111111|11111111|1101001           */     ( 0x7fffe9, 23),
                    /*    (167)  |11111111|11111110|11110             */     ( 0x1fffde, 21),
                    /*    (168)  |11111111|11111111|1101010           */     ( 0x7fffea, 23),
                    /*    (169)  |11111111|11111111|011101            */     ( 0x3fffdd, 22),
                    /*    (170)  |11111111|11111111|011110            */     ( 0x3fffde, 22),
                    /*    (171)  |11111111|11111111|11110000          */     ( 0xfffff0, 24),
                    /*    (172)  |11111111|11111110|11111             */     ( 0x1fffdf, 21),
                    /*    (173)  |11111111|11111111|011111            */     ( 0x3fffdf, 22),
                    /*    (174)  |11111111|11111111|1101011           */     ( 0x7fffeb, 23),
                    /*    (175)  |11111111|11111111|1101100           */     ( 0x7fffec, 23),
                    /*    (176)  |11111111|11111111|00000             */     ( 0x1fffe0, 21),
                    /*    (177)  |11111111|11111111|00001             */     ( 0x1fffe1, 21),
                    /*    (178)  |11111111|11111111|100000            */     ( 0x3fffe0, 22),
                    /*    (179)  |11111111|11111111|00010             */     ( 0x1fffe2, 21),
                    /*    (180)  |11111111|11111111|1101101           */     ( 0x7fffed, 23),
                    /*    (181)  |11111111|11111111|100001            */     ( 0x3fffe1, 22),
                    /*    (182)  |11111111|11111111|1101110           */     ( 0x7fffee, 23),
                    /*    (183)  |11111111|11111111|1101111           */     ( 0x7fffef, 23),
                    /*    (184)  |11111111|11111110|1010              */      ( 0xfffea, 20),
                    /*    (185)  |11111111|11111111|100010            */     ( 0x3fffe2, 22),
                    /*    (186)  |11111111|11111111|100011            */     ( 0x3fffe3, 22),
                    /*    (187)  |11111111|11111111|100100            */     ( 0x3fffe4, 22),
                    /*    (188)  |11111111|11111111|1110000           */     ( 0x7ffff0, 23),
                    /*    (189)  |11111111|11111111|100101            */     ( 0x3fffe5, 22),
                    /*    (190)  |11111111|11111111|100110            */     ( 0x3fffe6, 22),
                    /*    (191)  |11111111|11111111|1110001           */     ( 0x7ffff1, 23),
                    /*    (192)  |11111111|11111111|11111000|00       */    ( 0x3ffffe0, 26),
                    /*    (193)  |11111111|11111111|11111000|01       */    ( 0x3ffffe1, 26),
                    /*    (194)  |11111111|11111110|1011              */      ( 0xfffeb, 20),
                    /*    (195)  |11111111|11111110|001               */      ( 0x7fff1, 19),
                    /*    (196)  |11111111|11111111|100111            */     ( 0x3fffe7, 22),
                    /*    (197)  |11111111|11111111|1110010           */     ( 0x7ffff2, 23),
                    /*    (198)  |11111111|11111111|101000            */     ( 0x3fffe8, 22),
                    /*    (199)  |11111111|11111111|11110110|0        */    ( 0x1ffffec, 25),
                    /*    (200)  |11111111|11111111|11111000|10       */    ( 0x3ffffe2, 26),
                    /*    (201)  |11111111|11111111|11111000|11       */    ( 0x3ffffe3, 26),
                    /*    (202)  |11111111|11111111|11111001|00       */    ( 0x3ffffe4, 26),
                    /*    (203)  |11111111|11111111|11111011|110      */    ( 0x7ffffde, 27),
                    /*    (204)  |11111111|11111111|11111011|111      */    ( 0x7ffffdf, 27),
                    /*    (205)  |11111111|11111111|11111001|01       */    ( 0x3ffffe5, 26),
                    /*    (206)  |11111111|11111111|11110001          */     ( 0xfffff1, 24),
                    /*    (207)  |11111111|11111111|11110110|1        */    ( 0x1ffffed, 25),
                    /*    (208)  |11111111|11111110|010               */      ( 0x7fff2, 19),
                    /*    (209)  |11111111|11111111|00011             */     ( 0x1fffe3, 21),
                    /*    (210)  |11111111|11111111|11111001|10       */    ( 0x3ffffe6, 26),
                    /*    (211)  |11111111|11111111|11111100|000      */    ( 0x7ffffe0, 27),
                    /*    (212)  |11111111|11111111|11111100|001      */    ( 0x7ffffe1, 27),
                    /*    (213)  |11111111|11111111|11111001|11       */    ( 0x3ffffe7, 26),
                    /*    (214)  |11111111|11111111|11111100|010      */    ( 0x7ffffe2, 27),
                    /*    (215)  |11111111|11111111|11110010          */     ( 0xfffff2, 24),
                    /*    (216)  |11111111|11111111|00100             */     ( 0x1fffe4, 21),
                    /*    (217)  |11111111|11111111|00101             */     ( 0x1fffe5, 21),
                    /*    (218)  |11111111|11111111|11111010|00       */    ( 0x3ffffe8, 26),
                    /*    (219)  |11111111|11111111|11111010|01       */    ( 0x3ffffe9, 26),
                    /*    (220)  |11111111|11111111|11111111|1101     */    ( 0xffffffd, 28),
                    /*    (221)  |11111111|11111111|11111100|011      */    ( 0x7ffffe3, 27),
                    /*    (222)  |11111111|11111111|11111100|100      */    ( 0x7ffffe4, 27),
                    /*    (223)  |11111111|11111111|11111100|101      */    ( 0x7ffffe5, 27),
                    /*    (224)  |11111111|11111110|1100              */      ( 0xfffec, 20),
                    /*    (225)  |11111111|11111111|11110011          */     ( 0xfffff3, 24),
                    /*    (226)  |11111111|11111110|1101              */      ( 0xfffed, 20),
                    /*    (227)  |11111111|11111111|00110             */     ( 0x1fffe6, 21),
                    /*    (228)  |11111111|11111111|101001            */     ( 0x3fffe9, 22),
                    /*    (229)  |11111111|11111111|00111             */     ( 0x1fffe7, 21),
                    /*    (230)  |11111111|11111111|01000             */     ( 0x1fffe8, 21),
                    /*    (231)  |11111111|11111111|1110011           */     ( 0x7ffff3, 23),
                    /*    (232)  |11111111|11111111|101010            */     ( 0x3fffea, 22),
                    /*    (233)  |11111111|11111111|101011            */     ( 0x3fffeb, 22),
                    /*    (234)  |11111111|11111111|11110111|0        */    ( 0x1ffffee, 25),
                    /*    (235)  |11111111|11111111|11110111|1        */    ( 0x1ffffef, 25),
                    /*    (236)  |11111111|11111111|11110100          */     ( 0xfffff4, 24),
                    /*    (237)  |11111111|11111111|11110101          */     ( 0xfffff5, 24),
                    /*    (238)  |11111111|11111111|11111010|10       */    ( 0x3ffffea, 26),
                    /*    (239)  |11111111|11111111|1110100           */     ( 0x7ffff4, 23),
                    /*    (240)  |11111111|11111111|11111010|11       */    ( 0x3ffffeb, 26),
                    /*    (241)  |11111111|11111111|11111100|110      */    ( 0x7ffffe6, 27),
                    /*    (242)  |11111111|11111111|11111011|00       */    ( 0x3ffffec, 26),
                    /*    (243)  |11111111|11111111|11111011|01       */    ( 0x3ffffed, 26),
                    /*    (244)  |11111111|11111111|11111100|111      */    ( 0x7ffffe7, 27),
                    /*    (245)  |11111111|11111111|11111101|000      */    ( 0x7ffffe8, 27),
                    /*    (246)  |11111111|11111111|11111101|001      */    ( 0x7ffffe9, 27),
                    /*    (247)  |11111111|11111111|11111101|010      */    ( 0x7ffffea, 27),
                    /*    (248)  |11111111|11111111|11111101|011      */    ( 0x7ffffeb, 27),
                    /*    (249)  |11111111|11111111|11111111|1110     */    ( 0xffffffe, 28),
                    /*    (250)  |11111111|11111111|11111101|100      */    ( 0x7ffffec, 27),
                    /*    (251)  |11111111|11111111|11111101|101      */    ( 0x7ffffed, 27),
                    /*    (252)  |11111111|11111111|11111101|110      */    ( 0x7ffffee, 27),
                    /*    (253)  |11111111|11111111|11111101|111      */    ( 0x7ffffef, 27),
                    /*    (254)  |11111111|11111111|11111110|000      */    ( 0x7fffff0, 27),
                    /*    (255)  |11111111|11111111|11111011|10       */    ( 0x3ffffee, 26),
                    /*EOS (256)  |11111111|11111111|11111111|111111   */   ( 0x3fffffff, 30)
            };
            //https://github.com/aspnet/AspNetCore/blob/master/src/Servers/Kestrel/Core/src/Internal/Http2/HPack/Huffman.cs
            private static (int codeLength, int[] codes)[] _DecodingTable = new[]
            {
                    (5, new[] { 48, 49, 50, 97, 99, 101, 105, 111, 115, 116 }),
                    (6, new[] { 32, 37, 45, 46, 47, 51, 52, 53, 54, 55, 56, 57, 61, 65, 95, 98, 100, 102, 103, 104, 108, 109, 110, 112, 114, 117 }),
                    (7, new[] { 58, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 89, 106, 107, 113, 118, 119, 120, 121, 122 }),
                    (8, new[] { 38, 42, 44, 59, 88, 90 }),
                    (10, new[] { 33, 34, 40, 41, 63 }),
                    (11, new[] { 39, 43, 124 }),
                    (12, new[] { 35, 62 }),
                    (13, new[] { 0, 36, 64, 91, 93, 126 }),
                    (14, new[] { 94, 125 }),
                    (15, new[] { 60, 96, 123 }),
                    (19, new[] { 92, 195, 208 }),
                    (20, new[] { 128, 130, 131, 162, 184, 194, 224, 226 }),
                    (21, new[] { 153, 161, 167, 172, 176, 177, 179, 209, 216, 217, 227, 229, 230 }),
                    (22, new[] { 129, 132, 133, 134, 136, 146, 154, 156, 160, 163, 164, 169, 170, 173, 178, 181, 185, 186, 187, 189, 190, 196, 198, 228, 232, 233 }),
                    (23, new[] { 1, 135, 137, 138, 139, 140, 141, 143, 147, 149, 150, 151, 152, 155, 157, 158, 165, 166, 168, 174, 175, 180, 182, 183, 188, 191, 197, 231, 239 }),
                    (24, new[] { 9, 142, 144, 145, 148, 159, 171, 206, 215, 225, 236, 237 }),
                    (25, new[] { 199, 207, 234, 235 }),
                    (26, new[] { 192, 193, 200, 201, 202, 205, 210, 213, 218, 219, 238, 240, 242, 243, 255 }),
                    (27, new[] { 203, 204, 211, 212, 214, 221, 222, 223, 241, 244, 245, 246, 247, 248, 250, 251, 252, 253, 254 }),
                    (28, new[] { 2, 3, 4, 5, 6, 7, 8, 11, 12, 14, 15, 16, 17, 18, 19, 20, 21, 23, 24, 25, 26, 27, 28, 29, 30, 31, 127, 220, 249 }),
                    (30, new[] { 10, 13, 22, 256 })
                };
            #endregion
            static Connection()
            {
                _Ready = new (State, int)[256];
                for (int i = 0; i < 256; i++)
                {
                    if ((i & 0b1000_0000) == 0b1000_0000)
                        _Ready[i] = (State.Indexed, i & 0b0111_1111);
                    else if ((i & 0b1100_0000) == 0b0100_0000)
                        _Ready[i] = (State.Indexing, i & 0b0011_1111);
                    else if ((i & 0b1110_0000) == 0b0010_0000)
                        _Ready[i] = (State.SizeUpdate, i & 0b0001_1111);
                    else if ((i & 0b1111_0000) == 0b0001_0000)
                        _Ready[i] = (State.NeverIndexed, i & 0b0000_1111);
                    else if ((i & 0b1111_0000) == 0b0000_0000)
                        _Ready[i] = (State.WithoutIndexing, i & 0b0000_1111);
                    else
                        throw new InvalidOperationException("never");
                }
            }
            public class Http2Stream : IThreadPoolWorkItem, IHttp2Pusher, IDisposable
            {
                public Connection Connection;
                public int StreamId;
                public bool RemoteClosed;
                public bool LocalClosed;
                public HttpRequest Request;
                public Http2Content RequestBody;
                public HttpResponse Response;//TODO 204
                public IHttpContent ResponseBody;
                public async void Execute()
                {
                    Debug.Assert(Connection != null);
                    Debug.Assert(Request != null);
                    //TODO try catch
                    //Connection
                    Request.Connection(Connection);
                    //Pusher
                    if (Connection._enablePush == 1)
                        Request.Pusher(this);
                    try
                    {
                        Response = await Connection._service.Handler.HandleAsync(Request);
                    }
                    catch (Exception ex)
                    {
                        Response = new HttpResponse();
                        await FeaturesExtensions.UseException(Request, Response, ex);
                    }
                    if (Response == null)
                    {
                        var ex = new NullReferenceException(nameof(HttpResponse)).StatusCode(404);
                        Response = new HttpResponse();
                        await FeaturesExtensions.UseException(Request, Response, ex);//try
                    }
                    if (Response.StatusCode == 0)
                    {
                        Response.StatusCode = 200;
                    }
                    //TODO Version
                    try
                    {
                        await RequestBody.DrainAsync();
                    }
                    catch
                    {
                        Request.Dispose();
                        return;
                    }
                    lock (Connection) 
                    {
                        //TODO Remove DrainAsync
                        ResponseBody = Response.Content;
                        Connection.Enqueue(this);
                    }
                }
                public void Push(string path, HttpResponse response)
                {
                    if (path == null || response == null)
                        return;

                    if (response.StatusCode == 0)
                        response.StatusCode = 200;

                    lock (Connection)
                    {
                        if (Connection._closeWaiter != null)
                            return;
                        if (LocalClosed)
                            return;

                        Connection.Enqueue(() => Connection.WritePushPromise(this, path, response));
                    }
                }
                public void Dispose() 
                {
                    ResponseBody = null;
                    Response.Dispose();
                    Request?.Dispose();
                }
            }
            public class Http2Content : IHttpContent
            {
                public Http2Content(Http2Stream stream)
                {
                    _connection = stream.Connection;
                    _stream = stream;
                    _receiveQueue = new Queue<UnmanagedMemory<byte>>();

                    if (_stream.Request.Headers.TryGetValue("content-length", out var contentLength))
                    {
                        _length = long.Parse(contentLength);
                        if (_length < 0)
                            throw new InvalidDataException("content-length");
                    }
                }

                private Connection _connection;
                private Http2Stream _stream;
                private int _windowUpdate;
                private Exception _exception;
                private long _totalReceive;
                private long _position = 0;//if -1 end
                private long _length = -1;
                private int _available;
                private UnmanagedMemory<byte> _receive;//TODO ConnectionExtensions.GetBytes
                private Queue<UnmanagedMemory<byte>> _receiveQueue;
                private Memory<byte> _read;
                private TaskCompletionSource<int> _readWaiter;
                private void WindowUpdate(int increment)
                {
                    if (increment == 0)
                        return;

                    lock (_connection)
                    {
                        _windowUpdate += increment;
                        if (_windowUpdate > _connection._receiveWindowUpdate)
                        {
                            var windowUpdate = _windowUpdate;
                            _connection.Enqueue(() => {
                                _connection.WriteFrame(4, 0x8, 0, _stream.StreamId);
                                _connection.Write(windowUpdate);
                            });
                            _windowUpdate = 0;
                        }
                        _connection._windowUpdate += increment;
                        if (_connection._windowUpdate > _connection._receiveWindowUpdate)
                        {
                            var windowUpdate = _connection._windowUpdate;
                            _connection.Enqueue(() => {
                                _connection.WriteFrame(4, 0x8, 0, 0);
                                _connection.Write(windowUpdate);
                            });
                            _connection._windowUpdate = 0;
                            _connection._receiveWindow += windowUpdate;
                        }
                    }
                }
                public long Available => (_length == -1 && _position != -1) ? -1 : _length - _position;
                public long Length => _length;
                public long ComputeLength() => _length;
                public bool Rewind() => false;
                public int Read(Span<byte> buffer)
                {
                    if (buffer.IsEmpty)
                        return 0;

                    var result = 0;
                    for (; ; )
                    {
                        var readWaiter = default(TaskCompletionSource<int>);
                        lock (_stream)
                        {
                            if (_exception != null)
                                throw _exception;

                            if (_position == _length)
                                return 0;

                            if (_available > 0)
                            {
                                result = Math.Min(buffer.Length, _available);
                                _receive.GetSpan().Slice(_receive.Length - _available, result).CopyTo(buffer);
                                _available -= result;
                                _position += result;
                                if (_available == 0)
                                {
                                    _receive.Dispose();
                                    if (_receiveQueue.TryDequeue(out _receive))//_receive = null;
                                    {
                                        _available = _receive.Length;
                                    }
                                }
                                break;
                            }
                            if (_stream.RemoteClosed && _length == -1)
                            {
                                _position = -1;
                                return 0;
                            }
                            readWaiter = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                            _readWaiter = readWaiter;
                        }
                        readWaiter.Task.Wait();
                    }
                    Debug.Assert(result > 0);
                    WindowUpdate(result);
                    return result;
                }
                public int Read(byte[] buffer, int offset, int count)
                {
                    return ReadAsync(buffer.AsMemory(offset, count)).Result;
                }
                public async ValueTask<int> ReadAsync(Memory<byte> buffer)
                {
                    if (buffer.IsEmpty)
                        return 0;

                    var readWaiter = default(TaskCompletionSource<int>);
                    var result = 0;
                    lock (_stream)
                    {
                        if (_exception != null)
                            throw _exception;

                        if (_position == _length)
                            return 0;

                        if (_available > 0)
                        {
                            result = Math.Min(buffer.Length, _available);
                            _receive.GetSpan().Slice(_receive.Length - _available, result).CopyTo(buffer.Span);
                            _available -= result;
                            _position += result;
                            if (_available == 0)
                            {
                                _receive.Dispose();
                                if (_receiveQueue.TryDequeue(out _receive))
                                {
                                    _available = _receive.Length;
                                }
                            }
                            goto windowUpdate;
                        }
                        if (_stream.RemoteClosed && _length == -1)
                        {
                            _position = -1;
                            return 0;
                        }
                        readWaiter = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                        _readWaiter = readWaiter;
                        _read = buffer;
                    }
                    result = await readWaiter.Task;
                windowUpdate:
                    WindowUpdate(result);
                    return result;
                }
                public ValueTask<int> ReadAsync(byte[] buffer, int offset, int count)
                {
                    return ReadAsync(buffer.AsMemory(offset, count));
                }
                public void OnData(ReadOnlySpan<byte> payload)
                {
                    if (payload.Length == 0)
                        return;

                    lock (_connection)
                    {
                        if (_stream.RemoteClosed) 
                        {
                            _connection._windowUpdate += payload.Length;
                            if (_connection._windowUpdate > _connection._receiveWindowUpdate)
                            {
                                var windowUpdate = _connection._windowUpdate;
                                _connection.Enqueue(() => {
                                    _connection.WriteFrame(4, 0x8, 0, 0);
                                    _connection.Write(windowUpdate);
                                });
                                _connection._windowUpdate = 0;
                                _connection._receiveWindow += windowUpdate;
                            }
                            return;
                        }
                        Monitor.Enter(_stream);
                    }
                    try
                    {
                        Debug.Assert(Monitor.IsEntered(_stream));
                        _totalReceive += payload.Length;
                        if (_length != -1 && _totalReceive > _length)
                            throw new InvalidDataException("totalReceive Too Large");//连接异常

                        if (_readWaiter != null)//_available == 0
                        {
                            Debug.Assert(_available == 0);
                            Debug.Assert(_receiveQueue.Count == 0);
                            if (_read.IsEmpty)
                            {
                                _readWaiter.SetResult(0);
                                _readWaiter = null;
                            }
                            else
                            {
                                var toRead = Math.Min(payload.Length, _read.Length);
                                payload.Slice(0, toRead).CopyTo(_read.Span);
                                payload = payload.Slice(toRead);
                                _position += toRead;
                                _read = Memory<byte>.Empty;
                                _readWaiter.SetResult(toRead);
                                _readWaiter = null;
                            }
                            if (payload.Length > 0)
                            {
                                _receive = new UnmanagedMemory<byte>(payload.Length);
                                payload.CopyTo(_receive.GetSpan());
                                _available = payload.Length;
                            }
                        }
                        else if (_available > 0)
                        {
                            var reveive = new UnmanagedMemory<byte>(payload.Length);
                            payload.CopyTo(reveive.GetSpan());
                            _receiveQueue.Enqueue(reveive);
                        }
                        else
                        {
                            _receive = new UnmanagedMemory<byte>(payload.Length);
                            payload.CopyTo(_receive.GetSpan());
                            _available = payload.Length;
                        }
                    }
                    finally
                    {
                        Monitor.Exit(_stream);
                    }
                }
                public void Close(Exception exception)
                {
                    Debug.Assert(Monitor.IsEntered(_connection));
                    Debug.Assert(_stream.RemoteClosed);
                    //in lock(_connection)
                    lock (_stream)
                    {
                        if (exception == null)
                        {
                            if (_length != -1 && _length != _totalReceive)
                            {
                                Debug.Assert(_length > _totalReceive);
                                exception = new InvalidDataException("content-length!=totalReceive");//直接抛出异常?
                            }
                            else
                            {
                                if (_readWaiter != null)
                                {
                                    _readWaiter.SetResult(0);
                                    _readWaiter = null;
                                    _read = Memory<byte>.Empty;
                                }
                                return;
                            }
                        }
                        Debug.Assert(exception != null);
                        _exception = exception;
                        if (_readWaiter != null)
                        {
                            _readWaiter.SetException(exception);
                            _readWaiter = null;
                            _read = Memory<byte>.Empty;
                        }
                        else if (_receive != null)
                        {
                            _receive.Dispose();
                            _receive = null;
                            while (_receiveQueue.TryDequeue(out var receive))
                            {
                                receive.Dispose();
                            }
                        }

                        var increment = (int)(_totalReceive - _position);
                        if (increment > 0)
                        {
                            _connection._windowUpdate += increment;
                            if (_connection._windowUpdate >= _connection._receiveWindowUpdate)
                            {
                                var windowUpdate = _connection._windowUpdate;
                                _connection.Enqueue(() => {
                                    _connection.WriteFrame(4, 0x8, 0, 0);
                                    _connection.Write(windowUpdate);
                                });
                                _connection._windowUpdate = 0;
                                _connection._receiveWindow += windowUpdate;
                            }
                        }
                    }
                }
            }
            public Connection(Http2Service service, IConnection connection)
            {
                _service = service;
                _connection = connection;

                _streams = new Dictionary<int, Http2Stream>();
                _sendQueue = new Queue<(Action, Http2Stream)>();
                _dataQueue = new Queue<Http2Stream>();

                _enablePush = 1;
                _maxSendStreams = int.MaxValue;
                _maxReceiveStreams = service._maxConcurrentStreams;
                _nextStreamId = 2;
                _initialSendWindow = _InitialWindowSize;
                _sendWindow = _initialSendWindow;
                _initialReceiveWindow = _service._initialWindowSize;
                _receiveWindowUpdate = _initialReceiveWindow / 2;
                _receiveWindow = _initialReceiveWindow;

                _encoderTable = new EncoderTable(_HeaderTableSize);
                _decoderTable = new DecoderTable(_HeaderTableSize);

                _sendQueue.Enqueue((() => Write(_service._startupBytes), null));
            }

            private Http2Service _service;
            private IConnection _connection;

            //Settings
            private int _settings;
            private int _enablePush;//0 1
            private int _maxSendStreams;//TODO use
            private int _maxReceiveStreams;
            private int _initialSendWindow;
            private int _initialReceiveWindow;
            private int _receiveWindowUpdate;

            private int _sendWindow;
            private int _receiveWindow;
            private int _windowUpdate;

            private int _lastStreamId;
            private int _nextStreamId;
            private int _activeStreams;
            private int _receiveStreams;
            private Dictionary<int, Http2Stream> _streams;
            private Queue<(Action, Http2Stream)> _sendQueue;//TODO PriorityQueue
            private Queue<Http2Stream> _dataQueue;

            private EncoderTable _encoderTable;
            private DecoderTable _decoderTable;

            private Task _readTask;
            private Task _writeTask;
            private TaskCompletionSource<object> _writeWaiter;
            private TaskCompletionSource<object> _closeWaiter;
            private TaskCompletionSource<object> _handleWaiter;

            //frame
            private int _frameLength;
            private byte _frameType;
            private byte _frameFlags;
            private int _frameStreamId;

            //read
            private int _start;
            private int _end;
            private int _position;
            private unsafe byte* _pRead;
            private Memory<byte> _read;
            private MemoryHandle _readHandle;
            private IDisposable _readDisposable;
            private Queue<(Memory<byte>, IDisposable)> _readQueue;
            private int _headers;
            private int _headersSize;
            private Queue<UnmanagedMemory<byte>> _headersQueue;

            //write
            private int _available;
            private unsafe byte* _pWrite;
            private Memory<byte> _write;
            private MemoryHandle _writeHandle;
            private IDisposable _writeDisposable;
            private Queue<(Memory<byte>, IDisposable)> _writeQueue;
            private void Close(int lastStreamId, Exception exception)
            {
                lock (this)
                {
                    foreach ((var streamId, var stream) in _streams)
                    {
                        if (streamId > lastStreamId)
                        {
                            if (!stream.RemoteClosed)
                            {
                                stream.RemoteClosed = true;
                                stream.RequestBody?.Close(exception);
                            }
                            Debug.Assert(!stream.LocalClosed);
                            stream.LocalClosed = true;
                            stream.Dispose();
                            _streams.Remove(streamId);//Not Use Dequeue()
                            _activeStreams -= 1;
                            if (stream.Request != null)
                                _receiveStreams -= 1;
                        }
                    }

                    if (_activeStreams == 0)
                    {
                        Debug.Assert(_streams.Count == 0);
                        Debug.Assert(_receiveStreams == 0);
                        _activeStreams = -1;
                        if (_writeWaiter != null)
                        {
                            _writeWaiter.SetException(new IOException("Close"));
                            _writeWaiter = null;
                        }
                        if (_closeWaiter != null)
                        {
                            _closeWaiter.SetResult(null);
                            return;
                        }
                        _closeWaiter = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                        _closeWaiter.SetResult(null);
                    }
                    else
                    {
                        if (_closeWaiter != null)
                            return;
                        _closeWaiter = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                    }
                }
                ThreadPool.QueueUserWorkItem(async (_) => {
                    await _closeWaiter.Task;
                    Debug.Assert(_activeStreams == -1);
                    try { _connection.Close(); } catch { }
                    try { await _writeTask; } catch { }
                    try { await _readTask; } catch { }
                    //_handleWaiter.SetResult(null);
                    if (exception == null)
                        _handleWaiter.SetResult(null);
                    else
                        _handleWaiter.SetException(exception);
                });
            }
            private void Enqueue(Action writer)
            {
                //in lock(this)
                Debug.Assert(Monitor.IsEntered(this));
                Debug.Assert(writer != null);
                _sendQueue.Enqueue((writer, null));
                if (_writeWaiter != null)
                {
                    _writeWaiter.SetResult(null);
                    _writeWaiter = null;
                }
            }
            private void Enqueue(Http2Stream stream)
            {
                Debug.Assert(stream.Response != null);
                //in lock(this)
                Debug.Assert(Monitor.IsEntered(this));
                Debug.Assert(stream.Response != null);
                _sendQueue.Enqueue((null, stream));
                if (_writeWaiter != null)
                {
                    _writeWaiter.SetResult(null);
                    _writeWaiter = null;
                }
            }
            private void Dequeue(Http2Stream stream)
            {
                Debug.Assert(Monitor.IsEntered(this));
                Debug.Assert(stream.RemoteClosed);
                Debug.Assert(stream.LocalClosed);
                Debug.Assert(_activeStreams > 0);
                _streams.Remove(stream.StreamId);
                _activeStreams -= 1;
                if (stream.Request != null)
                    _receiveStreams -= 1;
                if (_closeWaiter != null)
                {
                    if (_activeStreams == 0)
                    {
                        _streams.Clear();
                        _activeStreams = -1;
                        if (_writeWaiter != null)
                        {
                            _writeWaiter.SetException(new IOException("Close"));
                            _writeWaiter = null;
                        }
                        _closeWaiter.SetResult(null);
                    }
                }
            }
            private void TryWrite()
            {
                //[MethodImpl(MethodImplOptions.AggressiveInlining)]
                if (_available > 0)
                    return;

                if (_write.Length > 0)
                {
                    if (_writeQueue == null)
                        _writeQueue = new Queue<(Memory<byte>, IDisposable)>();
                    _writeQueue.Enqueue((_write, _writeDisposable));
                    _writeHandle.Dispose();
                }
                _write = ConnectionExtensions.GetBytes(out _writeDisposable);
                _available = _write.Length;
                _writeHandle = _write.Pin();
                unsafe { _pWrite = (byte*)_writeHandle.Pointer; }
            }
            private void Write(byte value)
            {
                if (_available == 0)
                    TryWrite();

                unsafe
                {
                    var pData = _pWrite + (_write.Length - _available);
                    *pData = value;
                    _available -= 1;
                }
            }
            private void Write(ReadOnlySpan<byte> value)
            {
                if (value.IsEmpty)
                    return;

                unsafe
                {
                    fixed (byte* pValue = value)
                    {
                        var tempOffset = 0;
                        var tempCount = value.Length;
                        while (tempCount > 0)
                        {
                            TryWrite();
                            var bytesToCopy = tempCount < _available ? tempCount : _available;
                            Buffer.MemoryCopy(pValue + tempOffset, _pWrite + (_write.Length - _available), bytesToCopy, bytesToCopy);
                            tempOffset += bytesToCopy;
                            tempCount -= bytesToCopy;
                            _available -= bytesToCopy;
                        }
                    }
                }
            }
            private void Write(int value)
            {
                if (_available >= 4)
                {
                    unsafe
                    {
                        var pData = _pWrite + (_write.Length - _available);
                        pData[0] = (byte)((value & 0xFF000000) >> 24);
                        pData[1] = (byte)((value & 0x00FF0000) >> 16);
                        pData[2] = (byte)((value & 0x0000FF00) >> 8);
                        pData[3] = (byte)(value & 0x000000FF);
                        _available -= 4;
                    }
                }
                else
                {
                    Span<byte> pData = stackalloc byte[4];
                    pData[0] = (byte)((value & 0xFF000000) >> 24);
                    pData[1] = (byte)((value & 0x00FF0000) >> 16);
                    pData[2] = (byte)((value & 0x0000FF00) >> 8);
                    pData[3] = (byte)(value & 0x000000FF);
                    Write(pData);
                }
            }
            private void Write(ReadOnlySpan<char> value)
            {
                if (value.IsEmpty)
                    return;
                unsafe
                {
                    fixed (char* pValue = value)
                    {
                        var tempCount = value.Length;
                        while (tempCount > 0)
                        {
                            TryWrite();
                            var bytesToCopy = tempCount < _available ? tempCount : _available;
                            var pData = pValue + (value.Length - tempCount);
                            var pTempBytes = _pWrite + (_write.Length - _available);
                            var tempBytesToCopy = bytesToCopy;

                            while (tempBytesToCopy > 4)
                            {
                                *(pTempBytes) = (byte)*(pData);
                                *(pTempBytes + 1) = (byte)*(pData + 1);
                                *(pTempBytes + 2) = (byte)*(pData + 2);
                                *(pTempBytes + 3) = (byte)*(pData + 3);
                                pTempBytes += 4;
                                pData += 4;
                                tempBytesToCopy -= 4;
                            }
                            while (tempBytesToCopy > 0)
                            {
                                *(pTempBytes) = (byte)*(pData);
                                pTempBytes += 1;
                                pData += 1;
                                tempBytesToCopy -= 1;
                            }

                            tempCount -= bytesToCopy;
                            _available -= bytesToCopy;
                        }
                    }
                }
            }
            private void WriteFrame(int length, byte type, byte flags, int streamId)
            {
                if (_available >= 9)
                {
                    unsafe
                    {
                        var pData = _pWrite + (_write.Length - _available);
                        pData[0] = (byte)((length & 0x00FF0000) >> 16);
                        pData[1] = (byte)((length & 0x0000FF00) >> 8);
                        pData[2] = (byte)(length & 0x000000FF);
                        pData[3] = type;
                        pData[4] = flags;
                        pData[5] = (byte)((streamId & 0xFF000000) >> 24);
                        pData[6] = (byte)((streamId & 0x00FF0000) >> 16);
                        pData[7] = (byte)((streamId & 0x0000FF00) >> 8);
                        pData[8] = (byte)(streamId & 0x000000FF);
                        _available -= 9;
                    }
                }
                else
                {
                    Span<byte> pData = stackalloc byte[9];
                    pData[0] = (byte)((length & 0x00FF0000) >> 16);
                    pData[1] = (byte)((length & 0x0000FF00) >> 8);
                    pData[2] = (byte)(length & 0x000000FF);
                    pData[3] = type;
                    pData[4] = flags;
                    pData[5] = (byte)((streamId & 0xFF000000) >> 24);
                    pData[6] = (byte)((streamId & 0x00FF0000) >> 16);
                    pData[7] = (byte)((streamId & 0x0000FF00) >> 8);
                    pData[8] = (byte)(streamId & 0x000000FF);
                    Write(pData);
                }
            }
            private void WriteFrame(byte type, int streamId, out Span<byte> flags, out Span<byte> len1, out Span<byte> len2, out Span<byte> len3)
            {
                //TODO? ref byte
                if (_available >= 9)
                {
                    var pData = _write.Slice(_write.Length - _available).Span;
                    len1 = pData.Slice(0, 1);
                    len2 = pData.Slice(1, 1);
                    len3 = pData.Slice(2, 1);
                    pData[3] = type;
                    flags = pData.Slice(4, 1);
                    pData[5] = (byte)((streamId & 0xFF000000) >> 24);
                    pData[6] = (byte)((streamId & 0x00FF0000) >> 16);
                    pData[7] = (byte)((streamId & 0x0000FF00) >> 8);
                    pData[8] = (byte)(streamId & 0x000000FF);
                    _available -= 9;
                }
                else
                {
                    TryWrite();
                    len1 = _write.Span.Slice(_write.Length - _available, 1);
                    _available += 1;
                    TryWrite();
                    len2 = _write.Span.Slice(_write.Length - _available, 1);
                    _available += 1;
                    TryWrite();
                    len3 = _write.Span.Slice(_write.Length - _available, 1);
                    _available += 1;
                    TryWrite();
                    unsafe
                    {
                        _pWrite[_write.Length - _available] = type;
                        _available += 1;
                    }
                    TryWrite();
                    flags = _write.Span.Slice(_write.Length - _available, 1);
                    _available += 1;
                    Write(streamId);
                }
            }
            private void WriteHpack(byte prefix, int prefixBits, int value)
            {
                Debug.Assert(prefixBits >= 0 && prefixBits <= 8);

                prefixBits = 0xFF >> (8 - prefixBits);
                if (value < prefixBits)
                {
                    Write((byte)(prefix | value));
                }
                else
                {
                    Write((byte)(prefix | prefixBits));
                    value = value - prefixBits;
                    for (; ; )
                    {
                        if ((value & ~0x7F) == 0)
                        {
                            Write((byte)value);
                            return;
                        }
                        else
                        {
                            Write((byte)((value & 0x7F) | 0x80));
                            value >>= 7;
                        }
                    }
                }
            }
            private void WriteHuffman(string value)
            {
                if (string.IsNullOrEmpty(value))
                {
                    WriteHpack(0b10000000, 7, 0);
                    return;
                }
                //huffmanLength
                {
                    var count = 0L;
                    var i = 0;
                    while (i < value.Length)
                    {
                        count += _EncodingTable[value[i++]].bitLength;
                    }
                    WriteHpack(0b10000000, 7, (int)((count + 7) >> 3));
                }
                //huffmanBytes
                {
                    ulong b = 0;//buffer
                    var bits = 0;//bit数
                    var i = 0;
                    while (i < value.Length)
                    {
                        char ch = value[i++];
                        if (ch >= 128 || ch < 0)//256?
                            throw new InvalidDataException("Huffman");

                        var (code, bitLength) = _EncodingTable[ch];
                        b <<= bitLength;
                        b |= code;
                        bits += bitLength;
                        while (bits >= 8)
                        {
                            bits -= 8;
                            Write((byte)(b >> bits));
                        }
                    }
                    if (bits > 0)
                    {
                        b <<= (8 - bits);
                        b |= (uint)(0xFF >> bits);
                        Write((byte)b);
                    }
                }
            }
            private void WriteHeaders(Http2Stream stream)
            {
                //判断流是否关闭
                Debug.Assert(_writeQueue == null);

                var offset = (_write.Length - _available + 9);//开始长度+9
                WriteFrame(0x1, stream.StreamId, out var flags, out var len1, out var len2, out var len3);

                var response = stream.Response;

                #region :status
                var status = response.StatusCode;
                switch (status)
                {
                    case 200:
                        WriteHpack(0b1000_0000, 7, 8);
                        break;
                    case 204:
                        WriteHpack(0b1000_0000, 7, 9);
                        break;
                    case 206:
                        WriteHpack(0b1000_0000, 7, 10);
                        break;
                    case 304:
                        WriteHpack(0b1000_0000, 7, 11);
                        break;
                    case 400:
                        WriteHpack(0b1000_0000, 7, 12);
                        break;
                    case 404:
                        WriteHpack(0b1000_0000, 7, 13);
                        break;
                    case 500:
                        WriteHpack(0b1000_0000, 7, 14);
                        break;
                    default:
                        {
                            var statusString = status.ToString();
                            WriteHpack(0b0000_0000, 4, 8);
                            WriteHpack(0b0000_0000, 7, statusString.Length);
                            Write(statusString);
                        }
                        break;
                }
                #endregion

                var headers = response.Headers;
                if (headers.Contains(HttpHeaders.Connection))
                    throw new NotSupportedException(HttpHeaders.Connection);

                if (headers.TryGetValue(HttpHeaders.TransferEncoding, out var transferEncoding) && !transferEncoding.EqualsIgnoreCase("identity"))
                    throw new NotSupportedException($"{HttpHeaders.TransferEncoding}:{transferEncoding}");

                if (response.Content != null && !headers.Contains(HttpHeaders.ContentLength))//TODO?
                {
                    var contentLength = response.Content.ComputeLength();
                    if (contentLength != -1)
                    {
                        Span<char> span = stackalloc char[20];
                        contentLength.TryFormat(span, out var charsWritten);
                        span = span.Slice(0, charsWritten);
                        WriteHpack(0b0000_0000, 4, 28);
                        WriteHpack(0b0000_0000, 7, span.Length);
                        Write(span);
                    }
                }

                for (int i = 0; i < headers.Count; i++)
                {
                    var header = headers[i];
                    if (_encoderTable.TryGetIndex(header.Key, out var index))
                    {
                        if (index > 0)
                        {
                            //Raw
                            //WriteHpack(0b00000000, 4, index);
                            //WriteHpack(0b00000000, 7, header.Value.Length);
                            //Write(header.Value);

                            //Huffman
                            WriteHpack(0b0000_0000, 4, index);
                            WriteHuffman(header.Value);
                        }
                    }
                    else
                    {
                        WriteHpack(0b0000_0000, 4, 0);
                        WriteHpack(0b0000_0000, 7, header.Key.Length);
                        unsafe//LowerCase
                        {
                            fixed (char* pValue = header.Key)
                            {
                                var tempCount = header.Key.Length;
                                while (tempCount > 0)
                                {
                                    TryWrite();
                                    var bytesToCopy = tempCount < _available ? tempCount : _available;
                                    var pData = pValue + (header.Key.Length - tempCount);
                                    var pTempBytes = _pWrite + (_write.Length - _available);
                                    var tempBytesToCopy = bytesToCopy;
                                    while (tempBytesToCopy > 0)
                                    {
                                        *pTempBytes = (*pData >= 'A' && *pData <= 'Z') ? (byte)(*pData + 32) : (byte)*pData;
                                        pTempBytes += 1;
                                        pData += 1;
                                        tempBytesToCopy -= 1;
                                    }
                                    tempCount -= bytesToCopy;
                                    _available -= bytesToCopy;
                                }
                            }
                        }
                        WriteHuffman(header.Value);
                        //WriteHpack(0b00000000, 7, header.Value.Length);
                        //Write(header.Value);
                    }
                }

                var frameSize = _MinFrameSize;//线程不安全 固定 不使用变量
                var length = _write.Length - _available;
                if (_writeQueue != null)
                {
                    foreach ((var write, var _) in _writeQueue)
                    {
                        length += write.Length;
                    }
                }
                length -= offset;
                if (length <= frameSize)
                {
                    len1[0] = (byte)((length & 0x00FF0000) >> 16);
                    len2[0] = (byte)((length & 0x0000FF00) >> 8);
                    len3[0] = (byte)(length & 0x000000FF);
                    if (response.Content == null)
                        flags[0] = 0b0000_0100 | 0b0000_0001;//EndHeaders EndStream
                    else
                        flags[0] = 0b0000_0100;//EndHeaders
                }
                else
                {
                    var headersFlag = true;//当前帧是Headers还是Continuation
                    var frameOffset = -offset;
                    var writeQueue = new Queue<(Memory<byte>, IDisposable)>();
                    if (_writeQueue != null)
                    {
                        foreach ((var write, var disposable) in _writeQueue)
                        {
                            var temp = write.Length;
                            for (; ; )
                            {
                                var toFrame = frameSize - frameOffset;
                                if (temp >= toFrame)
                                {
                                    if (headersFlag)
                                    {
                                        len1[0] = (byte)((frameSize & 0x00FF0000) >> 16);
                                        len2[0] = (byte)((frameSize & 0x0000FF00) >> 8);
                                        len3[0] = (byte)(frameSize & 0x000000FF);
                                        if (response.Content == null)
                                            flags[0] = 0b00000001;//EndStream
                                        else
                                            flags[0] = 0b00000000;
                                        headersFlag = false;
                                    }
                                    else
                                    {
                                        len1[0] = (byte)((frameSize & 0x00FF0000) >> 16);
                                        len2[0] = (byte)((frameSize & 0x0000FF00) >> 8);
                                        len3[0] = (byte)(frameSize & 0x000000FF);
                                        flags[0] = 0b00000000;
                                    }
                                    var frameHeader = new byte[9];//Continuation TODO? use Copy
                                    len1 = frameHeader.AsSpan(0, 1);
                                    len2 = frameHeader.AsSpan(1, 1);
                                    len3 = frameHeader.AsSpan(2, 1);
                                    flags = frameHeader.AsSpan(4, 1);
                                    frameHeader[3] = 0x9;
                                    frameHeader[5] = (byte)((stream.StreamId & 0xFF000000) >> 24);
                                    frameHeader[6] = (byte)((stream.StreamId & 0x00FF0000) >> 16);
                                    frameHeader[7] = (byte)((stream.StreamId & 0x0000FF00) >> 8);
                                    frameHeader[8] = (byte)(stream.StreamId & 0x000000FF);
                                    frameOffset = 0;
                                    if (temp == toFrame)
                                    {
                                        writeQueue.Enqueue((write.Slice(write.Length - toFrame), disposable));
                                        writeQueue.Enqueue((frameHeader, Disposable.Empty));
                                        break;
                                    }
                                    writeQueue.Enqueue((write.Slice(write.Length - temp, toFrame), Disposable.Empty));
                                    temp -= toFrame;
                                    writeQueue.Enqueue((frameHeader, Disposable.Empty));
                                }
                                else
                                {
                                    writeQueue.Enqueue((write.Slice(write.Length - temp), disposable));
                                    frameOffset += temp;
                                    break;
                                }
                            }
                        }
                    }
                    //_write
                    {
                        var temp = _write.Length - _available;
                        for (; ; )
                        {
                            var toFrame = frameSize - frameOffset;
                            if (temp > toFrame)
                            {
                                if (headersFlag)
                                {
                                    len1[0] = (byte)((frameSize & 0x00FF0000) >> 16);
                                    len2[0] = (byte)((frameSize & 0x0000FF00) >> 8);
                                    len3[0] = (byte)(frameSize & 0x000000FF);
                                    if (response.Content == null)
                                        flags[0] = 0b0000_0001;//EndStream
                                    else
                                        flags[0] = 0b0000_0000;
                                    headersFlag = false;
                                }
                                else
                                {
                                    len1[0] = (byte)((frameSize & 0x00FF0000) >> 16);
                                    len2[0] = (byte)((frameSize & 0x0000FF00) >> 8);
                                    len3[0] = (byte)(frameSize & 0x000000FF);
                                    flags[0] = 0b0000_0000;
                                }
                                var tempBytes = new byte[toFrame + 9];
                                //frameHeader
                                len1 = tempBytes.AsSpan(toFrame, 1);
                                len2 = tempBytes.AsSpan(toFrame + 1, 1);
                                len3 = tempBytes.AsSpan(toFrame + 2, 1);
                                flags = tempBytes.AsSpan(toFrame + 4, 1);
                                tempBytes[toFrame + 3] = 0x9;
                                tempBytes[toFrame + 5] = (byte)((stream.StreamId & 0xFF000000) >> 24);
                                tempBytes[toFrame + 6] = (byte)((stream.StreamId & 0x00FF0000) >> 16);
                                tempBytes[toFrame + 7] = (byte)((stream.StreamId & 0x0000FF00) >> 8);
                                tempBytes[toFrame + 8] = (byte)(stream.StreamId & 0x000000FF);

                                _write.Slice(_write.Length - _available - temp, toFrame).Span.CopyTo(tempBytes);
                                writeQueue.Enqueue((tempBytes, Disposable.Empty));
                                temp -= toFrame;
                                frameOffset = 0;
                            }
                            else
                            {
                                Debug.Assert(temp > 0);
                                Debug.Assert(headersFlag == false);

                                var available = _write.Length - temp - _available;
                                _write.Slice(available, temp).CopyTo(_write);
                                _available += available;
                                _writeQueue = writeQueue;

                                frameSize = temp + frameOffset;
                                len1[0] = (byte)((frameSize & 0x00FF0000) >> 16);
                                len2[0] = (byte)((frameSize & 0x0000FF00) >> 8);
                                len3[0] = (byte)(frameSize & 0x000000FF);
                                flags[0] = 0b0000_0100;//EndHeaders
                                break;
                            }
                        }
                    }
                }
            }
            private void WritePushPromise(Http2Stream stream, string path, HttpResponse response)
            {
                Debug.Assert(stream != null);
                Debug.Assert(path != null);
                Debug.Assert(response != null);
                if (stream.LocalClosed)
                    return;

                var offset = (_write.Length - _available + 9);
                WriteFrame(0x5, stream.StreamId, out var flags, out var len1, out var len2, out var len3);
                var streamId = _nextStreamId;
                if (streamId > int.MaxValue)
                    return;
                _nextStreamId += 2;
                Write(streamId);
                //Get
                WriteHpack(0b1000_0000, 7, 2);

                #region :scheme
                var scheme = stream.Request.Url.Scheme;
                if (scheme.EqualsIgnoreCase(Url.SchemeHttps))
                {
                    WriteHpack(0b1000_0000, 7, 7);
                }
                else if (scheme.EqualsIgnoreCase(Url.SchemeHttp))
                {
                    WriteHpack(0b1000_0000, 7, 6);
                }
                else
                {
                    WriteHpack(0b0000_0000, 4, 6);
                    WriteHpack(0b0000_0000, 7, scheme.Length);
                    Write(scheme);
                }
                #endregion

                #region :path
                if (string.IsNullOrEmpty(path) || path.Length == 1)
                {
                    WriteHpack(0b10000000, 7, 4);
                }
                else
                {
                    WriteHpack(0b00000000, 4, 4);
                    WriteHpack(0b00000000, 7, path.Length);
                    Write(path);
                }
                #endregion

                var host = stream.Request.Url.Host;
                var port = stream.Request.Url.Port;
                if (port.HasValue)
                {
                    Span<char> span = stackalloc char[11];
                    port.Value.TryFormat(span, out var charsWritten);
                    span = span.Slice(0, charsWritten);
                    WriteHpack(0b0000_0000, 4, 1);
                    WriteHpack(0b0000_0000, 7, host.Length + 1 + span.Length);
                    Write(host);
                    Write((byte)':');
                    Write(span);
                }
                else
                {
                    WriteHpack(0b0000_0000, 4, 1);
                    WriteHpack(0b0000_0000, 7, host.Length);
                    Write(host);
                }

                var length = _write.Length - _available;
                if (_writeQueue != null)
                {
                    foreach ((var write, var _) in _writeQueue)
                    {
                        length += write.Length;
                    }
                }
                length -= offset;
                Debug.Assert(length < _MinFrameSize);
                len1[0] = (byte)((length & 0x00FF0000) >> 16);
                len2[0] = (byte)((length & 0x0000FF00) >> 8);
                len3[0] = (byte)(length & 0x000000FF);
                flags[0] = 0b0000_0100;//EndHeaders

                lock (this)
                {
                    //if (_closeWaiter == null)
                    //    return;
                    var promisedStream = new Http2Stream()
                    {
                        RemoteClosed = true,
                        StreamId = streamId,
                        Response = response,
                        ResponseBody = response.Content
                    };
                    if (!_streams.TryAdd(streamId, promisedStream))
                        throw new ProtocolViolationException("StreamId");
                    _activeStreams += 1;
                    Enqueue(promisedStream);
                }
            }
            private async Task WriteAsync()
            {
                TryWrite();
                try
                {
                    for (; ; )
                    {
                        var sendWindow = 0;
                        var stream = default(Http2Stream);
                        var writer = default(Action);
                        var writeWaiter = default(TaskCompletionSource<object>);
                        lock (this)
                        {
                            if (_dataQueue.Count > 0 && _sendWindow >= 4096)
                            {
                                while (_dataQueue.TryPeek(out stream))
                                {
                                    if (stream.LocalClosed)
                                    {
                                        var __stream = _dataQueue.Dequeue();
                                        Debug.Assert(__stream == stream);
                                        continue;
                                    }
                                    break;
                                }
                            }
                            if (stream != null)
                            {
                                sendWindow = _sendWindow;
                                _sendWindow = 0;
                            }
                            else
                            {
                                if (!_sendQueue.TryDequeue(out writer, out stream))
                                {
                                    if (_activeStreams == -1)
                                        return;

                                    Debug.Assert(_writeWaiter == null);
                                    writeWaiter = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                                    _writeWaiter = writeWaiter;
                                }
                            }
                        }
                        if (writeWaiter != null)
                        {
                            await writeWaiter.Task;
                            continue;
                        }
                        if (sendWindow > 0)
                        {
                            Debug.Assert(_available == _write.Length);
                            Debug.Assert(stream != null);
                            Debug.Assert(stream.Response.Content != null);
                            for (; ; )
                            {
                                Debug.Assert(sendWindow >= 4096);
                                Debug.Assert(_write.Length > 9);
                                unsafe
                                {
                                    _pWrite[3] = 0x0;//Data
                                    _pWrite[5] = (byte)((stream.StreamId & 0xFF000000) >> 24);
                                    _pWrite[6] = (byte)((stream.StreamId & 0x00FF0000) >> 16);
                                    _pWrite[7] = (byte)((stream.StreamId & 0x0000FF00) >> 8);
                                    _pWrite[8] = (byte)(stream.StreamId & 0x000000FF);
                                }

                                var responseBody = stream.ResponseBody;
                                if (responseBody == null) 
                                {
                                    lock (this)
                                    {
                                        _sendWindow += sendWindow;
                                        var __stream = _dataQueue.Dequeue();
                                        Debug.Assert(__stream == stream);
                                        break;
                                    }
                                }
                                var result = await responseBody.ReadAsync(_write.Slice(9, Math.Min(_MinFrameSize, Math.Min(sendWindow, _write.Length - 9))));
                                var endStream = result == 0 || responseBody.Available == 0;
                                unsafe
                                {
                                    _pWrite[0] = (byte)((result & 0x00FF0000) >> 16);
                                    _pWrite[1] = (byte)((result & 0x0000FF00) >> 8);
                                    _pWrite[2] = (byte)(result & 0x000000FF);
                                    if (endStream)
                                        _pWrite[4] = 0b0000_0001;//EndStream
                                    else
                                        _pWrite[4] = 0b0000_0000;

                                }
                                await SendAsync(_write.Slice(0, result + 9));
                                sendWindow -= result;
                                lock (this) 
                                {
                                    if (stream.LocalClosed)
                                    {
                                        _sendWindow += sendWindow;
                                        var __stream = _dataQueue.Dequeue();
                                        Debug.Assert(__stream == stream);
                                        break;
                                    }
                                    else if (endStream) 
                                    {
                                        if (!stream.RemoteClosed)
                                        {
                                            stream.RemoteClosed = true;
                                            stream.RequestBody?.Close(new InvalidDataException("Drain"));
                                        }
                                        stream.LocalClosed = true;
                                        stream.Dispose();
                                        Dequeue(stream);
                                        _sendWindow += sendWindow;
                                        var __stream = _dataQueue.Dequeue();
                                        Debug.Assert(__stream == stream);
                                        break;
                                    }
                                    if (sendWindow < 4096)
                                    {
                                        Debug.Assert(sendWindow >= 0);
                                        _sendWindow += sendWindow;
                                        break;
                                    }
                                }
                            }
                            continue;
                        }
                        else
                        {
                            for (; ; )
                            {
                                if (writer != null)
                                {
                                    writer.Invoke();
                                }
                                else if (stream != null)
                                {
                                    WriteHeaders(stream);
                                    lock (this)
                                    {
                                        if (!stream.LocalClosed) 
                                        {
                                            if (stream.Response.Content == null)
                                            {
                                                if (!stream.RemoteClosed)
                                                {
                                                    stream.RemoteClosed = true;
                                                    stream.RequestBody?.Close(new InvalidDataException("Drain"));
                                                }
                                                stream.LocalClosed = true;
                                                stream.Dispose();
                                                Dequeue(stream);
                                            }
                                            else
                                            {
                                                if (_dataQueue.Count > _maxReceiveStreams) //RST_Stream OOM
                                                {
                                                    var streams = _dataQueue.ToArray();
                                                    _dataQueue.Clear();
                                                    for (int i = 0; i < streams.Length; i++)
                                                    {
                                                        if (!streams[i].RemoteClosed)
                                                            _dataQueue.Enqueue(streams[i]);
                                                    }
                                                }
                                                _dataQueue.Enqueue(stream);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    await SendAsync(_write.Slice(0, _write.Length - _available));
                                    _available = _write.Length;
                                    break;
                                }
                                if (_writeQueue != null)
                                {
                                    while (_writeQueue.TryDequeue(out var write, out var disposable))
                                    {
                                        try
                                        {
                                            await SendAsync(write);
                                        }
                                        finally
                                        {
                                            disposable.Dispose();
                                        }
                                    }
                                    _writeQueue = null;
                                    await SendAsync(_write.Slice(0, _write.Length - _available));
                                    _available = _write.Length;
                                    break;
                                }
                                if (_available < 1024)
                                {
                                    await SendAsync(_write.Slice(0, _write.Length - _available));
                                    _available = _write.Length;
                                    break;
                                }
                                lock (this) 
                                {
                                    _sendQueue.TryDequeue(out writer, out stream);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Close(-1, ex);
                }
                finally
                {
                    #region Dispose
                    Debug.Assert(_write.Length > 0);
                    _available = 0;
                    _write = Memory<byte>.Empty;
                    _writeHandle.Dispose();
                    unsafe { _pWrite = (byte*)0; }
                    _writeDisposable.Dispose();
                    _writeDisposable = null;
                    if (_writeQueue != null)
                    {
                        while (_writeQueue.TryDequeue(out _, out var disposable))
                        {
                            disposable.Dispose();
                        }
                        _writeQueue = null;
                    }
                    #endregion
                }
            }
            private async ValueTask ReceiveAsync(int length)
            {
                var toReceive = _end - _start;
                if (toReceive >= length)//length=0
                {
                    _position = _start + length;
                }
                else
                {
                    if (toReceive == 0)
                    {
                        var result = await ReceiveAsync(_read);
                        if (result == 0)
                            throw new InvalidDataException("FIN");

                        _start = 0;
                        _end = result;
                        toReceive = result;
                    }
                    while (toReceive < length)
                    {
                        if (_end < _read.Length)
                        {
                            var result = await ReceiveAsync(_read.Slice(_end));
                            if (result == 0)
                                throw new InvalidDataException("FIN");

                            _end += result;
                            toReceive += result;
                        }
                        else
                        {
                            Debug.Assert(_end == _read.Length);
                            if (_readQueue == null)
                            {
                                if (_start == 0 || (_start << 1) > _read.Length)//_start过半了
                                {
                                    _readQueue = new Queue<(Memory<byte>, IDisposable)>();
                                    _readQueue.Enqueue((_read.Slice(_start), _readDisposable));
                                    _readHandle.Dispose();

                                    _read = ConnectionExtensions.GetBytes(out _readDisposable);
                                    _readHandle = _read.Pin();
                                    unsafe { _pRead = (byte*)_readHandle.Pointer; }

                                    var result = await ReceiveAsync(_read);
                                    if (result == 0)
                                        throw new InvalidDataException("FIN");

                                    _start = 0;
                                    _end = result;
                                    toReceive += result;
                                }
                                else
                                {
                                    var count = _end - _start;
                                    _read.Span.Slice(_start).CopyTo(_read.Span.Slice(0, count));
                                    _start = 0;
                                    _end = count;

                                    var result = await ReceiveAsync(_read.Slice(_end));
                                    if (result == 0)
                                        throw new InvalidDataException("FIN");

                                    _end += result;
                                    toReceive += result;
                                }
                            }
                            else
                            {
                                _readQueue.Enqueue((_read.Slice(_start), _readDisposable));
                                _readHandle.Dispose();

                                _read = ConnectionExtensions.GetBytes(out _readDisposable);
                                _readHandle = _read.Pin();
                                unsafe { _pRead = (byte*)_readHandle.Pointer; }

                                var result = await ReceiveAsync(_read);
                                if (result == 0)
                                    throw new InvalidDataException("FIN");

                                _start = 0;
                                _end = result;
                                toReceive += result;
                            }
                        }
                    }
                    _position = _end - (toReceive - length);
                }
            }
            private async ValueTask ReadFrameAsync()
            {
                const int frameHeaderLength = 9;
                void ReadFrame()
                {
                    var frameBytes = ReadBytes();
                    Debug.Assert(frameBytes.Length == frameHeaderLength);
                    _frameLength = (frameBytes[0] << 16) | (frameBytes[1] << 8) | frameBytes[2];
                    _frameType = frameBytes[3];
                    _frameFlags = frameBytes[4];
                    _frameStreamId = ((frameBytes[5] << 24) | (frameBytes[6] << 16) | (frameBytes[7] << 8) | frameBytes[8]) & 0x7FFFFFFF;

                    Debug.WriteLine($"StreamId={_frameStreamId};Type={_frameType};Flags={_frameFlags};Length={_frameLength}");

                    if (_frameLength > _MinFrameSize)
                        throw new ProtocolViolationException("Protocol Error");
                }
                //KeepAlive
                if (_start >= _end)
                {
                    var result = await KeepAliveAsync(_read);
                    if (result == 0)
                        throw new InvalidDataException("FIN");
                    _start = 0;
                    _end = result;
                }
                await ReceiveAsync(frameHeaderLength);
                ReadFrame();
            }
            private async ValueTask DrainFrameAsync()
            {
                Debug.Assert(_readQueue == null);
                if (_frameLength == 0)
                    return;

                while (_frameLength > 0)
                {
                    var available = _end - _start;
                    if (available <= 0)
                    {
                        available = await ReceiveAsync(_read);
                        if (available == 0)
                            throw new InvalidOperationException("FIN");

                        _start = 0;
                        _end = available;
                    }
                    var toDrain = Math.Min(available, _frameLength);
                    _frameLength -= toDrain;
                    _start += toDrain;
                }
                Debug.Assert(_frameLength == 0);
            }
            private ReadOnlySpan<byte> ReadBytes()
            {
                Debug.Assert(_position >= _start);
                Debug.Assert(_position <= _end);

                if (_readQueue == null)
                {
                    unsafe
                    {
                        var span = new ReadOnlySpan<byte>(_pRead + _start, _position - _start);
                        _start = _position;
                        return span;
                    }
                }
                else
                {
                    unsafe
                    {
                        var span = new ReadOnlySpan<byte>(_pRead + _start, _position - _start);
                        _start = _position;

                        var length = span.Length;
                        foreach ((var read, var _) in _readQueue)
                        {
                            length += read.Length;
                        }
                        var bytes = new byte[length].AsSpan();
                        var tempBytes = bytes;
                        foreach ((var read, var disposable) in _readQueue)
                        {
                            read.Span.CopyTo(tempBytes);
                            tempBytes = tempBytes.Slice(read.Length);
                            disposable.Dispose();
                        }
                        span.CopyTo(tempBytes);
                        _readQueue = null;
                        return bytes;
                    }
                }
            }
            private async Task ReadAsync()
            {
                _read = ConnectionExtensions.GetBytes(out _readDisposable);
                _readHandle = _read.Pin();
                unsafe { _pRead = (byte*)_readHandle.Pointer; }
                try
                {
                    await ReceiveAsync(_Preface.Length);
                    if (!ReadBytes().SequenceEqual(_Preface))
                        throw new ProtocolViolationException("Client Preface");
                    await ReadFrameAsync();
                    if (_frameType != 0x4)
                        throw new ProtocolViolationException("Settings");
                    await ReadSettingsAsync();
                    for (; ; )
                    {
                        await ReadFrameAsync();
                        switch (_frameType)
                        {
                            case 0x0://Data
                                await ReadDataAsync();//如果丢弃Data别忘记发送WindowUpdate
                                break;
                            case 0x1://Headers 
                                await ReadHeadersAsync();
                                break;
                            case 0x3://RstStream
                                await ReadRstStreamAsync();
                                break;
                            case 0x4://Settings
                                await ReadSettingsAsync();
                                break;
                            case 0x6://Ping
                                await ReadPingAsync();
                                break;
                            case 0x7://GoAway
                                await ReadGoAwayAsync();
                                break;
                            case 0x8://WindowUpdate
                                await ReadWindowUpdateAsync();
                                break;
                            case 0x2://Priority
                                await DrainFrameAsync();
                                break;
                            case 0x5://PushPromise
                            case 0x9://Continuation
                                throw new ProtocolViolationException("Protocol Error");
                            default:
                                await DrainFrameAsync();
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Close(-1, ex);
                }
                finally
                {
                    #region Dispose
                    Debug.Assert(_read.Length > 0);
                    _start = 0;
                    _end = 0;
                    _position = 0;
                    _read = Memory<byte>.Empty;
                    _readHandle.Dispose();
                    unsafe { _pRead = (byte*)0; }
                    _readDisposable.Dispose();
                    _readDisposable = null;
                    if (_readQueue != null)
                    {
                        while (_readQueue.TryDequeue(out _, out var disposable))
                        {
                            disposable.Dispose();
                        }
                        _readQueue = null;
                    }
                    if (_headersQueue != null)
                    {
                        while (_headersQueue.TryDequeue(out var disposable))
                        {
                            disposable.Dispose();
                        }
                        _headersQueue = null;
                    }
                    #endregion
                }
            }
            private async ValueTask ReadSettingsAsync()
            {
                const int settingLength = 6;
                void ReadSetting()
                {
                    var settingBytes = ReadBytes();
                    Debug.Assert(settingBytes.Length == settingLength);
                    ushort settingId = (ushort)((settingBytes[0] << 8) | settingBytes[1]);
                    uint settingValue = (uint)((settingBytes[2] << 24) | (settingBytes[3] << 16) | (settingBytes[4] << 8) | settingBytes[5]);
                    switch (settingId)
                    {
                        case 0x1://HeaderTableSize
                            Debug.WriteLine($"Settings-HeaderTableSize:{settingValue}");
                            if (settingValue > _MaxHeaderTableSize)//Chrome 修改DecoderTable实现
                                throw new ProtocolViolationException("Setting HeaderTableSize");

                            _decoderTable.MaxSize = (int)settingValue;//Read中执行 不会发生并发
                            break;
                        case 0x2://EnablePush
                            Debug.WriteLine($"Settings-EnablePush:{settingValue}");
                            if (settingValue == 0)
                                _enablePush = 0;
                            else if (settingValue == 1)
                                _enablePush = 1;
                            else
                                throw new ProtocolViolationException("Setting EnablePush");
                            break;
                        case 0x3://MaxConcurrentStreams
                            Debug.WriteLine($"Settings-MaxConcurrentStreams:{settingValue}|{_maxSendStreams}");
                            if (settingValue == 0 || settingValue > 0x7FFFFFFF)
                                throw new ProtocolViolationException("Setting MaxConcurrentStreams");

                            lock (this)
                            {
                                _maxSendStreams = (int)settingValue;
                            }
                            break;
                        case 0x4://InitialWindowSize
                            Debug.WriteLine($"Settings-InitialWindowSize:{settingValue}");
                            if (settingValue == 0 || settingValue > 0x7FFFFFFF)
                                throw new ProtocolViolationException("Setting InitialWindowSize");
                            if (settingValue < 4096)
                                throw new NotSupportedException($"InitialWindowSize:{settingValue}");

                            _initialSendWindow = (int)settingValue;
                            break;
                        case 0x5://MaxFrameSize 
                            Debug.WriteLine($"Settings-MaxFrameSize:{settingValue}");
                            if (settingValue < _MinFrameSize || settingValue > _MaxFrameSize)
                                throw new ProtocolViolationException("Setting MaxFrameSize");
                            //ignore
                            //_maxFrameSize = (int)settingValue;
                            break;
                        default:
                            //ignore
                            break;
                    }
                }

                if ((_frameFlags & 0b00000001) != 0)//SettingsAck
                {
                    if (_frameLength != 0)
                        throw new ProtocolViolationException("SettingsAck length must 0");

                    Debug.WriteLine($"SettingsAck:Receive");
                    //Ignore
                }
                else
                {
                    if (_settings++ >= _service._maxSettings)
                        throw new ProtocolViolationException($"Max Settings:{_service._maxSettings}");
                    if (_frameLength == 0)
                        return;
                    if ((_frameLength % 6) != 0)
                        throw new ProtocolViolationException("Settings Size Error");

                    while (_frameLength > 0)
                    {
                        await ReceiveAsync(settingLength);
                        _frameLength -= settingLength;
                        ReadSetting();
                    }

                    //SettingsAck
                    lock (this)
                    {
                        Enqueue(() => { WriteFrame(0, 0x4, 0b00000001, 0); });
                    }
                }
            }
            private async ValueTask ReadRstStreamAsync()
            {
                const int rstStreamLength = 4;
                void ReadRstStream()
                {
                    var rstStreamBytes = ReadBytes();
                    Debug.Assert(rstStreamBytes.Length == rstStreamLength);
                    var errorCode = (rstStreamBytes[0] << 24) | (rstStreamBytes[1] << 16) | (rstStreamBytes[2] << 8) | rstStreamBytes[3];
                    Debug.WriteLine($"RstStream:{_frameStreamId},ErrorCode:{errorCode}");

                    Http2Stream stream;
                    lock (this)
                    {
                        if (!_streams.TryGetValue(_frameStreamId, out stream))
                            return;

                        if (!stream.RemoteClosed)
                        {
                            stream.RemoteClosed = true;
                            stream.RequestBody?.Close(new InvalidDataException("Receive Rst_Stream"));
                        }
                        stream.LocalClosed = true;
                        stream.Dispose();
                        Dequeue(stream);
                    }
                }
                if (_frameStreamId == 0)
                    throw new ProtocolViolationException("Protocol Error");
                if (_frameLength != rstStreamLength)
                    throw new ProtocolViolationException("RstStream length must 4");

                await ReceiveAsync(rstStreamLength);
                ReadRstStream();
            }
            private async ValueTask ReadPingAsync()
            {
                const int pingLength = 8;
                void ReadPing()//long or 2*int
                {
                    var pingBytes = ReadBytes();
                    Debug.WriteLine($"Ping:{Encoding.ASCII.GetString(pingBytes)}");
                    Debug.Assert(pingBytes.Length == pingLength);
                    //OpaqueData
                    var opaqueData1 = (pingBytes[0] << 24) | (pingBytes[1] << 16) | (pingBytes[2] << 8) | pingBytes[3];
                    var opaqueData2 = (pingBytes[4] << 24) | (pingBytes[5] << 16) | (pingBytes[6] << 8) | pingBytes[7];
                    lock (this)
                    {
                        Enqueue(() => {
                            WriteFrame(8, 0x6, 0b00000001, 0);//Ack
                            Write(opaqueData1);
                            Write(opaqueData2);
                        });
                    }
                }

                if (_frameStreamId != 0)
                    throw new ProtocolViolationException("Protocol Error");
                if (_frameLength != pingLength)
                    throw new ProtocolViolationException("Ping length must 8");

                if ((_frameFlags & 0b00000001) != 0)//PingAck
                {
                    Debug.WriteLine($"PingAck:Receive");
                    await DrainFrameAsync();
                }
                else
                {
                    await ReceiveAsync(pingLength);
                    ReadPing();
                }
            }
            private async ValueTask ReadGoAwayAsync()
            {
                void ReadGoAway()
                {
                    var goAwayBytes = ReadBytes();
                    var lastStreamId = ((goAwayBytes[0] << 24) | (goAwayBytes[1] << 16) | (goAwayBytes[2] << 8) | goAwayBytes[3]) & 0x7FFFFFFF;
                    var errorCode = (goAwayBytes[4] << 24) | (goAwayBytes[5] << 16) | (goAwayBytes[6] << 8) | goAwayBytes[7];
                    //12=INADEQUATE_SECURITY
                    //if (lastStreamId == 0)
                    //    throw new ProtocolViolationException("Protocol Error");
                    //是否放在这里
                    if (lastStreamId > _lastStreamId)
                        throw new ProtocolViolationException("Protocol Error");

                    Debug.WriteLine($"GoAway:lastStreamId={lastStreamId},errorCode={errorCode}");
                    Close(lastStreamId, new ProtocolViolationException($"GoAway:ErrorCode={errorCode}"));
                }
                if (_frameStreamId != 0)
                    throw new ProtocolViolationException("Protocol Error");
                if (_frameLength < 8)
                    throw new ProtocolViolationException("Frame Size Error");

                await ReceiveAsync(8);
                ReadGoAway();
                //Additional Debug Data
                _frameLength -= 8;
                await DrainFrameAsync();
            }
            private async ValueTask ReadWindowUpdateAsync()
            {
                const int windowUpdateLength = 4;
                void ReadWindowUpdate()
                {
                    var windowUpdateBytes = ReadBytes();
                    Debug.Assert(windowUpdateBytes.Length == windowUpdateLength);
                    var increment = ((windowUpdateBytes[0] << 24) | (windowUpdateBytes[1] << 16) | (windowUpdateBytes[2] << 8) | windowUpdateBytes[3]) & 0x7FFFFFFF;
                    Debug.WriteLine($"WindowUpdate:{increment} StreamId:{_frameStreamId}");
                    if (_frameStreamId > 0)
                        return;//TODO?
                    lock (this)
                    {
                        _sendWindow += increment;
                        if (_writeWaiter != null && _sendWindow > 0)
                        {
                            _writeWaiter.SetResult(null);
                            _writeWaiter = null;
                        }
                    }
                }
                await ReceiveAsync(windowUpdateLength);
                ReadWindowUpdate();
            }
            private async ValueTask ReadDataAsync()
            {
                if ((_frameStreamId & 1) == 0)
                    throw new ProtocolViolationException("StreamId");

                Http2Stream stream;
                lock (this)
                {
                    if (_frameLength > _receiveWindow)
                        throw new ProtocolViolationException("Flow_Control_Error");

                    _receiveWindow -= _frameLength;
                    _streams.TryGetValue(_frameStreamId, out stream);

                    if (stream.RemoteClosed)
                        stream = null;
                    //if (stream.RemoteClosed)
                    //    throw new ProtocolViolationException("RemoteClosed");
                }

                if (stream == null)//Ignore
                {
                    Debug.WriteLine("Ignore DataFrame");
                    var increment = _frameLength;
                    await DrainFrameAsync();
                    lock (this)//WindowUpdate
                    {
                        _windowUpdate += increment;
                        if (_windowUpdate > _receiveWindowUpdate)
                        {
                            var windowUpdate = _windowUpdate;
                            Enqueue(() => {
                                WriteFrame(4, 0x8, 0, 0);
                                Write(windowUpdate);
                            });
                            _windowUpdate = 0;
                            _receiveWindow += windowUpdate;
                        }
                        return;
                    }
                }

                Debug.Assert(stream.RequestBody != null);
                if ((_frameFlags & 0b00001000) != 0)//Padded
                {
                    if (_frameLength == 0)
                        throw new ProtocolViolationException("Padded");

                    await ReceiveAsync(1);
                    var padLength = ReadBytes()[0];
                    _frameLength -= 1;

                    if (_frameLength < padLength)
                        throw new ProtocolViolationException("Padded");

                    while (_frameLength > padLength)
                    {
                        var available = _end - _start;
                        if (available <= 0)
                        {
                            available = await ReceiveAsync(_read);
                            if (available == 0)
                                throw new InvalidOperationException("FIN");
                            _start = 0;
                            _end = available;
                        }
                        var toRead = Math.Min(available, _frameLength - padLength);
                        stream.RequestBody.OnData(_read.Span.Slice(_start, toRead));
                        _frameLength -= toRead;
                        _start += toRead;
                    }
                    await DrainFrameAsync();
                    var increment = padLength + 1;
                    lock (this)//WindowUpdate
                    {
                        _windowUpdate += increment;
                        if (_windowUpdate > _receiveWindowUpdate)
                        {
                            var windowUpdate = _windowUpdate;
                            Enqueue(() => {
                                WriteFrame(4, 0x8, 0, 0);
                                Write(windowUpdate);
                            });
                            _windowUpdate = 0;
                            _receiveWindow += windowUpdate;
                        }
                    }
                }
                else
                {
                    while (_frameLength > 0)
                    {
                        var available = _end - _start;
                        if (available <= 0)
                        {
                            available = await ReceiveAsync(_read);
                            if (available == 0)
                                throw new InvalidOperationException("FIN");
                            _start = 0;
                            _end = available;
                        }
                        var toRead = Math.Min(available, _frameLength);
                        stream.RequestBody.OnData(_read.Span.Slice(_start, toRead));
                        _frameLength -= toRead;
                        _start += toRead;
                    }
                }

                if ((_frameFlags & 0b00000001) != 0)//EndStream
                {
                    lock (this)
                    {
                        if (stream.RemoteClosed)
                            return;
                        stream.RemoteClosed = true;
                        stream.RequestBody.Close(null);
                    }
                }
            }
            private async ValueTask ReadHeadersAsync()
            {
                //odd-numbered
                if ((_frameStreamId & 1) == 0)
                    throw new ProtocolViolationException("StreamId");

                var stream = new Http2Stream()
                {
                    Connection = this,
                    StreamId = _frameStreamId,
                    Request = new HttpRequest()
                };
                lock (this)
                {
                    if (_closeWaiter != null)
                        throw new ProtocolViolationException("StreamId");
                    if (_receiveStreams >= _maxReceiveStreams)
                        throw new ProtocolViolationException("MaxConcurrentStreams");
                    if (_frameStreamId <= _lastStreamId)
                        throw new ProtocolViolationException("StreamId");

                    if (!_streams.TryAdd(_frameStreamId, stream))
                        throw new ProtocolViolationException("StreamId");

                    _activeStreams += 1;
                    _receiveStreams += 1;
                    _lastStreamId = _frameStreamId;
                }
                await ReceiveAsync(_frameLength);
                if (!ReadHeaders(stream, out var endStream))//endHeaders
                {
                    for (; ; )
                    {
                        await ReadFrameAsync();
                        if (_frameType != 0x9)//_frameStreamId
                            throw new ProtocolViolationException("Continuation");

                        await ReceiveAsync(_frameLength);
                        if (ReadContinuation(stream))
                        {
                            break;
                        }
                    }
                }
                if (endStream)
                {
                    lock (this)
                    {
                        if (stream.RemoteClosed)
                            return;
                        stream.RemoteClosed = true;
                    }
                }
                else
                {
                    var content = new Http2Content(stream);
                    lock (this)
                    {
                        if (stream.RemoteClosed)
                            return;
                        stream.RequestBody = content;
                        stream.Request.Content = content;
                    }
                }
                //handler
                ThreadPool.UnsafeQueueUserWorkItem(stream, false);
            }
            private bool ReadHeaders(Http2Stream stream, out bool endStream)
            {
                var headersBytes = ReadBytes();
                if ((_frameFlags & 0b00001000) != 0)//Padded
                {
                    if (headersBytes.Length == 0)
                        throw new ProtocolViolationException();

                    int padLength = headersBytes[0];
                    headersBytes = headersBytes.Slice(1);

                    if (headersBytes.Length < padLength)
                        throw new ProtocolViolationException();

                    headersBytes = headersBytes.Slice(0, headersBytes.Length - padLength);
                }

                if ((_frameFlags & 0b00100000) != 0)//Priority
                {
                    if (headersBytes.Length < 5)//StreamDependency(4)+Weight(1)
                        throw new ProtocolViolationException();

                    headersBytes = headersBytes.Slice(5);//ignore
                }
                _headers = headersBytes.Length;
                if (_headers > _service._maxHeaderListSize)//roughly
                    throw new ProtocolViolationException($"MaxHeaderListSize");
                _headersSize = 0;

                endStream = (_frameFlags & 0b00000001) != 0;//EndStream

                if ((_frameFlags & 0b00000100) != 0)//EndHeaders
                {
                    ReadHeaders(headersBytes, stream.Request);
                    return true;
                }

                if (headersBytes.Length > 0)//是否允许为空
                {
                    Debug.Assert(_headersQueue == null);
                    _headersQueue = new Queue<UnmanagedMemory<byte>>();
                    var tempBytes = new UnmanagedMemory<byte>(headersBytes.Length);
                    headersBytes.CopyTo(tempBytes.GetSpan());
                    _headersQueue.Enqueue(tempBytes);
                }
                return false;
            }
            private bool ReadContinuation(Http2Stream stream)
            {
                if (_frameStreamId != stream.StreamId)
                    throw new ProtocolViolationException("StreamId");

                var headersBytes = ReadBytes();
                _headers += headersBytes.Length;
                if (_headers > _service._maxHeaderListSize)//roughly
                    throw new ProtocolViolationException($"MaxHeaderListSize");

                if ((_frameFlags & 0b00000100) != 0)//EndHeaders
                {
                    ReadHeaders(headersBytes, stream.Request);
                    return true;
                }

                if (headersBytes.Length > 0)
                {
                    Debug.Assert(_headersQueue != null);
                    var tempBytes = new UnmanagedMemory<byte>(headersBytes.Length);
                    headersBytes.CopyTo(tempBytes.GetSpan());
                    _headersQueue.Enqueue(tempBytes);
                }
                return false;
            }
            private void ReadHeaders(ReadOnlySpan<byte> headersBytes, HttpRequest request)
            {
                //NotSupported \0
                if (_headersQueue != null)
                {
                    var tempBytes = new byte[_headers];
                    var tempSpan = tempBytes.AsSpan();
                    while (_headersQueue.TryDequeue(out var headerBytes))
                    {
                        headerBytes.GetSpan().CopyTo(tempSpan);
                        tempSpan = tempSpan.Slice(headerBytes.Length);
                        headerBytes.Dispose();
                    }
                    _headersQueue = null;
                    Debug.Assert(tempSpan.Length == headersBytes.Length);
                    headersBytes.CopyTo(tempSpan);
                    headersBytes = tempBytes;
                }
                if (headersBytes.IsEmpty)
                    throw new ProtocolViolationException("HeaderBlock Empty");

                //是否异常包装
                (var state, var prefix) = _Ready[headersBytes[0]];
                var offset = 1;
                if (state == State.SizeUpdate)//https://tools.ietf.org/html/rfc7541#section-4.2
                {
                    var size = prefix == 0b0001_1111 ? ReadHpack(headersBytes, prefix, ref offset) : prefix;
                    if (size > _decoderTable.MaxSize)
                        throw new ProtocolViolationException("Update HeaderTableSize");
                    _decoderTable.Size = size;
                    state = State.Ready;
                }
                string method = null;
                string scheme = null;
                string authority = null;
                string path = null;
                for (; ; )
                {
                    switch (state)
                    {
                        case State.Ready:
                            (state, prefix) = _Ready[headersBytes[offset++]];
                            continue;
                        case State.Indexed://0b1000_0000
                            {
                                if (prefix == 0)
                                    throw new ProtocolViolationException("Index");

                                var index = prefix == 0b0111_1111 ? ReadHpack(headersBytes, prefix, ref offset) : prefix;
                                if (!_decoderTable.TryGetField(index, out var name, out var value))
                                    throw new ProtocolViolationException("Index Not Found");
                                Debug.Assert(name.Length > 0);
                                _headersSize = _headersSize + name.Length + value.Length + 32;
                                if(_headersSize>_service._maxHeaderListSize)
                                    throw new ProtocolViolationException("MaxHeaderTableSize");
                                switch (name)
                                {
                                    case ":method":
                                        if (method != null)
                                            throw new ProtocolViolationException(":method");
                                        method = value;
                                        break;
                                    case ":scheme":
                                        if (scheme != null)
                                            throw new ProtocolViolationException(":scheme");
                                        scheme = value;
                                        break;
                                    case ":authority":
                                        if (authority != null)
                                            throw new ProtocolViolationException(":authority");
                                        authority = value;
                                        break;
                                    case ":path":
                                        if (path != null)
                                            throw new ProtocolViolationException(":path");
                                        path = value;
                                        break;
                                    default:
                                        request.Headers.Add(name, value);
                                        break;
                                }
                                state = State.Ready;
                                break;
                            }
                        case State.Indexing://0b0100_0000
                            if (prefix == 0)//Name Value
                            {
                                var name = ReadLiteral(headersBytes, ref offset);
                                var value = ReadLiteral(headersBytes, ref offset);
                                if (string.IsNullOrEmpty(name))
                                    throw new ProtocolViolationException("HeaderName Empty");
                                for (int i = 0; i < name.Length; i++)
                                {
                                    if (name[i] >= 65 && name[i] <= 90)//A-Z
                                        throw new ProtocolViolationException("HeaderName Must LowerCase");
                                }
                                _headersSize = _headersSize + name.Length + value.Length + 32;
                                if (_headersSize > _service._maxHeaderListSize)
                                    throw new ProtocolViolationException("MaxHeaderTableSize");
                                switch (name)
                                {
                                    case ":method":
                                        if (method != null)
                                            throw new ProtocolViolationException(":method");
                                        method = value;
                                        break;
                                    case ":scheme":
                                        if (scheme != null)
                                            throw new ProtocolViolationException(":scheme");
                                        scheme = value;
                                        break;
                                    case ":authority":
                                        if (authority != null)
                                            throw new ProtocolViolationException(":authority");
                                        authority = value;
                                        break;
                                    case ":path":
                                        if (path != null)
                                            throw new ProtocolViolationException(":path");
                                        path = value;
                                        break;
                                    default:
                                        request.Headers.Add(name, value);
                                        break;
                                }
                                _decoderTable.Add(name, value);
                                state = State.Ready;
                                break;
                            }
                            else
                            {
                                var index = prefix == 0b0011_1111 ? ReadHpack(headersBytes, prefix, ref offset) : prefix;
                                if (!_decoderTable.TryGetField(index, out var name, out var _))
                                    throw new ProtocolViolationException("Index Not Found");
                                var value = ReadLiteral(headersBytes, ref offset);
                                _headersSize = _headersSize + name.Length + value.Length + 32;
                                if (_headersSize > _service._maxHeaderListSize)
                                    throw new ProtocolViolationException("MaxHeaderTableSize");
                                switch (name)
                                {
                                    case ":method":
                                        if (method != null)
                                            throw new ProtocolViolationException(":method");
                                        method = value;
                                        break;
                                    case ":scheme":
                                        if (scheme != null)
                                            throw new ProtocolViolationException(":scheme");
                                        scheme = value;
                                        break;
                                    case ":authority":
                                        if (authority != null)
                                            throw new ProtocolViolationException(":authority");
                                        authority = value;
                                        break;
                                    case ":path":
                                        if (path != null)
                                            throw new ProtocolViolationException(":path");
                                        path = value;
                                        break;
                                    default:
                                        request.Headers.Add(name, value);
                                        break;
                                }
                                _decoderTable.Add(name, value);
                                state = State.Ready;
                                break;
                            }
                        case State.WithoutIndexing://0000
                        case State.NeverIndexed://0001
                            if (prefix == 0)//Name Value
                            {
                                var name = ReadLiteral(headersBytes, ref offset);
                                var value = ReadLiteral(headersBytes, ref offset);
                                if (string.IsNullOrEmpty(name))
                                    throw new ProtocolViolationException("HeaderName Empty");
                                for (int i = 0; i < name.Length; i++)
                                {
                                    if (name[i] >= 65 && name[i] <= 90)//A-Z
                                        throw new ProtocolViolationException("HeaderName Must LowerCase");
                                }
                                _headersSize = _headersSize + name.Length + value.Length + 32;
                                if (_headersSize > _service._maxHeaderListSize)
                                    throw new ProtocolViolationException("MaxHeaderTableSize");
                                switch (name)
                                {
                                    case ":method":
                                        if (method != null)
                                            throw new ProtocolViolationException(":method");
                                        method = value;
                                        break;
                                    case ":scheme":
                                        if (scheme != null)
                                            throw new ProtocolViolationException(":scheme");
                                        scheme = value;
                                        break;
                                    case ":authority":
                                        if (authority != null)
                                            throw new ProtocolViolationException(":authority");
                                        authority = value;
                                        break;
                                    case ":path":
                                        if (path != null)
                                            throw new ProtocolViolationException(":path");
                                        path = value;
                                        break;
                                    default:
                                        request.Headers.Add(name, value);
                                        break;
                                }
                                state = State.Ready;
                                break;
                            }
                            else
                            {
                                var index = prefix == 0b0000_1111 ? ReadHpack(headersBytes, prefix, ref offset) : prefix;
                                if (!_decoderTable.TryGetField(index, out var name, out var _))
                                    throw new ProtocolViolationException("Index Not Found");
                                var value = ReadLiteral(headersBytes, ref offset);
                                _headersSize = _headersSize + name.Length + value.Length + 32;
                                if (_headersSize > _service._maxHeaderListSize)
                                    throw new ProtocolViolationException("MaxHeaderTableSize");
                                switch (name)
                                {
                                    case ":method":
                                        if (method != null)
                                            throw new ProtocolViolationException(":method");
                                        method = value;
                                        break;
                                    case ":scheme":
                                        if (scheme != null)
                                            throw new ProtocolViolationException(":scheme");
                                        scheme = value;
                                        break;
                                    case ":authority":
                                        if (authority != null)
                                            throw new ProtocolViolationException(":authority");
                                        authority = value;
                                        break;
                                    case ":path":
                                        if (path != null)
                                            throw new ProtocolViolationException(":path");
                                        path = value;
                                        break;
                                    default:
                                        request.Headers.Add(name, value);
                                        break;
                                }
                                state = State.Ready;
                                break;
                            }
                        case State.SizeUpdate://001
                            throw new ProtocolViolationException("SizeUpdate");
                        default:
                            throw new InvalidOperationException("Never");
                    }

                    if (offset == _headers)
                        break;
                }
                if (method == null || scheme == null || authority == null || path == null || state != State.Ready)
                    throw new ProtocolViolationException("BadRequest");
                //method
                switch (method)
                {
                    case "GET":
                        request.Method = HttpMethod.Get;
                        break;
                    case "POST":
                        request.Method = HttpMethod.Post;
                        break;
                    case "OPTIONS":
                        request.Method = HttpMethod.Options;
                        break;
                    case "HEAD":
                        request.Method = HttpMethod.Head;
                        break;
                    case "DELETE":
                        request.Method = HttpMethod.Delete;
                        break;
                    case "PUT":
                        request.Method = HttpMethod.Put;
                        break;
                    default:
                        throw new ProtocolViolationException("BadRequest");
                }
                //url
                request.Url.Scheme = scheme;
                request.Url.Authority = authority;
                request.Url.AbsolutePath = path;
                request.Version = HttpVersion.Version20;
            }
            private static int ReadHpack(ReadOnlySpan<byte> headersBytes, int prefix, ref int offset)
            {
                long value = prefix;
                var bits = 0;
                while (bits < 32)
                {
                    var b = headersBytes[offset++];
                    //value = value + (b & 0b0111_1111) << bits; 运算符优先级
                    value += (b & 0b0111_1111) << bits;
                    if ((b & 0b1000_0000) == 0)
                        break;
                    bits += 7;
                }
                if (value > int.MaxValue)
                    throw new ProtocolViolationException("> int.MaxValue");

                return (int)value;
            }
            private static string ReadHuffman(ReadOnlySpan<byte> bytes)
            {
                if (bytes.IsEmpty)
                    return string.Empty;

                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    var i = 0;
                    var lastDecodedBits = 0;
                    while (i < bytes.Length)
                    {
                        var next = (uint)(bytes[i] << 24 + lastDecodedBits);
                        next |= (i + 1 < bytes.Length ? (uint)(bytes[i + 1] << 16 + lastDecodedBits) : 0);
                        next |= (i + 2 < bytes.Length ? (uint)(bytes[i + 2] << 8 + lastDecodedBits) : 0);
                        next |= (i + 3 < bytes.Length ? (uint)(bytes[i + 3] << lastDecodedBits) : 0);
                        next |= (i + 4 < bytes.Length ? (uint)(bytes[i + 4] >> (8 - lastDecodedBits)) : 0);

                        var ones = (uint)(int.MinValue >> (8 - lastDecodedBits - 1));
                        if (i == bytes.Length - 1 && lastDecodedBits > 0 && (next & ones) == ones)
                            break;

                        var validBits = Math.Min(30, (8 - lastDecodedBits) + (bytes.Length - i - 1) * 8);

                        var ch = -1;
                        var decodedBits = 0;
                        var codeMax = 0;
                        for (var j = 0; j < _DecodingTable.Length && _DecodingTable[j].codeLength <= validBits; j++)
                        {
                            var (codeLength, codes) = _DecodingTable[j];

                            if (j > 0)
                            {
                                codeMax <<= codeLength - _DecodingTable[j - 1].codeLength;
                            }

                            codeMax += codes.Length;

                            var mask = int.MinValue >> (codeLength - 1);
                            var masked = (next & mask) >> (32 - codeLength);

                            if (masked < codeMax)
                            {
                                decodedBits = codeLength;
                                ch = codes[codes.Length - (codeMax - masked)];
                                break;
                            }
                        }

                        if (ch == -1)
                            throw new InvalidDataException("Huffman");
                        else if (ch == 256)
                            throw new InvalidDataException("Huffman");

                        sb.Write((char)ch);

                        lastDecodedBits += decodedBits;
                        i += lastDecodedBits / 8;

                        lastDecodedBits %= 8;
                    }
                    return sb.ToString();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            private static string ReadLiteral(ReadOnlySpan<byte> headersBytes, ref int offset)
            {
                var @byte = headersBytes[offset++];
                if ((@byte & 0b1000_0000) == 0b1000_0000)//huffman
                {
                    var prefix = @byte & 0b0111_1111;
                    var length = prefix == 0b0111_1111 ? ReadHpack(headersBytes, prefix, ref offset) : prefix;

                    var huffmanBytes = headersBytes.Slice(offset, length);
                    offset += length;
                    return ReadHuffman(huffmanBytes);
                }
                else
                {
                    var prefix = @byte & 0b0111_1111;
                    var length = prefix == 0b0111_1111 ? ReadHpack(headersBytes, prefix, ref offset) : prefix;

                    var literalBytes = headersBytes.Slice(offset, length);
                    offset += length;
                    return literalBytes.ToByteString();
                }
            }
            public Task HandleAsync()
            {
                _handleWaiter = new TaskCompletionSource<object>();
                //_readTask = ReadAsync();
                //_writeTask = WriteAsync();
                var readTask = new Task<Task>(() => ReadAsync());
                var writeTask = new Task<Task>(() => WriteAsync());
                _readTask = readTask.Unwrap();
                _writeTask = writeTask.Unwrap();
                readTask.Start();
                writeTask.Start();
                return _handleWaiter.Task;
            }

            #region IConnection
            public PropertyCollection<IConnection> Properties => _connection.Properties;
            public EndPoint LocalEndPoint => _connection.LocalEndPoint;
            public EndPoint RemoteEndPoint => _connection.RemoteEndPoint;
            public bool Connected => _connection.Connected;
            public ISecurity Security => _connection.Security;
            public async ValueTask<int> KeepAliveAsync(Memory<byte> buffer)
            {
                var valueTask = _connection.ReceiveAsync(buffer);
                if (valueTask.IsCompleted)
                    return valueTask.Result;

                var task = valueTask.AsTask();
                try
                {
                    return await task.Timeout(_service._keepAliveQueue);
                }
                catch (TimeoutException)
                {
                    _connection.Close();
                    return await task;
                }
            }
            public int Receive(Span<byte> buffer)
            {
                unsafe
                {
                    fixed (byte* pBytes = buffer)
                    {
                        var valueTask = _connection.ReceiveAsync(new UnmanagedMemory<byte>(pBytes, buffer.Length));
                        if (valueTask.IsCompleted)
                            return valueTask.Result;

                        var task = valueTask.AsTask();
                        try
                        {
                            return task.Timeout(_service._keepAliveQueue).Result;
                        }
                        catch (TimeoutException)
                        {
                            _connection.Close();
                            return task.Result;
                        }
                    }
                }
            }
            public int Receive(byte[] buffer, int offset, int count) 
            {
                var valueTask = _connection.ReceiveAsync(buffer, offset, count);
                if (valueTask.IsCompleted)
                    return valueTask.Result;

                var task = valueTask.AsTask();
                try
                {
                    return task.Timeout(_service._keepAliveQueue).Result;
                }
                catch (TimeoutException)
                {
                    _connection.Close();
                    return task.Result;
                }
            }
            public async ValueTask<int> ReceiveAsync(Memory<byte> buffer)
            {
                var valueTask = _connection.ReceiveAsync(buffer);
                if (valueTask.IsCompleted)
                    return valueTask.Result;

                var task = valueTask.AsTask();
                try
                {
                    return await task.Timeout(_service._receiveQueue);
                }
                catch (TimeoutException)
                {
                    _connection.Close();
                    return await task;
                }
            }
            public async ValueTask<int> ReceiveAsync(byte[] buffer, int offset, int count)
            {
                var valueTask = _connection.ReceiveAsync(buffer, offset, count);
                if (valueTask.IsCompleted)
                    return valueTask.Result;

                var task = valueTask.AsTask();
                try
                {
                    return await task.Timeout(_service._receiveQueue);
                }
                catch (TimeoutException)
                {
                    _connection.Close();
                    return await task;
                }
            }
            public void Send(ReadOnlySpan<byte> buffer) 
            {
                unsafe
                {
                    fixed (byte* pBytes = buffer)
                    {
                        var task = _connection.SendAsync(new UnmanagedMemory<byte>(pBytes, buffer.Length));
                        try
                        {
                            task.Timeout(_service._sendQueue).Wait();
                        }
                        catch (TimeoutException)
                        {
                            _connection.Close();
                            task.Wait();
                        }
                    }
                }
            }
            public void Send(byte[] buffer, int offset, int count) 
            {
                var task = _connection.SendAsync(buffer, offset, count);
                try
                {
                    task.Timeout(_service._sendQueue).Wait();
                }
                catch (TimeoutException)
                {
                    _connection.Close();
                    task.Wait();
                }
            }
            public async Task SendAsync(ReadOnlyMemory<byte> buffer)
            {
                var task = _connection.SendAsync(buffer);
                try
                {
                    await task.Timeout(_service._sendQueue);
                }
                catch (TimeoutException)
                {
                    _connection.Close();
                    await task;
                }
            }
            public async Task SendAsync(byte[] buffer, int offset, int count)
            {
                var task = _connection.SendAsync(buffer, offset, count);
                try
                {
                    await task.Timeout(_service._sendQueue);
                }
                catch (TimeoutException)
                {
                    _connection.Close();
                    await task;
                }
            }
            public void SendFile(string fileName) => _connection.SendFile(fileName);
            public Task SendFileAsync(string fileName) => _connection.SendFileAsync(fileName);
            public void Close() => _connection.Close();
            #endregion
        }
        public Task HandleAsync(IConnection connection)
        {
            return new Connection(this, connection).HandleAsync();
        }
    }
}
