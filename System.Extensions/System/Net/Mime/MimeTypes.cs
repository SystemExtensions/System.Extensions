
namespace System.Net.Mime
{
    using System.IO;
    using System.Diagnostics;
    using System.Collections;
    using System.Collections.Generic;
    [DebuggerDisplay("Count = {Count}")]
    [DebuggerTypeProxy(typeof(DebugView))]
    public sealed class MimeTypes : IEnumerable<KeyValuePair<string, string>>
    {
        private static EqualityComparer _Comparer;
        private static MimeTypes _Default;
        public static MimeTypes Default => _Default;
        //public static MimeTypes Images => ;?
        //public static MimeTypes Texts => ;?
        static MimeTypes()
        {
            _Comparer = new EqualityComparer();
            _Default = new MimeTypes(512) {
                { ".323", "text/h323" },
                { ".3g2", "video/3gpp2" },
                { ".3gp2", "video/3gpp2" },
                { ".3gp", "video/3gpp" },
                { ".3gpp", "video/3gpp" },
                { ".aac", "audio/aac" },
                { ".aaf", "application/octet-stream" },
                { ".aca", "application/octet-stream" },
                { ".accdb", "application/msaccess" },
                { ".accde", "application/msaccess" },
                { ".accdt", "application/msaccess" },
                { ".acx", "application/internet-property-stream" },
                { ".adt", "audio/vnd.dlna.adts" },
                { ".adts", "audio/vnd.dlna.adts" },
                { ".afm", "application/octet-stream" },
                { ".ai", "application/postscript" },
                { ".aif", "audio/x-aiff" },
                { ".aifc", "audio/aiff" },
                { ".aiff", "audio/aiff" },
                { ".appcache", "text/cache-manifest" },
                { ".application", "application/x-ms-application" },
                { ".art", "image/x-jg" },
                { ".asd", "application/octet-stream" },
                { ".asf", "video/x-ms-asf" },
                { ".asi", "application/octet-stream" },
                { ".asm", "text/plain" },
                { ".asr", "video/x-ms-asf" },
                { ".asx", "video/x-ms-asf" },
                { ".atom", "application/atom+xml" },
                { ".au", "audio/basic" },
                { ".avi", "video/x-msvideo" },
                { ".axs", "application/olescript" },
                { ".bas", "text/plain" },
                { ".bcpio", "application/x-bcpio" },
                { ".bin", "application/octet-stream" },
                { ".bmp", "image/bmp" },
                { ".c", "text/plain" },
                { ".cab", "application/vnd.ms-cab-compressed" },
                { ".calx", "application/vnd.ms-office.calx" },
                { ".cat", "application/vnd.ms-pki.seccat" },
                { ".cdf", "application/x-cdf" },
                { ".chm", "application/octet-stream" },
                { ".class", "application/x-java-applet" },
                { ".clp", "application/x-msclip" },
                { ".cmx", "image/x-cmx" },
                { ".cnf", "text/plain" },
                { ".cod", "image/cis-cod" },
                { ".cpio", "application/x-cpio" },
                { ".cpp", "text/plain" },
                { ".crd", "application/x-mscardfile" },
                { ".crl", "application/pkix-crl" },
                { ".crt", "application/x-x509-ca-cert" },
                { ".csh", "application/x-csh" },
                { ".css", "text/css" },
                { ".csv", "application/octet-stream" },
                { ".cur", "application/octet-stream" },
                { ".dcr", "application/x-director" },
                { ".deploy", "application/octet-stream" },
                { ".der", "application/x-x509-ca-cert" },
                { ".dib", "image/bmp" },
                { ".dir", "application/x-director" },
                { ".disco", "text/xml" },
                { ".dlm", "text/dlm" },
                { ".doc", "application/msword" },
                { ".docm", "application/vnd.ms-word.document.macroEnabled.12" },
                { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
                { ".dot", "application/msword" },
                { ".dotm", "application/vnd.ms-word.template.macroEnabled.12" },
                { ".dotx", "application/vnd.openxmlformats-officedocument.wordprocessingml.template" },
                { ".dsp", "application/octet-stream" },
                { ".dtd", "text/xml" },
                { ".dvi", "application/x-dvi" },
                { ".dvr-ms", "video/x-ms-dvr" },
                { ".dwf", "drawing/x-dwf" },
                { ".dwp", "application/octet-stream" },
                { ".dxr", "application/x-director" },
                { ".eml", "message/rfc822" },
                { ".emz", "application/octet-stream" },
                { ".eot", "application/vnd.ms-fontobject" },
                { ".eps", "application/postscript" },
                { ".etx", "text/x-setext" },
                { ".evy", "application/envoy" },
                { ".fdf", "application/vnd.fdf" },
                { ".fif", "application/fractals" },
                { ".fla", "application/octet-stream" },
                { ".flr", "x-world/x-vrml" },
                { ".flv", "video/x-flv" },
                { ".gif", "image/gif" },
                { ".gtar", "application/x-gtar" },
                { ".gz", "application/x-gzip" },
                { ".h", "text/plain" },
                { ".hdf", "application/x-hdf" },
                { ".hdml", "text/x-hdml" },
                { ".hhc", "application/x-oleobject" },
                { ".hhk", "application/octet-stream" },
                { ".hhp", "application/octet-stream" },
                { ".hlp", "application/winhlp" },
                { ".hqx", "application/mac-binhex40" },
                { ".hta", "application/hta" },
                { ".htc", "text/x-component" },
                { ".htm", "text/html" },
                { ".html", "text/html" },
                { ".htt", "text/webviewhtml" },
                { ".hxt", "text/html" },
                { ".ical", "text/calendar" },
                { ".icalendar", "text/calendar" },
                { ".ico", "image/x-icon" },
                { ".ics", "text/calendar" },
                { ".ief", "image/ief" },
                { ".ifb", "text/calendar" },
                { ".iii", "application/x-iphone" },
                { ".inf", "application/octet-stream" },
                { ".ins", "application/x-internet-signup" },
                { ".isp", "application/x-internet-signup" },
                { ".IVF", "video/x-ivf" },
                { ".jar", "application/java-archive" },
                { ".java", "application/octet-stream" },
                { ".jck", "application/liquidmotion" },
                { ".jcz", "application/liquidmotion" },
                { ".jfif", "image/pjpeg" },
                { ".jpb", "application/octet-stream" },
                { ".jpe", "image/jpeg" },
                { ".jpeg", "image/jpeg" },
                { ".jpg", "image/jpeg" },
                { ".js", "application/javascript" },
                { ".json", "application/json" },
                { ".jsx", "text/jscript" },
                { ".latex", "application/x-latex" },
                { ".lit", "application/x-ms-reader" },
                { ".lpk", "application/octet-stream" },
                { ".lsf", "video/x-la-asf" },
                { ".lsx", "video/x-la-asf" },
                { ".lzh", "application/octet-stream" },
                { ".m13", "application/x-msmediaview" },
                { ".m14", "application/x-msmediaview" },
                { ".m1v", "video/mpeg" },
                { ".m2ts", "video/vnd.dlna.mpeg-tts" },
                { ".m3u", "audio/x-mpegurl" },
                { ".m3u8","audio/x-mpegurl"},
                { ".m4a", "audio/mp4" },
                { ".m4v", "video/mp4" },
                { ".man", "application/x-troff-man" },
                { ".manifest", "application/x-ms-manifest" },
                { ".map", "text/plain" },
                { ".markdown", "text/markdown" },
                { ".md", "text/markdown" },
                { ".mdb", "application/x-msaccess" },
                { ".mdp", "application/octet-stream" },
                { ".me", "application/x-troff-me" },
                { ".mht", "message/rfc822" },
                { ".mhtml", "message/rfc822" },
                { ".mid", "audio/mid" },
                { ".midi", "audio/mid" },
                { ".mix", "application/octet-stream" },
                { ".mmf", "application/x-smaf" },
                { ".mno", "text/xml" },
                { ".mny", "application/x-msmoney" },
                { ".mov", "video/quicktime" },
                { ".movie", "video/x-sgi-movie" },
                { ".mp2", "video/mpeg" },
                { ".mp3", "audio/mpeg" },
                { ".mp4", "video/mp4" },
                { ".mp4v", "video/mp4" },
                { ".mpa", "video/mpeg" },
                { ".mpe", "video/mpeg" },
                { ".mpeg", "video/mpeg" },
                { ".mpg", "video/mpeg" },
                { ".mpp", "application/vnd.ms-project" },
                { ".mpv2", "video/mpeg" },
                { ".ms", "application/x-troff-ms" },
                { ".msi", "application/octet-stream" },
                { ".mso", "application/octet-stream" },
                { ".mvb", "application/x-msmediaview" },
                { ".mvc", "application/x-miva-compiled" },
                { ".nc", "application/x-netcdf" },
                { ".nsc", "video/x-ms-asf" },
                { ".nws", "message/rfc822" },
                { ".ocx", "application/octet-stream" },
                { ".oda", "application/oda" },
                { ".odc", "text/x-ms-odc" },
                { ".ods", "application/oleobject" },
                { ".oga", "audio/ogg" },
                { ".ogg", "video/ogg" },
                { ".ogv", "video/ogg" },
                { ".ogx", "application/ogg" },
                { ".one", "application/onenote" },
                { ".onea", "application/onenote" },
                { ".onetoc", "application/onenote" },
                { ".onetoc2", "application/onenote" },
                { ".onetmp", "application/onenote" },
                { ".onepkg", "application/onenote" },
                { ".osdx", "application/opensearchdescription+xml" },
                { ".otf", "font/otf" },
                { ".p10", "application/pkcs10" },
                { ".p12", "application/x-pkcs12" },
                { ".p7b", "application/x-pkcs7-certificates" },
                { ".p7c", "application/pkcs7-mime" },
                { ".p7m", "application/pkcs7-mime" },
                { ".p7r", "application/x-pkcs7-certreqresp" },
                { ".p7s", "application/pkcs7-signature" },
                { ".pbm", "image/x-portable-bitmap" },
                { ".pcx", "application/octet-stream" },
                { ".pcz", "application/octet-stream" },
                { ".pdf", "application/pdf" },
                { ".pfb", "application/octet-stream" },
                { ".pfm", "application/octet-stream" },
                { ".pfx", "application/x-pkcs12" },
                { ".pgm", "image/x-portable-graymap" },
                { ".pko", "application/vnd.ms-pki.pko" },
                { ".pma", "application/x-perfmon" },
                { ".pmc", "application/x-perfmon" },
                { ".pml", "application/x-perfmon" },
                { ".pmr", "application/x-perfmon" },
                { ".pmw", "application/x-perfmon" },
                { ".png", "image/png" },
                { ".pnm", "image/x-portable-anymap" },
                { ".pnz", "image/png" },
                { ".pot", "application/vnd.ms-powerpoint" },
                { ".potm", "application/vnd.ms-powerpoint.template.macroEnabled.12" },
                { ".potx", "application/vnd.openxmlformats-officedocument.presentationml.template" },
                { ".ppam", "application/vnd.ms-powerpoint.addin.macroEnabled.12" },
                { ".ppm", "image/x-portable-pixmap" },
                { ".pps", "application/vnd.ms-powerpoint" },
                { ".ppsm", "application/vnd.ms-powerpoint.slideshow.macroEnabled.12" },
                { ".ppsx", "application/vnd.openxmlformats-officedocument.presentationml.slideshow" },
                { ".ppt", "application/vnd.ms-powerpoint" },
                { ".pptm", "application/vnd.ms-powerpoint.presentation.macroEnabled.12" },
                { ".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation" },
                { ".prf", "application/pics-rules" },
                { ".prm", "application/octet-stream" },
                { ".prx", "application/octet-stream" },
                { ".ps", "application/postscript" },
                { ".psd", "application/octet-stream" },
                { ".psm", "application/octet-stream" },
                { ".psp", "application/octet-stream" },
                { ".pub", "application/x-mspublisher" },
                { ".qt", "video/quicktime" },
                { ".qtl", "application/x-quicktimeplayer" },
                { ".qxd", "application/octet-stream" },
                { ".ra", "audio/x-pn-realaudio" },
                { ".ram", "audio/x-pn-realaudio" },
                { ".rar", "application/octet-stream" },
                { ".ras", "image/x-cmu-raster" },
                { ".rf", "image/vnd.rn-realflash" },
                { ".rgb", "image/x-rgb" },
                { ".rm", "application/vnd.rn-realmedia" },
                { ".rmi", "audio/mid" },
                { ".roff", "application/x-troff" },
                { ".rpm", "audio/x-pn-realaudio-plugin" },
                { ".rtf", "application/rtf" },
                { ".rtx", "text/richtext" },
                { ".scd", "application/x-msschedule" },
                { ".sct", "text/scriptlet" },
                { ".sea", "application/octet-stream" },
                { ".setpay", "application/set-payment-initiation" },
                { ".setreg", "application/set-registration-initiation" },
                { ".sgml", "text/sgml" },
                { ".sh", "application/x-sh" },
                { ".shar", "application/x-shar" },
                { ".sit", "application/x-stuffit" },
                { ".sldm", "application/vnd.ms-powerpoint.slide.macroEnabled.12" },
                { ".sldx", "application/vnd.openxmlformats-officedocument.presentationml.slide" },
                { ".smd", "audio/x-smd" },
                { ".smi", "application/octet-stream" },
                { ".smx", "audio/x-smd" },
                { ".smz", "audio/x-smd" },
                { ".snd", "audio/basic" },
                { ".snp", "application/octet-stream" },
                { ".spc", "application/x-pkcs7-certificates" },
                { ".spl", "application/futuresplash" },
                { ".spx", "audio/ogg" },
                { ".src", "application/x-wais-source" },
                { ".ssm", "application/streamingmedia" },
                { ".sst", "application/vnd.ms-pki.certstore" },
                { ".stl", "application/vnd.ms-pki.stl" },
                { ".sv4cpio", "application/x-sv4cpio" },
                { ".sv4crc", "application/x-sv4crc" },
                { ".svg", "image/svg+xml" },
                { ".svgz", "image/svg+xml" },
                { ".swf", "application/x-shockwave-flash" },
                { ".t", "application/x-troff" },
                { ".tar", "application/x-tar" },
                { ".tcl", "application/x-tcl" },
                { ".tex", "application/x-tex" },
                { ".texi", "application/x-texinfo" },
                { ".texinfo", "application/x-texinfo" },
                { ".tgz", "application/x-compressed" },
                { ".thmx", "application/vnd.ms-officetheme" },
                { ".thn", "application/octet-stream" },
                { ".tif", "image/tiff" },
                { ".tiff", "image/tiff" },
                { ".toc", "application/octet-stream" },
                { ".tr", "application/x-troff" },
                { ".trm", "application/x-msterminal" },
                { ".ts", "video/vnd.dlna.mpeg-tts" },
                { ".tsv", "text/tab-separated-values" },
                { ".ttc", "application/x-font-ttf" },
                { ".ttf", "application/x-font-ttf" },
                { ".tts", "video/vnd.dlna.mpeg-tts" },
                { ".txt", "text/plain" },
                { ".u32", "application/octet-stream" },
                { ".uls", "text/iuls" },
                { ".ustar", "application/x-ustar" },
                { ".vbs", "text/vbscript" },
                { ".vcf", "text/x-vcard" },
                { ".vcs", "text/plain" },
                { ".vdx", "application/vnd.ms-visio.viewer" },
                { ".vml", "text/xml" },
                { ".vsd", "application/vnd.visio" },
                { ".vss", "application/vnd.visio" },
                { ".vst", "application/vnd.visio" },
                { ".vsto", "application/x-ms-vsto" },
                { ".vsw", "application/vnd.visio" },
                { ".vsx", "application/vnd.visio" },
                { ".vtx", "application/vnd.visio" },
                { ".wav", "audio/wav" },
                { ".wax", "audio/x-ms-wax" },
                { ".wbmp", "image/vnd.wap.wbmp" },
                { ".wcm", "application/vnd.ms-works" },
                { ".wdb", "application/vnd.ms-works" },
                { ".webm", "video/webm" },
                { ".webp", "image/webp" },
                { ".wks", "application/vnd.ms-works" },
                { ".wm", "video/x-ms-wm" },
                { ".wma", "audio/x-ms-wma" },
                { ".wmd", "application/x-ms-wmd" },
                { ".wmf", "application/x-msmetafile" },
                { ".wml", "text/vnd.wap.wml" },
                { ".wmlc", "application/vnd.wap.wmlc" },
                { ".wmls", "text/vnd.wap.wmlscript" },
                { ".wmlsc", "application/vnd.wap.wmlscriptc" },
                { ".wmp", "video/x-ms-wmp" },
                { ".wmv", "video/x-ms-wmv" },
                { ".wmx", "video/x-ms-wmx" },
                { ".wmz", "application/x-ms-wmz" },
                { ".woff", "application/font-woff" }, // https://www.w3.org/TR/WOFF/#appendix-b
                { ".woff2", "font/woff2" }, // https://www.w3.org/TR/WOFF2/#IMT
                { ".wps", "application/vnd.ms-works" },
                { ".wri", "application/x-mswrite" },
                { ".wrl", "x-world/x-vrml" },
                { ".wrz", "x-world/x-vrml" },
                { ".wsdl", "text/xml" },
                { ".wtv", "video/x-ms-wtv" },
                { ".wvx", "video/x-ms-wvx" },
                { ".x", "application/directx" },
                { ".xaf", "x-world/x-vrml" },
                { ".xaml", "application/xaml+xml" },
                { ".xap", "application/x-silverlight-app" },
                { ".xbap", "application/x-ms-xbap" },
                { ".xbm", "image/x-xbitmap" },
                { ".xdr", "text/plain" },
                { ".xht", "application/xhtml+xml" },
                { ".xhtml", "application/xhtml+xml" },
                { ".xla", "application/vnd.ms-excel" },
                { ".xlam", "application/vnd.ms-excel.addin.macroEnabled.12" },
                { ".xlc", "application/vnd.ms-excel" },
                { ".xlm", "application/vnd.ms-excel" },
                { ".xls", "application/vnd.ms-excel" },
                { ".xlsb", "application/vnd.ms-excel.sheet.binary.macroEnabled.12" },
                { ".xlsm", "application/vnd.ms-excel.sheet.macroEnabled.12" },
                { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
                { ".xlt", "application/vnd.ms-excel" },
                { ".xltm", "application/vnd.ms-excel.template.macroEnabled.12" },
                { ".xltx", "application/vnd.openxmlformats-officedocument.spreadsheetml.template" },
                { ".xlw", "application/vnd.ms-excel" },
                { ".xml", "text/xml" },
                { ".xof", "x-world/x-vrml" },
                { ".xpm", "image/x-xpixmap" },
                { ".xps", "application/vnd.ms-xpsdocument" },
                { ".xsd", "text/xml" },
                { ".xsf", "text/xml" },
                { ".xsl", "text/xml" },
                { ".xslt", "text/xml" },
                { ".xsn", "application/octet-stream" },
                { ".xtp", "application/octet-stream" },
                { ".xwd", "image/x-xwindowdump" },
                { ".z", "application/x-compress" },
                { ".zip", "application/x-zip-compressed" },
            };
        }
        private class EqualityComparer : IEqualityComparer<(string, int)>
        {
            public bool Equals((string, int) x, (string, int) y)
            {
                (var xSource, var xOffset) = x;
                (var ySource, var yOffset) = y;
                return xSource.AsSpan(xOffset).Equals(ySource.AsSpan(yOffset), StringComparison.OrdinalIgnoreCase);
            }
            public int GetHashCode((string, int) obj)
            {
                (var objSource, var objOffset) = obj;
                return string.GetHashCode(objSource.AsSpan(objOffset), StringComparison.OrdinalIgnoreCase);
            }
        }
        public MimeTypes()
        {
            _mimeTypes = new Dictionary<(string,int), string>(256, _Comparer);
        }
        public MimeTypes(int capacity)
        {
            _mimeTypes = new Dictionary<(string, int), string>(capacity, _Comparer);
        }
        public MimeTypes(MimeTypes mimeTypes)
        {
            if (mimeTypes == null)
                throw new ArgumentNullException(nameof(mimeTypes));

            _defaultMimeType = mimeTypes._defaultMimeType;
            _mimeTypes = new Dictionary<(string, int), string>(mimeTypes._mimeTypes.Count, _Comparer);
            foreach (var mimeType in mimeTypes._mimeTypes)
            {
                _mimeTypes.Add(mimeType.Key, mimeType.Value);
            }
        }

        private string _defaultMimeType;
        private Dictionary<(string, int), string> _mimeTypes;
        public string this[string extName]
        {
            get
            {
                if (extName == null || extName.Length == 0)
                    return null;
                if (extName == "*")
                    return _defaultMimeType;

                if (extName[0] != '.')
                    return null;

                _mimeTypes.TryGetValue((extName, 1), out var mimeType);
                return mimeType;
            }
            set
            {
                if (extName == null || extName.Length == 0)
                    throw new ArgumentNullException(nameof(extName));
                if (value == null || value.Length == 0)
                    throw new ArgumentNullException(nameof(value));
                if (extName == "*")
                {
                    _defaultMimeType = value;
                    return;
                }
                if (extName[0] != '.')
                    throw new ArgumentException(nameof(extName));

                _mimeTypes.Add((extName, 1), value);
            }
        }
        public int Count
        {
            get
            {
                if (_defaultMimeType == null)
                    return _mimeTypes.Count;
                return _mimeTypes.Count + 1;
            }
        }
        public void Add(string extName, string mimeType)
        {
            if (extName == null || extName.Length == 0)
                throw new ArgumentNullException(nameof(extName));
            if (mimeType == null || mimeType.Length == 0)
                throw new ArgumentNullException(nameof(mimeType));
            if (extName == "*")
            {
                _defaultMimeType = mimeType;
                return;
            }
            if (extName[0] != '.')
                throw new ArgumentException(nameof(extName));

            _mimeTypes.Add((extName, 1), mimeType);
        }
        public bool Remove(string extName)
        {
            if (extName == null || extName.Length == 0)
                return false;
            if (extName == "*")
            {
                if (_defaultMimeType == null)
                    return false;
                _defaultMimeType = null;
                return true;
            }
            if (extName[0] != '.')
                return false;

            return _mimeTypes.Remove((extName, 1));
        }
        public bool TryGetValue(string fileName, out string mimeType)
        {
            if (fileName == null || fileName.Length == 0)
            {
                mimeType = null;
                return false;
            }
            var length = fileName.Length;
            var index = length;
            while (--index >= 0)
            {
                var c = fileName[index];
                if (c == '.')
                {
                    if (_mimeTypes.TryGetValue((fileName, index + 1), out mimeType))
                        return true;
                    break;
                }
                if (c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar || c == Path.VolumeSeparatorChar)
                {
                    break;
                }
            }
            if (_defaultMimeType != null)
            {
                mimeType = _defaultMimeType;
                return true;
            }
            mimeType = null;
            return false;
        }
        public void Clear()
        {
            _defaultMimeType = null;
            _mimeTypes.Clear();
        }
        public Enumerator GetEnumerator() => new Enumerator(this);
        IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public struct Enumerator: IEnumerator<KeyValuePair<string, string>>
        {
            internal Enumerator(MimeTypes mimeTypes)
            {
                _defaultMimeType = mimeTypes._defaultMimeType;
                _enumerator = mimeTypes._mimeTypes.GetEnumerator();
                _current = default;
            }

            private string _defaultMimeType;
            private KeyValuePair<string, string> _current;
            private Dictionary<(string, int), string>.Enumerator _enumerator;
            public KeyValuePair<string, string> Current => _current;
            object IEnumerator.Current => _current;
            public bool MoveNext()
            {
                if (_defaultMimeType != null)
                {
                    _current = new KeyValuePair<string, string>("*", _defaultMimeType);
                    _defaultMimeType = null;
                    return true;
                }
                if (_enumerator.MoveNext())
                {
                    var item = _enumerator.Current;
                    _current = new KeyValuePair<string, string>(item.Key.Item1, item.Value);
                    return true;
                }
                _current = default;
                return false;
            }
            public void Reset()
            {
                throw new NotSupportedException(nameof(Reset));
            }
            public void Dispose()
            {

            }
        }
        private class DebugView
        {
            public DebugView(MimeTypes mimeTypes)
            {
                _mimeTypes = mimeTypes;
            }
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private MimeTypes _mimeTypes;
            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            internal KeyValuePair<string, string>[] Items
            {
                get
                {
                    var items = new KeyValuePair<string, string>[_mimeTypes.Count];
                    var i = 0;
                    foreach (var item in _mimeTypes)
                    {
                        items[i++] = item;
                    }
                    return items;
                }
            }
        }
    }
}
