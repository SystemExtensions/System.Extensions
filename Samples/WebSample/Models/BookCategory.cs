
namespace WebSample.Models
{
    
    public class BookCategory
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
    //use inherit => different behaviors
    [RawString]
    public class BookCategory1 : BookCategory
    {

    }
}
