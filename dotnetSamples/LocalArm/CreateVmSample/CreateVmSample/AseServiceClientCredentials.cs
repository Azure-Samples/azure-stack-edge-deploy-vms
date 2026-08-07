using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CreateVmSample
{
    public class AseServiceClientCredentials : ServiceClientCredentials
    {
        private const string PowerShellAppId = "1950a258-227b-4e31-a9cf-717495945fc2";
        private static readonly object CertificateValidationLock = new object();
        private static readonly HashSet<string> AllowedCertificateHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static string ExpectedCertificateThumbprint;

        /// <summary>
        /// login client id
        /// </summary>
        protected string ClientId
        {
            get;
            set;
        }

        /// <summary>
        /// Login client secret
        /// </summary>
        protected string ClientSecretKey
        {
            get;
            set;
        }

        /// <summary>
        /// After succesfull logon will contain the Authentication Token including bearer for making authenticated
        /// rest calls
        /// </summary>
        public string AuthenticationToken
        {
            get;
            protected set;
        }

        /// <summary>
        /// Tenant Id to connect to
        /// </summary>
        public string TenantId
        {
            get;
            protected set;
        }

        /// <summary>
        /// Login endpoint for the client
        /// </summary>
        public string AuthenticationAuthority
        {
            get;
            protected set;
        }

        /// <summary>
        /// Target url to reach for queries
        /// </summary>
        public string AudienceResourceUrl
        {
            get;
            protected set;
        }

        public AseServiceClientCredentials(string clientId,
            string clientSecret,
            string tenantId,
            string audianceResourceUrl,
            string authenticationAuthority,
            string expectedCertificateThumbprint)
        {
            this.ClientId = clientId;
            this.ClientSecretKey = clientSecret;
            this.TenantId = tenantId;
            this.AudienceResourceUrl = audianceResourceUrl;
            this.AuthenticationAuthority = authenticationAuthority;
            ConfigureCertificateValidation(expectedCertificateThumbprint, audianceResourceUrl, authenticationAuthority);
        }

        public AseServiceClientCredentials(string clientId,
            string clientSecret,
            string tenantId,
            string edgeApplianceHostName)
            : this(
                 clientId,
                 clientSecret,
                 tenantId,
                 $"https://management.dbe-{edgeApplianceHostName.ToLower()}.microsoftdatabox.com/",
                 $"https://login.dbe-{edgeApplianceHostName.ToLower()}.microsoftdatabox.com/adfs/",
                 null
                 )
        {

        }

        public AseServiceClientCredentials(string clientId,
            string clientSecret,
            string tenantId,
            string edgeApplianceHostName,
            string expectedCertificateThumbprint)
            : this(
                 clientId,
                 clientSecret,
                 tenantId,
                 $"https://management.dbe-{edgeApplianceHostName.ToLower()}.microsoftdatabox.com/",
                 $"https://login.dbe-{edgeApplianceHostName.ToLower()}.microsoftdatabox.com/adfs/",
                 expectedCertificateThumbprint
                 )
        {

        }

        internal static void ConfigureCertificateValidationForTest(string expectedCertificateThumbprint, string audienceResourceUrl, string authenticationAuthority)
        {
            ConfigureCertificateValidation(expectedCertificateThumbprint, audienceResourceUrl, authenticationAuthority);
        }

        internal static void ResetCertificateValidationForTest()
        {
            lock (CertificateValidationLock)
            {
                AllowedCertificateHosts.Clear();
                ExpectedCertificateThumbprint = null;
                ServicePointManager.ServerCertificateValidationCallback = null;
            }
        }

        private static void ConfigureCertificateValidation(string expectedCertificateThumbprint, string audienceResourceUrl, string authenticationAuthority)
        {
            var normalizedThumbprint = NormalizeThumbprint(expectedCertificateThumbprint);
            if (string.IsNullOrEmpty(normalizedThumbprint))
            {
                return;
            }

            lock (CertificateValidationLock)
            {
                ExpectedCertificateThumbprint = normalizedThumbprint;
                AllowedCertificateHosts.Clear();
                AllowedCertificateHosts.Add(new Uri(audienceResourceUrl).Host);
                AllowedCertificateHosts.Add(new Uri(authenticationAuthority).Host);
                ServicePointManager.ServerCertificateValidationCallback = ValidatePinnedCertificate;
            }
        }

        internal static bool ValidatePinnedCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }

            if (certificate == null || string.IsNullOrEmpty(ExpectedCertificateThumbprint))
            {
                return false;
            }

            var requestHost = ExtractRequestHost(sender);
            if (string.IsNullOrEmpty(requestHost) || !AllowedCertificateHosts.Contains(requestHost))
            {
                return false;
            }

            return string.Equals(
                NormalizeThumbprint(certificate.GetCertHashString()),
                ExpectedCertificateThumbprint,
                StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtractRequestHost(object sender)
        {
            switch (sender)
            {
                case HttpWebRequest webRequest:
                    return webRequest.RequestUri?.Host;
                case HttpRequestMessage requestMessage:
                    return requestMessage.RequestUri?.Host;
                case string host:
                    return host;
                default:
                    return null;
            }
        }

        internal static string NormalizeThumbprint(string thumbprint)
        {
            if (string.IsNullOrWhiteSpace(thumbprint))
            {
                return null;
            }

            return new string(thumbprint.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        }

        public override void InitializeServiceClient<T>(ServiceClient<T> client)
        {
            var authContext = new AuthenticationContext($"{this.AuthenticationAuthority}", false);
            var credentials = new UserPasswordCredential(this.ClientId, this.ClientSecretKey);
            var token = authContext.AcquireTokenAsync(this.AudienceResourceUrl, PowerShellAppId, credentials).Result;
            this.AuthenticationToken = token.CreateAuthorizationHeader();
        }

        public override Task ProcessHttpRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            if (string.IsNullOrEmpty(AuthenticationToken))
            {
                throw new InvalidOperationException("Token Provider Cannot Be Null");
            }

            request.Headers.Add("Authorization", this.AuthenticationToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            return base.ProcessHttpRequestAsync(request, cancellationToken);
        }

    }
}
