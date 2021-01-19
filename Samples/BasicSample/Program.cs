using System;
using System.Threading.Tasks;

namespace BasicSample
{
    class Program
    {
        static void Main(string[] args)
        {
            //HttpSample.Run();
            //HandlerCompilerSample.Run();
            //HttpRouterSample.Run();


            //HttpServerClientSample.RunHttp();
            HttpServerClientSample.RunHttps();
            //HttpServerClientSample.RunHttpAndHttps();
            //HttpServerClientSample.RunHttp2();
            //HttpServerClientSample.RunHttpAndH2();
            //HttpServerClientSample.RunH2c();
            //HttpServerClientSample.RunGateway();
            //HttpServerClientSample.RunUnixDomainSocket();
            //HttpServerClientSample.RunParallelClient();
            //HttpServerClientSample.RunIPv6();


            //HttpProxySample.RunHttp();
            //HttpProxySample.RunHttps();
            //HttpProxySample.RunHttpAndHttps();
            //HttpProxySample.RunHttpsFiddle();
            //HttpProxySample.RunLocalRemote();


            //Task.Run(async () => await SqlDbSample.RunSQLite()).Wait();
            //Task.Run(async () => await SqlDbSample.RunSqlServer()).Wait();
            //Task.Run(async () => await SqlDbSample.RunMySql()).Wait();
            //Task.Run(async () => await SqlDbSample.RunPostgre()).Wait();
            //Task.Run(async () => await SqlDbSample.RunOracle()).Wait();


            //UrlSample.Run();
            //UrlSample.RunEncoding();


            //BufferSample.Run();


            //JsonWriterSample.Run();
            //JsonReaderSample.Run();


            //CompressionSample.RunGzip();
            //CompressionSample.RunDeflate();


            //CacheSample.Run();
            //Task.Run(async () => await CacheSample.RunDataAsync()).Wait();


            //Task.Run(async () => await TimeoutSample.Run()).Wait();
            //Task.Run(async () => await TimeoutSample.RunQueue()).Wait();
            //Task.Run(async () => await TimeoutSample.RunQueueLax()).Wait();


            //SynchronizationSample.RunRegister();
            //Task.Run(async () => await SynchronizationSample.RunOrder()).Wait();
            //Task.Run(async () => await SynchronizationSample.RunTransfer()).Wait();


            //PropertyCollectionSample.Run();


            Console.ReadLine();
        }
    }
}
