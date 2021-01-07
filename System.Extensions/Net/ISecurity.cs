
namespace System.Extensions.Net
{
    using System.Net.Security;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    public interface ISecurity
    {
        X509Certificate LocalCertificate { get; }
        X509Certificate RemoteCertificate { get; }
        SslApplicationProtocol ApplicationProtocol { get; }
        SslProtocols Protocol { get; }
        CipherAlgorithmType CipherAlgorithm { get; }
        int CipherStrength { get; }
        HashAlgorithmType HashAlgorithm { get; }
        int HashStrength { get; }
        ExchangeAlgorithmType KeyExchangeAlgorithm { get; }
        int KeyExchangeStrength { get; }
    }
}
