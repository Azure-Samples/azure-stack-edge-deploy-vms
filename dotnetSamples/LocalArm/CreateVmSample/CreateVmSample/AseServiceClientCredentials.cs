using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CreateVmSample
{
    public class AseServiceClientCredentials : ServiceClientCredentials
    {
        private const string PowerShellAppId = "1950a258-227b-4e31-a9cf-717495945fc2";

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

        static AseServiceClientCredentials()
        {
            ServicePointManager.ServerCertificateValidationCallback = new
                RemoteCertificateValidationCallback
                (
                    delegate { return true; }
                );
        }

        public AseServiceClientCredentials(string clientId,
            string clientSecret,
            string tenantId,
            string audianceResourceUrl,
            string authenticationAuthority)
        {
            this.ClientId = clientId;
            this.ClientSecretKey = clientSecret;
            this.TenantId = tenantId;
            this.AudienceResourceUrl = audianceResourceUrl;
            this.AuthenticationAuthority = authenticationAuthority;
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
                 $"https://login.dbe-{edgeApplianceHostName.ToLower()}.microsoftdatabox.com/adfs/"
                 )
        {

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
