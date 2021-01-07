using System;
using System.Runtime.Serialization;

namespace WebSample.Models
{
    public class Book
    {
        public int Id { get; set; }
        public string ISBN { get; set; }
        [Required("Name is required")]
        public string Name { get; set; }
        public string Author { get; set; }
        [Validate(nameof(ValidateUrl))]
        public string ImageUrl { get; set; }
        public int CategoryId { get; set; }
        public BookCategory Category { get; set; }
        [DataFormat("yyyy-MM-dd HH:mm:ss")]//JSON
        public DateTime CreateTime { get; set; }
        public string ValidateUrl() 
        {
            if (string.IsNullOrEmpty(ImageUrl))
                return null;

            //for Test
            if (ImageUrl.StartsWith('/')
                || ImageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || ImageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return null;

            return "Url Error";
        }
    }
}