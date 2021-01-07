using System;
using System.Collections.Generic;
using System.Text;

namespace WebSample
{
    public class EmailAttribute:Attribute
    {
        static EmailAttribute() 
        {
            Validator.Register<EmailAttribute, string>((attribute, value) => {
                var index = value.IndexOf('@');
                if (index > 0 &&
                    index != value.Length - 1 &&
                    index == value.LastIndexOf('@'))
                    return null;
                return attribute.ErrorMessage;
            });
        }

        public EmailAttribute(string errorMessage) 
        {
            if (errorMessage == null)
                throw new ArgumentNullException(nameof(errorMessage));

            _errorMessage = errorMessage;
        }

        private string _errorMessage;
        public string ErrorMessage => _errorMessage;
    }
}
