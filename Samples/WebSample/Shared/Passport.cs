using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Extensions.Http;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace WebSample
{
    public class Passport
    {
        //persistence use json
        //static Passport()
        //{
        //    Constructor.Register(() => new Passport());
        //    if (File.Exists("passport.json"))
        //    {
        //        using (var reader = new StreamReader("passport.json"))
        //        {
        //            var passports = JsonReader.FromJson<List<(Passport, DateTimeOffset)>>(reader);
        //            foreach ((var passport, var expire) in passports)
        //            {
        //                _Passports.TryAdd(passport.Key, passport, expire);
        //            }
        //        }
        //    }
        //}
        //public static void Save()
        //{
        //    var passports = new List<(Passport, DateTimeOffset)>();
        //    _Passports.ForEach((key, passport, expire) => {
        //        passports.Add((passport, expire));
        //    });
        //    using (var writer = new StreamWriter("passport.json"))
        //    {
        //        JsonWriter.ToJson(passports, writer);
        //    }
        //}
        //private Passport()
        //{

        //}

        private static string _CookieName = "Passport";
        private static TimeSpan _Timeout = TimeSpan.FromHours(3);
        private static Cache<string, Passport> _Passports = new Cache<string, Passport>();
        public static Passport Load(HttpRequest request)
        {
            var cookieParams = request.CookieParams();
            if (cookieParams.TryGetValue(_CookieName, out var key)
                && _Passports.TryUpdate(key, DateTimeOffset.Now.Add(_Timeout), out var passport))
            {
                return passport;
            }
            return null;
        }
        public Passport(HttpResponse response)
        {
            Key = new string('\0', 64);//128
            var key = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(Key.AsSpan()), Key.Length);//OR unsafe
            do
            {
                Guid.NewGuid().TryFormat(key, out _, "N");
                Guid.NewGuid().TryFormat(key.Slice(32), out _, "N");
                //Guid.NewGuid().TryFormat(key.Slice(64), out _, "N");
                //Guid.NewGuid().TryFormat(key.Slice(96), out _, "N");
            } while (!_Passports.TryAdd(Key, this, DateTimeOffset.Now.Add(_Timeout)));
            response.UseCookie("Passport", Key, httpOnly: true);
        }
        public void Remove(HttpResponse response)
        {
            _Passports.TryRemove(Key, out _);
            response.UseCookie("Passport", null, maxAge: 0);//Remove Cookie
        }
        public string Key { get; set; }
        //public DateTimeOffset Expire { get; set; }

        //Custom Property
        public int Id { get; set; }//uid
        public string Name { get; set; }

        //ADD
    }
    public class PassportAttribute : Attribute
    {
        public IHttpHandler Invoke(MethodInfo method)
        {
            if (method.ReturnType == typeof(JsonData) ||method.ReturnType==typeof(Task<JsonData>))
            {
                return HttpHandler.CreateModule((request, handler) =>
                {
                    var passport = request.GetPassport();
                    if (passport == null)
                    {
                        var response = request.CreateResponse();
                        var jsonData = new JsonData
                        {
                            Code = 403,
                            Message = "Not Login"
                        };
                        jsonData.Invoke(request, response);
                        return Task.FromResult(response);
                    }
                    return handler.HandleAsync(request);
                });
            }
            else
            {
                return HttpHandler.CreateModule((request, handler) => {
                    var passport = request.GetPassport();
                    if (passport == null)
                    {
                        var response = request.CreateResponse();
                        response.UseRedirect("/Login");
                        return Task.FromResult(response);
                    }
                    return handler.HandleAsync(request);
                });
            }
        }
    }
    public static class PassportExtensions
    {
        private static Property<HttpRequest> _Passport = new Property<HttpRequest>("Passport");
        public static Passport GetPassport(this HttpRequest @this)
        {
            var passport = (Passport)@this.Properties[_Passport];
            if (passport == null)
            {
                passport = Passport.Load(@this);
                @this.Properties[_Passport] = passport;
            }
            return passport;
        }
    }
}
