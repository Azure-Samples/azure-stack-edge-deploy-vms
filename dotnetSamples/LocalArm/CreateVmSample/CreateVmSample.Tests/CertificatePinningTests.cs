using System;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using CreateVmSample;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CreateVmSample.Tests
{
    [TestClass]
    public class CertificatePinningTests
    {
        private const string ManagementUrl = "https://management.dbe-appliance.microsoftdatabox.com/";
        private const string AuthorityUrl = "https://login.dbe-appliance.microsoftdatabox.com/adfs/";
        private const string ManagementHost = "management.dbe-appliance.microsoftdatabox.com";

        [TestInitialize]
        public void TestInitialize()
        {
            AseServiceClientCredentials.ResetCertificateValidationForTest();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AseServiceClientCredentials.ResetCertificateValidationForTest();
        }

        private static X509Certificate2 CreateSelfSignedCertificate(string subjectName = "CN=ase-pinning-test")
        {
            using (var rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest(
                    subjectName,
                    rsa,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);

                return request.CreateSelfSigned(
                    DateTimeOffset.UtcNow.AddDays(-1),
                    DateTimeOffset.UtcNow.AddDays(1));
            }
        }

        [TestMethod]
        public void NormalizeThumbprint_StripsSeparatorsAndUppercases()
        {
            var normalizedWithSeparators = AseServiceClientCredentials.NormalizeThumbprint("ab:cd ef");
            var normalizedPlain = AseServiceClientCredentials.NormalizeThumbprint("ABCDEF");

            Assert.AreEqual("ABCDEF", normalizedWithSeparators);
            Assert.AreEqual(normalizedPlain, normalizedWithSeparators);
        }

        [TestMethod]
        public void NormalizeThumbprint_NullOrWhitespace_ReturnsNull()
        {
            Assert.IsNull(AseServiceClientCredentials.NormalizeThumbprint(null));
            Assert.IsNull(AseServiceClientCredentials.NormalizeThumbprint(string.Empty));
            Assert.IsNull(AseServiceClientCredentials.NormalizeThumbprint("   "));
        }

        [TestMethod]
        public void Validate_NoSslErrors_ReturnsTrue()
        {
            using (var cert = CreateSelfSignedCertificate())
            {
                var result = AseServiceClientCredentials.ValidatePinnedCertificate(
                    ManagementHost,
                    cert,
                    null,
                    SslPolicyErrors.None);

                Assert.IsTrue(result);
            }
        }

        [TestMethod]
        public void Validate_MatchingThumbprint_AllowedHost_ChainError_ReturnsTrue()
        {
            using (var cert = CreateSelfSignedCertificate())
            {
                AseServiceClientCredentials.ConfigureCertificateValidationForTest(
                    cert.GetCertHashString(),
                    ManagementUrl,
                    AuthorityUrl);

                var result = AseServiceClientCredentials.ValidatePinnedCertificate(
                    ManagementHost,
                    cert,
                    null,
                    SslPolicyErrors.RemoteCertificateChainErrors);

                Assert.IsTrue(result);
            }
        }

        [TestMethod]
        public void Validate_MatchingThumbprint_DisallowedHost_ReturnsFalse()
        {
            using (var cert = CreateSelfSignedCertificate())
            {
                AseServiceClientCredentials.ConfigureCertificateValidationForTest(
                    cert.GetCertHashString(),
                    ManagementUrl,
                    AuthorityUrl);

                var result = AseServiceClientCredentials.ValidatePinnedCertificate(
                    "evil.contoso.com",
                    cert,
                    null,
                    SslPolicyErrors.RemoteCertificateChainErrors);

                Assert.IsFalse(result);
            }
        }

        [TestMethod]
        public void Validate_NonMatchingThumbprint_AllowedHost_ReturnsFalse()
        {
            using (var configuredCert = CreateSelfSignedCertificate("CN=ase-pinning-configured"))
            using (var presentedCert = CreateSelfSignedCertificate("CN=ase-pinning-presented"))
            {
                AseServiceClientCredentials.ConfigureCertificateValidationForTest(
                    configuredCert.GetCertHashString(),
                    ManagementUrl,
                    AuthorityUrl);

                var result = AseServiceClientCredentials.ValidatePinnedCertificate(
                    ManagementHost,
                    presentedCert,
                    null,
                    SslPolicyErrors.RemoteCertificateChainErrors);

                Assert.IsFalse(result);
            }
        }

        [TestMethod]
        public void Validate_NullCertificate_ReturnsFalse()
        {
            using (var cert = CreateSelfSignedCertificate())
            {
                AseServiceClientCredentials.ConfigureCertificateValidationForTest(
                    cert.GetCertHashString(),
                    ManagementUrl,
                    AuthorityUrl);

                var result = AseServiceClientCredentials.ValidatePinnedCertificate(
                    ManagementHost,
                    null,
                    null,
                    SslPolicyErrors.RemoteCertificateChainErrors);

                Assert.IsFalse(result);
            }
        }

        [TestMethod]
        public void Validate_HttpWebRequestSender_HostExtracted_ReturnsTrue()
        {
            using (var cert = CreateSelfSignedCertificate())
            {
                AseServiceClientCredentials.ConfigureCertificateValidationForTest(
                    cert.GetCertHashString(),
                    ManagementUrl,
                    AuthorityUrl);

                var sender = (HttpWebRequest)WebRequest.Create(ManagementUrl);

                var result = AseServiceClientCredentials.ValidatePinnedCertificate(
                    sender,
                    cert,
                    null,
                    SslPolicyErrors.RemoteCertificateChainErrors);

                Assert.IsTrue(result);
            }
        }

        [TestMethod]
        public void Validate_HttpRequestMessageSender_HostExtracted_ReturnsTrue()
        {
            using (var cert = CreateSelfSignedCertificate())
            {
                AseServiceClientCredentials.ConfigureCertificateValidationForTest(
                    cert.GetCertHashString(),
                    ManagementUrl,
                    AuthorityUrl);

                using (var sender = new HttpRequestMessage(HttpMethod.Get, ManagementUrl))
                {
                    var result = AseServiceClientCredentials.ValidatePinnedCertificate(
                        sender,
                        cert,
                        null,
                        SslPolicyErrors.RemoteCertificateChainErrors);

                    Assert.IsTrue(result);
                }
            }
        }

        [TestMethod]
        public void Validate_UnknownSenderType_ReturnsFalse()
        {
            using (var cert = CreateSelfSignedCertificate())
            {
                AseServiceClientCredentials.ConfigureCertificateValidationForTest(
                    cert.GetCertHashString(),
                    ManagementUrl,
                    AuthorityUrl);

                var result = AseServiceClientCredentials.ValidatePinnedCertificate(
                    new object(),
                    cert,
                    null,
                    SslPolicyErrors.RemoteCertificateChainErrors);

                Assert.IsFalse(result);
            }
        }
    }
}
