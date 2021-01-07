
namespace System.Collections.Generic
{
    using System.Diagnostics;
    public static class CollectionExtensions
    {
        public static string Concat(this IList<string> @this)
        {
            if (@this == null || @this.Count == 0)
                return null;

            if (@this.Count == 1)
                return @this[0];

            var length = 0;
            for (int i = 0; i < @this.Count; i++)
            {
                if (@this[i] != null)
                    length += @this[i].Length;
            }
            if (length == 0)
                return null;

            var value = new string('\0', length);
            unsafe
            {
                fixed (char* pDest = value)
                {
                    var tempSpan = new Span<char>(pDest, length);
                    for (int i = 0; i < @this.Count; i++)
                    {
                        if (@this[i] != null) 
                        {
                            @this[i].AsSpan().CopyTo(tempSpan);
                            tempSpan = tempSpan.Slice(@this[i].Length);
                        }
                    }
                    Debug.Assert(tempSpan.IsEmpty);
                }
            }
            return value;
        }
        public static string Concat(this IList<string> @this, (int, int) seg)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            (var s0, var e0) = seg;
            var length = 0;
            for (int i = s0; i < e0; i++)
            {
                if (@this[i] != null)
                    length += @this[i].Length;
            }
            if (length == 0)
                return null;
            var value = new string('\0', length);
            unsafe
            {
                fixed (char* pDest = value)
                {
                    var tempSpan = new Span<char>(pDest, length);
                    for (int i = s0; i < e0; i++)
                    {
                        if (@this[i] != null) 
                        {
                            @this[i].AsSpan().CopyTo(tempSpan);
                            tempSpan = tempSpan.Slice(@this[i].Length);
                        }
                    }
                    Debug.Assert(tempSpan.IsEmpty);
                }
            }
            return value;
        }
        public static string Concat(this IList<string> @this, (int, int) seg0, (int, int) seg1) 
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            (var s0, var e0) = seg0;
            (var s1, var e1) = seg1;
            var length = 0;
            for (int i = s0; i < e0; i++)
            {
                if (@this[i] != null)
                    length += @this[i].Length;
            }
            for (int i = s1; i < e1; i++)
            {
                if (@this[i] != null)
                    length += @this[i].Length;
            }
            if (length == 0)
                return null;
            var value = new string('\0', length);
            unsafe
            {
                fixed (char* pDest = value)
                {
                    var tempSpan = new Span<char>(pDest, length);
                    for (int i = s0; i < e0; i++)
                    {
                        if (@this[i] != null) 
                        {
                            @this[i].AsSpan().CopyTo(tempSpan);
                            tempSpan = tempSpan.Slice(@this[i].Length);
                        }
                          
                    }
                    for (int i = s1; i < e1; i++)
                    {
                        if (@this[i] != null)
                        {
                            @this[i].AsSpan().CopyTo(tempSpan);
                            tempSpan = tempSpan.Slice(@this[i].Length);
                        }
                    }
                    Debug.Assert(tempSpan.IsEmpty);
                }
            }
            return value;
        }
        public static string Concat(this IList<string> @this, (int, int) seg0, (int, int) seg1, (int, int) seg2)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            (var s0, var e0) = seg0;
            (var s1, var e1) = seg1;
            (var s2, var e2) = seg2;
            var length = 0;
            for (int i = s0; i < e0; i++)
            {
                if (@this[i] != null)
                    length += @this[i].Length;
            }
            for (int i = s1; i < e1; i++)
            {
                if (@this[i] != null)
                    length += @this[i].Length;
            }
            for (int i = s2; i < e2; i++)
            {
                if (@this[i] != null)
                    length += @this[i].Length;
            }
            if (length == 0)
                return null;
            var value = new string('\0', length);
            unsafe
            {
                fixed (char* pDest = value)
                {
                    var tempSpan = new Span<char>(pDest, length);
                    for (int i = s0; i < e0; i++)
                    {
                        if (@this[i] != null) 
                        {
                            @this[i].AsSpan().CopyTo(tempSpan);
                            tempSpan = tempSpan.Slice(@this[i].Length);
                        }  
                    }
                    for (int i = s1; i < e1; i++)
                    {
                        if (@this[i] != null)
                        {
                            @this[i].AsSpan().CopyTo(tempSpan);
                            tempSpan = tempSpan.Slice(@this[i].Length);
                        }
                    }
                    for (int i = s2; i < e2; i++)
                    {
                        if (@this[i] != null)
                        {
                            @this[i].AsSpan().CopyTo(tempSpan);
                            tempSpan = tempSpan.Slice(@this[i].Length);
                        }
                    }
                    Debug.Assert(tempSpan.IsEmpty);
                }
            }
            return value;
        }
        public static string Concat(this IList<string> @this, (int, int) seg0, (int, int) seg1, (int, int) seg2, (int, int) seg3)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            (var s0, var e0) = seg0;
            (var s1, var e1) = seg1;
            (var s2, var e2) = seg2;
            (var s3, var e3) = seg3;
            var length = 0;
            for (int i = s0; i < e0; i++)
            {
                if (@this[i] != null)
                    length += @this[i].Length;
            }
            for (int i = s1; i < e1; i++)
            {
                if (@this[i] != null)
                    length += @this[i].Length;
            }
            for (int i = s2; i < e2; i++)
            {
                if (@this[i] != null)
                    length += @this[i].Length;
            }
            for (int i = s3; i < e3; i++)
            {
                if (@this[i] != null)
                    length += @this[i].Length;
            }
            if (length == 0)
                return null;
            var value = new string('\0', length);
            unsafe
            {
                fixed (char* pDest = value)
                {
                    var tempSpan = new Span<char>(pDest, length);
                    for (int i = s0; i < e0; i++)
                    {
                        if (@this[i] != null)
                        {
                            @this[i].AsSpan().CopyTo(tempSpan);
                            tempSpan = tempSpan.Slice(@this[i].Length);
                        }
                    }
                    for (int i = s1; i < e1; i++)
                    {
                        if (@this[i] != null)
                        {
                            @this[i].AsSpan().CopyTo(tempSpan);
                            tempSpan = tempSpan.Slice(@this[i].Length);
                        }
                    }
                    for (int i = s2; i < e2; i++)
                    {
                        if (@this[i] != null)
                        {
                            @this[i].AsSpan().CopyTo(tempSpan);
                            tempSpan = tempSpan.Slice(@this[i].Length);
                        }
                    }
                    for (int i = s3; i < e3; i++)
                    {
                        if (@this[i] != null)
                        {
                            @this[i].AsSpan().CopyTo(tempSpan);
                            tempSpan = tempSpan.Slice(@this[i].Length);
                        }
                    }
                    Debug.Assert(tempSpan.IsEmpty);
                }
            }
            return value;
        }
        public static string Concat(this IList<string> @this, (int, int) seg0, (int, int) seg1, (int, int) seg2, (int, int) seg3, (int, int) seg4)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            (var s0, var e0) = seg0;
            (var s1, var e1) = seg1;
            (var s2, var e2) = seg2;
            (var s3, var e3) = seg3;
            (var s4, var e4) = seg4;
            var length = 0;
            for (int i = s0; i < e0; i++)
            {
                if (@this[i] != null)
                    length += @this[i].Length;
            }
            for (int i = s1; i < e1; i++)
            {
                if (@this[i] != null)
                    length += @this[i].Length;
            }
            for (int i = s2; i < e2; i++)
            {
                if (@this[i] != null)
                    length += @this[i].Length;
            }
            for (int i = s3; i < e3; i++)
            {
                if (@this[i] != null)
                    length += @this[i].Length;
            }
            for (int i = s4; i < e4; i++)
            {
                if (@this[i] != null)
                    length += @this[i].Length;
            }
            if (length == 0)
                return null;
            var value = new string('\0', length);
            unsafe
            {
                fixed (char* pDest = value)
                {
                    var tempSpan = new Span<char>(pDest, length);
                    for (int i = s0; i < e0; i++)
                    {
                        if (@this[i] != null)
                        {
                            @this[i].AsSpan().CopyTo(tempSpan);
                            tempSpan = tempSpan.Slice(@this[i].Length);
                        }
                    }
                    for (int i = s1; i < e1; i++)
                    {
                        if (@this[i] != null)
                        {
                            @this[i].AsSpan().CopyTo(tempSpan);
                            tempSpan = tempSpan.Slice(@this[i].Length);
                        }
                    }
                    for (int i = s2; i < e2; i++)
                    {
                        if (@this[i] != null)
                        {
                            @this[i].AsSpan().CopyTo(tempSpan);
                            tempSpan = tempSpan.Slice(@this[i].Length);
                        }
                    }
                    for (int i = s3; i < e3; i++)
                    {
                        if (@this[i] != null)
                        {
                            @this[i].AsSpan().CopyTo(tempSpan);
                            tempSpan = tempSpan.Slice(@this[i].Length);
                        }
                    }
                    for (int i = s4; i < e4; i++)
                    {
                        if (@this[i] != null)
                        {
                            @this[i].AsSpan().CopyTo(tempSpan);
                            tempSpan = tempSpan.Slice(@this[i].Length);
                        }
                    }
                    Debug.Assert(tempSpan.IsEmpty);
                }
            }
            return value;
        }
        public static string Concat(this IList<string> @this, (int, int) seg0, (int, int) seg1, (int, int) seg2, (int, int) seg3, (int, int) seg4, (int, int) seg5)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            (var s0, var e0) = seg0;
            (var s1, var e1) = seg1;
            (var s2, var e2) = seg2;
            (var s3, var e3) = seg3;
            (var s4, var e4) = seg4;
            (var s5, var e5) = seg5;
            var length = 0;
            for (int i = s0; i < e0; i++)
            {
                if (@this[i] != null)
                    length += @this[i].Length;
            }
            for (int i = s1; i < e1; i++)
            {
                if (@this[i] != null)
                    length += @this[i].Length;
            }
            for (int i = s2; i < e2; i++)
            {
                if (@this[i] != null)
                    length += @this[i].Length;
            }
            for (int i = s3; i < e3; i++)
            {
                if (@this[i] != null)
                    length += @this[i].Length;
            }
            for (int i = s4; i < e4; i++)
            {
                if (@this[i] != null)
                    length += @this[i].Length;
            }
            for (int i = s5; i < e5; i++)
            {
                if (@this[i] != null)
                    length += @this[i].Length;
            }
            if (length == 0)
                return null;
            var value = new string('\0', length);
            unsafe
            {
                fixed (char* pDest = value)
                {
                    var tempSpan = new Span<char>(pDest, length);
                    for (int i = s0; i < e0; i++)
                    {
                        if (@this[i] != null)
                        {
                            @this[i].AsSpan().CopyTo(tempSpan);
                            tempSpan = tempSpan.Slice(@this[i].Length);
                        }
                    }
                    for (int i = s1; i < e1; i++)
                    {
                        if (@this[i] != null)
                        {
                            @this[i].AsSpan().CopyTo(tempSpan);
                            tempSpan = tempSpan.Slice(@this[i].Length);
                        }
                    }
                    for (int i = s2; i < e2; i++)
                    {
                        if (@this[i] != null)
                        {
                            @this[i].AsSpan().CopyTo(tempSpan);
                            tempSpan = tempSpan.Slice(@this[i].Length);
                        }
                    }
                    for (int i = s3; i < e3; i++)
                    {
                        if (@this[i] != null)
                        {
                            @this[i].AsSpan().CopyTo(tempSpan);
                            tempSpan = tempSpan.Slice(@this[i].Length);
                        }
                    }
                    for (int i = s4; i < e4; i++)
                    {
                        if (@this[i] != null)
                        {
                            @this[i].AsSpan().CopyTo(tempSpan);
                            tempSpan = tempSpan.Slice(@this[i].Length);
                        }
                    }
                    for (int i = s5; i < e5; i++)
                    {
                        if (@this[i] != null)
                        {
                            @this[i].AsSpan().CopyTo(tempSpan);
                            tempSpan = tempSpan.Slice(@this[i].Length);
                        }
                    }
                    Debug.Assert(tempSpan.IsEmpty);
                }
            }
            return value;
        }
        public static string Concat(this IList<string> @this, (int, int) seg0, (int, int) seg1, (int, int) seg2, (int, int) seg3, (int, int) seg4, (int, int) seg5, (int, int) seg6)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            (var s0, var e0) = seg0;
            (var s1, var e1) = seg1;
            (var s2, var e2) = seg2;
            (var s3, var e3) = seg3;
            (var s4, var e4) = seg4;
            (var s5, var e5) = seg5;
            (var s6, var e6) = seg6;
            var length = 0;
            for (int i = s0; i < e0; i++)
            {
                if (@this[i] != null)
                    length += @this[i].Length;
            }
            for (int i = s1; i < e1; i++)
            {
                if (@this[i] != null)
                    length += @this[i].Length;
            }
            for (int i = s2; i < e2; i++)
            {
                if (@this[i] != null)
                    length += @this[i].Length;
            }
            for (int i = s3; i < e3; i++)
            {
                if (@this[i] != null)
                    length += @this[i].Length;
            }
            for (int i = s4; i < e4; i++)
            {
                if (@this[i] != null)
                    length += @this[i].Length;
            }
            for (int i = s5; i < e5; i++)
            {
                if (@this[i] != null)
                    length += @this[i].Length;
            }
            for (int i = s6; i < e6; i++)
            {
                if (@this[i] != null)
                    length += @this[i].Length;
            }
            if (length == 0)
                return null;
            var value = new string('\0', length);
            unsafe
            {
                fixed (char* pDest = value)
                {
                    var tempSpan = new Span<char>(pDest, length);
                    for (int i = s0; i < e0; i++)
                    {
                        if (@this[i] != null)
                        {
                            @this[i].AsSpan().CopyTo(tempSpan);
                            tempSpan = tempSpan.Slice(@this[i].Length);
                        }
                    }
                    for (int i = s1; i < e1; i++)
                    {
                        if (@this[i] != null)
                        {
                            @this[i].AsSpan().CopyTo(tempSpan);
                            tempSpan = tempSpan.Slice(@this[i].Length);
                        }
                    }
                    for (int i = s2; i < e2; i++)
                    {
                        if (@this[i] != null)
                        {
                            @this[i].AsSpan().CopyTo(tempSpan);
                            tempSpan = tempSpan.Slice(@this[i].Length);
                        }
                    }
                    for (int i = s3; i < e3; i++)
                    {
                        if (@this[i] != null)
                        {
                            @this[i].AsSpan().CopyTo(tempSpan);
                            tempSpan = tempSpan.Slice(@this[i].Length);
                        }
                    }
                    for (int i = s4; i < e4; i++)
                    {
                        if (@this[i] != null)
                        {
                            @this[i].AsSpan().CopyTo(tempSpan);
                            tempSpan = tempSpan.Slice(@this[i].Length);
                        }
                    }
                    for (int i = s5; i < e5; i++)
                    {
                        if (@this[i] != null)
                        {
                            @this[i].AsSpan().CopyTo(tempSpan);
                            tempSpan = tempSpan.Slice(@this[i].Length);
                        }
                    }
                    for (int i = s6; i < e6; i++)
                    {
                        if (@this[i] != null)
                        {
                            @this[i].AsSpan().CopyTo(tempSpan);
                            tempSpan = tempSpan.Slice(@this[i].Length);
                        }
                    }
                    Debug.Assert(tempSpan.IsEmpty);
                }
            }
            return value;
        }
        public static bool TryDequeue<T1, T2>(this Queue<(T1, T2)> @this, out T1 item1, out T2 item2)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var @bool = @this.TryDequeue(out var result);
            item1 = result.Item1;
            item2 = result.Item2;
            return @bool;
        }
        public static bool TryDequeue<T1, T2, T3>(this Queue<(T1, T2, T3)> @this, out T1 item1, out T2 item2, out T3 item3)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var @bool = @this.TryDequeue(out var result);
            item1 = result.Item1;
            item2 = result.Item2;
            item3 = result.Item3;
            return @bool;
        }
        public static bool TryGetValue<TKey, T1, T2>(this IDictionary<TKey, (T1, T2)> @this, TKey key, out T1 item1, out T2 item2)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var @bool = @this.TryGetValue(key, out var value);
            item1 = value.Item1;
            item2 = value.Item2;
            return @bool;
        }
        public static bool TryGetValue<TKey, T1, T2, T3>(this IDictionary<TKey, (T1, T2, T3)> @this, TKey key, out T1 item1, out T2 item2, out T3 item3)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var @bool = @this.TryGetValue(key, out var value);
            item1 = value.Item1;
            item2 = value.Item2;
            item3 = value.Item3;
            return @bool;
        }
    }
}
