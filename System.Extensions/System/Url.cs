
namespace System
{
    using System.Net;
    using System.Text;
    using System.Buffers;
    using System.Reflection;
    public class Url
    {
        #region private
        private static readonly bool[,] _ccTLDs;
        private unsafe delegate bool Ipv4StringToAddress(ReadOnlySpan<char> ipSpan, out long address);
        private unsafe delegate bool Ipv6StringToAddress(ReadOnlySpan<char> ipSpan, Span<ushort> numbers, int numbersLength, out uint scope);
        private static Ipv4StringToAddress _Ipv4StringToAddress;
        private static Ipv6StringToAddress _Ipv6StringToAddress;
        //0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-_.~
        private static readonly bool[] _SafeChars = new bool[128] { false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, true, true, false, true, true, true, true, true, true, true, true, true, true, false, false, false, false, false, false, false, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, false, false, false, false, true, false, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, false, false, false, true, false };
        #endregion
        static Url()
        {
            //IPAddressParser
            foreach (var type in typeof(IPAddress).Assembly.GetTypes())
            {
                if (type.Name == "IPAddressParser")
                {
                    try
                    {
                        //https://github.com/dotnet/runtime/blob/master/src/libraries/System.Net.Primitives/src/System/Net/IPAddressParser.cs
                        _Ipv4StringToAddress = type.GetMethod(nameof(Ipv4StringToAddress)).CreateDelegate<Ipv4StringToAddress>();
                        _Ipv6StringToAddress = type.GetMethod(nameof(Ipv6StringToAddress)).CreateDelegate<Ipv6StringToAddress>();
                    }
                    catch 
                    {
                        Console.WriteLine("IPAddressParser");
                    }
                    break;
                }
            }

            _ccTLDs = new bool[26, 26]{
                {false,false,false,true,true,true,true,false,true,false,false,true,true,true,true,false,true,true,true,true,true,false,true,false,false,true},
                {true,true,false,true,true,true,true,true,true,true,false,false,true,true,true,false,false,true,true,true,false,true,true,false,true,true},
                {false,false,false,false,false,true,true,true,true,false,true,true,true,true,false,false,true,true,false,false,true,true,false,true,true,true},
                {false,false,false,false,true,false,false,false,false,true,true,false,true,false,true,false,false,false,false,false,false,false,false,false,false,true},
                {false,false,true,false,true,false,true,true,false,false,false,false,false,false,false,false,false,false,true,true,false,true,false,false,false,false},
                {false,false,false,false,false,false,false,false,true,true,true,false,true,false,true,false,false,true,false,false,false,false,false,false,false,false},
                {true,true,false,true,true,true,false,true,true,false,false,true,true,true,false,true,false,true,false,true,true,false,true,false,true,false},
                {false,false,false,false,false,false,false,false,false,false,true,false,true,true,false,false,false,true,false,true,true,false,false,false,false,false},
                {false,false,false,true,true,false,false,false,false,false,false,true,false,true,true,false,true,true,true,true,false,false,false,false,false,false},
                {false,false,false,false,false,false,false,false,false,false,false,false,true,false,true,true,false,false,false,false,false,false,false,false,false,false},
                {false,false,false,false,true,false,true,true,true,false,false,false,true,true,false,true,false,true,false,false,false,false,true,false,true,true},
                {true,true,true,false,false,false,false,false,true,false,true,false,false,false,false,false,false,true,true,true,true,true,false,false,true,false},
                {true,false,true,true,false,false,true,true,false,false,false,true,true,true,true,true,true,true,true,true,false,true,true,true,true,true},
                {true,false,true,false,true,true,true,false,true,false,false,true,false,false,true,true,false,true,false,true,true,false,false,false,false,true},
                {false,false,false,false,false,false,false,false,false,false,false,false,true,false,false,false,false,false,false,false,false,false,false,false,false,false},
                {true,false,false,false,true,true,true,true,false,false,true,true,true,true,false,false,false,true,false,true,false,false,true,false,true,false},
                {true,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false},
                {false,false,false,false,true,false,false,false,false,false,false,false,false,false,true,false,false,false,false,false,true,false,true,false,false,false},
                {true,true,true,true,true,false,true,true,true,true,true,true,true,true,true,false,false,true,false,true,true,false,false,false,true,true},
                {false,false,true,true,false,true,true,true,false,true,true,false,true,true,true,true,false,true,false,true,false,false,true,false,false,true},
                {true,false,false,false,false,false,true,false,false,false,true,false,false,false,false,false,false,false,false,false,false,false,false,false,true,false},
                {true,false,true,false,true,false,true,false,false,false,false,false,false,true,false,false,false,false,false,false,true,false,false,false,false,false},
                {false,false,false,false,false,true,false,false,false,false,false,false,false,false,false,false,false,false,true,false,false,false,false,false,false,false},
                {false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false},
                {false,false,false,false,true,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,true,false,false,false,false,false},
                {true,false,false,false,false,false,false,false,false,false,false,false,true,false,false,false,false,true,false,false,false,false,true,false,false,false}};
            //co us tv me ca
            //string ntlds = "ad,ae,af,ag,ai,al,am,an,ao,aq,ar,as,at,au,aw,az,ba,bb,bd,be,bf,bg,bh,bi," +
            //    "bj,bm,bn,bo,br,bs,bt,bv,bw,by,bz,cf,cg,ch,ci,ck,cl,cm,cn,cq,cr,cu,cv,cx,cy,cz,de,dj,dk,dm,do," +
            //    "dz,ec,ee,eg,eh,es,et,ev,fi,fj,fk,fm,fo,fr,ga,gb,gd,ge,gf,gh,gi,gl,gm,gn,gp,gr,gt,gu,gw,gy,hk,hm," +
            //    "hn,hr,ht,hu,id,ie,il,in,io,iq,ir,is,it,jm,jo,jp,ke,kg,kh,ki,km,kn,kp,kr,kw,ky,kz,la,lb,lc,li,lk,lr," +
            //    "ls,lt,lu,lv,ly,ma,mc,md,mg,mh,ml,mm,mn,mo,mp,mq,mr,ms,mt,mv,mw,mx,my,mz,na,nc,ne,nf,ng,ni,nl,no,np,nr," +
            //    "nt,nu,nz,om,pa,pe,pf,pg,ph,pk,pl,pm,pn,pr,pt,pw,py,qa,re,ro,ru,rw,sa,sb,sc,sd,se,sg,sh,si,sj,sk,sl,sm,sn," +
            //    "so,sr,st,su,sy,sz,tc,td,tf,tg,th,tj,tk,tm,tn,to,tp,tr,tt,tw,tz,ua,ug,uk,uy,va,vc,ve,vg,vn,vu,wf,ws,ye,yu,za,zm,zr,zw";
            //var ntldsArray = ntlds.Split(',');
            //foreach (var item in ntldsArray)
            //{
            //    _ccTLDs[item[0] - 'a', item[1] - 'a'] = true;
            //}
        }

        #region const
        private const int _MaxStackSize = 2048;//TODO? static int
        public const string SchemeHttp = "http";
        public const string SchemeHttps = "https";
        public const string SchemeDelimiter = "://";//private
        //public const char UserInfoDelimiter = '@';
        //public const char PortDelimiter = ':';
        //public const char PathDelimiter = '/';
        //public const char QueryDelimiter = '?';
        //public const char FragmentDelimiter = '#';
        #endregion

        #region private
        private string _scheme;
        private string _userInfo;
        private string _host;
        private int? _port;
        private string _path;
        private string _query;
        private string _fragment;
        private UriHostNameType _hostNameType;
        #endregion
        public Url()
        { }
        public Url(string urlString)
        {
            if (string.IsNullOrEmpty(urlString))
                return;

            if (urlString[0] == '/')
                AbsolutePath = urlString;
            else
                AbsoluteUri = urlString;
        }
        public Url(Url url) 
        {
            if (url == null)
                throw new ArgumentNullException(nameof(url));

            _scheme = url._scheme;
            _userInfo = url._userInfo;
            _host = url._host;
            _port = url._port;
            _path = url._path;
            _query = url._query;
            _fragment = url._fragment;
            _hostNameType = url._hostNameType;
        }
        //new Uri(baseUri, relativeUri)
        //public Url(Url baseUri, string relativeUri)
        //{
        //    if (baseUri == null)
        //        throw new ArgumentNullException(nameof(baseUri));
        //    if (string.IsNullOrEmpty(baseUri._scheme))
        //        throw new ArgumentException("baseUri must be absolute");

        //    if (string.IsNullOrEmpty(relativeUri))
        //    {
        //        _scheme = baseUri._scheme;
        //        _userInfo = baseUri._userInfo;
        //        _host = baseUri._host;
        //        _hostNameType = baseUri._hostNameType;
        //        _port = baseUri._port;
        //        _path = baseUri._path;
        //        _query = baseUri._query;
        //    }
        //    else if (relativeUri[0] == '/')
        //    {
        //        if (relativeUri.Length > 1 && relativeUri[1] == '/')
        //        {
        //            _scheme = baseUri._scheme;
        //            ParseSchemePart(relativeUri.AsSpan(2));
        //        }
        //        else
        //        {
        //            _scheme = baseUri._scheme;
        //            _userInfo = baseUri._userInfo;
        //            _host = baseUri._host;
        //            _hostNameType = baseUri._hostNameType;
        //            _port = baseUri._port;
        //            var queryIndex = -1;
        //            var fragmentIndex = -1;
        //            for (int i = 1; i < relativeUri.Length; i++)
        //            {
        //                char temp = relativeUri[i];
        //                if (temp > byte.MaxValue || temp <= ' ' || temp == 127)
        //                    throw new FormatException(nameof(relativeUri));

        //                if (temp == '?')
        //                {
        //                    if (queryIndex == -1)
        //                        queryIndex = i;
        //                }
        //                else if (temp == '#')
        //                {
        //                    if (queryIndex == -1)
        //                        queryIndex = -2;
        //                    if (fragmentIndex == -1)
        //                        fragmentIndex = i;
        //                }
        //            }
        //            if (queryIndex > 0)
        //            {
        //                _path = relativeUri.Substring(0, queryIndex);
        //                if (fragmentIndex > 0)
        //                {
        //                    _query = relativeUri.Substring(queryIndex, fragmentIndex - queryIndex);
        //                    _fragment = relativeUri.Substring(fragmentIndex);
        //                }
        //                else
        //                {
        //                    _query = relativeUri.Substring(queryIndex);
        //                }
        //            }
        //            else if (fragmentIndex > 0)
        //            {
        //                _path = relativeUri.Substring(0, fragmentIndex);
        //                _fragment = relativeUri.Substring(fragmentIndex);
        //            }
        //            else
        //            {
        //                _path = relativeUri;
        //            }
        //        }
        //    }
        //    else if (relativeUri[0] == '?')
        //    {
        //        _scheme = baseUri._scheme;
        //        _userInfo = baseUri._userInfo;
        //        _host = baseUri._host;
        //        _hostNameType = baseUri._hostNameType;
        //        _port = baseUri._port;
        //        _path = baseUri._path;
        //        var fragmentIndex = relativeUri.IndexOf('#');
        //        if (fragmentIndex == -1)
        //        {
        //            if (!IsQuery(relativeUri))
        //                throw new FormatException(nameof(relativeUri));
        //            _query = relativeUri;
        //        }
        //        else
        //        {
        //            var query = relativeUri.AsSpan(0, fragmentIndex);
        //            if (!IsQuery(query))
        //                throw new FormatException(nameof(relativeUri));
        //            _query = new string(query);
        //            var fragment = relativeUri.AsSpan(fragmentIndex);
        //            if (!IsFragment(fragment))
        //                throw new FormatException(nameof(relativeUri));
        //            _fragment = new string(fragment);
        //        }
        //    }
        //    else if (relativeUri[0] == '#')
        //    {
        //        _scheme = baseUri._scheme;
        //        _userInfo = baseUri._userInfo;
        //        _host = baseUri._host;
        //        _hostNameType = baseUri._hostNameType;
        //        _port = baseUri._port;
        //        _path = baseUri._path;
        //        _query = baseUri._query;
        //        if (!IsFragment(relativeUri))
        //            throw new FormatException(nameof(relativeUri));
        //        _fragment = relativeUri;
        //    }
        //    else
        //    {
        //        var schemeIndex = relativeUri.IndexOf(SchemeDelimiter);
        //        if (schemeIndex > 0)
        //        {
        //            var scheme = relativeUri.AsSpan(0, schemeIndex);
        //            if (IsScheme(scheme))
        //            {
        //                _scheme = new string(scheme);
        //                ParseSchemePart(relativeUri.AsSpan(schemeIndex + 3));
        //                return;
        //            }
        //        }
        //        _scheme = baseUri._scheme;
        //        _userInfo = baseUri._userInfo;
        //        _host = baseUri._host;
        //        _hostNameType = baseUri._hostNameType;
        //        _port = baseUri._port;
        //        var absPath = string.IsNullOrEmpty(baseUri._path) ?
        //            '/' + relativeUri : baseUri._path[baseUri._path.Length - 1] == '/'
        //            ? baseUri._path + relativeUri : baseUri._path + '/' + relativeUri;

        //        var queryIndex = -1;
        //        var fragmentIndex = -1;
        //        for (int i = 1; i < absPath.Length; i++)
        //        {
        //            char temp = absPath[i];
        //            if (temp > byte.MaxValue || temp <= ' ' || temp == 127)
        //                throw new FormatException(nameof(relativeUri));

        //            if (temp == '?')
        //            {
        //                if (queryIndex == -1)
        //                    queryIndex = i;
        //            }
        //            else if (temp == '#')
        //            {
        //                if (queryIndex == -1)
        //                    queryIndex = -2;
        //                if (fragmentIndex == -1)
        //                    fragmentIndex = i;
        //            }
        //        }
        //        ReadOnlySpan<char> path;
        //        if (queryIndex > 0)
        //        {
        //            path = absPath.AsSpan(0, queryIndex);
        //            if (fragmentIndex > 0)
        //            {
        //                _query = absPath.Substring(queryIndex, fragmentIndex - queryIndex);
        //                _fragment = absPath.Substring(fragmentIndex);
        //            }
        //            else
        //            {
        //                _query = absPath.Substring(queryIndex);
        //            }
        //        }
        //        else if (fragmentIndex > 0)
        //        {
        //            path = absPath.AsSpan(0, fragmentIndex);
        //            _fragment = absPath.Substring(fragmentIndex);
        //        }
        //        else
        //        {
        //            path = absPath;
        //        }
        //        //Relative path conversion
        //        var segments = new (int, int)[4];
        //        var index = 0;
        //        var offset = 0;
        //        for (int i = 1; i < path.Length; i++)
        //        {
        //            if (path[i] == '/')
        //            {
        //                var segment = path.Slice(offset, i - offset);
        //                if (segment.SequenceEqual("/."))
        //                {
        //                    offset = i;
        //                }
        //                else if (segment.SequenceEqual("/.."))
        //                {
        //                    if (index > 0)
        //                        index -= 1;
        //                    offset = i;
        //                }
        //                else
        //                {
        //                    if (index == segments.Length)
        //                        Array.Resize(ref segments, segments.Length * 2);

        //                    segments[index++] = (offset, i - offset);
        //                    offset = i;
        //                }
        //            }
        //        }
        //        var last = path.Slice(offset);
        //        if (last.SequenceEqual("/."))
        //        {
        //            last = "/";
        //        }
        //        else if (last.SequenceEqual("/.."))
        //        {
        //            if (index > 0)
        //                index -= 1;
        //            last = "/";
        //        }
        //        var length = last.Length;
        //        for (int i = 0; i < index; i++)
        //        {
        //            length += segments[i].Item2;
        //        }
        //        _path = new string('\0', length);
        //        unsafe
        //        {
        //            fixed (char* pData = _path)
        //            {
        //                var span = new Span<char>(pData, length);
        //                for (int i = 0; i < index; i++)
        //                {
        //                    var (startIndex, count) = segments[i];
        //                    path.Slice(startIndex, count).CopyTo(span);
        //                    span = span.Slice(count);
        //                }
        //                last.CopyTo(span);
        //            }
        //        }
        //    }
        //}
        public string Scheme
        {
            get
            {
                return _scheme;
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    _scheme = value;
                    return;
                }
                if (!IsScheme(value))
                    throw new ArgumentException(nameof(Scheme));

                _scheme = value;
            }
        }
        public string UserInfo
        {
            get
            {
                return _userInfo;
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    _userInfo = value;
                    return;
                }
                if (!IsUserInfo(value))
                    throw new ArgumentException(nameof(UserInfo));

                _userInfo = value;
            }
        }
        public string Host
        {
            get
            {
                return _host;
            }
            set
            {
                var hostSpan = value.AsSpan();
                if (hostSpan.Length == 0)
                {
                    _hostNameType = UriHostNameType.Unknown;
                    _host = value;
                    return;
                }
                if (IsIPv6(hostSpan))
                {
                    _hostNameType = UriHostNameType.IPv6;
                    _host = value.StartsWith('[') ? value : $"[{value}]";
                    return;
                }
                if (IsIPv4(hostSpan))
                {
                    _hostNameType = UriHostNameType.IPv4;
                    _host = value;
                    return;
                }
                if (IsDns(hostSpan))
                {
                    _hostNameType = UriHostNameType.Dns;
                    _host = value;
                    return;
                }
                throw new ArgumentException(nameof(Host));

                //TODO? >127=> Punycode
            }
        }
        public UriHostNameType HostNameType => _hostNameType;
        public int? Port
        {
            get { return _port; }
            set
            {
                if (value == null)
                {
                    _port = null;
                    return;
                }
                if (value < IPEndPoint.MinPort || value > IPEndPoint.MaxPort)
                    throw new ArgumentOutOfRangeException(nameof(Port));

                _port = value;
            }
        }
        public string Path
        {
            get
            {
                return _path;
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    _path = value;
                    return;
                }
                if (!IsPath(value))//TODO \ ex or replace(/)
                    throw new ArgumentException(nameof(Path));

                _path = value[0] == '/' ? value : '/' + value;
            }
        }
        public string Query
        {
            get
            {
                return _query;
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    _query = value;
                    return;
                }
                if (!IsQuery(value))
                    throw new ArgumentException(nameof(Query));

                _query = value[0] == '?' ? value : '?' + value;
            }
        }
        public string Fragment
        {
            get
            {
                return _fragment;
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    _fragment = value;
                    return;
                }
                if (!IsFragment(value))
                    throw new ArgumentException(nameof(Fragment));

                _fragment = value[0] == '#' ? value : '#' + value;
            }
        }
        public string Authority
        {
            get
            {
                if (string.IsNullOrEmpty(_host))
                    return null;

                if (string.IsNullOrEmpty(_userInfo) && !_port.HasValue)
                    return _host;

                //string.Concat
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    if (!string.IsNullOrEmpty(_userInfo))
                    {
                        sb.Write(_userInfo);
                        sb.Write('@');
                    }
                    sb.Write(_host);
                    if (_port.HasValue)
                    {
                        sb.Write(':');
                        sb.Write(_port.Value);
                    }
                    return sb.ToString();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    _userInfo = null;
                    _host = null;
                    _hostNameType = UriHostNameType.Unknown;
                    _port = null;
                    return;
                }

                var userInfoIndex = value.IndexOf('@');
                if (userInfoIndex == -1)
                {
                    _userInfo = null;
                }
                else
                {
                    _userInfo = value.Substring(0, userInfoIndex);
                    if (!IsUserInfo(_userInfo))
                        throw new FormatException(nameof(Authority));
                }
                userInfoIndex += 1;
                var hostPort = value.AsSpan(userInfoIndex);
                if (hostPort.IsEmpty)
                    throw new FormatException(nameof(Authority));

                if (hostPort[0] == '[')//IPv6
                {
                    var ipV6SepIndex = hostPort.IndexOf(']');
                    if (ipV6SepIndex == -1)
                        throw new FormatException(nameof(Authority));

                    var host = hostPort.Slice(0, ipV6SepIndex + 1);
                    var portSpan = hostPort.Slice(ipV6SepIndex + 1);
                    if (!IsIPv6(host))
                        throw new FormatException(nameof(Authority));
                    _hostNameType = UriHostNameType.IPv6;
                    if (portSpan.IsEmpty)
                    {
                        _host = userInfoIndex == 0 ? value : new string(host);
                        _port = null;
                        return;
                    }
                    else
                    {
                        if (portSpan[0] != ':')
                            throw new FormatException(nameof(Authority));

                        portSpan = portSpan.Slice(1);
                        if (portSpan.IsEmpty)
                            throw new FormatException(nameof(Authority));

                        if (!int.TryParse(portSpan, out var port))
                            throw new FormatException(nameof(Authority));
                        if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
                            throw new FormatException(nameof(Authority));
                        _host = new string(host);
                        _port = port;
                        return;
                    }
                }
                else
                {
                    var portIndex = hostPort.IndexOf(':');
                    if (portIndex == -1)
                    {
                        _port = null;
                        if ((hostPort[0] >= '0' && hostPort[0] <= '9') && IsIPv4(hostPort))
                        {
                            _hostNameType = UriHostNameType.IPv4;
                            _host = userInfoIndex == 0 ? value : new string(hostPort);
                            return;
                        }
                        else if (IsDns(hostPort))
                        {
                            _hostNameType = UriHostNameType.Dns;
                            _host = userInfoIndex == 0 ? value : new string(hostPort);
                            return;
                        }
                        throw new FormatException(nameof(Authority));
                    }
                    else
                    {
                        var host = hostPort.Slice(0, portIndex);
                        var portSpan = hostPort.Slice(portIndex + 1);
                        if (host.IsEmpty)
                            throw new FormatException(nameof(Authority));

                        if ((host[0] >= '0' && host[0] <= '9') && IsIPv4(host))
                        {
                            _hostNameType = UriHostNameType.IPv4;
                            _host = new string(host);
                        }
                        else if (IsDns(host))
                        {
                            _hostNameType = UriHostNameType.Dns;
                            _host = new string(host);
                        }
                        else
                        {
                            throw new FormatException(nameof(Authority));
                        }

                        if (portSpan.Length == 0)
                            throw new FormatException(nameof(Authority));

                        if (!int.TryParse(portSpan, out var port))
                            throw new FormatException(nameof(Authority));
                        if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
                            throw new FormatException(nameof(Authority));
                        _port = port;
                    }
                }
            }
        }
        public string AbsolutePath
        {
            get
            {
                if (string.IsNullOrEmpty(_path))
                    return null;

                if (string.IsNullOrEmpty(_query) && string.IsNullOrEmpty(_fragment))
                    return _path;

                return string.Concat(_path, _query, _fragment);
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    _path = null;
                    _query = null;
                    _fragment = null;
                    return;
                }
                if (value[0] != '/')
                    throw new FormatException(nameof(AbsolutePath));
                var queryIndex = -1;
                var fragmentIndex = -1;
                for (int i = 1; i < value.Length; i++)
                {
                    char temp = value[i];
                    if (temp > byte.MaxValue || temp <= ' ' || temp == 127)
                        throw new FormatException(nameof(AbsolutePath));

                    if (temp == '?')
                    {
                        if (queryIndex == -1)
                            queryIndex = i;
                    }
                    else if (temp == '#')
                    {
                        if (queryIndex == -1)
                            queryIndex = -2;
                        if (fragmentIndex == -1)
                            fragmentIndex = i;
                    }
                }
                if (queryIndex > 0)
                {
                    _path = value.Substring(0, queryIndex);
                    if (fragmentIndex > 0)
                    {
                        _query = value.Substring(queryIndex, fragmentIndex - queryIndex);
                        _fragment = value.Substring(fragmentIndex);
                    }
                    else
                    {
                        _query = value.Substring(queryIndex);
                        _fragment = null;
                    }
                }
                else if (fragmentIndex > 0)
                {
                    _path = value.Substring(0, fragmentIndex);
                    _query = null;
                    _fragment = value.Substring(fragmentIndex);
                }
                else
                {
                    _path = value;
                    _query = null;
                    _fragment = null;
                }
            }
        }
        public string AbsoluteUri
        {
            get
            {
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    if (!string.IsNullOrEmpty(_scheme))
                    {
                        sb.Write(_scheme);
                        sb.Write(SchemeDelimiter);
                        if (!string.IsNullOrEmpty(_host))
                        {
                            if (!string.IsNullOrEmpty(_userInfo))
                            {
                                sb.Write(_userInfo);
                                sb.Write('@');
                            }
                            sb.Write(_host);
                            if (_port.HasValue)
                            {
                                sb.Write(':');
                                sb.Write(_port.Value);
                            }
                        }
                    }
                    sb.Write(_path);
                    sb.Write(_query);
                    sb.Write(_fragment);

                    return sb.ToString();
                }
                finally
                {
                    disposable.Dispose();
                }
            }
            set
            {
                var schemeIndex = value.IndexOf(SchemeDelimiter);
                if (schemeIndex <= 0)
                    throw new FormatException(nameof(AbsoluteUri));

                _scheme = value.Substring(0, schemeIndex);
                if (!IsScheme(_scheme))
                    throw new FormatException(nameof(AbsoluteUri));


                ParseSchemePart(value.AsSpan(schemeIndex + 3));
            }
        }
        public string Domain 
        {
            get 
            {
                if (_host == null)
                    return null;

                if (_hostNameType != UriHostNameType.Dns)
                    return null;//TODO??? return _host;

                var dotIndex = _host.LastIndexOf('.');
                if (dotIndex == -1)
                    return null;

                var domain1 = _host.AsSpan(dotIndex + 1);
                if (domain1.Length == 2)
                {
                    var x = char.ToLower(domain1[0]) - 'a';
                    var y = char.ToLower(domain1[1]) - 'a';
                    if (x >= 0 && x < 26 && y >= 0 && y < 26 && _ccTLDs[x, y])
                    {
                        var dotIndex2 = _host.AsSpan(0, dotIndex).LastIndexOf('.');
                        if (dotIndex2 == -1)
                        {
                            var domain2 = _host.AsSpan(0, dotIndex);
                            if (domain2.EqualsIgnoreCase("co") || domain2.EqualsIgnoreCase("com") || domain2.EqualsIgnoreCase("net") || domain2.EqualsIgnoreCase("org") || domain2.EqualsIgnoreCase("gov"))
                            {
                                return null;
                            }
                            else
                            {
                                return _host;
                            }
                        }
                        else
                        {
                            var domain2 = _host.AsSpan(dotIndex2 + 1, dotIndex - dotIndex2 - 1);//co,com,net,org,gov
                            if (domain2.EqualsIgnoreCase("co") || domain2.EqualsIgnoreCase("com") || domain2.EqualsIgnoreCase("net") || domain2.EqualsIgnoreCase("org") || domain2.EqualsIgnoreCase("gov"))
                            {
                                dotIndex = dotIndex2;
                            }
                            else
                            {
                                return _host.Substring(dotIndex2 + 1);
                            }
                        }
                    }
                }
                dotIndex = _host.AsSpan(0, dotIndex).LastIndexOf('.');
                if (dotIndex == -1)
                    return _host;
                return _host.Substring(dotIndex + 1);
            }
        }
        public override string ToString() => AbsoluteUri;
        private void ParseSchemePart(ReadOnlySpan<char> schemePart)
        {
            var pathIndex = -1;
            var queryIndex = -1;
            var fragmentIndex = -1;
            for (int i = 0; i < schemePart.Length; i++)
            {
                char temp = schemePart[i];
                if (temp > byte.MaxValue || temp <= ' ' || temp == 127)
                    throw new FormatException(nameof(schemePart));

                if (temp == '/')
                {
                    if (pathIndex == -1)
                        pathIndex = i;
                }
                else if (temp == '?')
                {
                    if (pathIndex == -1)
                        pathIndex = -2;
                    if (queryIndex == -1)
                        queryIndex = i;
                }
                else if (temp == '#')
                {
                    if (pathIndex == -1)
                        pathIndex = -2;
                    if (queryIndex == -1)
                        queryIndex = -2;
                    if (fragmentIndex == -1)
                        fragmentIndex = i;
                }
            }
            var authorityIndex = pathIndex > 0 ? pathIndex : queryIndex > 0 ? queryIndex : fragmentIndex > 0 ? fragmentIndex : schemePart.Length;
            var authority = schemePart.Slice(0, authorityIndex);
            if (authority.IsEmpty)
            {
                _userInfo = null;
                _host = null;
                _hostNameType = UriHostNameType.Unknown;
                _port = null;
            }
            else
            {
                var userInfoIndex = authority.IndexOf('@');
                _userInfo = userInfoIndex == -1 ? null : new string(authority.Slice(0, userInfoIndex));
                userInfoIndex += 1;
                var hostPort = authority.Slice(userInfoIndex);
                if (hostPort.IsEmpty)
                    throw new FormatException(nameof(schemePart));
                if (hostPort[0] == '[')//IPv6
                {
                    var ipV6SepIndex = hostPort.IndexOf(']');
                    if (ipV6SepIndex == -1)
                        throw new FormatException(nameof(schemePart));

                    _host = new string(hostPort.Slice(0, ipV6SepIndex + 1));
                    var portSpan = hostPort.Slice(ipV6SepIndex + 1);
                    if (!IsIPv6(_host))
                        throw new FormatException(nameof(schemePart));
                    _hostNameType = UriHostNameType.IPv6;
                    if (portSpan.IsEmpty)
                    {
                        _port = null;
                    }
                    else
                    {
                        if (portSpan[0] != ':')
                            throw new FormatException(nameof(schemePart));

                        portSpan = portSpan.Slice(1);
                        if (portSpan.IsEmpty)
                            throw new FormatException(nameof(schemePart));

                        if (!int.TryParse(portSpan, out var portValue))
                            throw new FormatException(nameof(schemePart));
                        if (portValue < IPEndPoint.MinPort || portValue > IPEndPoint.MaxPort)
                            throw new FormatException(nameof(schemePart));
                        _port = portValue;
                    }
                }
                else
                {
                    var portIndex = hostPort.IndexOf(':');
                    if (portIndex == -1)
                    {
                        _port = null;
                        if ((hostPort[0] >= '0' && hostPort[0] <= '9') && IsIPv4(hostPort))
                        {
                            _hostNameType = UriHostNameType.IPv4;
                            _host = new string(hostPort);
                        }
                        else if (IsDns(hostPort))
                        {
                            _hostNameType = UriHostNameType.Dns;
                            _host = new string(hostPort);
                        }
                        else
                        {
                            throw new FormatException(nameof(schemePart));
                        }
                    }
                    else
                    {
                        var host = hostPort.Slice(0, portIndex);
                        var portSpan = hostPort.Slice(portIndex + 1);
                        if (host.IsEmpty)
                            throw new FormatException(nameof(schemePart));

                        if ((host[0] >= '0' && host[0] <= '9') && IsIPv4(host))
                        {
                            _hostNameType = UriHostNameType.IPv4;
                            _host = new string(host);
                        }
                        else if (IsDns(host))
                        {
                            _hostNameType = UriHostNameType.Dns;
                            _host = new string(host);
                        }
                        else
                        {
                            throw new FormatException(nameof(schemePart));
                        }

                        if (portSpan.IsEmpty)
                            throw new FormatException(nameof(schemePart));

                        if (!int.TryParse(portSpan, out var port))
                            throw new FormatException(nameof(schemePart));
                        if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
                            throw new FormatException(nameof(schemePart));
                        _port = port;
                    }
                }
            }

            if (pathIndex < 0)
            {
                _path = null;
                if (queryIndex > 0)
                {
                    if (fragmentIndex > 0)
                    {
                        _query = new string(schemePart.Slice(queryIndex, fragmentIndex - queryIndex));
                        _fragment = new string(schemePart.Slice(fragmentIndex));
                    }
                    else
                    {
                        _query = new string(schemePart.Slice(queryIndex));
                        _fragment = null;
                    }
                }
                else
                {
                    _query = null;
                    if (fragmentIndex > 0)
                    {
                        _fragment = new string(schemePart.Slice(fragmentIndex));
                    }
                    else
                    {
                        _fragment = null;
                    }
                }
            }
            else
            {
                if (queryIndex > 0)
                {
                    _path = new string(schemePart.Slice(pathIndex, queryIndex - pathIndex));
                    if (fragmentIndex > 0)
                    {
                        _query = new string(schemePart.Slice(queryIndex, fragmentIndex - queryIndex));
                        _fragment =new string(schemePart.Slice(fragmentIndex));
                    }
                    else
                    {
                        _query = new string(schemePart.Slice(queryIndex));
                        _fragment = null;
                    }
                }
                else
                {
                    _query = null;
                    if (fragmentIndex > 0)
                    {
                        _path = new string(schemePart.Slice(pathIndex, fragmentIndex - pathIndex));
                        _fragment = new string(schemePart.Slice(fragmentIndex));
                    }
                    else
                    {
                        _path = new string(schemePart.Slice(pathIndex));
                        _fragment = null;
                    }
                }
            }
        }
        public static bool IsScheme(ReadOnlySpan<char> scheme)
        {
            // alpha *(alpha | digit | '+' | '-' | '.')
            var c = scheme[0];
            if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')))
                return false;

            for (int i = scheme.Length - 1; i > 0; --i)
            {
                var ch = scheme[i];
                if (!((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z')
                    || (ch >= '0' && ch <= '9')
                    || ch == '+'
                    || ch == '-'
                    || ch == '.'))
                    return false;
            }
            return true;
        }
        public static bool IsUserInfo(ReadOnlySpan<char> userInfo)
        {
            var length = userInfo.Length;
            for (int i = 0; i < length; i++)
            {
                var temp = userInfo[i];
                //TODO 优化//Better
                if (temp > byte.MaxValue || temp <= ' ' || temp == 127
                    || temp == '@' || temp == '/' || temp == '?' || temp == '#')
                    return false;
            }
            return true;
        }
        public static bool IsPath(ReadOnlySpan<char> path)
        {
            var length = path.Length;
            for (int i = 0; i < length; i++)
            {
                var temp = path[i];
                if (temp > byte.MaxValue || temp <= ' ' || temp == 127
                    || temp == '?' || temp == '#')
                    return false;
            }
            return true;
        }
        public static bool IsQuery(ReadOnlySpan<char> query)
        {
            var length = query.Length;
            for (int i = 0; i < length; i++)
            {
                var temp = query[i];
                if (temp > byte.MaxValue || temp <= ' ' || temp == 127 || temp == '#')
                    return false;
            }
            return true;
        }
        public static bool IsFragment(ReadOnlySpan<char> fragment)
        {
            var length = fragment.Length;
            for (int i = 0; i < length; i++)
            {
                var temp = fragment[i];
                if (temp > byte.MaxValue || temp <= ' ' || temp == 127)
                    return false;
            }
            return true;
        }
        public static bool IsIPv6(ReadOnlySpan<char> host)
        {
            if (_Ipv6StringToAddress == null)
                return IPAddress.TryParse(host, out var _);

            const int IPv6AddressShorts = 8;
            unsafe
            {
                Span<ushort> numbers = stackalloc ushort[IPv6AddressShorts];
                numbers.Clear();
                return _Ipv6StringToAddress(host, numbers, IPv6AddressShorts, out uint scope);
            }
        }
        public static bool IsIPv4(ReadOnlySpan<char> host)
        {
            if (_Ipv4StringToAddress == null)
                return IPAddress.TryParse(host, out var _);

            return _Ipv4StringToAddress.Invoke(host, out var _);
        }
        public static bool IsDns(ReadOnlySpan<char> host)
        {
            //Lax TODO? Strict
            //<label> -> <alphanum> [<alphanum> | <hyphen> | <underscore>] * 62
            const int _Size = 62;
            var length = host.Length;
            if (length > 255)
                return false;

            var label = _Size;
            for (int i = 0; i < length;)
            {
                var ch = host[i++];
                if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || (ch >= 'A' && ch <= 'Z') || ch == '-' || ch == '_')
                {
                    if (label == 0)
                        return false;
                    label -= 1;
                    continue;
                }
                if (ch == '.')
                {
                    if (label == _Size)
                        return false;
                    label = _Size;
                    continue;
                }
                return false;
            }
            if (label == _Size)
                return false;
            return true;
        }
        public static string Encode(string stringToEncode)
        {
            if (stringToEncode == null)
                throw new ArgumentNullException(nameof(stringToEncode));

            var length = stringToEncode.Length;
            if (length == 0)
                return string.Empty;
            int safeCount = 0;
            unsafe
            {
                fixed (char* pSrc = stringToEncode)
                {
                    for (int i = 0; i < length; i++)
                    {
                        var temp = pSrc[i];
                        if (temp < 128 && _SafeChars[temp])
                            safeCount++;
                    }
                    if (safeCount == length)
                        return stringToEncode;
                    int byteCount = Encoding.UTF8.GetByteCount(pSrc,length);
                    var value = new string('\0', byteCount + (byteCount - safeCount) * 2);
                    fixed (char* pDest = value)
                    {
                        var bytes = (byte*)pDest + (value.Length * 2 - byteCount);
                        Encoding.UTF8.GetBytes(pSrc, length, bytes, byteCount);
                        var pData = pDest;
                        for (int i = 0; i < byteCount; i++)
                        {
                            var temp = bytes[i];
                            if (temp < 128 && _SafeChars[temp])
                            {
                                *pData++ = (char)temp;
                            }
                            else
                            {
                                var h = (temp >> 4) & 0xf;
                                var l = temp & 0x0f;
                                *pData++ = '%';
                                *pData++ = h <= 9 ? (char)(h + '0') : (char)(h - 10 + 'A');
                                *pData++ = l <= 9 ? (char)(l + '0') : (char)(l - 10 + 'A');
                            }
                        }
                    }
                    return value;
                }
            }
        }
        public static string Encode(ReadOnlySpan<char> charsToEncode)
        {
            var length = charsToEncode.Length;
            if (length == 0)
                return string.Empty;
            int safeCount = 0;
            unsafe
            {
                fixed (char* pSrc = charsToEncode)
                {
                    for (int i = 0; i < length; i++)
                    {
                        var temp = pSrc[i];
                        if (temp < 128 && _SafeChars[temp])
                            safeCount++;
                    }
                    if (safeCount == length)
                        return new string(pSrc, 0, length);
                    int byteCount = Encoding.UTF8.GetByteCount(pSrc,length);
                    var value = new string('\0', byteCount + (byteCount - safeCount) * 2);
                    fixed (char* pDest = value )
                    {
                        var bytes = (byte*)pDest + (value.Length * 2 - byteCount);
                        Encoding.UTF8.GetBytes(pSrc, length, bytes, byteCount);
                        var pData = pDest;
                        for (int i = 0; i < byteCount; i++)
                        {
                            var temp = bytes[i];
                            if (temp < 128 && _SafeChars[temp])
                            {
                                *pData++ = (char)temp;
                            }
                            else
                            {
                                var h = (temp >> 4) & 0xf;
                                var l = temp & 0x0f;
                                *pData++ = '%';
                                *pData++ = h <= 9 ? (char)(h + '0') : (char)(h - 10 + 'A');
                                *pData++ = l <= 9 ? (char)(l + '0') : (char)(l - 10 + 'A');
                            }
                        }
                    }
                    return value;
                }
            }
        }
        public static string Encode(ReadOnlySequence<char> seqToEncode)
        {
            var length = checked((int)seqToEncode.Length);
            if (length == 0)
                return string.Empty;
            if (seqToEncode.IsSingleSegment)
                return Encode(seqToEncode.First.Span);
            int safeCount = 0;
            foreach (var segm in seqToEncode)
            {
                var segmSpan = segm.Span;
                for (int i = 0; i < segmSpan.Length; i++)
                {
                    var temp = segmSpan[i];
                    if (temp < 128 && _SafeChars[temp])
                    {
                        safeCount++;
                    }
                }
            }
            if (safeCount == length)
                return seqToEncode.ToString();
            var byteCount = Encoding.UTF8.GetByteCount(seqToEncode);
            var value = new string('\0', byteCount + (byteCount - safeCount) * 2);
            unsafe
            {
                fixed (char* pDest = value)
                {
                    var pData = pDest;
                    var bytes = (byte*)pDest + (value.Length * 2 - byteCount);
                    foreach (var segm in seqToEncode)
                    {
                        var segmSpan = segm.Span;
                        fixed (char* pSrc = segmSpan)
                        {
                            var count = Encoding.UTF8.GetBytes(pSrc, segmSpan.Length, bytes, byteCount);
                            for (int i = 0; i < count; i++)
                            {
                                var temp = bytes[i];
                                if (temp < 128 && _SafeChars[temp])
                                {
                                    *pData++ = (char)temp;
                                }
                                else
                                {
                                    var h = (temp >> 4) & 0xf;
                                    var l = temp & 0x0f;
                                    *pData++ = '%';
                                    *pData++ = h <= 9 ? (char)(h + '0') : (char)(h - 10 + 'A');
                                    *pData++ = l <= 9 ? (char)(l + '0') : (char)(l - 10 + 'A');
                                }
                            }
                            bytes += count;
                            byteCount -= count;
                        }
                    }
                }
            }
            return value;
        }
        public static string Encode(string stringToEncode, Encoding encoding)
        {
            if (stringToEncode == null)
                throw new ArgumentNullException(nameof(stringToEncode));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            if (encoding == Encoding.UTF8)
                return Encode(stringToEncode);
            var length = stringToEncode.Length;
            if (length == 0)
                return string.Empty;
            int encodeIndex = -1;
            unsafe
            {
                fixed (char* pSrc = stringToEncode)
                {
                    for (int i = 0; i < length; i++)
                    {
                        var temp = pSrc[i];
                        if (temp >= 128 || !_SafeChars[temp])
                        {
                            encodeIndex = i;
                            break;
                        }
                    }
                    if (encodeIndex == -1)
                        return stringToEncode;
                    var byteCount= encoding.GetMaxByteCount(length- encodeIndex);
                    if (byteCount > _MaxStackSize)
                        byteCount = _MaxStackSize;
                    var bytes = stackalloc byte[byteCount];
                    var encoder = encoding.GetEncoder();
                    var sb = StringExtensions.ThreadRent(out var disposable);
                    try
                    {
                        sb.Write(new ReadOnlySpan<char>(pSrc, encodeIndex));
                        for (int i = encodeIndex; i < length; i++)
                        {
                            var temp = pSrc[i];
                            if (encodeIndex == -1)
                            {
                                if (temp < 128 && _SafeChars[temp])
                                    sb.Write(temp);
                                else
                                    encodeIndex = i;
                            }
                            else
                            {
                                if (temp < 128 && _SafeChars[temp])
                                {
                                    var pData = pSrc + encodeIndex;
                                    bool completed; int charsUsed, bytesUsed, charCount = i - encodeIndex;
                                    do
                                    {
                                        encoder.Convert(pData, charCount, bytes, byteCount, true, out charsUsed, out bytesUsed, out completed);
                                        charCount -= charsUsed;
                                        pData += charsUsed;
                                        for (int j = 0; j < bytesUsed; j++)
                                        {
                                            var b = bytes[j];
                                            var h = (b >> 4) & 0xf;
                                            var l = b & 0x0f;
                                            sb.Write('%');
                                            sb.Write(h <= 9 ? (char)(h + '0') : (char)(h - 10 + 'A'));
                                            sb.Write(l <= 9 ? (char)(l + '0') : (char)(l - 10 + 'A'));
                                        }
                                    } while (charCount > 0 || !completed);
                                    sb.Write(temp);
                                    encodeIndex = -1;
                                }
                            }
                        }
                        if (encodeIndex != -1)
                        {
                            var pData = pSrc + encodeIndex;
                            bool completed; int charsUsed, bytesUsed, charCount = length - encodeIndex;
                            do
                            {
                                encoder.Convert(pData, charCount, bytes, byteCount, true, out charsUsed, out bytesUsed, out completed);
                                charCount -= charsUsed;
                                pData += charsUsed;
                                for (int j = 0; j < bytesUsed; j++)
                                {
                                    var b = bytes[j];
                                    var h = (b >> 4) & 0xf;
                                    var l = b & 0x0f;
                                    sb.Write('%');
                                    sb.Write(h <= 9 ? (char)(h + '0') : (char)(h - 10 + 'A'));
                                    sb.Write(l <= 9 ? (char)(l + '0') : (char)(l - 10 + 'A'));
                                }
                            } while (charCount > 0 || !completed);
                        }
                        return sb.ToString();
                    }
                    finally
                    {
                        disposable.Dispose();
                    }
                }
            }
        }
        public static string Encode(ReadOnlySpan<char> charsToEncode, Encoding encoding)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            if (encoding == Encoding.UTF8)
                return Encode(charsToEncode);
            var length = charsToEncode.Length;
            if (length == 0)
                return string.Empty;
            var encodeIndex = -1;
            unsafe
            {
                fixed (char* pSrc = charsToEncode)
                {
                    for (int i = 0; i < length; i++)
                    {
                        var temp = pSrc[i];
                        if (temp >= 128 || !_SafeChars[temp])
                        {
                            encodeIndex = i;
                            break;
                        }
                    }
                    if (encodeIndex == -1)
                        return new string(pSrc, 0, length);
                    var byteCount = encoding.GetMaxByteCount(length - encodeIndex);//?>160
                    if (byteCount > _MaxStackSize)
                        byteCount = _MaxStackSize;
                    var bytes = stackalloc byte[byteCount];
                    var encoder = encoding.GetEncoder();
                    var sb = StringExtensions.ThreadRent(out var disposable);
                    try
                    {
                        sb.Write(new ReadOnlySpan<char>(pSrc, encodeIndex));
                        for (int i = encodeIndex; i < length; i++)
                        {
                            var temp = pSrc[i];
                            if (encodeIndex == -1)
                            {
                                if (temp < 128 && _SafeChars[temp])
                                    sb.Write(temp);
                                else
                                    encodeIndex = i;
                            }
                            else
                            {
                                if (temp < 128 && _SafeChars[temp])
                                {
                                    var pData = pSrc + encodeIndex;
                                    bool completed; int charsUsed, bytesUsed, charCount = i - encodeIndex;
                                    do
                                    {
                                        encoder.Convert(pData, charCount, bytes, byteCount, true, out charsUsed, out bytesUsed, out completed);
                                        charCount -= charsUsed;
                                        pData += charsUsed;
                                        for (int j = 0; j < bytesUsed; j++)
                                        {
                                            var b = bytes[j];
                                            var h = (b >> 4) & 0xf;
                                            var l = b & 0x0f;
                                            sb.Write('%');
                                            sb.Write(h <= 9 ? (char)(h + '0') : (char)(h - 10 + 'A'));
                                            sb.Write(l <= 9 ? (char)(l + '0') : (char)(l - 10 + 'A'));
                                        }
                                    } while (charCount > 0 || !completed);
                                    sb.Write(temp);
                                    encodeIndex = -1;
                                }
                            }
                        }
                        if (encodeIndex != -1)
                        {
                            var pData = pSrc + encodeIndex;
                            bool completed; int charsUsed, bytesUsed, charCount = length - encodeIndex;
                            do
                            {
                                encoder.Convert(pData, charCount, bytes, byteCount, true, out charsUsed, out bytesUsed, out completed);
                                charCount -= charsUsed;
                                pData += charsUsed;
                                for (int j = 0; j < bytesUsed; j++)
                                {
                                    var b = bytes[j];
                                    var h = (b >> 4) & 0xf;
                                    var l = b & 0x0f;
                                    sb.Write('%');
                                    sb.Write(h <= 9 ? (char)(h + '0') : (char)(h - 10 + 'A'));
                                    sb.Write(l <= 9 ? (char)(l + '0') : (char)(l - 10 + 'A'));
                                }
                            } while (charCount > 0 || !completed);
                        }
                        return sb.ToString();
                    }
                    finally
                    {
                        disposable.Dispose();
                    }
                }
            }
        }
        public static void Encode(ReadOnlySpan<char> charsToEncode, Encoding encoding, BufferWriter<char> output)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));
            if (output == null)
                throw new ArgumentNullException(nameof(encoding));

            var length = charsToEncode.Length;
            if (length == 0)
                return;
            var encodeIndex = -1;
            unsafe
            {
                fixed (char* pSrc = charsToEncode)
                {
                    for (int i = 0; i < length; i++)
                    {
                        var temp = pSrc[i];
                        if (temp >= 128 || !_SafeChars[temp])
                        {
                            encodeIndex = i;
                            break;
                        }
                    }
                    if (encodeIndex == -1)
                    {
                        output.Write(new ReadOnlySpan<char>(pSrc, length));
                        return;
                    }
                    var byteCount = encoding.GetMaxByteCount(length - encodeIndex);
                    if (byteCount > _MaxStackSize)
                        byteCount = _MaxStackSize;
                    var bytes = stackalloc byte[byteCount];
                    var encoder = encoding.GetEncoder();
                    output.Write(new ReadOnlySpan<char>(pSrc, encodeIndex));
                    for (int i = encodeIndex; i < length; i++)
                    {
                        var temp = pSrc[i];
                        if (encodeIndex == -1)
                        {
                            if (temp < 128 && _SafeChars[temp])
                                output.Write(temp);
                            else
                                encodeIndex = i;
                        }
                        else
                        {
                            if (temp < 128 && _SafeChars[temp])
                            {
                                var pData = pSrc + encodeIndex;
                                bool completed; int charsUsed, bytesUsed, charCount = i - encodeIndex;
                                do
                                {
                                    encoder.Convert(pData, charCount, bytes, byteCount, true, out charsUsed, out bytesUsed, out completed);
                                    charCount -= charsUsed;
                                    pData += charsUsed;
                                    for (int j = 0; j < bytesUsed; j++)
                                    {
                                        var b = bytes[j];
                                        var h = (b >> 4) & 0xf;
                                        var l = b & 0x0f;
                                        output.Write('%');
                                        output.Write(h <= 9 ? (char)(h + '0') : (char)(h - 10 + 'A'));
                                        output.Write(l <= 9 ? (char)(l + '0') : (char)(l - 10 + 'A'));
                                    }
                                } while (charCount > 0 || !completed);
                                output.Write(temp);
                                encodeIndex = -1;
                            }
                        }
                    }
                    if (encodeIndex != -1)
                    {
                        var pData = pSrc + encodeIndex;
                        bool completed; int charsUsed, bytesUsed, charCount = length - encodeIndex;
                        do
                        {
                            encoder.Convert(pData, charCount, bytes, byteCount, true, out charsUsed, out bytesUsed, out completed);
                            charCount -= charsUsed;
                            pData += charsUsed;
                            for (int j = 0; j < bytesUsed; j++)
                            {
                                var b = bytes[j];
                                var h = (b >> 4) & 0xf;
                                var l = b & 0x0f;
                                output.Write('%');
                                output.Write(h <= 9 ? (char)(h + '0') : (char)(h - 10 + 'A'));
                                output.Write(l <= 9 ? (char)(l + '0') : (char)(l - 10 + 'A'));
                            }
                        } while (charCount > 0 || !completed);
                    }
                }
            }
        }
        public static string Encode(ReadOnlySequence<char> seqToEncode, Encoding encoding)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            if (encoding == Encoding.UTF8)
                return Encode(seqToEncode);
            var length = checked((int)seqToEncode.Length);
            if (length == 0)
                return string.Empty;

            if (seqToEncode.IsSingleSegment)
                return Encode(seqToEncode.First.Span, encoding);

            var encoder = encoding.GetEncoder();
            var sb = StringExtensions.ThreadRent(out var disposable);
            try
            {
                unsafe
                {
                    var byteCount = encoding.GetMaxByteCount(length);//TODO? in the loop
                    if (byteCount > _MaxStackSize)
                        byteCount = _MaxStackSize;
                    var bytes = stackalloc byte[byteCount];
                    foreach (var segm in seqToEncode)
                    {
                        var segmLength = segm.Length;
                        var encodeIndex = -1;
                        fixed (char* pSrc = segm.Span)
                        {
                            for (int i = 0; i < segmLength; i++)
                            {
                                var temp = pSrc[i];
                                if (temp >= 128 || !_SafeChars[temp])
                                {
                                    encodeIndex = i;
                                    break;
                                }
                            }
                            if (encodeIndex == -1)
                            {
                                sb.Write(new ReadOnlySpan<char>(pSrc, segmLength));
                                continue;
                            }
                            sb.Write(new ReadOnlySpan<char>(pSrc, encodeIndex));
                            for (int i = encodeIndex; i < segmLength; i++)
                            {
                                var temp = pSrc[i];
                                if (encodeIndex == -1)
                                {
                                    if (temp < 128 && _SafeChars[temp])
                                        sb.Write(temp);
                                    else
                                        encodeIndex = i;
                                }
                                else
                                {
                                    if (temp < 128 && _SafeChars[temp])
                                    {
                                        var pData = pSrc + encodeIndex;
                                        bool completed; int charsUsed, bytesUsed, charCount = i - encodeIndex;
                                        do
                                        {
                                            encoder.Convert(pData, charCount, bytes, byteCount, true, out charsUsed, out bytesUsed, out completed);
                                            charCount -= charsUsed;
                                            pData += charsUsed;
                                            for (int j = 0; j < bytesUsed; j++)
                                            {
                                                var b = bytes[j];
                                                var h = (b >> 4) & 0xf;
                                                var l = b & 0x0f;
                                                sb.Write('%');
                                                sb.Write(h <= 9 ? (char)(h + '0') : (char)(h - 10 + 'A'));
                                                sb.Write(l <= 9 ? (char)(l + '0') : (char)(l - 10 + 'A'));
                                            }
                                        } while (charCount > 0 || !completed);
                                        sb.Write(temp);
                                        encodeIndex = -1;
                                    }
                                }
                            }
                            if (encodeIndex != -1)
                            {
                                var pData = pSrc + encodeIndex;
                                bool completed; int charsUsed, bytesUsed, charCount = segmLength - encodeIndex;
                                do
                                {
                                    encoder.Convert(pData, charCount, bytes, byteCount, true, out charsUsed, out bytesUsed, out completed);
                                    charCount -= charsUsed;
                                    pData += charsUsed;
                                    for (int j = 0; j < bytesUsed; j++)
                                    {
                                        var b = bytes[j];
                                        var h = (b >> 4) & 0xf;
                                        var l = b & 0x0f;
                                        sb.Write('%');
                                        sb.Write(h <= 9 ? (char)(h + '0') : (char)(h - 10 + 'A'));
                                        sb.Write(l <= 9 ? (char)(l + '0') : (char)(l - 10 + 'A'));
                                    }
                                } while (charCount > 0 || !completed);
                            }
                        }
                    }
                }
                return sb.ToString();
            }
            finally
            {
                disposable.Dispose();
            }
        }
        public static string Decode(string stringToDecode)
        {
            if (stringToDecode == null)
                throw new ArgumentNullException(nameof(stringToDecode));

            var length = stringToDecode.Length;
            if (length == 0)
                return string.Empty;
            unsafe
            {
                fixed (char* pSrc = stringToDecode)
                {
                    var hasPlus = false;
                    var percentCount = 0;
                    for (int i = 0; i < length;)
                    {
                        switch (pSrc[i])
                        {
                            case '%':
                                percentCount++;
                                if (i + 2 >= length)
                                    throw new FormatException(nameof(stringToDecode));
                                i += 3;
                                break;
                            case '+':
                                hasPlus = true;
                                i++;
                                break;
                            default:
                                i++;
                                break;
                        }
                    }
                    if (percentCount == 0)
                        return hasPlus ? stringToDecode.Replace('+', ' ') : stringToDecode;

                    //!hasPlus 通过分支减少判断?//Reduce judgment by branching
                    var byteCount = length - percentCount * 2;
                    if (byteCount <= _MaxStackSize)
                    {
                        var bytes = stackalloc byte[byteCount];
                        var bytesOffset = 0;
                        for (int i = 0; i < length;)
                        {
                            switch (pSrc[i])
                            {
                                case '%':
                                    var hHex = pSrc[i + 1];//不支持不规范的%u5f20//Nonstandard is not supported %u5f20
                                    var lHex = pSrc[i + 2];
                                    var h = (hHex >= '0' && hHex <= '9') ? hHex - '0' :
                                            (hHex >= 'A' && hHex <= 'F') ? hHex - 'A' + 10 :
                                            (hHex >= 'a' && hHex <= 'f') ? hHex - 'a' + 10 : -1;
                                    var l = (lHex >= '0' && lHex <= '9') ? lHex - '0' :
                                            (lHex >= 'A' && lHex <= 'F') ? lHex - 'A' + 10 :
                                            (lHex >= 'a' && lHex <= 'f') ? lHex - 'a' + 10 : -1;
                                    if (h == -1 || l == -1)
                                        throw new FormatException(nameof(stringToDecode));
                                    bytes[bytesOffset++] = (byte)((h << 4) | l);
                                    i += 3;
                                    break;
                                case '+':
                                    bytes[bytesOffset++] = (byte)' ';
                                    i++;
                                    break;
                                default:
                                    bytes[bytesOffset++] = (byte)pSrc[i];
                                    i++;
                                    break;
                            }
                        }
                        return Encoding.UTF8.GetString(bytes, byteCount);
                    }
                    else
                    {
                        byteCount = _MaxStackSize;
                        var bytes = stackalloc byte[byteCount];
                        var bytesOffset = 0;
                        var decoder = Encoding.UTF8.GetDecoder();
                        var sb = StringExtensions.ThreadRent(out var disposable);
                        try
                        {
                            for (int i = 0; i < length;)
                            {
                                switch (pSrc[i])
                                {
                                    case '%':
                                        var hHex = pSrc[i + 1];
                                        var lHex = pSrc[i + 2];
                                        var h = (hHex >= '0' && hHex <= '9') ? hHex - '0' :
                                                (hHex >= 'A' && hHex <= 'F') ? hHex - 'A' + 10 :
                                                (hHex >= 'a' && hHex <= 'f') ? hHex - 'a' + 10 : -1;
                                        var l = (lHex >= '0' && lHex <= '9') ? lHex - '0' :
                                                (lHex >= 'A' && lHex <= 'F') ? lHex - 'A' + 10 :
                                                (lHex >= 'a' && lHex <= 'f') ? lHex - 'a' + 10 : -1;
                                        if (h == -1 || l == -1)
                                            throw new FormatException(nameof(stringToDecode));
                                        bytes[bytesOffset++] = (byte)((h << 4) | l);
                                        i += 3;
                                        break;
                                    case '+':
                                        bytes[bytesOffset++] = (byte)' ';
                                        i++;
                                        break;
                                    default:
                                        bytes[bytesOffset++] = (byte)pSrc[i];
                                        i++;
                                        break;
                                }
                                if (bytesOffset == byteCount)
                                {
                                    sb.WriteBytes(bytes, bytesOffset, false, decoder);
                                    bytesOffset = 0;
                                }
                            }
                            if (bytesOffset > 0)
                                sb.WriteBytes(bytes, bytesOffset, true, decoder);

                            return sb.ToString();
                        }
                        finally
                        {
                            disposable.Dispose();
                        }
                    }
                }
            }
        }
        public static string Decode(ReadOnlySpan<char> charsToDecode)
        {
            var length = charsToDecode.Length;
            if (length == 0)
                return string.Empty;
            unsafe
            {
                fixed (char* pSrc = charsToDecode)
                {
                    var hasPlus = false;
                    var percentCount = 0; 
                    for (int i = 0; i < length;)
                    {
                        switch (pSrc[i])
                        {
                            case '%':
                                percentCount++;
                                if (i + 2 >= length)
                                    throw new FormatException(nameof(charsToDecode));
                                i += 3;
                                break;
                            case '+':
                                hasPlus = true;
                                i++;
                                break;
                            default:
                                i++;
                                break;
                        }
                    }
                    if (percentCount == 0)
                    {
                        if(!hasPlus)
                            return new string(pSrc, 0, length);

                        var value = new string('\0', length);
                        fixed (char* pDest = value)
                        {
                            for (int i = 0; i < length; i++)
                            {
                                if (pSrc[i] == '+')
                                    pDest[i] = ' ';
                                else
                                    pDest[i] = pSrc[i];
                            }
                        }
                        return value;
                    }
                    var byteCount = length - percentCount * 2;
                    if (byteCount <= _MaxStackSize)
                    {
                        var bytes = stackalloc byte[byteCount];
                        var bytesOffset = 0;
                        for (int i = 0; i < length;)
                        {
                            switch (pSrc[i])
                            {
                                case '%':
                                    var hHex = pSrc[i + 1];
                                    var lHex = pSrc[i + 2];
                                    var h = (hHex >= '0' && hHex <= '9') ? hHex - '0' :
                                            (hHex >= 'A' && hHex <= 'F') ? hHex - 'A' + 10 :
                                            (hHex >= 'a' && hHex <= 'f') ? hHex - 'a' + 10 : -1;
                                    var l = (lHex >= '0' && lHex <= '9') ? lHex - '0' :
                                            (lHex >= 'A' && lHex <= 'F') ? lHex - 'A' + 10 :
                                            (lHex >= 'a' && lHex <= 'f') ? lHex - 'a' + 10 : -1;
                                    if (h == -1 || l == -1)
                                        throw new FormatException(nameof(charsToDecode));
                                    bytes[bytesOffset++] = (byte)((h << 4) | l);
                                    i += 3;
                                    break;
                                case '+':
                                    bytes[bytesOffset++] = (byte)' ';
                                    i++;
                                    break;
                                default:
                                    bytes[bytesOffset++] = (byte)pSrc[i];
                                    i++;
                                    break;
                            }
                        }
                        return Encoding.UTF8.GetString(bytes, byteCount);
                    }
                    else
                    {
                        byteCount = _MaxStackSize;
                        var bytes = stackalloc byte[byteCount];
                        var bytesOffset = 0;
                        var decoder = Encoding.UTF8.GetDecoder();
                        var sb = StringExtensions.ThreadRent(out var disposable);
                        try
                        {
                            for (int i = 0; i < length;)
                            {
                                switch (pSrc[i])
                                {
                                    case '%':
                                        var hHex = pSrc[i + 1];
                                        var lHex = pSrc[i + 2];
                                        var h = (hHex >= '0' && hHex <= '9') ? hHex - '0' :
                                                (hHex >= 'A' && hHex <= 'F') ? hHex - 'A' + 10 :
                                                (hHex >= 'a' && hHex <= 'f') ? hHex - 'a' + 10 : -1;
                                        var l = (lHex >= '0' && lHex <= '9') ? lHex - '0' :
                                                (lHex >= 'A' && lHex <= 'F') ? lHex - 'A' + 10 :
                                                (lHex >= 'a' && lHex <= 'f') ? lHex - 'a' + 10 : -1;
                                        if (h == -1 || l == -1)
                                            throw new FormatException(nameof(charsToDecode));
                                        bytes[bytesOffset++] = (byte)((h << 4) | l);
                                        i += 3;
                                        break;
                                    case '+':
                                        bytes[bytesOffset++] = (byte)' ';
                                        i++;
                                        break;
                                    default:
                                        bytes[bytesOffset++] = (byte)pSrc[i];
                                        i++;
                                        break;
                                }
                                if (bytesOffset == byteCount)
                                {
                                    sb.WriteBytes(bytes, bytesOffset, false, decoder);
                                    bytesOffset = 0;
                                }
                            }
                            if (bytesOffset > 0)
                                sb.WriteBytes(bytes, bytesOffset, true, decoder);

                            return sb.ToString();
                        }
                        finally
                        {
                            disposable.Dispose();
                        }
                    }
                }
            }
        }
        public static string Decode(ReadOnlySequence<char> seqToDecode)
        {
            return Decode(seqToDecode, Encoding.UTF8);
        }
        public static string Decode(string stringToDecode, Encoding encoding)
        {
            if (stringToDecode == null)
                throw new ArgumentNullException(nameof(stringToDecode));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            if (encoding == Encoding.UTF8)
                return Decode(stringToDecode);
            var length = stringToDecode.Length;
            if (length == 0)
                return string.Empty;
            unsafe
            {
                fixed (char* pSrc = stringToDecode)
                {
                    var hasPlus = false;
                    var percentCount = 0;
                    for (int i = 0; i < length;)
                    {
                        switch (pSrc[i])
                        {
                            case '%':
                                percentCount++;
                                if (i + 2 >= length)
                                    throw new FormatException(nameof(stringToDecode));
                                i += 3;
                                break;
                            case '+':
                                hasPlus = true;
                                i++;
                                break;
                            default:
                                i++;
                                break;
                        }
                    }
                    if (percentCount == 0)
                        return hasPlus ? stringToDecode.Replace('+', ' ') : stringToDecode;
                    
                    var byteCount = length - percentCount * 2;
                    if (byteCount > _MaxStackSize)
                        byteCount = _MaxStackSize;
                    var bytes = stackalloc byte[byteCount];
                    var decoder = encoding.GetDecoder();
                    var sb = StringExtensions.ThreadRent(out var disposable);
                    try
                    {
                        var pos = 0;
                        for (; ; )
                        {
                            for (; ; )
                            {
                                if (pos == length)
                                    return sb.ToString();
                                var temp = pSrc[pos];
                                if (temp == '%')
                                    break;
                                pos += 1;
                                sb.Write(temp == '+' ? ' ' : temp);
                            }
                            var bytesOffset = 0;
                            for (; ; )
                            {
                                var hHex = pSrc[pos + 1];
                                var lHex = pSrc[pos + 2];
                                var h = (hHex >= '0' && hHex <= '9') ? hHex - '0' :
                                        (hHex >= 'A' && hHex <= 'F') ? hHex - 'A' + 10 :
                                        (hHex >= 'a' && hHex <= 'f') ? hHex - 'a' + 10 : -1;
                                var l = (lHex >= '0' && lHex <= '9') ? lHex - '0' :
                                        (lHex >= 'A' && lHex <= 'F') ? lHex - 'A' + 10 :
                                        (lHex >= 'a' && lHex <= 'f') ? lHex - 'a' + 10 : -1;
                                if (h == -1 || l == -1)
                                    throw new FormatException(nameof(stringToDecode));

                                bytes[bytesOffset++] = (byte)((h << 4) | l);
                                if (bytesOffset == byteCount)
                                {
                                    sb.WriteBytes(bytes, byteCount, false, decoder);
                                    bytesOffset = 0;
                                }
                                pos += 3;
                                if (pos == length)
                                {
                                    if (bytesOffset > 0)
                                        sb.WriteBytes(bytes, bytesOffset, true, decoder);
                                    return sb.ToString();
                                }
                                if (pSrc[pos] != '%')
                                {
                                    if (bytesOffset > 0)
                                        sb.WriteBytes(bytes, bytesOffset, true, decoder);
                                    sb.Write(pSrc[pos] == '+' ? ' ' : pSrc[pos]);
                                    pos += 1;
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
        }
        public static string Decode(ReadOnlySpan<char> charsToDecode, Encoding encoding)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            if (encoding == Encoding.UTF8)
                return Decode(charsToDecode);
            var length = charsToDecode.Length;
            if (length == 0)
                return string.Empty;
            unsafe
            {
                fixed (char* pSrc = charsToDecode)
                {
                    var hasPlus = false;
                    var percentCount = 0;
                    for (int i = 0; i < length;)
                    {
                        switch (pSrc[i])
                        {
                            case '%':
                                percentCount++;
                                if (i + 2 >= length)
                                    throw new FormatException(nameof(charsToDecode));
                                i += 3;
                                break;
                            case '+':
                                hasPlus = true;
                                i++;
                                break;
                            default:
                                i++;
                                break;
                        }
                    }
                    if (percentCount == 0)
                    {
                        if(!hasPlus)
                            return new string(pSrc, 0, length);

                        var value = new string('\0', length);
                        fixed (char* pDest = value)
                        {
                            for (int i = 0; i < length; i++)
                            {
                                if (pSrc[i] == '+')
                                    pDest[i] = ' ';
                                else
                                    pDest[i] = pSrc[i];
                            }
                        }
                        return value;
                    }
                    var byteCount = length - percentCount * 2;
                    if (byteCount > _MaxStackSize)
                        byteCount = _MaxStackSize;
                    var bytes = stackalloc byte[byteCount];
                    var decoder = encoding.GetDecoder();
                    var sb = StringExtensions.ThreadRent(out var disposable);
                    try
                    {
                        var pos = 0;
                        for (; ; )
                        {
                            for (; ; )
                            {
                                if (pos == length)
                                    return sb.ToString();
                                var temp = pSrc[pos];
                                if (temp == '%')
                                    break;
                                pos += 1;
                                sb.Write(temp == '+' ? ' ' : temp);
                            }
                            var bytesOffset = 0;
                            for (; ; )
                            {
                                var hHex = pSrc[pos + 1];
                                var lHex = pSrc[pos + 2];
                                var h = (hHex >= '0' && hHex <= '9') ? hHex - '0' :
                                        (hHex >= 'A' && hHex <= 'F') ? hHex - 'A' + 10 :
                                        (hHex >= 'a' && hHex <= 'f') ? hHex - 'a' + 10 : -1;
                                var l = (lHex >= '0' && lHex <= '9') ? lHex - '0' :
                                        (lHex >= 'A' && lHex <= 'F') ? lHex - 'A' + 10 :
                                        (lHex >= 'a' && lHex <= 'f') ? lHex - 'a' + 10 : -1;
                                if (h == -1 || l == -1)
                                    throw new FormatException(nameof(charsToDecode));

                                bytes[bytesOffset++] = (byte)((h << 4) | l);
                                if (bytesOffset == byteCount)
                                {
                                    sb.WriteBytes(bytes, byteCount, false, decoder);
                                    bytesOffset = 0;
                                }
                                pos += 3;
                                if (pos == length)
                                {
                                    if (bytesOffset > 0)
                                        sb.WriteBytes(bytes, bytesOffset, true, decoder);
                                    return sb.ToString();
                                }
                                if (pSrc[pos] != '%')
                                {
                                    if (bytesOffset > 0)
                                        sb.WriteBytes(bytes, bytesOffset, true, decoder);
                                    sb.Write(pSrc[pos] == '+' ? ' ' : pSrc[pos]);
                                    pos += 1;
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
        }
        public static string Decode(ReadOnlySequence<char> seqToDecode, Encoding encoding)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            var length = checked((int)seqToDecode.Length);
            if (length == 0)
                return string.Empty;

            if (seqToDecode.IsSingleSegment)
                return Decode(seqToDecode.First.Span, encoding);

            var hasPlus = false;
            var percentCount = 0;
            var sbEnumerator = seqToDecode.GetEnumerator();
            while (sbEnumerator.MoveNext())
            {
                var segmSpan = sbEnumerator.Current.Span;
                var segmLength = segmSpan.Length;
                for (int i = 0; i < segmLength;)
                {
                    if (segmSpan[i] != '%')
                    {
                        if (segmSpan[i] == '+')
                            hasPlus = true;
                        i += 1;
                        continue;
                    }
                    var tempPercentCount = 0;
                    for (; ; )
                    {
                        tempPercentCount++;
                        if (i + 2 < segmLength)
                        {
                            i += 3;
                        }
                        else if (i + 1 == segmLength)
                        {
                            if (!sbEnumerator.MoveNext())
                                throw new FormatException(nameof(seqToDecode));
                            segmSpan = sbEnumerator.Current.Span;
                            segmLength = segmSpan.Length;
                            if (segmLength == 1)
                            {
                                if (!sbEnumerator.MoveNext())
                                    throw new FormatException(nameof(seqToDecode));
                                segmSpan = sbEnumerator.Current.Span;
                                segmLength = segmSpan.Length;
                                i = 1;
                            }
                            else
                            {
                                i = 2;
                            }
                        }
                        else if (i + 2 == segmLength)
                        {
                            if (!sbEnumerator.MoveNext())
                                throw new FormatException(nameof(seqToDecode));
                            segmSpan = sbEnumerator.Current.Span;
                            segmLength = segmSpan.Length;
                            i = 1;
                        }

                        if (i == segmLength)
                        {
                            if (!sbEnumerator.MoveNext())
                            {
                                if (tempPercentCount > percentCount)
                                    percentCount = tempPercentCount;
                                goto decode;
                            }
                            segmSpan = sbEnumerator.Current.Span;
                            segmLength = segmSpan.Length;
                            i = 0;
                        }
                        if (segmSpan[i]=='%')
                            continue;

                        if (tempPercentCount > percentCount)
                            percentCount = tempPercentCount;
                        if (segmSpan[i] == '+')
                            hasPlus = true;
                        i += 1;
                        break;
                    }
                }
            }

        decode:
            if (percentCount == 0)
            {
                if (!hasPlus)
                    return seqToDecode.ToString();

                var value = seqToDecode.ToString();
                if (value.Length != length)
                    throw new InvalidOperationException(nameof(seqToDecode));
                unsafe
                {
                    fixed (char* pData = value)
                    {
                        for (int i = 0; i < length; i++)
                        {
                            if (pData[i] == '+')
                                pData[i] = ' ';
                        }
                    }
                }
                return value;
            }
            unsafe
            {
                var byteCount = percentCount * 2;
                if (byteCount > _MaxStackSize)
                    byteCount = _MaxStackSize;
                var bytes = stackalloc byte[byteCount];
                sbEnumerator = seqToDecode.GetEnumerator();
                var segmSpan = sbEnumerator.Current.Span;
                var segmLength = segmSpan.Length;
                var decoder = encoding.GetDecoder();
                var sb = StringExtensions.ThreadRent(out var disposable);
                try
                {
                    var pos = 0;
                    for (; ; )
                    {
                        for (; ; )
                        {
                            if (pos == segmLength)
                            {
                                if (!sbEnumerator.MoveNext())
                                    return sb.ToString();
                                segmSpan = sbEnumerator.Current.Span;
                                segmLength = segmSpan.Length;
                                pos = 0;
                            }

                            var temp = segmSpan[pos];
                            if (temp == '%')
                                break;
                            pos += 1;
                            sb.Write(temp == '+' ? ' ' : temp);
                        }
                        var bytesOffset = 0;
                        for (; ; )
                        {
                            var hHex = '\0';
                            var lHex = '\0';
                            if (pos + 2 < segmLength)
                            {
                                hHex = segmSpan[pos + 1];
                                lHex = segmSpan[pos + 2];
                                pos += 3;
                            }
                            else if (pos + 1 == segmLength)
                            {
                                if (!sbEnumerator.MoveNext())
                                    throw new FormatException(nameof(seqToDecode));
                                segmSpan = sbEnumerator.Current.Span;
                                segmLength = segmSpan.Length;
                                if (segmLength == 1)
                                {
                                    hHex = segmSpan[0];
                                    if (!sbEnumerator.MoveNext())
                                        throw new FormatException(nameof(seqToDecode));
                                    segmSpan = sbEnumerator.Current.Span;
                                    segmLength = segmSpan.Length;
                                    lHex = segmSpan[0];
                                    pos = 1;
                                }
                                else
                                {
                                    hHex = segmSpan[0];
                                    lHex = segmSpan[1];
                                    pos = 2;
                                }
                            }
                            else if (pos + 2 == segmLength)
                            {
                                hHex = segmSpan[pos + 1];
                                if (!sbEnumerator.MoveNext())
                                    throw new FormatException(nameof(seqToDecode));
                                segmSpan = sbEnumerator.Current.Span;
                                segmLength = segmSpan.Length;
                                lHex = segmSpan[0];
                                pos = 1;
                            }
                            var h = (hHex >= '0' && hHex <= '9') ? hHex - '0' :
                                    (hHex >= 'A' && hHex <= 'F') ? hHex - 'A' + 10 :
                                    (hHex >= 'a' && hHex <= 'f') ? hHex - 'a' + 10 : -1;
                            var l = (lHex >= '0' && lHex <= '9') ? lHex - '0' :
                                    (lHex >= 'A' && lHex <= 'F') ? lHex - 'A' + 10 :
                                    (lHex >= 'a' && lHex <= 'f') ? lHex - 'a' + 10 : -1;
                            if (h == -1 || l == -1)
                                throw new FormatException(nameof(seqToDecode));

                            bytes[bytesOffset++] = (byte)((h << 4) | l);
                            if (bytesOffset == byteCount)
                            {
                                sb.WriteBytes(bytes, byteCount, false, decoder);
                                bytesOffset = 0;
                            }
                            if (pos == segmLength)
                            {
                                if (!sbEnumerator.MoveNext())
                                {
                                    if (bytesOffset > 0)
                                        sb.WriteBytes(bytes, bytesOffset, true, decoder);
                                    return sb.ToString();
                                }
                                segmSpan = sbEnumerator.Current.Span;
                                segmLength = segmSpan.Length;
                                pos = 0;
                            }
                            if (segmSpan[pos] != '%')
                            {
                                if (bytesOffset > 0)
                                    sb.WriteBytes(bytes, bytesOffset, true, decoder);
                                sb.Write(segmSpan[pos] == '+' ? ' ' : segmSpan[pos]);
                                pos += 1;
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

        #region Url <=> Uri
        public static implicit operator Url(Uri uri)
        {
            if (uri == null)
                return null;
            var @this = new Url();
            @this._scheme = uri.Scheme;
            @this._userInfo = uri.UserInfo;
            @this._host = uri.IdnHost;
            @this._port = uri.IsDefaultPort ? null : (int?)uri.Port;
            @this._path = uri.AbsolutePath;
            @this._query = uri.Query;
            @this._fragment = uri.Fragment;
            @this._hostNameType = uri.HostNameType;
            return @this;
        }

        public static implicit operator Uri(Url @this)
        {
            if (@this == null)
                return null;
            return new Uri(@this.AbsoluteUri);//TODO? Reflection
        }
        #endregion
    }
}
