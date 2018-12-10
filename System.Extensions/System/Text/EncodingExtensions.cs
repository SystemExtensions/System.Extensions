
namespace System.Text
{
    public static class EncodingExtensions
    {
        public static int GetByteCount(this Encoding @this, StringBuffer value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            var num = 0;
            foreach (var segm in value.GetEnumerable())
            {
                num += @this.GetByteCount(segm.Span);
            }
            return num;
        }

        //GetBytes
        //var x = new StackBuilder(16);
        //ValueStringBuilder
    }
}
