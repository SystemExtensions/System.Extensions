
namespace System
{
    public static class DateTimeExtensions
    {
        public static DateTime Year(this DateTime @this)
        {
            return new DateTime(@this.Year, 1, 1, 0, 0, 0, @this.Kind);
        }
        public static DateTime Year(this DateTime @this, int value)
        {
            return new DateTime(@this.Year, 1, 1, 0, 0, 0, @this.Kind).AddYears(value);
        }
        public static DateTime Month(this DateTime @this)
        {
            return new DateTime(@this.Year, @this.Month, 1, 0, 0, 0, @this.Kind);
        }
        public static DateTime Month(this DateTime @this, int value)
        {
            return new DateTime(@this.Year, @this.Month, 1, 0, 0, 0, @this.Kind).AddMonths(value);
        }
        public static DateTime Day(this DateTime @this)
        {
            return new DateTime(@this.Year, @this.Month, @this.Day, 0, 0, 0, @this.Kind);
        }
        public static DateTime Day(this DateTime @this, int value)
        {
            return new DateTime(@this.Year, @this.Month, @this.Day, 0, 0, 0, @this.Kind).AddDays(value);
        }
        public static DateTime Hour(this DateTime @this)
        {
            return new DateTime(@this.Year, @this.Month, @this.Day, @this.Hour, 0, 0, @this.Kind);
        }
        public static DateTime Hour(this DateTime @this, int value)
        {
            return new DateTime(@this.Year, @this.Month, @this.Day, @this.Hour, 0, 0, @this.Kind).AddHours(value);
        }
        public static DateTime Minute(this DateTime @this)
        {
            return new DateTime(@this.Year, @this.Month, @this.Day, @this.Hour, @this.Minute, 0, @this.Kind);
        }
        public static DateTime Minute(this DateTime @this, int value)
        {
            return new DateTime(@this.Year, @this.Month, @this.Day, @this.Hour, @this.Minute, 0, @this.Kind).AddMinutes(value);
        }
        public static DateTime Second(this DateTime @this)
        {
            return new DateTime(@this.Year, @this.Month, @this.Day, @this.Hour, @this.Minute, @this.Second, @this.Kind);
        }
        public static DateTime Second(this DateTime @this, int value)
        {
            return new DateTime(@this.Year, @this.Month, @this.Day, @this.Hour, @this.Minute, @this.Second, @this.Kind).AddSeconds(value);
        }
        public static DateTimeOffset Year(this DateTimeOffset @this)
        {
            return new DateTimeOffset(@this.Year, 1, 1, 0, 0, 0, @this.Offset);
        }
        public static DateTimeOffset Year(this DateTimeOffset @this, int value)
        {
            return new DateTimeOffset(@this.Year, 1, 1, 0, 0, 0, @this.Offset).AddYears(value);
        }
        public static DateTimeOffset Month(this DateTimeOffset @this)
        {
            return new DateTimeOffset(@this.Year, @this.Month, 1, 0, 0, 0, @this.Offset);
        }
        public static DateTimeOffset Month(this DateTimeOffset @this, int value)
        {
            return new DateTimeOffset(@this.Year, @this.Month, 1, 0, 0, 0, @this.Offset).AddMonths(value);
        }
        public static DateTimeOffset Day(this DateTimeOffset @this)
        {
            return new DateTimeOffset(@this.Year, @this.Month, @this.Day, 0, 0, 0, @this.Offset);
        }
        public static DateTimeOffset Day(this DateTimeOffset @this, int value)
        {
            return new DateTimeOffset(@this.Year, @this.Month, @this.Day, 0, 0, 0, @this.Offset).AddDays(value);
        }
        public static DateTimeOffset Hour(this DateTimeOffset @this)
        {
            return new DateTimeOffset(@this.Year, @this.Month, @this.Day, @this.Hour, 0, 0, @this.Offset);
        }
        public static DateTimeOffset Hour(this DateTimeOffset @this, int value)
        {
            return new DateTimeOffset(@this.Year, @this.Month, @this.Day, @this.Hour, 0, 0, @this.Offset).AddHours(value);
        }
        public static DateTimeOffset Minute(this DateTimeOffset @this)
        {
            return new DateTimeOffset(@this.Year, @this.Month, @this.Day, @this.Hour, @this.Minute, 0, @this.Offset);
        }
        public static DateTimeOffset Minute(this DateTimeOffset @this, int value)
        {
            return new DateTimeOffset(@this.Year, @this.Month, @this.Day, @this.Hour, @this.Minute, 0, @this.Offset).AddMinutes(value);
        }
        public static DateTimeOffset Second(this DateTimeOffset @this)
        {
            return new DateTimeOffset(@this.Year, @this.Month, @this.Day, @this.Hour, @this.Minute, @this.Second, @this.Offset);
        }
        public static DateTimeOffset Second(this DateTimeOffset @this, int value)
        {
            return new DateTimeOffset(@this.Year, @this.Month, @this.Day, @this.Hour, @this.Minute, @this.Second, @this.Offset).AddSeconds(value);
        }
    }
}
