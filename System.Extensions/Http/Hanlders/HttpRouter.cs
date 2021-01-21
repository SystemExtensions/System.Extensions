
namespace System.Extensions.Http
{
    using System.Diagnostics;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    public class HttpRouter : IHttpHandler
    {
        private Tree _getTree, _postTree, _putTree, _deleteTree, _headTree;
        public class Tree
        {
            [ThreadStatic] private static Stack<KeyValuePair<int, Node>> _NodesStack;
            [ThreadStatic] private static List<KeyValuePair<int, int>> _PathSegments;
            private class Node
            {
                // /abc/cde/{tt}/xyz 匹配 /abc/cde/mmm/xy
                public string Value;///index/
                public long ValuePrefixLong;//前4个字符public int ValuePrefixInt;//前两个字符
                public List<KeyValuePair<string, int>> ParamInfo;
                public IHttpHandler Handler;
                public string Template;//用于遍历
                public int Depth;//节点深度

                public List<Node> ValueNodes;
                public Node ParamNode;//List<Node> 参数条件
                public Node PathParamNode;//是否Catch all 
            }

            private readonly Node _root;
            public Tree()
            {
                // 不用/{string.Empty}放在_root上 快速
                _root = new Node() { Value = string.Empty, Depth = 0 };//1 0 
            }
            public void Map(string template, IHttpHandler handler)
            {
                if (string.IsNullOrEmpty(template))
                    throw new ArgumentNullException(nameof(template));

                //TODO {}转义
                if (template == "/")
                {
                    _root.Handler = handler;
                    _root.Template = "/";
                    return;
                }
                //必须/开头
                string[] templateSegs;
                if (template.StartsWith("/"))
                {
                    templateSegs = template.Substring(1).Split('/');
                }
                else
                {
                    templateSegs = template.Split('/');
                    template = "/" + template;
                }
                Node tempNode = _root;
                var paramInfo = new List<KeyValuePair<string, int>>();
                for (int i = 0; i < templateSegs.Length; i++)
                {
                    string temp = templateSegs[i];
                    if (temp.StartsWith("{"))
                    {
                        if (!temp.EndsWith("}"))
                            throw new FormatException(template);

                        if (temp.StartsWith("{*"))
                        {
                            if (i != templateSegs.Length - 1)
                                throw new FormatException(template);

                            var paramName = temp.Substring(2, temp.Length - 3);
                            paramInfo.Add(new KeyValuePair<string, int>(paramName, i));
                            if (tempNode.PathParamNode == null)
                                tempNode.PathParamNode = new Node() { Depth = tempNode.Depth + 1 };

                            tempNode.PathParamNode.ParamInfo = paramInfo;
                            tempNode.PathParamNode.Handler = handler;
                            tempNode.PathParamNode.Template = template;
                        }//?}
                        else
                        {
                            var paramName = temp.Substring(1, temp.Length - 2);
                            paramInfo.Add(new KeyValuePair<string, int>(paramName, i));
                            if (tempNode.ParamNode == null)
                                tempNode.ParamNode = new Node() { Depth = tempNode.Depth + 1 };
                            tempNode = tempNode.ParamNode;

                            if (i != templateSegs.Length - 1)//不是最后一个
                                continue;

                            tempNode.ParamInfo = paramInfo;
                            tempNode.Handler = handler;
                            tempNode.Template = template;
                        }
                    }
                    else
                    {
                        if (tempNode.ValueNodes == null)
                            tempNode.ValueNodes = new List<Node>();

                        var isHasValue = false;
                        foreach (var item in tempNode.ValueNodes)
                        {
                            if (item.Value.Equals(temp))
                            {
                                isHasValue = true;
                                tempNode = item;//设置到下一级
                                break;
                            }
                        }
                        if (!isHasValue)
                        {
                            var node = new Node() { Depth = tempNode.Depth + 1 };
                            node.Value = temp;
                            if (temp.Length >= 4)
                            {
                                unsafe
                                {
                                    fixed (char* pTemp = temp)
                                    {
                                        node.ValuePrefixLong = *(long*)pTemp;
                                    }
                                }
                            }
                            tempNode.ValueNodes.Add(node);
                            tempNode = node;
                        }

                        if (i != templateSegs.Length - 1)//不是最后一个
                            continue;

                        if (paramInfo.Count > 0)
                            tempNode.ParamInfo = paramInfo;//设置参数
                        tempNode.Handler = handler;
                        tempNode.Template = template;
                    }
                }
            }
            public IHttpHandler Match(ReadOnlySpan<char> path, PathParams pathParams)
            {
                var length = path.Length;
                if (length == 0)
                    return null;
                if (path[0] != '/')
                    return null;
                if (length == 1)
                {
                    if (_root.Handler != null)
                        return _root.Handler;
                    //=>/{param},/{*param}
                }
                var stack = _NodesStack;
                if (stack == null)
                {
                    stack = new Stack<KeyValuePair<int, Node>>(8);
                    _NodesStack = stack;
                }
                var pathSegs = _PathSegments;
                if (pathSegs == null)
                {
                    pathSegs = new List<KeyValuePair<int, int>>(8);
                    _PathSegments = pathSegs;
                }
                stack.Clear();
                pathSegs.Clear();
                unsafe
                {
                    static bool Equals(string value, long valuePrefixLong, char* chars, int startIndex)
                    {
                        var offset = 0;
                        var tempCount = value.Length;
                        if (tempCount < 4)
                            goto ret;
                        if (valuePrefixLong != *(long*)(chars + startIndex))
                            return false;
                        offset = 4;
                        startIndex += 4;
                        tempCount -= 4;
                        while (tempCount >= 4)
                        {
                            if (value[offset++] != chars[startIndex++])
                                return false;
                            if (value[offset++] != chars[startIndex++])
                                return false;
                            if (value[offset++] != chars[startIndex++])
                                return false;
                            if (value[offset++] != chars[startIndex++])
                                return false;
                            tempCount -= 4;
                        }
                        ret:
                        switch (tempCount)
                        {
                            case 0:
                                return true;
                            case 1:
                                return value[offset] == chars[startIndex];
                            case 2:
                                return value[offset] == chars[startIndex] && value[offset + 1] == chars[startIndex + 1];
                            case 3:
                                return value[offset] == chars[startIndex] && value[offset + 1] == chars[startIndex + 1] && value[offset + 2] == chars[startIndex + 2];
                            default:
                                throw new InvalidOperationException("Never");
                        }
                    }

                    fixed (char* pathPtr = path)
                    {
                        const int _ValueTag = 1, _ParamTag = 2, _PathParamTag = 3;
                        var tempTag = _ValueTag;// 1 value 2 参数 3 catchAll
                        var tempNode = _root;
                        var tempOffset = 1;//-1就是完成了
                        for (; ; )
                        {
                            Debug.Assert(tempNode != null);
                            if (tempOffset != -1 && pathSegs.Count <= tempNode.Depth)
                            {
                                var tempIndex = -1;
                                for (int i = tempOffset; i < length; i++)
                                {
                                    if (pathPtr[i] == '/')
                                    {
                                        tempIndex = i;
                                        break;
                                    }
                                }
                                if (tempIndex == -1)
                                {
                                    pathSegs.Add(new KeyValuePair<int, int>(tempOffset, length - tempOffset));
                                    tempOffset = -1;
                                }
                                else
                                {
                                    pathSegs.Add(new KeyValuePair<int, int>(tempOffset, tempIndex - tempOffset));
                                    tempOffset = tempIndex + 1;
                                    //BUG
                                    //if (tempOffset == length)
                                    //    tempOffset = -1;
                                }
                            }

                            if (tempTag == _ValueTag)//进行value判断
                            {
                                if (tempNode.ValueNodes != null)
                                {
                                    var pathSeg = pathSegs[tempNode.Depth];
                                    Node node = null;
                                    for (int i = 0; i < tempNode.ValueNodes.Count; i++)
                                    {
                                        //if (tempNode.ValueNodes[i].Value.AsSpan().Equals(path.Slice(pathSeg.Key, pathSeg.Value), StringComparison.OrdinalIgnoreCase))
                                        //{
                                        //    matchIndex = i;
                                        //    break;
                                        //}
                                        //if (tempNode.ValueNodes[i].Value.AsSpan().Equals(path.Slice(pathSeg.Key, pathSeg.Value), StringComparison.Ordinal))
                                        //{
                                        //    matchIndex = i;
                                        //    break;
                                        //}
                                        var currNode = tempNode.ValueNodes[i];
                                        if (currNode.Value.Length == pathSeg.Value && Equals(currNode.Value, currNode.ValuePrefixLong, pathPtr, pathSeg.Key))
                                        {
                                            node = currNode;
                                            break;
                                        }
                                    }
                                    if (node != null)
                                    {
                                        if (tempOffset != -1 || pathSegs.Count != node.Depth)//
                                        {
                                            if (tempNode.ParamNode != null)
                                                stack.Push(new KeyValuePair<int, Node>(_ParamTag, tempNode));
                                            else if (tempNode.PathParamNode != null)
                                                stack.Push(new KeyValuePair<int, Node>(_PathParamTag, tempNode));
                                            tempTag = _ValueTag;
                                            tempNode = node;
                                            continue;
                                        }
                                        if (node.Handler != null)//路径匹配完成
                                        {
                                            if (node.ParamInfo != null)
                                            {
                                                foreach (var item in node.ParamInfo)
                                                {
                                                    var pathValueSeg = pathSegs[item.Value];
                                                    pathParams.Add(item.Key, Url.Decode(path.Slice(pathValueSeg.Key, pathValueSeg.Value)));
                                                    //Console.WriteLine($"{item.Key}={item.Value}");
                                                }
                                            }
                                            return node.Handler;
                                        }
                                    }
                                }
                                if (tempNode.ParamNode != null)
                                    tempTag = _ParamTag;
                                else if (tempNode.PathParamNode != null)
                                    tempTag = _PathParamTag;
                                else
                                {
                                    if (stack.Count == 0)
                                        return null;
                                    var item = stack.Pop();
                                    tempTag = item.Key;
                                    tempNode = item.Value;
                                }
                            }
                            else if (tempTag == _ParamTag)
                            {
                                var node = tempNode.ParamNode;
                                if (tempOffset != -1 || pathSegs.Count != node.Depth)
                                {
                                    if (tempNode.PathParamNode != null)
                                        stack.Push(new KeyValuePair<int, Node>(_PathParamTag, tempNode));
                                    tempTag = _ValueTag;
                                    tempNode = node;
                                    continue;
                                }
                                if (node.Handler != null)
                                {
                                    if (node.ParamInfo != null)
                                    {
                                        foreach (var item in node.ParamInfo)
                                        {
                                            var pathValueSeg = pathSegs[item.Value];
                                            pathParams.Add(item.Key, Url.Decode(path.Slice(pathValueSeg.Key, pathValueSeg.Value)));
                                        }
                                    }
                                    return node.Handler;
                                }

                                if (tempNode.PathParamNode != null)
                                    tempTag = _PathParamTag;
                                else
                                {
                                    if (stack.Count == 0)
                                        return null;
                                    var item = stack.Pop();
                                    tempTag = item.Key;
                                    tempNode = item.Value;
                                }
                            }
                            else if (tempTag == _PathParamTag)
                            {
                                var node = tempNode.PathParamNode;
                                if (node.ParamInfo != null)
                                {
                                    var paramInfoEndIndex = node.ParamInfo.Count - 1;
                                    if (paramInfoEndIndex > 0)
                                    {
                                        for (int i = 0; i < paramInfoEndIndex; i++)
                                        {
                                            var item = node.ParamInfo[i];
                                            var pathValueSeg = pathSegs[item.Value];
                                            pathParams.Add(item.Key, Url.Decode(path.Slice(pathValueSeg.Key, pathValueSeg.Value)));
                                        }
                                    }
                                    var pathItem = node.ParamInfo[paramInfoEndIndex];//放入最后一个
                                    var pathParamValueSeg = pathSegs[pathItem.Value];
                                    pathParams.Add(pathItem.Key, Url.Decode(path.Slice(pathParamValueSeg.Key)));
                                }
                                return node.Handler;
                            }
                        }
                    }
                }
            }
            public Enumerator GetEnumerator() => new Enumerator(this);
            public struct Enumerator
            {
                private Stack<Node> _stack;
                private KeyValuePair<string, IHttpHandler> _current;
                internal Enumerator(Tree tree)
                {
                    _stack = new Stack<Node>(16);
                    _stack.Push(tree._root);
                    _current = default;
                }
                public KeyValuePair<string, IHttpHandler> Current => _current;
                public bool MoveNext()
                {
                    if (_stack == null || _stack.Count == 0)
                        return false;

                    var temp = _stack.Pop();

                    if (temp.PathParamNode != null)
                        _stack.Push(temp.PathParamNode);
                    if (temp.ParamNode != null)
                        _stack.Push(temp.ParamNode);

                    if (temp.ValueNodes != null)
                    {
                        foreach (var valueNode in temp.ValueNodes)
                        {
                            _stack.Push(valueNode);
                        }
                    }

                    if (temp.Handler == null)
                        return MoveNext();
                    _current = new KeyValuePair<string, IHttpHandler>(temp.Template,temp.Handler);
                    return true;
                }
            }

            //TODO??
            //[ThreadStatic] private static PathParams _PathParams;
            //IHttpHandler Match(ReadOnlySpan<char> path, out PathParams pathParams)
        }
        public Tree GetTree 
        {
            get => _getTree;
            set => _getTree = value ?? throw new ArgumentNullException(nameof(GetTree));
        }
        public Tree PostTree
        {
            get => _postTree;
            set => _postTree = value ?? throw new ArgumentNullException(nameof(PostTree));
        }
        public Tree PutTree 
        {
            get => _putTree;
            set => _putTree = value ?? throw new ArgumentNullException(nameof(PutTree));
        }
        public Tree DeleteTree 
        {
            get => _deleteTree;
            set => _deleteTree = value ?? throw new ArgumentNullException(nameof(DeleteTree));
        }
        public Tree HeadTree 
        {
            get => _headTree;
            set => _headTree = value ?? throw new ArgumentNullException(nameof(HeadTree));
        }
        public HttpRouter()
        {
            _getTree = new Tree();
            _postTree = new Tree();
            _putTree = new Tree();
            _deleteTree = new Tree();
            _headTree = new Tree();
        }
        public async Task<HttpResponse> HandleAsync(HttpRequest request)
        {
            if (request == null)
                return null;

            var path = request.Url.Path;
            if (string.IsNullOrEmpty(path))
                return null;

            PathParams pathParams;
            request.PathParams(out var _pathParams);
            if (_pathParams == null)
            {
                pathParams = new PathParams();
                request.PathParams(pathParams);
            }
            else 
            {
                pathParams = _pathParams as PathParams;
                if (pathParams == null)
                {
                    pathParams = new PathParams();
                    for (int i = 0; i < _pathParams.Count; i++)
                    {
                        var pathParam = _pathParams[i];
                        pathParams.Add(pathParam.Key, pathParam.Value);
                    }
                    request.PathParams(pathParams);
                }
            }

            var handler =
                request.Method == HttpMethod.Get ? _getTree.Match(path, pathParams) :
                request.Method == HttpMethod.Post ? _postTree.Match(path, pathParams) :
                request.Method == HttpMethod.Put ? _putTree.Match(path, pathParams) :
                request.Method == HttpMethod.Delete ? _deleteTree.Match(path, pathParams) :
                request.Method == HttpMethod.Head ? _headTree.Match(path, pathParams) : null;

            if (handler != null)
                return await handler.HandleAsync(request);
            return null;
        }
        public override string ToString() => nameof(HttpRouter);
    }
}
