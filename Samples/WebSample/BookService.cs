using System;
using System.Threading.Tasks;
using System.Extensions.Http;
using System.Data;
using WebSample.Models;

namespace WebSample
{
    [Passport]
    public class BookService : BaseService
    {
        [Get("/Books")]
        public View Books() 
        {
            return View("/Book/Books");
        }
        //无扩展的代码
        //no extensions code
        [Post("/Books")]
        public async Task<JsonData> Books(IFormParams formParams)
        {
            var isbn = formParams.GetValue<string>("isbn");
            var name = formParams.GetValue<string>("name");
            var page = formParams.GetValue<int>("page");
            var limit = formParams.GetValue<int>("limit");
            var field = formParams.GetValue<string>("field");
            var order = formParams.GetValue<string>("order");

            //var where = Db.Where<Book>();
            //where = where.AndIf(!string.IsNullOrEmpty(isbn), (b, s) => b.ISBN.Contains(isbn));//不支持通配符,not support wildcard
            //where = where.AndIf(!string.IsNullOrEmpty(name), (b, s) => s.Like(b.Name, $"%{name}%")));

            var where = Db.Where<Book>()
                .AndIf(!string.IsNullOrEmpty(isbn), (b, s) => b.ISBN.Contains(isbn))//better
                .AndIf(!string.IsNullOrEmpty(name), (b, s) => s.Like(b.Name, $"%{name}%"));

            var orderBy = Db.OrderBy<Book>();
            if (order == "asc")
            {
                if (field == "CreateTime")
                    orderBy = (b, s) => s.Asc(b.CreateTime);
                else if (field == "Category.Name")
                    orderBy = (b, s) => s.Asc(b.Category.Name);
            }
            else if (order == "desc") 
            {
                if (field == "CreateTime")
                    orderBy = (b, s) => s.Desc(b.CreateTime);
                else if (field == "Category.Name")
                    orderBy = (b, s) => s.Desc(b.Category.Name);
            }

            var offset = (page - 1) * limit;

            (var books, var count) = await Db.SelectPagedAsync(offset, limit, (b, s) => s.Navigate(b), where, orderBy);

            return Json(0, "ok", count, books);
        }
        //Extended operations
        //cache Expression
        //private static OrderBy<Book> _OrderBy = new OrderBy<Book>((b, s) => s.Desc(b.CreateTime));
        //private static Select<Book> _Select = Select<Book>.Navigate;
        //private static Select<Book> _Select = new Select<Book>((b, s) => s.Navigate(b));
        //private static OrderByDictionary<Book> _OrderBy1 = new OrderByDictionary<Book>("ASC", "DESC", b => new object[] { b.CreateTime, b.Category.Name })
        //    .Add("name", b => b.Name)
        //    .AddAsc("isbn", b => b.ISBN)
        //    .AddDesc("a", b => b.Author);
        private static OrderByDictionary<Book> _OrderBy = new OrderByDictionary<Book>(b => new object[] { b.CreateTime, b.Category.Name });
        [Post("/Books1")]
        public async Task<JsonData> Books1(IFormParams formParams)
        {
            var isbn = formParams.GetValue<string>("isbn");
            var name = formParams.GetValue<string>("name");
            var page = formParams.GetValue<int>("page");
            var limit = formParams.GetValue<int>("limit");
            var field = formParams.GetValue<string>("field");
            var order = formParams.GetValue<string>("order");

            //var where = new Where<Book>();
            //where.AndIf(isbn, (b, s) => b.ISBN.Contains(isbn));
            ////where.AndIf(!string.IsNullOrEmpty(isbn), (b, s) => b.ISBN.Contains(isbn));
            //where.AndIf(name, (b, s) => s.Like(b.Name, $"%{name}%"));
            ////where.AndIf(!string.IsNullOrEmpty(name), (b, s) => s.Like(b.Name, $"%{name}%")));

            var where = new Where<Book>()
                .AndIf(isbn, (b, s) => b.ISBN.Contains(isbn))//better
                .AndIf(name, (b, s) => s.Like(b.Name, $"%{name}%"));

            var orderBy = _OrderBy[(field, order)];

            var offset = (page - 1) * limit;

            (var books, var count) = await Db.SelectPagedAsync<Book>(offset, limit, where, orderBy);

            return Json(0, "ok", count, books);
        }

        [Post("/Book/Delete/{id}")]
        public async Task<JsonData> Delete(int id) 
        {
            var count = await Db.DeleteAsync<Book>(id);

            if (count == 0)
                return Json(2002, "no data");

            return Json(0, "delete success");
        }
        [Post("/Book/Delete")]
        public async Task<JsonData> Delete(IFormParams formParams)
        {
            var ids = formParams.GetValue<string>("ids").Split<int>(',');

            var count = await Db.DeleteAsync<Book>((b, s) => s.In(b.Id, ids));

            return Json(0, $"delete {count} success");
        }
        [Get("/Book/Add")]
        public async Task<View> Add() 
        {
            var categories = await Db.SelectAsync<BookCategory>((c, s) => c, null);
            ViewData["Categories"] = categories;
            return View("/Book/Book", new Book());
        }
        [Post("/Book/Add")]
        public async Task<JsonData> Add(IFormParams formParams) 
        {
            var book = formParams.GetValue<Book>();
            if (!Validator.Validate(book, out var errorMsg))
            {
                return Json(444, errorMsg);
            }
            ////no extensions code
            //book.CreateTime = DateTime.Now;
            //await Db.InsertAsync(book, b => SqlDbExtensions.Except(new { b.Id }));

            await Db.InsertAsync(book);
            return Json(0, "ok");
        }
        [Get("/Book/Edit/{id}")]
        public async Task<View> Edit(int id)
        {
            var categories = await Db.SelectAsync<BookCategory>((c, s) => c, null);
            ViewData["Categories"] = categories;

            //var book = await Db.SelectSingleAsync<Book>((b, s) => s.Navigate(b), id);
            var book = await Db.SelectSingleAsync<Book>(id);
            return View("/Book/Book", book);
        }
        [Post("/Book/Edit")]
        public async Task<JsonData> Edit(IFormParams formParams)
        {
            var book = formParams.GetValue<Book>();
            if (!Validator.Validate(book, out var errorMsg))
            {
                return Json(444, errorMsg);
            }
            ////no extensions code
            //await Db.UpdateAsync(book,
            //    b => SqlDbExtensions.Except(new { b.Id, b.CreateTime }),
            //    (b, s) => b.Id == book.Id);
            //OR
            //await Db.UpdateAsync(book,
            //    b => SqlDbExtensions.Except(new { b.Id, b.CreateTime }));

            await Db.UpdateAsync(book);
            return Json(0, "ok");
        }
        private static string[] _ImgExts = new[] { ".jpg", ".png", ".gif" };
        [Post("/Book/Upload")]
        public async Task<JsonData> Upload(IFormFile image) //IFormFileParams TryGetValue("image")
        {   
            if (image == null)
            {
                return Json(3001, "no image");
            }
            if (image.Length > 1024 * 1024)
            {
                return Json(3002, "length error");
            }
            if (!image.TryGetExtension(out var extName, _ImgExts))//(out var extName, ".jpg", ".png", ".gif")
            {
                return Json(3003, "ext error");
            }

            var fileName = $"{Guid.NewGuid().ToString("N")}{extName}";
            await image.SaveAsync($"StaticFiles/Upload/{fileName}");
            return Json(0, "OK", $"/Upload/{fileName}");
        }
    }
}
