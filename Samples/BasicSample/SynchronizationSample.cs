using System;
using System.Threading;
using System.Threading.Tasks;

namespace BasicSample
{
    public class SynchronizationSample
    {
        private static Synchronization<string> _Register = new Synchronization<string>();
        public static void RunRegister() 
        {
            var email = "zhanghe1024@qq.com";
            //var pwd = "123456";
            if (_Register.TryWait(email))
            {
                try
                {
                    Console.WriteLine("Do Register");
                }
                finally
                {
                    _Register.Realese(email);
                }
            }
            else 
            {
                Console.WriteLine("server is busy");
            }
        }
        private static Synchronization<int> _Order = new Synchronization<int>();
        public static async Task RunOrder() 
        {
            var orderId = 100012;
            //var status = "ToPay";
            await _Order.WaitAsync(orderId);
            try
            {
                Console.WriteLine("Do Order");
            }
            finally
            {
                _Order.Realese(orderId);
            }
        }
        private static Synchronization<int> _Transfer = new Synchronization<int>();
        public static async Task RunTransfer() 
        {
            var uid1 = 100;
            var uid2 = 200;
            //var money = 5000;
            if (uid1 == uid2) 
            {
                Console.WriteLine("not allow");
                return;
            }
            var smallUid = 0;
            var bigUid = 0;
            if (uid1 > uid2)
            {
                bigUid = uid1;
                smallUid = uid2;
            }
            else 
            {
                bigUid = uid2;
                smallUid = uid1;
            }
            await _Transfer.WaitAsync(smallUid);//big->small OR small->big
            try
            {
                await _Transfer.WaitAsync(bigUid);
                try
                {

                    Console.WriteLine("Do Transfer");
                }
                finally
                {
                    _Transfer.Realese(bigUid);
                }
            }
            finally
            {
                _Transfer.Realese(smallUid);
            }
        }
    }
}
