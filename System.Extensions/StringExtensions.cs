
namespace System.Extensions
{
    using System.Text;
    public static class StringExtensions
    {
        static StringExtensions()
        {
            #region Converter
            TConverter<string>.Converter = (val) => (true, val);
            TConverter<byte>.Converter = (val) => byte.TryParse(val, out var res) ? (true, res) : (false, default);
            TConverter<byte?>.Converter = (val) => byte.TryParse(val, out var res) ? (true, (byte?)res) : (true, null);
            TConverter<sbyte>.Converter = (val) => sbyte.TryParse(val, out var res) ? (true, res) : (false, default);
            TConverter<sbyte?>.Converter = (val) => sbyte.TryParse(val, out var res) ? (true, (sbyte?)res) : (true, null);
            TConverter<short>.Converter = (val) => short.TryParse(val, out var res) ? (true, res) : (false, default);
            TConverter<short?>.Converter = (val) => short.TryParse(val, out var res) ? (true, (short?)res) : (true, null);
            TConverter<ushort>.Converter = (val) => ushort.TryParse(val, out var res) ? (true, res) : (false, default);
            TConverter<ushort?>.Converter = (val) => ushort.TryParse(val, out var res) ? (true, (ushort?)res) : (true, null);
            TConverter<int>.Converter = (val) => int.TryParse(val, out var res) ? (true, res) : (false, default);
            TConverter<int?>.Converter = (val) => int.TryParse(val, out var res) ? (true, (int?)res) : (true, null);
            TConverter<uint>.Converter = (val) => uint.TryParse(val, out var res) ? (true, res) : (false, default);
            TConverter<uint?>.Converter = (val) => uint.TryParse(val, out var res) ? (true, (uint?)res) : (true, null);
            TConverter<long>.Converter = (val) => long.TryParse(val, out var res) ? (true, res) : (false, default);
            TConverter<long?>.Converter = (val) => long.TryParse(val, out var res) ? (true, (long?)res) : (true, null);
            TConverter<ulong>.Converter = (val) => ulong.TryParse(val, out var res) ? (true, res) : (false, default);
            TConverter<ulong?>.Converter = (val) => ulong.TryParse(val, out var res) ? (true, (ulong?)res) : (true, null);
            TConverter<float>.Converter = (val) => float.TryParse(val, out var res) ? (true, res) : (false, default);
            TConverter<float?>.Converter = (val) => float.TryParse(val, out var res) ? (true, (float?)res) : (true, null);
            TConverter<double>.Converter = (val) => double.TryParse(val, out var res) ? (true, res) : (false, default);
            TConverter<double?>.Converter = (val) => double.TryParse(val, out var res) ? (true, (double?)res) : (true, null);
            TConverter<decimal>.Converter = (val) => decimal.TryParse(val, out var res) ? (true, res) : (false, default);
            TConverter<decimal?>.Converter = (val) => decimal.TryParse(val, out var res) ? (true, (decimal?)res) : (true, null);
            TConverter<DateTime>.Converter = (val) => DateTime.TryParse(val, out var res) ? (true, res) : (false, default);
            TConverter<DateTime?>.Converter = (val) => DateTime.TryParse(val, out var res) ? (true, (DateTime?)res) : (true, null);
            TConverter<DateTimeOffset>.Converter = (val) => DateTimeOffset.TryParse(val, out var res) ? (true, res) : (false, default);
            TConverter<DateTimeOffset?>.Converter = (val) => DateTimeOffset.TryParse(val, out var res) ? (true, (DateTimeOffset?)res) : (true, null);
            #endregion
        }
        private class TConverter<T>
        {
            public static Func<string, (bool Success, T Value)> Converter;
        }
        public static bool TryConvert<T>(this string @this, out T value)
        {
            var converter = TConverter<T>.Converter;
            if (converter != null)
            {
                var result = converter.Invoke(@this);
                value = result.Value;
                return result.Success;
            }
            value = default;
            return false;
        }
        public static void Converter<T>(Func<string, (bool, T)> converter)
        {
            TConverter<T>.Converter = converter;
        }
        public static Func<string, (bool, T)> Converter<T>()
        {
            return TConverter<T>.Converter;
        }

        #region ThreadStatic
        [ThreadStatic]
        private static StringBuffer _InstanceA;
        [ThreadStatic]
        private static StringBuffer _InstanceB;
        [ThreadStatic]
        private static StringBuffer _InstanceC;
        #endregion
        public static StringBuffer Rent()
        {
            var sb = _InstanceA;
            if (sb != null)
            {
                _InstanceA = null;
                return sb;
            }
            sb = _InstanceB;
            if (sb != null)
            {
                _InstanceB = null;
                return sb;
            }
            sb = _InstanceC;
            if (sb != null)
            {
                _InstanceC = null;
                return sb;
            }
            return new StringBuffer(128);
        }
        public static void Return(StringBuffer sb)
        {
            if (_InstanceA == null)
            {
                sb.Clear();
                _InstanceA = sb;
            }
            else if (_InstanceB == null)
            {
                sb.Clear();
                _InstanceB = sb;
            }
            else if (_InstanceC == null)
            {
                sb.Clear();
                _InstanceC = sb;
            }
        }
        public static string GetReturn(StringBuffer sb)
        {
            var result = sb.ToString();
            Return(sb);
            return result;
        }
    }
}
