using System;
using System.Data;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Extensions.Http;
using System.Extensions.Net;
using WebSample.Models;

namespace WebSample
{
    public class AccountService : BaseService
    {
        [Get("/Register")]
        public View Register() 
        {
            return View("/Register");
        }
        private static Synchronization<string> _Register = new Synchronization<string>();
        [Post("/Register")]
        public JsonData Register(IFormParams formParams)
        {
            var name = formParams.GetValue<string>("name");
            var password = formParams.GetValue<string>("password");

            if (string.IsNullOrEmpty(name))
                return Json(1001, "name is empty");
            if (string.IsNullOrEmpty(password))
                return Json(1002, "password is empty");

            //OR _Register.WaitAsync(name)
            if (_Register.TryWait(name))
            {
                try
                {
                    var count= Db.Select<Account, int>((a, s) => s.Count(), (a, s) => a.Name == name);
                    if (count > 0)
                        return Json(1003, "name is exist");

                    Db.Insert<Account>((s) => new Account()
                    {
                        Name = name,
                        Password = password
                    });

                    return Json(0, "register success");
                }
                finally
                {
                    _Register.Realese(name);
                }
            }
            else 
            {
                return Json(1003, "server is busy");
            }
        }
        [Get("/")]
        [Get("/Login")]
        public View Login(Passport passport, IHttp2Pusher pusher) 
        {
            if (passport != null)
            {
                return View("/Book/Books");
            }

            if (pusher != null)
            {
                //only for Test(StaticFile don't use push)
                var jqResp = new HttpResponse();//not use Request.CreateResponse();
                jqResp.UseFile("StaticFiles/Js/jQuery-3.3.1/jquery.js");//push not Minify
                pusher.Push("/Js/jQuery-3.3.1/jquery.min.js", jqResp);
            }
            return View("/Login");
        }
        [Post("/Login")]
        public async Task<JsonData> Login(IFormParams formParams)
        {
            var name = formParams.GetValue<string>("name");
            var password = formParams.GetValue<string>("password");

            var account = await Db.SelectSingleAsync<Account>((a, s) => a, (a, s) => a.Name == name && a.Password == password);
            if (account == null)
                return Json(1101, "login error");

            var passport = new Passport(Response);
            passport.Id = account.Id;
            passport.Name = account.Name;

            return Json(0, "ok", "/Books");
        }
        [Post("/Logout")]
        public JsonData Logout(Passport passport)
        {
            passport?.Remove(Response);
            return Json(0, "ok");
        }
    }
}
