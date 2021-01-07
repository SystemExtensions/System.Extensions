using System;
using System.Collections.Generic;
using System.Text;

namespace WebSample
{
    public class LengthAttribute:Attribute
    {
        static LengthAttribute() 
        {
            Validator.Register<LengthAttribute, string>((attribute, value) => {
                var lenth = value.Length;
                if (attribute.MinimumLength >= 0&& attribute.MinimumLength > lenth) 
                {
                    return attribute.ErrorMessage;
                }
                if (attribute.MaximumLength >= 0&& attribute.MaximumLength < lenth)
                {
                    return attribute.ErrorMessage;
                }
                return null;
            });
        }
        public LengthAttribute( int minimumLength, int maximumLength,string errorMessage) 
        {
            if (errorMessage == null)
                throw new ArgumentNullException(nameof(errorMessage));

            _minimumLength = minimumLength;
            _maximumLength = maximumLength;
            _errorMessage = errorMessage;
        }
        private int _minimumLength;
        private int _maximumLength;
        private string _errorMessage;
        public int MinimumLength => _minimumLength;
        public int MaximumLength => _maximumLength;

        public string ErrorMessage => _errorMessage;
    }
}
