
namespace System.IO
{
    using System.Diagnostics;
    using System.Reflection;
    using System.Linq.Expressions;
    public static class DirectoryInfoExtensions//TODO? Move FileExtensions
    {
        static DirectoryInfoExtensions()
        {
            try
            {
                //https://github.com/dotnet/runtime/blob/master/src/libraries/System.Private.CoreLib/src/System/IO/PathInternal.cs#L108
                var path = Expression.Parameter(typeof(string), "path");
                var rootLength = Expression.Parameter(typeof(int), "rootLength");
                var removeRelativeSegments = typeof(Path).Assembly.GetType("System.IO.PathInternal").GetMethod("RemoveRelativeSegments", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(string), typeof(int) }, null);
                var ctor = typeof(FileInfo).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(string), typeof(string), typeof(string), typeof(bool) }, null);
                _GetFile = Expression.Lambda<Func<string, int, FileInfo>>(
                    Expression.New(ctor,
                        Expression.Call(removeRelativeSegments, path, rootLength),
                        Expression.Constant(null, typeof(string)),
                        Expression.Constant(null, typeof(string)),
                        Expression.Constant(true)),
                    path, rootLength).Compile();
            }
            catch
            {
                Trace.WriteLine(nameof(_GetFile));
                _GetFile = (path, rootPath) =>
                {
                    var file = new FileInfo(path);
                    if (!file.FullName.AsSpan().StartsWith(path.AsSpan(0, rootPath)))
                        return null;
                    return file;
                };
            }
        }

        private static Func<string, int, FileInfo> _GetFile;//TODO? ValueStringBuilder(IntPtr)
        public static FileInfo GetFile(this DirectoryInfo @this, string path)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            var root = @this.FullName;
            return Path.EndsInDirectorySeparator(root)
                ? _GetFile($"{root}{path}", root.Length)
                : _GetFile($"{root}{Path.DirectorySeparatorChar}{path}", root.Length + 1);//TODO?? optimization unnecessary
        }
    }
}
