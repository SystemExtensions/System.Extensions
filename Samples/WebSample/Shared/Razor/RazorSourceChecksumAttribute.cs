
namespace Microsoft.AspNetCore.Razor.Hosting
{
    using System;
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public sealed class RazorSourceChecksumAttribute : Attribute
    {
        public RazorSourceChecksumAttribute(string checksumAlgorithm, string checksum, string identifier)
        {
            if (checksumAlgorithm == null)
                throw new ArgumentNullException(nameof(checksumAlgorithm));
            if (checksum == null)
                throw new ArgumentNullException(nameof(checksum));
            if (identifier == null)
                throw new ArgumentNullException(nameof(identifier));

            ChecksumAlgorithm = checksumAlgorithm;
            Checksum = checksum;
            Identifier = identifier;
        }
        public string Checksum { get; }
        public string ChecksumAlgorithm { get; }
        public string Identifier { get; }
    }
}