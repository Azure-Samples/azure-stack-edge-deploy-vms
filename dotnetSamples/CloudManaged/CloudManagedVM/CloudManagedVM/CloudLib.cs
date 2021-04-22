using System;
using System.Diagnostics;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.CSharp.RuntimeBinder;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Threading.Tasks;
using System.Threading;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;

namespace CloudManagedVM
{
    public class CloudLib
    {
        static string TenantId;
        static string ClientId;
        static string SubscriptionId;
        static string ArmEndpoint;
        static string Token;
        static string SAAS_API_VERSION = "2020-06-01-preview";
        static string LINKED_API_VERSION = "2018-09-01";
        static string SAAS_ASE_RESOURCE_API_VERSION = "2020-09-01";
        IRestClient Client;

        public CloudLib(string accessKey, string tenantId, string clientId, string subscriptionId, string armEndPoint = "management.azure.com")
        {
            TenantId = tenantId;
            ClientId = clientId;
            SubscriptionId = subscriptionId;
            var task = GetAzureAccessTokenAsync(TenantId, ClientId, accessKey);
            Token = task.GetAwaiter().GetResult();

            ArmEndpoint = $"https://{armEndPoint}/";
            Client = new RestClient(ArmEndpoint);
        }

        public CloudLib(string accesssToken, string subscriptionId, string armEndPoint = "management.azure.com")
        {
            Token = accesssToken;
            SubscriptionId = subscriptionId;

            ArmEndpoint = $"https://{armEndPoint}/";
            Client = new RestClient(ArmEndpoint);
        }

        #region PublicMethods

        /// <summary>
        /// Get Azure Stack Edge (ASE) resource details.
        /// </summary>
        /// <param name="saasDeviceName">ASE device name</param>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <returns>ASE resource details</returns>
        public String GetAseResource(string saasDeviceName, string saasResourceGroup)
        {
            Console.WriteLine($"Fetching the ASE resource object for device {saasDeviceName}");
            string uri = $"/subscriptions/{SubscriptionId}/resourcegroups/{saasResourceGroup}/providers/Microsoft.DataboxEdge/dataBoxEdgeDevices/{saasDeviceName}";
            IRestResponse response = MakeRestCallWithRetry(uri, SAAS_ASE_RESOURCE_API_VERSION);
            return response.Content;
        }

        /// <summary>
        /// Get LinkedSubscriptionId of ASE device
        /// </summary>
        /// <param name="saasDeviceName">ASE device name</param>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <returns>LinkedSubscriptionId of ASE device</returns>
        public String GetLinkedSubscriptionId(string saasDeviceName, string saasResourceGroup)
        {
            string response = GetAseResource(saasDeviceName, saasResourceGroup);
            dynamic resp = JObject.Parse(response);
            return resp.properties.edgeProfile.subscription.subscriptionId;
        }

        /// <summary>
        /// Get local resources on ASE device
        /// </summary>
        /// <param name="linkedResourceType">Type of the resource e.g. "Microsoft.Compute/images"</param>
        /// <param name="linkedResourceName">Name of the resource on device</param>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="linkedSubId">LinkedSubscriptionId of ASE device</param>
        /// <returns>Resource object on the ASE device</returns>
        public string GetLinkedResource(string linkedResourceType, string linkedResourceName, string linkedResourceGroup, string saasResourceGroup, string linkedSubId)
        {
            Console.WriteLine($"Getting resource {linkedResourceName} of type {linkedResourceType} for edge {linkedSubId} under saas resource group {saasResourceGroup} and linked resource group {linkedResourceGroup}");
            string uri = $"/subscriptions/{SubscriptionId}/resourcegroups/{saasResourceGroup}/providers/Microsoft.AzureStack/linkedSubscriptions/{linkedSubId}/linkedResourceGroups/{linkedResourceGroup}/linkedproviders/{linkedResourceType}/{linkedResourceName}";
            IRestResponse response = MakeRestCallWithRetry(uri, SAAS_API_VERSION);

            return response.Content;
        }

        /// <summary>
        /// Get linked resource group on ASE device
        /// </summary>
        /// <param name="linkedResourceGroup">Resource Group on device</param>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="linkedSubId">LinkedSubscriptionId of ASE device</param>
        /// <returns>Resource Group object on ASE device</returns>
        public string GetLinkedResourceGroup(string linkedResourceGroup, string saasResourceGroup, string linkedSubId)
        {
            Console.WriteLine($"Getting resource group {linkedResourceGroup} on edge {linkedSubId} under Saas resource group {saasResourceGroup}");
            string uri = $"/subscriptions/{SubscriptionId}/resourcegroups/{saasResourceGroup}/providers/Microsoft.AzureStack/linkedSubscriptions/{linkedSubId}/linkedResourcegroups/{linkedResourceGroup}";
            IRestResponse response = MakeRestCallWithRetry(uri, SAAS_API_VERSION);
            return response.Content;
        }

        /// <summary>
        /// Get all linked resources of a given type on ASE device
        /// </summary>
        /// <param name="linkedResourceType">Type of the resource e.g. "Microsoft.Compute/images"</param>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="linkedSubId">LinkedSubscriptionId of ASE device</param>
        /// <returns>Resource objects on ASE device</returns>
        public string GetAllLinkedResourcesByType(string saasResourceGroup, string linkedSubId, string linkedResourceType)
        {
            Console.WriteLine($"Getting all resources of type {linkedResourceType} on edge device with subscription id {linkedSubId} under resource group {saasResourceGroup}");
            string uri = $"/subscriptions/{SubscriptionId}/resourcegroups/{saasResourceGroup}/providers/Microsoft.AzureStack/linkedSubscriptions/{linkedSubId}/linkedproviders/{linkedResourceType}";
            IRestResponse response = MakeRestCallWithRetry(uri, SAAS_API_VERSION);
            return response.Content;
        }

        /// <summary>
        /// Trigger a template deployment from Saas with separate template file and template params at resource group level.
        /// This is equivalent of New-AzureRmResourceGroupDeployment
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="saasDeviceName">ASE device name</param>
        /// <param name="templateFile">Json template to be deployed</param>
        /// <param name="templateParameterFile">Json template parameters to be used</param>
        /// <param name="deploymentName">Name of Saas deployment</param>
        /// <returns>None if deployment succeeds else throws exception</returns>
        public void DeployTemplateAtResourceGroupLevel(string saasResourceGroup, string saasDeviceName, string templateFile, string templateParameterFile, string linkedResourceGroup, string deploymentName = "")
        {
            Console.WriteLine($"Creating template");
            string template = CreateTemplate(templateFile, templateParameterFile);

            DeployTemplate(saasResourceGroup, saasDeviceName, template, linkedResourceGroup, deploymentName);
        }

        /// <summary>
        /// Trigger a template deployment from Saas with separate template file and template params at subscription level.
        /// This is equivalent of New-AzureRmDeployment
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="saasDeviceName">ASE device name</param>
        /// <param name="templateFile">Json template to be deployed</param>
        /// <param name="templateParameterFile">Json template parameters to be used</param>
        /// <param name="location">Location of deployment. For ASE, its always dbelocal</param>
        /// <param name="deploymentName">Name of Saas deployment</param>
        /// <returns>None if deployment succeeds else throws exception</returns>
        public void DeployTemplateAtSubscriptionLevel(string saasResourceGroup, string saasDeviceName, string templateFile, string templateParameterFile, string location, string deploymentName = "")
        {
            Console.WriteLine($"Creating template");
            string template = CreateTemplate(templateFile, templateParameterFile, location);

            DeployTemplate(saasResourceGroup, saasDeviceName, template, deploymentName:deploymentName);
        }

        /// <summary>
        /// Get Azure async header value from a given response
        /// </summary>
        /// <param name="response">Rest response</param>
        /// <returns> Return the uri value specified against Azure-Async key in response headers</returns>
        public static string GetAzureAsyncHeader(IRestResponse response)
        {
            var statusUri = "";
            foreach (var hdr in response.Headers)
            {
                if (hdr.ToString().Contains("Azure-Asyncoperation")
                    || hdr.ToString().Contains("Location"))
                {
                    int index = hdr.ToString().IndexOf('=');
                    statusUri = hdr.ToString().Substring(index + 1);
                    Console.WriteLine($"Got the status uri to poll for: {statusUri}");
                    return statusUri;
                }
            }
            Console.WriteLine("Did not get Azure-Asyncoperation key in response headers. Following are response headers");
            foreach (var hdr in response.Headers)
            {
                Console.WriteLine(hdr.ToString());
            }
            throw new Exception($"Status uri not specified in response headers");
        }

        /// <summary>
        /// Get azure access token from Saas to make REST calls to Saas.
        /// </summary>
        /// <param name="tenantId">Tenant Id</param>
        /// <param name="clientId">Client Id</param>
        /// <param name="clientSecretKey">Client secret key</param>
        /// <returns>Access token</returns>
        public static async Task<string> GetAzureAccessTokenAsync(string tenantId, string clientId, string clientSecretKey)
        {
            Console.WriteLine($"Fetching token for tenant {tenantId} with clientId {clientId}");
            var context = new AuthenticationContext("https://login.windows.net/" + tenantId);
            ClientCredential clientCredential = new ClientCredential(clientId, clientSecretKey);
            var tokenResponse = await context.AcquireTokenAsync("https://management.azure.com/", clientCredential);
            var accessToken = tokenResponse.AccessToken;
            return accessToken;
        }

        /// <summary>
        /// Start a VM.
        /// </summary>
        /// <param name="saasDeviceName">ASE device name</param>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="vmName">VM name</param>
        /// <param name="vmResourceGroup">Linked resource Group containing VM</param>
        /// <returns>None</returns>
        public void StartVM(string saasDeviceName, string saasResourceGroup, string vmName, string vmResourceGroup)
        {
            Console.WriteLine($"Starting VM {vmName}, RG {vmResourceGroup}");
            String linkedSubId = GetLinkedSubscriptionId(saasDeviceName, saasResourceGroup);
            string uri = $"/subscriptions/{SubscriptionId}/resourcegroups/{saasResourceGroup}/providers/Microsoft.AzureStack/linkedSubscriptions/{linkedSubId}/linkedResourcegroups/{vmResourceGroup}/linkedProviders/Microsoft.Compute/virtualMachines/{vmName}/start";
            IRestResponse responsePost = MakeRestCall(uri, Method.POST, SAAS_API_VERSION);
            PollForStatus(responsePost, $"Starting VM {vmName}");
        }

        /// <summary>
        /// Stop a VM.
        /// </summary>
        /// <param name="saasDeviceName">ASE device name</param>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="vmName">VM name</param>
        /// <param name="vmResourceGroup">Linked resource Group containing VM</param>
        /// <param name="stayProvisioned">whether VM should be stay provisioned or deallocated</param>
        /// <returns></returns>
        public void StopVM(string saasDeviceName, string saasResourceGroup, string vmName, string vmResourceGroup, bool stayProvisioned = true)
        {
            Console.WriteLine($"Stopping VM {vmName}, RG {vmResourceGroup} with stayProvisioned set to {stayProvisioned}");
            String linkedSubId = GetLinkedSubscriptionId(saasDeviceName, saasResourceGroup);
            String vmAction = stayProvisioned ? "powerOff" : "deallocate";
            string uri = $"/subscriptions/{SubscriptionId}/resourcegroups/{saasResourceGroup}/providers/Microsoft.AzureStack/linkedSubscriptions/{linkedSubId}/linkedResourcegroups/{vmResourceGroup}/linkedProviders/Microsoft.Compute/virtualMachines/{vmName}/{vmAction}";
            IRestResponse responsePost = MakeRestCall(uri, Method.POST, SAAS_API_VERSION);
            PollForStatus(responsePost, $"Stopping the VM {vmName}({vmAction})");
        }

        /// <summary>
        /// Restart a VM.
        /// </summary>
        /// <param name="saasDeviceName">ASE device name</param>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="vmName">VM name</param>
        /// <param name="vmResourceGroup">Linked resource Group containing VM</param>
        /// <returns></returns>
        public void RestartVM(string saasDeviceName, string saasResourceGroup, string vmName, string vmResourceGroup)
        {
            Console.WriteLine($"Restarting VM {vmName}, RG {vmResourceGroup}");
            String linkedSubId = GetLinkedSubscriptionId(saasDeviceName, saasResourceGroup);
            string uri = $"/subscriptions/{SubscriptionId}/resourcegroups/{saasResourceGroup}/providers/Microsoft.AzureStack/linkedSubscriptions/{linkedSubId}/linkedResourcegroups/{vmResourceGroup}/linkedProviders/Microsoft.Compute/virtualMachines/{vmName}/restart";
            IRestResponse responsePost = MakeRestCall(uri, Method.POST, SAAS_API_VERSION);
            PollForStatus(responsePost, $"Restarting VM ({vmName})");
        }

        /// <summary>
        /// Delete linked resource group
        /// </summary>
        /// <param name="saasDeviceName">ASE device name</param>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="linkedResourceGroup">Resource Group on device</param>
        /// <returns></returns>
        public void DeleteLinkedResourceGroup(string saasDeviceName, string saasResourceGroup, string linkedResourceGroup)
        {
            Console.WriteLine($"Deleting linked resource group {linkedResourceGroup}");
            String linkedSubId = GetLinkedSubscriptionId(saasDeviceName, saasResourceGroup);
            string uri = $"/subscriptions/{SubscriptionId}/resourcegroups/{saasResourceGroup}/providers/Microsoft.AzureStack/linkedSubscriptions/{linkedSubId}/linkedResourcegroups/{linkedResourceGroup}";
            IRestResponse responseDelete = MakeRestCall(uri, Method.DELETE, SAAS_API_VERSION, LINKED_API_VERSION);
            PollForStatus(responseDelete, $"Deleting linked resource group {linkedResourceGroup}", 30);
        }

        /// <summary>
        /// Deleting VM and its related resources recursively.
        /// </summary>
        /// <param name="saasDeviceName">ASE device name</param>
        /// <param name="saasResourceGroup">Saas resource group containing device</param>
        /// <param name="vmName">VM name</param>
        /// <param name="vmResourceGroup">Linked resource Group containing VM</param>
        /// <returns></returns>
        public void DeleteVMAndItsDisksAndNics(string saasDeviceName, string saasResourceGroup, string vmName, string vmResourceGroup)
        {
            String linkedSubId = GetLinkedSubscriptionId(saasDeviceName, saasResourceGroup);
            var vmDisks = GetDiskResourcesAttachedToVM(saasDeviceName, saasResourceGroup, vmName, vmResourceGroup);
            var vmNics = GetNicResourcesAttachedToVM(saasDeviceName, saasResourceGroup, vmName, vmResourceGroup);

            DeleteVM(saasResourceGroup, vmResourceGroup, linkedSubId, vmName);

            foreach (var (diskName, diskResourceGroup) in vmDisks)
            {
                DeleteDisk(saasResourceGroup, diskResourceGroup, linkedSubId, diskName);
            }
            
            foreach (var (nicName, nicResourceGroup) in vmNics)
            {
                DeleteNIC(saasResourceGroup, nicResourceGroup, linkedSubId, nicName);
            }
        }

        /// <summary>
        /// Get nics attached to a VM
        /// </summary>
        /// <param name="saasDeviceName">ASE device name</param>
        /// <param name="saasResourceGroup">Saas resource group containing device</param>
        /// <param name="vmName">VM name</param>
        /// <param name="vmResourceGroup">Linked resource Group containing VM</param>
        /// <returns>List of pairs (nicname, nic resource group)</returns>
        public List<(String, String)> GetNicResourcesAttachedToVM(string saasDeviceName, string saasResourceGroup, string vmName, string vmResourceGroup)
        {
            String linkedSubId = GetLinkedSubscriptionId(saasDeviceName, saasResourceGroup);
            var vmJsonStr = GetLinkedResource("Microsoft.Compute/virtualMachines", vmName, vmResourceGroup, saasResourceGroup, linkedSubId);
            dynamic vmObj = JObject.Parse(vmJsonStr);

            var vmNics = new List<(String, String)>();
            JArray networkInterfaces = (JArray)vmObj.properties.networkProfile.networkInterfaces;
            foreach (dynamic networkInterface in networkInterfaces)
            {
                vmNics.Add(GetLinkedResourceAndResourceGroup((String)networkInterface.id));
            }
            return vmNics;
        }

        /// <summary>
        /// Get disks attached to a VM
        /// </summary>
        /// <param name="saasDeviceName">ASE device name</param>
        /// <param name="saasResourceGroup">Linked resource group containing device</param>
        /// <param name="vmName">VM name</param>
        /// <param name="vmResourceGroup">Linked resource Group containing VM</param>
        /// <returns>List of pairs (diskname, disk resource group)</returns>
        public List<(String, String)> GetDiskResourcesAttachedToVM(string saasDeviceName, string saasResourceGroup, string vmName, string vmResourceGroup)
        {
            String linkedSubId = GetLinkedSubscriptionId(saasDeviceName, saasResourceGroup);
            var vmJsonStr = GetLinkedResource("Microsoft.Compute/virtualMachines", vmName, vmResourceGroup, saasResourceGroup, linkedSubId);
            dynamic vmObj = JObject.Parse(vmJsonStr);

            var vmDisks = new List<(String, String)>();
            JArray dataDisks = (JArray)vmObj.properties.storageProfile.dataDisks;
            foreach (dynamic dataDisk in dataDisks)
            {
                vmDisks.Add(GetLinkedResourceAndResourceGroup((String)dataDisk.managedDisk.id));
            }

            // Add os disk as well
            vmDisks.Add(GetLinkedResourceAndResourceGroup((String)vmObj.properties.storageProfile.osDisk.managedDisk.id));

            return vmDisks;
        }

        /// <summary>
        /// Delete VM
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="vmResourceGroup">Linked resource Group containing vm</param>
        /// <param name="linkedSubId">LinkedSubscriptionId of ASE device</param>
        /// <param name="vmName">VM name</param>
        /// <returns></returns>
        public void DeleteVM(string saasResourceGroup, string vmResourceGroup, string linkedSubId, string vmName)
        {
            Console.WriteLine($"Deleting vm {vmName}");
            string uri = $"/subscriptions/{SubscriptionId}/resourcegroups/{saasResourceGroup}/providers/Microsoft.AzureStack/linkedSubscriptions/{linkedSubId}/linkedResourcegroups/{vmResourceGroup}/linkedProviders/Microsoft.Compute/virtualMachines/{vmName}";
            IRestResponse responseDelete = MakeRestCall(uri, Method.DELETE, SAAS_API_VERSION);
            PollForStatus(responseDelete, $"Deleting VM {vmName}");
        }

        /// <summary>
        /// Delete disk
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="diskResourceGroup">Linked resource Group containing disk</param>
        /// <param name="linkedSubId">LinkedSubscriptionId of ASE device</param>
        /// <param name="diskName">Disk name</param>
        /// <returns></returns>
        public void DeleteDisk(string saasResourceGroup, string diskResourceGroup, string linkedSubId, string diskName)
        {
            Console.WriteLine($"Deleting disk {diskName}");
            string uri = $"/subscriptions/{SubscriptionId}/resourcegroups/{saasResourceGroup}/providers/Microsoft.AzureStack/linkedSubscriptions/{linkedSubId}/linkedResourcegroups/{diskResourceGroup}/linkedProviders/Microsoft.Compute/disks/{diskName}";
            IRestResponse responseDelete = MakeRestCall(uri, Method.DELETE, SAAS_API_VERSION);
            PollForStatus(responseDelete, $"Deleting disk {diskName}");
        }

        /// <summary>
        /// Delete network interface
        /// </summary>
        /// <param name="resourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="nicResourceGroup">Linked resource Group containing nic</param>
        /// <param name="linkedSubId">LinkedSubscriptionId of ASE device</param>
        /// <param name="nicName">Nic name</param>
        /// <returns></returns>
        public void DeleteNIC(string resourceGroup, string nicResourceGroup, string linkedSubId, string nicName)
        {
            Console.WriteLine($"Deleting nic {nicName}");
            string uri = $"/subscriptions/{SubscriptionId}/resourcegroups/{resourceGroup}/providers/Microsoft.AzureStack/linkedSubscriptions/{linkedSubId}/linkedResourcegroups/{nicResourceGroup}/linkedProviders/Microsoft.Network/networkInterfaces/{nicName}";
            IRestResponse responseDelete = MakeRestCall(uri, Method.DELETE, SAAS_API_VERSION);
            PollForStatus(responseDelete, $"Deleting nic {nicName}");
        }

        /// <summary>
        /// Create a managed disk
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="linkedSubId">LinkedSubscriptionId of ASE device</param>
        /// <param name="diskName">Disk name</param>
        /// <param name="diskResourceGroup">Linked resource group containing disk</param>
        /// <param name="diskSizeGB">Disk size in GB</param>
        /// <returns></returns>
        public void CreateManagedDisk(string saasResourceGroup, string linkedSubId, string diskName, string diskResourceGroup, string diskSizeGB)
        {
            Console.WriteLine($"Creating a managed disk {diskName} of size {diskSizeGB} GB under linked resource group {diskResourceGroup}. SaasResourceGroup: {saasResourceGroup}, linkedSubId: {linkedSubId}");
            String uri = $"/subscriptions/{SubscriptionId}/resourcegroups/{saasResourceGroup}/providers/Microsoft.AzureStack/linkedSubscriptions/{linkedSubId}/linkedResourceGroups/{diskResourceGroup}/linkedProviders/Microsoft.Compute/disks/{diskName}";
            var body = new
            {
                location = "dbelocal",
                properties = new
                {
                    diskSizeGB = diskSizeGB,
                    creationData = new
                    {
                        createOption = "Empty"
                    }
                }
            };

            IRestResponse response = MakeRestCall(uri, Method.PUT, SAAS_API_VERSION, body: body);
            PollForStatus(response, $"Create disk {diskName}");
            Console.WriteLine($"Created a managed disk {diskName} of size {diskSizeGB} GB under linked resource group {diskResourceGroup}");
        }

        /// <summary>
        /// Create a nic
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="linkedSubId">LinkedSubscriptionId of ASE device</param>
        /// <param name="nicName">Nic name</param>
        /// <param name="nicResourceGroup">Linked resource group containing nic</param>
        /// <param name="vnetName">Virtual network where nic should be created</param>
        /// <param name="vnetResourceGroup">Linked resource group containing virtual network</param>
        /// <param name="ip">IP to be allocated to the nic</param>
        /// <returns></returns>
        public void CreateNic(string saasResourceGroup, string linkedSubId, string nicName, string nicResourceGroup, string vnetName, string vnetResourceGroup, string ip = "")
        {
            string ipConfig = String.IsNullOrEmpty(ip) ? "Dynamic" : "Static";
            Console.WriteLine($"Creating a nic {nicName} under linked resource group {nicResourceGroup} with {ipConfig} ip configuration. SaasResourceGroup: {saasResourceGroup}, linkedSubId: {linkedSubId}. IP: {ip}");

            //Get the subnet from vnet. Current vnet has only one subnet.
            String vnetJson = GetLinkedResource("Microsoft.Network/virtualNetworks", vnetName, vnetResourceGroup, saasResourceGroup, linkedSubId);
            dynamic vnetObj = JObject.Parse(vnetJson);

            String uri = $"/subscriptions/{SubscriptionId}/resourcegroups/{saasResourceGroup}/providers/Microsoft.AzureStack/linkedSubscriptions/{linkedSubId}/linkedResourceGroups/{nicResourceGroup}/linkedProviders/Microsoft.Network/networkInterfaces/{nicName}";

            // Create payload to create nic
            dynamic payload = new JObject();
            payload.location = "dbelocal";
            payload.properties = new JObject();
            payload.properties.ipConfigurations = new JArray();
            dynamic ipconfiguration = new JObject();
            ipconfiguration.name = "ipconfig" + Guid.NewGuid().ToString().Split('-')[0];
            ipconfiguration.properties = new JObject();
            ipconfiguration.properties.subnet = new JObject();
            ipconfiguration.properties.subnet.id = vnetObj.properties.subnets[0].id;
            if (ipConfig.Equals("Static", StringComparison.OrdinalIgnoreCase))
            {
                ipconfiguration.properties.privateIPAddress = ip;
                ipconfiguration.properties.privateIPAllocationMethod = ipConfig;
            }
            payload.properties.ipConfigurations.Add((JObject)ipconfiguration);

            IRestResponse response = MakeRestCall(uri, Method.PUT, SAAS_API_VERSION, body: (payload.ToString()));
            PollForStatus(response, $"Create nic {nicName}");
            Console.WriteLine($"Created nic {nicName} under linked resource group {nicResourceGroup} with {ipConfig} ip configuration");
        }

        /// <summary>
        /// Attach the specified disk to the specified VM
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="linkedSubId">LinkedSubscriptionId of ASE device</param>
        /// <param name="vmName">VM name</param>
        /// <param name="vmResourceGroup">Linked resource group containing VM</param>
        /// <param name="diskName">Disk name</param>
        /// <param name="diskResourceGroup">Linked resource group containing disk</param>
        /// <param name="lunId">Lun id with which disk should attach to VM</param>
        /// <returns></returns>
        public void AttachDisk(string saasResourceGroup, string linkedSubId, string vmName, string vmResourceGroup, string diskName, string diskResourceGroup, int lunId = 0)
        {
            Console.WriteLine($"Attaching disk {diskName} under linked resource group {diskResourceGroup} to vm {vmName} under linked resource group {vmResourceGroup} with lunId {lunId}");

            // Get the VM object
            String vmJson = GetLinkedResource("Microsoft.Compute/virtualMachines", vmName, vmResourceGroup, saasResourceGroup, linkedSubId);
            dynamic vmObj = JObject.Parse(vmJson);

            // Get the disk object
            String diskJson = GetLinkedResource("Microsoft.Compute/disks", diskName, diskResourceGroup, saasResourceGroup, linkedSubId);
            dynamic diskObj = JObject.Parse(diskJson);

            // Create disk object structure
            dynamic newDisk = new JObject();
            newDisk.lun = lunId;
            newDisk.name = diskObj.name;
            newDisk.createOption = "Attach";
            newDisk.caching = "None";
            newDisk.diskSizeGB = diskObj.properties.diskSizeGB;
            newDisk.managedDisk = new JObject();
            newDisk.managedDisk.storageAccountType = diskObj.sku.name;
            newDisk.managedDisk.id = GetLinkedResourceId((String)diskObj.id);

            // Add the disk to the list of existing disks
            JArray dataDisks = (JArray)vmObj.properties.storageProfile.dataDisks;
            dataDisks.Add((JObject)newDisk);

            // Create payload to attach the disk
            dynamic payload = new JObject();
            payload.properties = new JObject();
            payload.properties.storageProfile = new JObject();
            payload.properties.storageProfile.dataDisks = dataDisks;

            // Make request to attach the disk
            IRestResponse response = MakeRestCall((String)vmObj.id, Method.PATCH, SAAS_API_VERSION, body: (payload.ToString()));
            PollForStatus(response, $"Attach disk {diskName} to VM {vmName}");
            Console.WriteLine($"Attached the disk {diskName} in linked resource group {diskResourceGroup} to the VM {vmName} in linked resource group {vmResourceGroup}");
        }

        /// <summary>
        /// Detach the specified disk from the specified VM
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="linkedSubId">LinkedSubscriptionId of ASE device</param>
        /// <param name="vmName">VM name</param>
        /// <param name="vmResourceGroup">Linked resource group containing VM</param>
        /// <param name="diskName">Disk name</param>
        /// <param name="diskResourceGroup">Linked resource group containign disk</param>
        /// <returns></returns>
        public void DetachDisk(string saasResourceGroup, string linkedSubId, string vmName, string vmResourceGroup, string diskName, string diskResourceGroup)
        {
            Console.WriteLine($"Detaching disk {diskName} under linked resource group {diskResourceGroup} from vm {vmName} under linked resource group {vmResourceGroup}");

            // Get the VM object
            String vmJson = GetLinkedResource("Microsoft.Compute/virtualMachines", vmName, vmResourceGroup, saasResourceGroup, linkedSubId);
            dynamic vmObj = JObject.Parse(vmJson);

            // Remove the disk from VM object
            JArray dataDisks = (JArray)vmObj.properties.storageProfile.dataDisks;
            bool diskRemoved = false;
            foreach (dynamic diskObj in dataDisks)
            {
                if (diskObj.name == diskName && ((String)diskObj.managedDisk.id).Contains(diskResourceGroup))
                {
                    dataDisks.Remove(diskObj);
                    diskRemoved = true;
                    break;
                }
            }
            if (!diskRemoved)
            {
                Console.WriteLine($"Disk {diskName} in linked resource group {diskResourceGroup} is not part of VM disks currently. Current VM object: {vmObj.ToString()}");
                return;
            }

            // Create payload for removing the disk
            dynamic payload = new JObject();
            payload.properties = new JObject();
            payload.properties.storageProfile = new JObject();
            payload.properties.storageProfile.dataDisks = dataDisks;

            // Make request to attach the disk
            IRestResponse response = MakeRestCall((String)vmObj.id, Method.PATCH, SAAS_API_VERSION, body: (payload.ToString()));
            PollForStatus(response, $"Detach disk {diskName} from VM {vmName}");
            Console.WriteLine($"Detached the disk {diskName} in linked resource group {diskResourceGroup} to the VM {vmName} in linked resource group {vmResourceGroup}");
        }

        /// <summary>
        /// Resize a disk
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="linkedSubId">LinkedSubscriptionId of ASE device</param>
        /// <param name="diskName">Disk name</param>
        /// <param name="diskResourceGroup">Linked resource group containign disk</param>
        /// <param name="newSizeInGb">Disk size in GB</param>
        /// <returns></returns>
        public void ResizeDisk(string saasResourceGroup, string linkedSubId, string diskName, string diskResourceGroup, int newSizeInGb)
        {
            Console.WriteLine($"Resizing the disk {diskName} in linked resource group {diskResourceGroup} to {newSizeInGb} GB");

            // Get the disk object
            String diskJson = GetLinkedResource("Microsoft.Compute/disks", diskName, diskResourceGroup, saasResourceGroup, linkedSubId);
            dynamic diskObj = JObject.Parse(diskJson);

            // Update the disk object with new size
            Console.WriteLine($"Current size of disk {diskName} in resource group {diskResourceGroup}: {diskObj.properties.diskSizeGB}");
            if (diskObj.properties.diskSizeGB == newSizeInGb)
            {
                Console.WriteLine($"Disk {diskName} in resource group {diskResourceGroup} is already of size {newSizeInGb} GB. Skip resizing");
                return;
            }

            // Create payload to resize the disk
            dynamic payload = new JObject();
            payload.properties = new JObject();
            payload.properties.diskSizeGB = newSizeInGb;
            diskObj.properties.diskSizeGB = newSizeInGb;

            // Resize the disk
            IRestResponse response = MakeRestCall((String)diskObj.id, Method.PATCH, SAAS_API_VERSION, body: (payload.ToString()));
            PollForStatus(response, $"Resize disk {diskName}");
            Console.WriteLine($"Resized disk {diskName} in linked resource group {diskResourceGroup} to size {newSizeInGb} GB");
        }

        /// <summary>
        /// Resize a VM
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="linkedSubId">LinkedSubscriptionId of ASE device</param>
        /// <param name="vmName">VM name</param>
        /// <param name="vmResourceGroup">Linked resource group containing VM</param>
        /// <param name="newVMSize">VM size e.g. Standard_D1_V1</param>
        /// <returns></returns>
        public void ResizeVM(string saasResourceGroup, string linkedSubId, string vmName, string vmResourceGroup, string newVMSize)
        {
            Console.WriteLine($"Resizing the VM {vmName} in linked resource group {vmResourceGroup} to {newVMSize}");

            // Get the vm object
            String vmJson = GetLinkedResource("Microsoft.Compute/virtualMachines", vmName, vmResourceGroup, saasResourceGroup, linkedSubId);
            dynamic vmObj = JObject.Parse(vmJson);

            // Update the VM object with new size
            Console.WriteLine($"Current size of VM {vmName} in resource group {vmResourceGroup}: {vmObj.properties.hardwareProfile.vmSize}");
            if (vmObj.properties.hardwareProfile.vmSize == newVMSize)
            {
                Console.WriteLine($"VM {vmName} in resource group {vmResourceGroup} is already of size {newVMSize}. Skip resizing");
                return;
            }

            // Create payload to resize VM
            dynamic payload = new JObject();
            payload.properties = new JObject();
            payload.properties.hardwareProfile = new JObject();
            payload.properties.hardwareProfile.vmSize = newVMSize;

            // Resize VM
            IRestResponse response = MakeRestCall((String)vmObj.id, Method.PATCH, SAAS_API_VERSION, body: (payload.ToString()));
            PollForStatus(response, $"Resize vm {vmName}");
            Console.WriteLine($"Resized VM {vmName} in linked resource group {vmResourceGroup} to size {newVMSize}");
        }

        /// <summary>
        /// Check if the specified disk is attahced to the specified VM
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="linkedSubId">LinkedSubscriptionId of ASE device</param>
        /// <param name="vmName">VM name</param>
        /// <param name="vmResourceGroup">Linked resource group containing VM</param>
        /// <param name="diskName">Disk name</param>
        /// <param name="diskResourceGroup">Linked resource group containign disk</param>
        /// <param name="sleepTimeSec"> Sleep time in seconds before checking</param>
        /// <returns>bool</returns>
        public bool IsDiskAttachedToVM(string saasResourceGroup, string linkedSubId, string vmName, string vmResourceGroup, string diskName, string diskResourceGroup, int sleepTimeSec = 0)
        {

            Console.WriteLine($"Check if disk {diskName} in resource group {diskResourceGroup} is attached to VM {vmName} in resource group {vmResourceGroup}. Sleeping for {sleepTimeSec} seconds before checking");
            System.Threading.Thread.Sleep(sleepTimeSec * 1000);

            // Get the vm object
            String vmJson = GetLinkedResource("Microsoft.Compute/virtualMachines", vmName, vmResourceGroup, saasResourceGroup, linkedSubId);
            dynamic vmObj = JObject.Parse(vmJson);

            // Query data disks of VM
            JArray dataDisks = (JArray)vmObj.properties.storageProfile.dataDisks;
            foreach (dynamic diskObj in dataDisks)
            {
                if (diskObj.name == diskName && ((String)diskObj.managedDisk.id).Contains(diskResourceGroup))
                {
                    return true;
                }
            }
            Console.WriteLine($"Disk {diskName} in resource group {diskResourceGroup} is not part of VM disks currently. Current VM object: {vmObj.ToString()}");
            return false;
        }

        /// <summary>
        /// Check if the specified disk is of specified size
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="linkedSubId">LinkedSubscriptionId of ASE device</param>
        /// <param name="diskName">Disk name</param>
        /// <param name="diskResourceGroup">Linked resource group containign disk</param>
        /// <param name="expectedDiskSizeGb">Disk size in GB</param>
        /// <param name="sleepTimeSec">Sleep time in seconds before checking</param>
        /// <returns>bool</returns>
        public bool IsDiskSizeExpected(string saasResourceGroup, string linkedSubId, string diskName, string diskResourceGroup, int expectedDiskSizeGb, int sleepTimeSec = 0)
        {
            Console.WriteLine($"Verifying the disk {diskName} in resource group {diskResourceGroup} is of size {expectedDiskSizeGb} GB. Sleeping for {sleepTimeSec} seconds before checking");
            System.Threading.Thread.Sleep(sleepTimeSec * 1000);

            // Get the disk object
            String diskJson = GetLinkedResource("Microsoft.Compute/disks", diskName, diskResourceGroup, saasResourceGroup, linkedSubId);
            dynamic diskObj = JObject.Parse(diskJson);

            // Check the disk size
            if (diskObj.properties.diskSizeGB == expectedDiskSizeGb)
            {
                return true;
            }

            Console.WriteLine($"Current size of disk {diskName} in resource group {diskResourceGroup}: {diskObj.properties.diskSizeGB} GB. Disk obj: {diskObj.ToString()}");
            return false;
        }

        /// <summary>
        /// Check if the specified VM is of specified size
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="linkedSubId">LinkedSubscriptionId of ASE device</param>
        /// <param name="vmName">VM name</param>
        /// <param name="vmResourceGroup">Linked resource group containing VM</param>
        /// <param name="expectedVMsize">Expected VM size</param>
        /// <param name="sleepTimeSec">Sleep time in seconds before checking</param>
        /// <returns>bool</returns>
        public bool IsVMSizeExpected(string saasResourceGroup, string linkedSubId, string vmName, string vmResourceGroup, string expectedVMsize, int sleepTimeSec = 0)
        {
            Console.WriteLine($"Verifying if the VM {vmName} in resource group {vmResourceGroup} is of size {expectedVMsize}. Sleeping for {sleepTimeSec} seconds before checking");
            System.Threading.Thread.Sleep(sleepTimeSec * 1000);

            // Get the vm object
            String vmJson = GetLinkedResource("Microsoft.Compute/virtualMachines", vmName, vmResourceGroup, saasResourceGroup, linkedSubId);
            dynamic vmObj = JObject.Parse(vmJson);

            // Check the VM size
            if (vmObj.properties.hardwareProfile.vmSize == expectedVMsize)
            {
                return true;
            }
            Console.WriteLine($"Current size of VM {vmName} in resource group {vmResourceGroup}: {vmObj.properties.hardwareProfile.vmSize}. VM obj: {vmObj.ToString()}");
            return false;
        }

        /// <summary>
        /// Attach the specified nic to the specified VM
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="linkedSubId">LinkedSubscriptionId of ASE device</param>
        /// <param name="vmName">VM name</param>
        /// <param name="vmResourceGroup">Linked resource group containing VM</param>
        /// <param name="nicName">Nic name</param>
        /// <param name="nicResourceGroup">Linked resource group containing nic</param>
        /// <param name="primary">Whether nic should be attached as primary or not</param>
        /// <returns></returns>
        public void AttachNic(string saasResourceGroup, string linkedSubId, string vmName, string vmResourceGroup, string nicName, string nicResourceGroup, bool primary = false)
        {
            Console.WriteLine($"Attaching nic {nicName} under linked resource group {nicResourceGroup} to vm {vmName} under linked resource group {vmResourceGroup} with primary set to {primary}");

            // Get the VM object
            String vmJson = GetLinkedResource("Microsoft.Compute/virtualMachines", vmName, vmResourceGroup, saasResourceGroup, linkedSubId);
            dynamic vmObj = JObject.Parse(vmJson);

            // Get the nic object
            String nicJson = GetLinkedResource("Microsoft.Network/networkInterfaces", nicName, nicResourceGroup, saasResourceGroup, linkedSubId);
            dynamic nicObj = JObject.Parse(nicJson);

            // Create nic object structure
            dynamic newNic = new JObject();
            newNic.id = GetLinkedResourceId((String)nicObj.id);
            newNic.properties = new JObject();
            newNic.properties.primary = primary;

            // Add the nic to the list of existing interfaces
            JArray networkInterfaces = (JArray)vmObj.properties.networkProfile.networkInterfaces;
            if (networkInterfaces.Count == 1)
            {
                // Case where there is only one nic. It may have primary property set or missing
                dynamic currNic = networkInterfaces[0];
                if (currNic.properties == null)
                {
                    currNic.properties = new JObject();
                }
                // If new nic is to be primary, the older should be false and vice versa
                currNic.properties.primary = !primary;
            }
            else if (primary)
            {
                // New nic being added is set to primary, so set all other nics to false
                foreach (dynamic networkInterface in networkInterfaces)
                {
                    networkInterface.properties.primary = false;
                }
            }
            networkInterfaces.Add((JObject)newNic);

            // Create payload to attach the nic
            dynamic payload = new JObject();
            payload.properties = new JObject();
            payload.properties.networkProfile = new JObject();
            payload.properties.networkProfile.networkInterfaces = networkInterfaces;

            // Make request to attach the nic
            IRestResponse response = MakeRestCall((String)vmObj.id, Method.PATCH, SAAS_API_VERSION, body: (payload.ToString()));
            PollForStatus(response, $"Attach nic {nicName} to VM {vmName}");
            Console.WriteLine($"Attached the nic {nicName} in linked resource group {nicResourceGroup} to the VM {vmName} in linked resource group {vmResourceGroup} with primary set to {primary}");
        }

        /// <summary>
        /// Set the specified nic to the specified VM as primary nic
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="linkedSubId">LinkedSubscriptionId of ASE device</param>
        /// <param name="vmName">VM name</param>
        /// <param name="vmResourceGroup">Linked resource group containing VM</param>
        /// <param name="nicName">Nic name</param>
        /// <param name="nicResourceGroup">Linked resource group containing nic</param>
        /// <returns></returns>
        public void SetNicAsPrimary(string saasResourceGroup, string linkedSubId, string vmName, string vmResourceGroup, string nicName, string nicResourceGroup)
        {
            Console.WriteLine($"Setting nic {nicName} under linked resource group {nicResourceGroup} to vm {vmName} under linked resource group {vmResourceGroup} as primary");

            // Get the VM object
            String vmJson = GetLinkedResource("Microsoft.Compute/virtualMachines", vmName, vmResourceGroup, saasResourceGroup, linkedSubId);
            dynamic vmObj = JObject.Parse(vmJson);

            // If only one nic present, skip setting primary as its not a valid scenario.
            JArray networkInterfaces = (JArray)vmObj.properties.networkProfile.networkInterfaces;
            if (networkInterfaces.Count == 1)
            {
                Console.WriteLine($"VM {vmName} has only one nic. Setting primary is not a valid case here.");
                return;
            }

            // Specified nic is being set to primary, so set all other nics to false
            bool nicSetAsPrimary = false;
            foreach (dynamic networkInterface in networkInterfaces)
            {
                if (((String)networkInterface.id).Contains(nicName) && ((String)networkInterface.id).Contains(nicResourceGroup))
                {
                    networkInterface.properties.primary = true;
                    nicSetAsPrimary = true;
                }
                else
                {
                    networkInterface.properties.primary = false;
                }
            }
            if (!nicSetAsPrimary)
            {
                Console.WriteLine($"Nic {nicName} is not attached to VM {vmName}. Skip setting nic as primary");
                return;
            }

            // Create payload to set the specified as primary
            dynamic payload = new JObject();
            payload.properties = new JObject();
            payload.properties.networkProfile = new JObject();
            payload.properties.networkProfile.networkInterfaces = networkInterfaces;

            // Make request to attach the nic
            IRestResponse response = MakeRestCall((String)vmObj.id, Method.PATCH, SAAS_API_VERSION, body: (payload.ToString()));
            PollForStatus(response, $"Set nic {nicName} to VM {vmName} as primary");
            Console.WriteLine($"Set the nic {nicName} in linked resource group {nicResourceGroup} to the VM {vmName} in linked resource group {vmResourceGroup} as primary");
        }

        /// <summary>
        /// Detach the specified nic from the specified VM
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="linkedSubId">LinkedSubscriptionId of ASE device</param>
        /// <param name="vmName">VM name</param>
        /// <param name="vmResourceGroup">Linked resource group containing VM</param>
        /// <param name="nicName">Nic name</param>
        /// <param name="nicResourceGroup">Linked resource group containing nic</param>
        /// <returns></returns>
        public void DetachNic(string saasResourceGroup, string linkedSubId, string vmName, string vmResourceGroup, string nicName, string nicResourceGroup)
        {
            Console.WriteLine($"Detaching nic {nicName} under linked resource group {nicResourceGroup} from vm {vmName} under linked resource group {vmResourceGroup}");

            // Get the VM object
            String vmJson = GetLinkedResource("Microsoft.Compute/virtualMachines", vmName, vmResourceGroup, saasResourceGroup, linkedSubId);
            dynamic vmObj = JObject.Parse(vmJson);

            // Remove the nic from VM object
            JArray networkInterfaces = (JArray)vmObj.properties.networkProfile.networkInterfaces;
            if (networkInterfaces.Count == 1)
            {
                throw new Exception($"There is only one nic {networkInterfaces[0]["id"]} in the VM {vmName}. Cannot detach it.");
            }
            bool nicRemoved = false;
            foreach (dynamic nicObj in networkInterfaces)
            {
                if (((String)nicObj.id).Contains(nicName) && ((String)nicObj.id).Contains(nicResourceGroup))
                {
                    if ((bool)nicObj.properties.primary)
                    {
                        throw new Exception($"Nic {nicObj.id} is a primary nic for VM {vmName}. Cannot detach it.");
                    }
                    networkInterfaces.Remove(nicObj);
                    nicRemoved = true;
                    break;
                }
            }
            if (!nicRemoved)
            {
                Console.WriteLine($"Nic {nicName} in linked resource group {nicResourceGroup} is not part of VM nics currently. Current VM object: {vmObj.ToString()}");
                return;
            }

            // Create payload for removing the nic
            dynamic payload = new JObject();
            payload.properties = new JObject();
            payload.properties.networkProfile = new JObject();
            payload.properties.networkProfile.networkInterfaces = networkInterfaces;

            // Make request to attach the nic
            IRestResponse response = MakeRestCall((String)vmObj.id, Method.PATCH, SAAS_API_VERSION, body: (payload.ToString()));
            PollForStatus(response, $"Detach nic {nicName} from VM {vmName}");
            Console.WriteLine($"Detached the nic {nicName} in linked resource group {nicResourceGroup} to the VM {vmName} in linked resource group {vmResourceGroup}");
        }

        /// <summary>
        /// Check if the specified nic is attahced to the specified VM
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="linkedSubId">LinkedSubscriptionId of ASE device</param>
        /// <param name="vmName">VM name</param>
        /// <param name="vmResourceGroup">Linked resource group containing VM</param>
        /// <param name="nicName">Nic name</param>
        /// <param name="nicResourceGroup">Linked resource group containing nic</param>
        /// <param name="sleepTimeSec">Sleep time in seconds before checking</param>
        /// <returns>True if nic is attached to VM</returns>
        public bool IsNicAttachedToVM(string saasResourceGroup, string linkedSubId, string vmName, string vmResourceGroup, string nicName, string nicResourceGroup, int sleepTimeSec = 0)
        {

            Console.WriteLine($"Check if nic {nicName} in resource group {nicResourceGroup} is attached to VM {vmName} in resource group {vmResourceGroup}. Sleeping for {sleepTimeSec} seconds before checking");
            System.Threading.Thread.Sleep(sleepTimeSec * 1000);

            // Get the vm object
            String vmJson = GetLinkedResource("Microsoft.Compute/virtualMachines", vmName, vmResourceGroup, saasResourceGroup, linkedSubId);
            dynamic vmObj = JObject.Parse(vmJson);

            // Query nics of VM
            JArray networkInterfaces = (JArray)vmObj.properties.networkProfile.networkInterfaces;
            foreach (dynamic nicObj in networkInterfaces)
            {
                if (((String)nicObj.id).Contains(nicName) && ((String)nicObj.id).Contains(nicResourceGroup))
                {
                    return true;
                }
            }
            Console.WriteLine($"Nic {nicName} in resource group {nicResourceGroup} is not part of VM nics currently. Current VM object: {vmObj.ToString()}");
            return false;
        }

        /// <summary>
        /// Toggle a nic from static to dynamic ip configuration and vice versa
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="linkedSubId">LinkedSubscriptionId of ASE device</param>
        /// <param name="nicName">Nic name</param>
        /// <param name="nicResourceGroup">Linked resource group containing nic</param>
        /// <param name="staticIp">Static IP address</param>
        /// <returns></returns>
        public void SetNicIp(string saasResourceGroup, string linkedSubId, string nicName, string nicResourceGroup, string staticIp = "")
        {
            string ipConfig = String.IsNullOrEmpty(staticIp) ? "Dynamic" : "Static";
            Console.WriteLine($"Setting the ip configuration of nic {nicName} in linked resource group {nicResourceGroup} to {ipConfig}. IP: {staticIp}");

            // Get the nic object
            String nicJson = GetLinkedResource("Microsoft.Network/networkInterfaces", nicName, nicResourceGroup, saasResourceGroup, linkedSubId);
            dynamic nicObj = JObject.Parse(nicJson);

            // Update the nic object with new configuration
            Console.WriteLine($"Current IP config of nic {nicName}: {nicObj.properties.ipConfigurations[0].properties.privateIPAllocationMethod}, " +
                              $"Current IP of nic {nicName}: {nicObj.properties.ipConfigurations[0].properties.privateIPAddress},");
            if (ipConfig.Equals("Dynamic", StringComparison.OrdinalIgnoreCase) &&
                ((String)nicObj.properties.ipConfigurations[0].properties.privateIPAllocationMethod).Equals("Dynamic", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Nic {nicName} in resource group {nicResourceGroup} is already of configuration Dynamic. Skip setting");
                return;
            }

            // Create payload to set the ip configuration
            dynamic payload = new JObject();
            payload.location = "dbelocal";
            payload.properties = new JObject();
            payload.properties.macAddress = nicObj.properties.macAddress;
            payload.properties.ipConfigurations = new JArray();
            dynamic ipconfiguration = new JObject();
            ipconfiguration.name = "ipconfig" + Guid.NewGuid().ToString().Split('-')[0];
            ipconfiguration.properties = new JObject();
            ipconfiguration.properties.subnet = new JObject();
            ipconfiguration.properties.subnet.id = nicObj.properties.ipConfigurations[0].properties.subnet.id;
            if (ipConfig.Equals("Static", StringComparison.OrdinalIgnoreCase))
            {
                ipconfiguration.properties.privateIPAddress = staticIp;
                ipconfiguration.properties.privateIPAllocationMethod = ipConfig;
            }
            payload.properties.ipConfigurations.Add((JObject)ipconfiguration);

            // Resize the disk
            IRestResponse response = MakeRestCall((String)nicObj.id, Method.PUT, SAAS_API_VERSION, body: (payload.ToString()));
            PollForStatus(response, $"Set ip config of nic {nicName} to {ipConfig}. IP: {staticIp}");
            Console.WriteLine($"Set the ip config of nic {nicName} in linked resource group {nicResourceGroup} to {ipConfig} successfully");
        }

        /// <summary>
        /// Check if specifid nic has specified ip
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="linkedSubId">LinkedSubscriptionId of ASE device</param>
        /// <param name="nicName">Nic name</param>
        /// <param name="nicResourceGroup">Linked resource group containing nic</param>
        /// <param name="ip">IP address for nic</param>
        /// <param name="sleepTimeInSec">Sleep time in seconds before checking</param>
        /// <returns>True if the specified IP is assigned to the nic</returns>
        public bool HasNicIp(string saasResourceGroup, string linkedSubId, string nicName, string nicResourceGroup, string ip, int sleepTimeInSec = 0)
        {
            Console.WriteLine($"Checking if nic {nicName} in linked resource group {nicResourceGroup} has ip set to {ip}. Sleeping for {sleepTimeInSec} seconds before checking");
            Thread.Sleep(sleepTimeInSec * 1000);

            // Get the nic object
            String nicJson = GetLinkedResource("Microsoft.Network/networkInterfaces", nicName, nicResourceGroup, saasResourceGroup, linkedSubId);
            dynamic nicObj = JObject.Parse(nicJson);

            if (nicObj.properties.ipConfigurations[0].properties.privateIPAddress == ip)
            {
                return true;
            }
            Console.WriteLine($"Nic {nicName} in linked resource group {nicResourceGroup} has ip set to {nicObj.properties.ipConfigurations[0].properties.privateIPAddress}");
            return false;
        }

        /// <summary>
        /// Check if specifid nic has static ip configuration
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="linkedSubId">LinkedSubscriptionId of ASE device</param>
        /// <param name="nicName">Nic name</param>
        /// <param name="nicResourceGroup">Linked resource group containing nic</param>
        /// <param name="sleepTimeInSec">Sleep time in seconds before checking</param>
        /// <returns>True if Nic has static ip configuration</returns>
        public bool IsNicStatic(string saasResourceGroup, string linkedSubId, string nicName, string nicResourceGroup, int sleepTimeInSec = 0)
        {
            Console.WriteLine($"Checking if nic {nicName} in linked resource group {nicResourceGroup} has static ip configuration. Sleeping for {sleepTimeInSec} seconds before checking");
            Thread.Sleep(sleepTimeInSec * 1000);

            // Get the nic object
            String nicJson = GetLinkedResource("Microsoft.Network/networkInterfaces", nicName, nicResourceGroup, saasResourceGroup, linkedSubId);
            dynamic nicObj = JObject.Parse(nicJson);

            if (((String)nicObj.properties.ipConfigurations[0].properties.privateIPAllocationMethod).Equals("Static", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            Console.WriteLine($"Nic {nicName} in linked resource group {nicResourceGroup} has ip set to {nicObj.properties.ipConfigurations[0].properties.privateIPAllocationMethod}");
            return false;
        }

        /// <summary>
        /// Check if the specified nic is attahced to the specified VM as a primary nic
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="linkedSubId">LinkedSubscriptionId of ASE device</param>
        /// <param name="vmName">VM name</param>
        /// <param name="vmResourceGroup">Linked resource group containing VM</param>
        /// <param name="nicName">Nic name</param>
        /// <param name="nicResourceGroup">Linked resource group containing nic</param>
        /// <param name="sleepTimeSec">Sleep time in seconds before checking</param>
        /// <returns>True if Nic is a primary nic</returns>
        public bool IsNicPrimary(string saasResourceGroup, string linkedSubId, string vmName, string vmResourceGroup, string nicName, string nicResourceGroup, int sleepTimeSec = 0)
        {

            Console.WriteLine($"Check if nic {nicName} in resource group {nicResourceGroup} is attached to VM {vmName} in resource group {vmResourceGroup} as primary. Sleeping for {sleepTimeSec} seconds before checking");
            System.Threading.Thread.Sleep(sleepTimeSec * 1000);

            // Get the vm object
            String vmJson = GetLinkedResource("Microsoft.Compute/virtualMachines", vmName, vmResourceGroup, saasResourceGroup, linkedSubId);
            dynamic vmObj = JObject.Parse(vmJson);

            // Query nics of VM
            JArray networkInterfaces = (JArray)vmObj.properties.networkProfile.networkInterfaces;
            foreach (dynamic nicObj in networkInterfaces)
            {
                if (((String)nicObj.id).Contains(nicName) && ((String)nicObj.id).Contains(nicResourceGroup))
                {
                    if ((bool)nicObj.properties.primary)
                        return true;
                    Console.WriteLine($"Nic {nicName} is attached to VM {vmName} as non primary. Primary field set to {nicObj.properties.primary}");
                    return false;
                }
            }
            throw new Exception($"Nic {nicName} in resource group {nicResourceGroup} is not part of VM nics currently. Current VM object: {vmObj.ToString()}");
        }

        /// <summary>
        /// Get all the resources inside an linked resource group
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="linkedSubId">LinkedSubscriptionId of ASE device</param>
        /// <param name="linkedResourceGroup">Resource Group on device</param>
        /// <returns>Returns all resources in linked resource group</returns>
        public string GetAllResourcesInLinkedResourceGroup(string saasResourceGroup, string linkedSubId, string linkedResourceGroup)
        {
            Console.WriteLine($"Getting all resources inside linked resource group {linkedResourceGroup}. SaasResourceGroup: {saasResourceGroup}, linkedSubId: {linkedSubId}");

            String uri = $"/subscriptions/{SubscriptionId}/resourcegroups/{saasResourceGroup}/providers/Microsoft.AzureStack/linkedSubscriptions/{linkedSubId}/linkedResourceGroups/{linkedResourceGroup}/resources";

            IRestResponse response = MakeRestCallWithRetry(uri, SAAS_API_VERSION);
            return response.Content;
        }

        /// <summary>
        /// Convert a saas resource id to a linked resource id
        /// </summary>
        /// <param name="saasResourceId">Resource Id of a resource on ASE device when seen from cloud</param>
        /// <returns>Linked resource Id i.e. Resource Id of a resource on ASE device when seen from device itself</returns>
        public String GetLinkedResourceId(string saasResourceId)
        {
            Console.WriteLine($"Saas resource Id: {saasResourceId}");
            string localLinkedResourceId = saasResourceId.Split(new String[] { "Microsoft.AzureStack" }, StringSplitOptions.RemoveEmptyEntries)[1];
            string localResourceId = localLinkedResourceId.Replace("linked", "");
            Console.WriteLine($"Linked resource Id: {localResourceId}");
            return localResourceId;
        }

        /// <summary>
        /// Get linked resource name and its resource group from linked resource id
        /// </summary>
        /// <param name="linkedResourceId">Linked resource id</param>
        /// <returns>Returns a pair (linked resource name, linked resource group contining the linked resource)</returns>
        public (String, String) GetLinkedResourceAndResourceGroup(string linkedResourceId)
        {
            Match m = Regex.Match(linkedResourceId, "resourceGroups/([^/]*)/.*/([^/]*)$", RegexOptions.IgnoreCase);
            string resourceName = m.Groups[2].Value;
            string resourceGroup = m.Groups[1].Value;
            if (String.IsNullOrEmpty(resourceName) || String.IsNullOrEmpty(resourceGroup))
                throw new Exception($"Resource name or resource group not found for linked resource id: {linkedResourceId}");
            return (resourceName, resourceGroup);
        }

        /// <summary>
        /// Get the default virtual network on ASE device
        /// </summary>
        /// <param name="saasDeviceName">ASE Device name</param>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <returns>returns a tuple (Virtual network name, Virtual network resource group, Subnet name)</returns>
        public (String, String, String) GetDefaultVirtualNetwork(string saasDeviceName, string saasResourceGroup)
        {
            var linkedSubId = GetLinkedSubscriptionId(saasDeviceName, saasResourceGroup);
            var vnets = GetAllLinkedResourcesByType(saasResourceGroup, linkedSubId, "Microsoft.Network/virtualNetworks");
            Console.WriteLine(vnets);
            dynamic vnetObjs = JObject.Parse(vnets);
            var vnetName = vnetObjs.value[0].name;
            var subnetName = vnetObjs.value[0].properties.subnets[0].name;
            string vnetId = vnetObjs.value[0].id;

            // Fetch resource group from vnetId
            var vnetRg = "";
            foreach (Match m in Regex.Matches(vnetId, "linkedResourceGroups/([^/]*)"))
            {
                vnetRg = m.Groups[1].Value;
                break;
            }
            if (String.IsNullOrEmpty(vnetRg))
            {
                throw new Exception($"No resource group found for resource {vnetName} from ID {vnetId}. All vnets: {vnets}");
            }

            return (vnetName, vnetRg, subnetName);
        }
        #endregion

        #region PrivateHelper

        /// <summary>
        /// Validates a rest response and returns it as is if there is no exception
        /// </summary>
        /// <param name="response">Rest response</param>
        /// <param name="doNotThrowException">does not throw exception when set to true and fails silently</param>
        /// <returns>Rest response</returns>
        private static IRestResponse ValidateRestResponse(IRestResponse response, bool doNotThrowException = false)
        {
            Console.WriteLine(response.Content);
            Console.WriteLine($"isSuccessful: {response.IsSuccessful}, responseStatus: {response.ResponseStatus}, statusCode: {response.StatusCode}, errorMessage: {response.ErrorMessage}");
            if (response.IsSuccessful)
            {
                Console.WriteLine("Response was success");
                return response;
            }
            Console.WriteLine("Response was failure");
            if (doNotThrowException)
                return response;
            throw new Exception(response.Content);
        }

        /// <summary>
        /// Make a rest call
        /// </summary>
        /// <param name="uri">Request Uri to invoke</param>
        /// <param name="method">Rest method to invoke</param>
        /// <param name="saasApiVersion">Saas api version</param>
        /// <param name="linkedApiVersion">Linked resource specific api version</param>
        /// <param name="body">Request body</param>
        /// <param name="doNotThrowException">does not throw exception when set to true and fails silently</param>
        /// <returns>Rest response</returns>
        private IRestResponse MakeRestCall(string uri, Method method, string saasApiVersion = "", string linkedApiVersion = "", object body = null, bool doNotThrowException = false)
        {
            Console.WriteLine($"MakeRestCall Uri: {uri}, Method: {method}");
            IRestRequest request = new RestRequest(uri, method);
            if (!String.IsNullOrEmpty(saasApiVersion))
                request.AddQueryParameter("api-version", saasApiVersion);
            if (!String.IsNullOrEmpty(linkedApiVersion))
                request.AddQueryParameter("linked-api-version", linkedApiVersion);
            request.AddHeader("Authorization", "Bearer " + Token);
            if (method != Method.GET)
            {
                request.AddJsonBody(body);
            }
            IRestResponse response = Client.Execute(request);
            Console.WriteLine($"Actual URI called: {response.ResponseUri}");
            return ValidateRestResponse(response, doNotThrowException);
        }

        /// <summary>
        /// Make a rest call with retries to avoid intermittent failures. e.g. BadGateway etc.
        /// </summary>
        /// <param name="uri">Request Uri to invoke</param>
        /// <param name="saasApiVersion">Saas api version</param>
        /// <param name="linkedApiVersion">Resource specific api version</param>
        /// <param name="maxRetries">Max retries</param>
        /// <param name="retryFrequency">Time gap in seconds for rest call retires</param>
        /// <returns>Rest response</returns>
        private IRestResponse MakeRestCallWithRetry(string uri, string saasApiVersion = "", string linkedApiVersion = "", int maxRetries = 8, int retryFrequency = 15)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                IRestResponse response = MakeRestCall(uri, Method.GET, saasApiVersion: saasApiVersion, linkedApiVersion: linkedApiVersion, doNotThrowException: true);
                if (response.IsSuccessful)
                    return response;
                int responseCode = (int)response.StatusCode;
                // Retry in case of 5xx and 0 responses. 0 response is an invalid response i.e. request did not even sent or received properly
                if (responseCode >= 500 || responseCode == 0)
                {
                    Console.WriteLine($"Got response status code {response.StatusCode}. Sleeping for {retryFrequency} before retrying...");
                    System.Threading.Thread.Sleep(retryFrequency * 1000);
                    continue;
                }
                throw new Exception(response.Content);
            }
            throw new TimeoutException($"Timedout retrying after {maxRetries} times");
        }

        /// <summary>
        /// Poll for the status uri in the response object. This is for asynchronous operations. e.g. start a VM.
        /// </summary>
        /// <param name="response">Rest response</param>
        /// <param name="operationPerformed">Operation that was performed for above response</param>
        /// <param name="timeout"> Timeout in minutes for the async operation to finish</param>
        /// <returns></returns>
        private void PollForStatus(IRestResponse response, string operationPerformed, int timeout = 10)
        {
            string statusUri = GetAzureAsyncHeader(response);
            // Poll for the deleting the operation triggered above
            Console.WriteLine($"Poll for {operationPerformed}");
            // Wait for max 10 mins from now to delete
            Stopwatch timer = new Stopwatch();
            Console.WriteLine("Starting the timer");
            timer.Start();
            IRestResponse responseGet;

            try
            {
                while (timer.Elapsed < TimeSpan.FromMinutes(timeout))
                {
                    Console.WriteLine("Sleeping for 15 seconds");
                    System.Threading.Thread.Sleep(15000);

                    Console.WriteLine("Fetch operation status");
                    try
                    {
                        responseGet = MakeRestCallWithRetry(statusUri);
                    }
                    catch (Exception exc)
                    {
                        Console.WriteLine($"Got exception of type {exc.GetType()} and error messsage {exc.Message}");
                        dynamic error = JObject.Parse(exc.Message);
                        string errorCode = Convert.ToString(error.error.code);
                        if (errorCode.Equals("NotFound", StringComparison.OrdinalIgnoreCase))
                            continue;
                        else
                            throw exc;
                    }

                    if (!string.IsNullOrEmpty(responseGet.Content))
                    {
                        dynamic resp = JObject.Parse(responseGet.Content);
                        string status = resp.status;
                        Console.WriteLine($"Provisioning state: {status}");

                        if (status.Equals("Succeeded", StringComparison.OrdinalIgnoreCase))
                            return;
                        else if (status.Equals("Failed", StringComparison.OrdinalIgnoreCase))
                            throw new Exception(operationPerformed + " failed");
                    }
                }
            }
            finally
            {
                Console.WriteLine("Stopping the timer");
                timer.Stop();
            }
            throw new TimeoutException($"Timed out after {timeout} mins while waiting for {operationPerformed} to finish");
        }

        /// <summary>
        /// Create a template from the template file and its parameter file
        /// </summary>
        /// <param name="templateFile">Template file Path</param>
        /// <param name="templateParameterFile">Template parameters file path</param>
        /// <returns>Json string template after merging template and its parameter file</returns>
        private string CreateTemplate(string templateFile, string templateParameterFile, string location = "")
        {
            dynamic templateObj = JObject.Parse(File.ReadAllText(templateFile));
            dynamic templateParamsObj = JObject.Parse(File.ReadAllText(templateParameterFile));

            dynamic payload = new JObject();
            payload.properties = new JObject();
            payload.properties.template = templateObj;
            payload.properties.parameters = templateParamsObj.parameters;
            payload.properties.mode = "Incremental";
            if (!String.IsNullOrEmpty(location))
            {
                payload.location = location;
            }

            return payload.ToString();
        }

        /// <summary>
        /// Trigger a template deployment from Saas.
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="saasDeviceName">ASE device name</param>
        /// <param name="template">Json template to be deployed</param>
        /// <param name="deploymentName">Name of Saas deployment</param>
        /// <returns>None if deployment succeeds else throws exception</returns>
        private void DeployTemplate(string saasResourceGroup, string saasDeviceName, string template, string linkedResourceGroup = "", string deploymentName = "")
        {
            Console.WriteLine($"Deploying template under Saas ResourceGroup {saasResourceGroup} on ASE device {saasDeviceName}");
            // Generate deployment name dynamically if not provided
            if (String.IsNullOrEmpty(deploymentName))
            {
                deploymentName = "deployment" + Guid.NewGuid().ToString().Split('-')[0];
            }
            Console.WriteLine($"Deployment {deploymentName} is deploying template: {template}");

            string linkedSubId = GetLinkedSubscriptionId(saasDeviceName, saasResourceGroup);
            string deploymentUri;
            if (String.IsNullOrEmpty(linkedResourceGroup))
            {
                deploymentUri = $"/subscriptions/{SubscriptionId}/resourcegroups/{saasResourceGroup}/providers/Microsoft.AzureStack/linkedSubscriptions/{linkedSubId}/linkedProviders/Microsoft.Resources/deployments/{deploymentName}";
            }
            else
            {
                deploymentUri = $"/subscriptions/{SubscriptionId}/resourcegroups/{saasResourceGroup}/providers/Microsoft.AzureStack/linkedSubscriptions/{linkedSubId}/linkedResourceGroups/{linkedResourceGroup}/linkedProviders/Microsoft.Resources/deployments/{deploymentName}";
            }
            IRestResponse response = MakeRestCall(deploymentUri, Method.PUT, SAAS_API_VERSION, body: template);

            var statusUri = GetAzureAsyncHeader(response);

            // Poll for the deployment status triggered above
            Console.WriteLine($"Poll for the status of {deploymentName} with status uri {statusUri}");
            // Wait for max 60 mins from now for template deployment
            Stopwatch timer = new Stopwatch();
            int deploymentTimeout = 50;
            Console.WriteLine("Starting the timer");
            timer.Start();
            dynamic resp;
            string status;
            IRestResponse statusResponse;
            try
            {
                while (timer.Elapsed < TimeSpan.FromMinutes(deploymentTimeout))
                {
                    Console.WriteLine("Sleeping for 15 seconds");
                    System.Threading.Thread.Sleep(15000);

                    Console.WriteLine("Fetch deployment status");
                    try
                    {
                        statusResponse = MakeRestCallWithRetry(statusUri);
                    }
                    catch (Exception exc)
                    {
                        Console.WriteLine($"Got exception of type {exc.GetType()} and error messsage {exc.Message}");
                        dynamic error = JObject.Parse(exc.Message);
                        string errorCode = Convert.ToString(error.error.code);
                        if (errorCode.Equals("NotFound", StringComparison.OrdinalIgnoreCase))
                            continue;
                        else
                            throw exc;
                    }
                    resp = JObject.Parse(statusResponse.Content);
                    status = resp.status;
                    Console.WriteLine($"Provisioning state: {status}");

                    if (status.Equals("Succeeded", StringComparison.OrdinalIgnoreCase))
                        return;
                    else if (status.Equals("Failed", StringComparison.OrdinalIgnoreCase))
                        throw new Exception("Deployment failed");
                }
            }
            finally
            {
                Console.WriteLine("Stopping the timer");
                timer.Stop();
            }

            // Sometimes if AzureArmAgent crashes or restarts, we may get timed out while polling for status uri above.
            // We will check for the deployment resource once to confirm if things actually worked or not.
            Console.WriteLine($"Timed out after {deploymentTimeout} mins while waiting for statusURI to return Succeeded. Checking for deployment {deploymentName} status");
            response = MakeRestCallWithRetry(deploymentUri, SAAS_API_VERSION);
            resp = JObject.Parse(response.Content);
            status = resp.properties.provisioningState;
            Console.WriteLine($"Deployment {deploymentName} provisioning state: {status}");

            if (!status.Equals("Succeeded", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"Deployment {deploymentName} did not succeed");
            }
            Console.WriteLine($"Deployment {deploymentName} succeeded. ");
        }

        #endregion
    }
}