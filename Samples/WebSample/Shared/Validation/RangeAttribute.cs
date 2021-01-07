
namespace WebSample
{
    using System;
    using System.Linq.Expressions;
    public class RangeAttribute : Attribute
    {
        static RangeAttribute() 
        {
            Validator.Register<RangeAttribute, sbyte>((attribute, value) => value >= (sbyte)attribute.Minimum && value <= (sbyte)attribute.Maximum ? null : attribute.ErrorMessage);
            Validator.Register<RangeAttribute, byte>((attribute, value) => value >= (byte)attribute.Minimum && value <= (byte)attribute.Maximum ? null : attribute.ErrorMessage);
            Validator.Register<RangeAttribute, short>((attribute, value) => value >= (short)attribute.Minimum && value <= (short)attribute.Maximum ? null : attribute.ErrorMessage);
            Validator.Register<RangeAttribute, ushort>((attribute, value) => value >= (ushort)attribute.Minimum && value <= (ushort)attribute.Maximum ? null : attribute.ErrorMessage);
            Validator.Register<RangeAttribute, int>((attribute, value) => value >= (int)attribute.Minimum && value <= (int)attribute.Maximum ? null : attribute.ErrorMessage);
            Validator.Register<RangeAttribute, uint>((attribute, value) => value >= (uint)attribute.Minimum && value <= (uint)attribute.Maximum ? null : attribute.ErrorMessage);
            Validator.Register<RangeAttribute, long>((attribute, value) => value >= (long)attribute.Minimum && value <= (long)attribute.Maximum ? null : attribute.ErrorMessage);
            Validator.Register<RangeAttribute, ulong>((attribute, value) => value >= (ulong)attribute.Minimum && value <= (ulong)attribute.Maximum ? null : attribute.ErrorMessage);
            Validator.Register<RangeAttribute, float>((attribute, value) => value >= (float)attribute.Minimum && value <= (float)attribute.Maximum ? null : attribute.ErrorMessage);
            Validator.Register<RangeAttribute, double>((attribute, value) => value >= (double)attribute.Minimum && value <= (double)attribute.Maximum ? null : attribute.ErrorMessage);
            Validator.Register<RangeAttribute, decimal>((attribute, value) => value >= (decimal)attribute.Minimum && value <= (decimal)attribute.Maximum ? null : attribute.ErrorMessage);
        }
        public RangeAttribute(object minimum, object maximum,string errorMessage) 
        {
            if (errorMessage == null)
                throw new ArgumentNullException(nameof(errorMessage));

            _minimum = minimum;
            _maximum = maximum;
            _errorMessage = errorMessage;
        }
        private object _minimum;
        private object _maximum;
        private string _errorMessage;
        public object Minimum => _minimum;
        public object Maximum => _maximum;
        public string ErrorMessage => _errorMessage;
    }
}
