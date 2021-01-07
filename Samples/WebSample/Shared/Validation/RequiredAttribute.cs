using System;
using System.Collections.Generic;
using System.Text;

namespace WebSample
{
    using System.Linq.Expressions;
    public class RequiredAttribute:Attribute
    {
        static RequiredAttribute() 
        {
            Validator.Register((attribute, type, value) => {
                if (attribute.GetType() != typeof(RequiredAttribute))
                    return null;

                return Expression.Condition(
                    Expression.Equal(value, Expression.Default(type)),
                    Expression.Constant(((RequiredAttribute)attribute).ErrorMessage),
                    Expression.Constant(null, typeof(string))
                    );
            });
            Validator.Register<RequiredAttribute, string>((attribute, value) => string.IsNullOrEmpty(value) ? attribute.ErrorMessage : null);
        }
        public RequiredAttribute(string errorMessage) 
        {
            if (errorMessage == null)
                throw new ArgumentNullException(nameof(errorMessage));

            _errorMessage = errorMessage;
        }
        private string _errorMessage;
        public string ErrorMessage => _errorMessage;
    }
}
