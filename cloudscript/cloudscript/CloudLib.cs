using System;
using System.Diagnostics;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.CSharp.RuntimeBinder;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Threading.Tasks;
using System.Threading;
using System.Text.RegularExpressions;

namespace cloudscript
{
    public class CloudLib
    {
        static string TenantId;
        static string ClientId;
        static string AccessKey;
        static string SubscriptionId;
        static string ArmEndpoint = "https://management.azure.com/";
        static string Token;
        IRestClient Client;

        public CloudLib(string accessKey, string tenantId, string clientId, string subscriptionId)
        {
            AccessKey = accessKey;
            TenantId = tenantId;
            ClientId = clientId;
            SubscriptionId = subscriptionId;
            var task = GetAzureAccessTokenAsync(TenantId, ClientId, AccessKey);
            Token = task.GetAwaiter().GetResult();
            Client = new RestClient(ArmEndpoint);
        }

        #region PublicMethods

        /// <summary>
        /// Get Azure Stack Edge (ASE) resource details.
        /// </summary>
        /// <param name="deviceName">ASE device name</param>
        /// <param name="resourceGroup">Saas Resource Group containing ASE device</param>
        /// <returns>ASE resource details</returns>
        public String GetAseResource(string deviceName, string resourceGroup)
        {
            Console.WriteLine($"Fetching the ASE resource object for device {deviceName}");
            string uri = $"/subscriptions/{SubscriptionId}/resourcegroups/{resourceGroup}/providers/Microsoft.DataboxEdge/dataBoxEdgeDevices/{deviceName}";

            Stopwatch timer = new Stopwatch();
            timer.Start();
            try
            {
                while (timer.Elapsed < TimeSpan.FromMinutes(1))
                {
                    IRestResponse response = MakeRestCallWithRetry(uri, "2020-09-01");
                    dynamic resp = JObject.Parse(response.Content);
                    try
                    {
                        String edgeSubId = resp.properties.edgeProfile.subscription.subscriptionId;
                        String regId = resp.properties.edgeProfile.subscription.registrationId;
                        if (String.IsNullOrEmpty(edgeSubId) || String.IsNullOrEmpty(regId))
                        {
                            // Sleep for 5 seconds and retry fetching ase resource
                            Console.WriteLine("No EdgeSubscriptionId or RegistrationId present. Sleeping for 5 seconds before retrying");
                            System.Threading.Thread.Sleep(5000);
                            continue;
                        }
                        return response.Content;
                    }
                    catch (RuntimeBinderException)
                    {
                        // Sleep for 5 seconds and retry fetching ase resource
                        Console.WriteLine("No EdgeSubscriptionId or RegistrationId present. Sleeping for 5 seconds before retrying");
                        System.Threading.Thread.Sleep(5000);
                    }
                }
            }
            finally
            {
                timer.Stop();
            }
            throw new TimeoutException($"No Edge Subscription Id present for device {deviceName}. Timed out after a minute");
        }

        /// <summary>
        /// Get Edge Subscription id of ASE device
        /// </summary>
        /// <param name="deviceName">ASE device name</param>
        /// <param name="resourceGroup">Saas Resource Group containing ASE device</param>
        /// <returns>Edge Subscription Id of ASE device</returns>
        public String GetEdgeSubscriptionId(string deviceName, string resourceGroup)
        {
            string response = GetAseResource(deviceName, resourceGroup);
            dynamic resp = JObject.Parse(response);
            return resp.properties.edgeProfile.subscription.subscriptionId;
        }

        /// <summary>
        /// Get local resources on ASE device
        /// </summary>
        /// <param name="resourceType">Type of the resource e.g. "Microsoft.Compute/images"</param>
        /// <param name="resourceName">Name of the resource on device</param>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="edgeSubId">Edge Subscription Id of ASE device</param>
        /// <returns>Resource object on the ASE device</returns>
        public string GetEdgeResource(string resourceType, string resourceName, string edgeResourceGroup, string saasResourceGroup, string edgeSubId)
        {
            Console.WriteLine($"Getting resource {resourceName} of type {resourceType} for edge {edgeSubId} under saas resource group {saasResourceGroup} and edge resource group {edgeResourceGroup}");
            string uri = $"/subscriptions/{SubscriptionId}/resourcegroups/{saasResourceGroup}/providers/Microsoft.AzureStack/linkedSubscriptions/{edgeSubId}/linkedResourceGroups/{edgeResourceGroup}/linkedproviders/{resourceType}/{resourceName}";
            IRestResponse response = MakeRestCallWithRetry(uri, "2020-06-01-preview");

            return response.Content;
        }

        /// <summary>
        /// Get resource group on ASE device
        /// </summary>
        /// <param name="edgeResourceGroup">Resource Group on device</param>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="edgeSubId">Edge Subscription Id of ASE device</param>
        /// <returns>Resource Group object on ASE device</returns>
        public string GetEdgeResourceGroup(string edgeResourceGroup, string saasResourceGroup, string edgeSubId)
        {
            Console.WriteLine($"Getting resource group {edgeResourceGroup} on edge {edgeSubId} under Saas resource group {saasResourceGroup}");
            string uri = $"/subscriptions/{SubscriptionId}/resourcegroups/{saasResourceGroup}/providers/Microsoft.AzureStack/linkedSubscriptions/{edgeSubId}/linkedResourcegroups/{edgeResourceGroup}";
            IRestResponse response = MakeRestCallWithRetry(uri, "2020-06-01-preview");
            return response.Content;
        }

        /// <summary>
        /// Get all local resources of a given type on ASE device
        /// </summary>
        /// <param name="resourceType">Type of the resource e.g. "Microsoft.Compute/images"</param>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="edgeSubId">Edge Subscription Id of ASE device</param>
        /// <returns>Resource objects on ASE device</returns>
        public string GetAllResourcesByTypeOnDevice(string saasResourceGroup, string edgeSubId, string resourceType)
        {
            Console.WriteLine($"Getting all resources of type {resourceType} on edge device with subscription id {edgeSubId} under resource group {saasResourceGroup}");
            string uri = $"/subscriptions/{SubscriptionId}/resourcegroups/{saasResourceGroup}/providers/Microsoft.AzureStack/linkedSubscriptions/{edgeSubId}/linkedproviders/{resourceType}";
            IRestResponse response = MakeRestCallWithRetry(uri, "2020-06-01-preview");
            return response.Content;
        }

        /// <summary>
        /// Trigger a template deployment from Saas.
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="deviceName">ASE device name</param>
        /// <param name="template">Json template to be deployed</param>
        /// <param name="deploymentName">Name of Saas deployment</param>
        /// <returns>None if deployment succeeds else throws exception</returns>
        public void DeployTemplate(string saasResourceGroup, string deviceName, string template, string deploymentName = "")
        {
            Console.WriteLine($"Deploying template under Saas ResourceGroup {saasResourceGroup} on ASE device {deviceName}");
            // Generate deployment name dynamically if not provided
            if (String.IsNullOrEmpty(deploymentName))
            {
                deploymentName = "deployment" + Guid.NewGuid().ToString().Split('-')[0];
            }
            Console.WriteLine($"Deployment {deploymentName} is deploying template: {template}");

            string edgeSubId = GetEdgeSubscriptionId(deviceName, saasResourceGroup);
            string deploymentUri = $"/subscriptions/{SubscriptionId}/resourcegroups/{saasResourceGroup}/providers/Microsoft.AzureStack/linkedSubscriptions/{edgeSubId}/linkedProviders/Microsoft.Resources/deployments/{deploymentName}";
            IRestResponse response = MakeRestCall(deploymentUri, Method.PUT, "2020-06-01-preview", body: template);

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
                        PollForDeployments(saasResourceGroup, edgeSubId, deploymentUri);
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
            response = MakeRestCallWithRetry(deploymentUri, "2020-06-01-preview");
            resp = JObject.Parse(response.Content);
            status = resp.properties.provisioningState;
            Console.WriteLine($"Deployment {deploymentName} provisioning state: {status}");

            if (!status.Equals("Succeeded", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"Deployment {deploymentName} did not succeed");
            }
            Console.WriteLine($"Deployment {deploymentName} succeeded. ");
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
            Console.WriteLine($"Got token: {accessToken}");
            return accessToken;
        }

        /// <summary>
        /// Start a VM.
        /// </summary>
        /// <param name="deviceName">ASE device name</param>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="vmName">VM name</param>
        /// <param name="edgeResourceGroup">Resource Group on device</param>
        /// <returns>None</returns>
        public void StartVM(string deviceName, string saasResourceGroup, string vmName, string edgeResourceGroup)
        {
            Console.WriteLine($"Starting the VM {vmName}, RG {edgeResourceGroup}");
            string response1 = GetAseResource(deviceName, saasResourceGroup);
            dynamic resp1 = JObject.Parse(response1);
            String edgeSubId = resp1.properties.edgeProfile.subscription.subscriptionId;
            string uri = $"/subscriptions/{SubscriptionId}/resourcegroups/{saasResourceGroup}/providers/Microsoft.AzureStack/linkedSubscriptions/{edgeSubId}/linkedResourcegroups/{edgeResourceGroup}/linkedProviders/Microsoft.Compute/virtualMachines/{vmName}/start";
            IRestResponse responsePost = MakeRestCall(uri, Method.POST, "2020-06-01-preview");
            PollForStatus(responsePost, "Starting the VM");
        }

        /// <summary>
        /// Stop a VM.
        /// </summary>
        /// <param name="deviceName">ASE device name</param>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="vmName">VM name</param>
        /// <param name="edgeResourceGroup">Resource Group on device</param>
        /// <param name="stayProvisioned">whether VM should be stay provisioned or deallocated</param>
        /// <returns></returns>
        public void StopVM(string deviceName, string saasResourceGroup, string vmName, string edgeResourceGroup, bool stayProvisioned = true)
        {
            Console.WriteLine($"Stopping the VM {vmName}, RG {edgeResourceGroup} with stayProvisioned set to {stayProvisioned}");
            string response1 = GetAseResource(deviceName, saasResourceGroup);
            dynamic resp1 = JObject.Parse(response1);
            String edgeSubId = resp1.properties.edgeProfile.subscription.subscriptionId;
            String vmAction = stayProvisioned ? "powerOff" : "deallocate";
            string uri = $"/subscriptions/{SubscriptionId}/resourcegroups/{saasResourceGroup}/providers/Microsoft.AzureStack/linkedSubscriptions/{edgeSubId}/linkedResourcegroups/{edgeResourceGroup}/linkedProviders/Microsoft.Compute/virtualMachines/{vmName}/{vmAction}";
            IRestResponse responsePost = MakeRestCall(uri, Method.POST, "2020-06-01-preview");
            PollForStatus(responsePost, $"Stopping the VM ({vmAction})");
        }

        /// <summary>
        /// Deleting VM by deleting resource group
        /// </summary>
        /// <param name="deviceName">ASE device name</param>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="vmName">VM name</param>
        /// <param name="edgeResourceGroup">Resource Group on device</param>
        /// <returns></returns>
        public void DeleteRG(string deviceName, string saasResourceGroup, string vmName, string edgeResourceGroup)
        {
            Console.WriteLine($"Deleting the RG {edgeResourceGroup}");
            string response = GetAseResource(deviceName, saasResourceGroup);
            dynamic resp = JObject.Parse(response);
            String edgeSubId = resp.properties.edgeProfile.subscription.subscriptionId;
            string uri = $"/subscriptions/{SubscriptionId}/resourcegroups/{saasResourceGroup}/providers/Microsoft.AzureStack/linkedSubscriptions/{edgeSubId}/linkedResourcegroups/{edgeResourceGroup}";
            IRestResponse responseDelete = MakeRestCall(uri, Method.DELETE, "2020-06-01-preview", "2018-09-01");
            PollForStatus(responseDelete, $"Deleting vm {vmName} in edge resource group {edgeResourceGroup}", 30);
        }

        /// <summary>
        /// Deleting VM and its related resources recursively.
        /// </summary>
        /// <param name="deviceName">ASE device name</param>
        /// <param name="resourceGroup"></param>
        /// <param name="vmName">VM name</param>
        /// <param name="edgeResourceGroup">Resource Group on device</param>
        /// <param name="diskName">VM disk name</param>
        /// <param name="nicName">VM nic name</param>
        /// <returns></returns>
        public void DeleteVMResourcesRecursively(string deviceName, string resourceGroup, string vmName, string edgeResourceGroup, string diskName, string nicName)
        {
            string response = GetAseResource(deviceName, resourceGroup);
            dynamic resp = JObject.Parse(response);
            String edgeSubId = resp.properties.edgeProfile.subscription.subscriptionId;
            DeleteVM(resourceGroup, edgeResourceGroup, edgeSubId, vmName);
            DeleteDisk(resourceGroup, edgeResourceGroup, edgeSubId, diskName);
            DeleteNIC(resourceGroup, edgeResourceGroup, edgeSubId, nicName);
            DeleteRG(deviceName, resourceGroup, vmName, edgeResourceGroup);
        }

        /// <summary>
        /// Delete VM
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="edgeResourceGroup">Resource Group on device</param>
        /// <param name="edgeSubId">Edge Subscription Id of ASE device</param>
        /// <param name="vmName">VM name</param>
        /// <returns></returns>
        public void DeleteVM(string saasResourceGroup, string edgeResourceGroup, string edgeSubId, string vmName)
        {
            Console.WriteLine($"Deleting the vm {vmName}");
            string uri = $"/subscriptions/{SubscriptionId}/resourcegroups/{saasResourceGroup}/providers/Microsoft.AzureStack/linkedSubscriptions/{edgeSubId}/linkedResourcegroups/{edgeResourceGroup}/linkedProviders/Microsoft.Compute/virtualMachines/{vmName}";
            IRestResponse responseDelete = MakeRestCall(uri, Method.DELETE, "2020-06-01-preview");
            PollForStatus(responseDelete, "deleting VM");
        }

        /// <summary>
        /// Delete disk
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="edgeResourceGroup">Resource Group on device</param>
        /// <param name="edgeSubId">Edge Subscription Id of ASE device</param>
        /// <param name="diskName">Disk name</param>
        /// <returns></returns>
        public void DeleteDisk(string saasResourceGroup, string edgeResourceGroup, string edgeSubId, string diskName)
        {
            Console.WriteLine($"Deleting the disk {diskName}");
            string uri = $"/subscriptions/{SubscriptionId}/resourcegroups/{saasResourceGroup}/providers/Microsoft.AzureStack/linkedSubscriptions/{edgeSubId}/linkedResourcegroups/{edgeResourceGroup}/linkedProviders/Microsoft.Compute/disks/{diskName}";
            IRestResponse responseDelete = MakeRestCall(uri, Method.DELETE, "2020-06-01-preview");
            PollForStatus(responseDelete, "deleting Disk");
        }

        /// <summary>
        /// Delete network interface
        /// </summary>
        /// <param name="resourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="edgeResourceGroup">Resource Group on device</param>
        /// <param name="edgeSubId">Edge Subscription Id of ASE device</param>
        /// <param name="nicName">Nic name</param>
        /// <returns></returns>
        public void DeleteNIC(string resourceGroup, string edgeResourceGroup, string edgeSubId, string nicName)
        {
            Console.WriteLine($"Deleting the Network Interface {nicName}");
            string uri = $"/subscriptions/{SubscriptionId}/resourcegroups/{resourceGroup}/providers/Microsoft.AzureStack/linkedSubscriptions/{edgeSubId}/linkedResourcegroups/{edgeResourceGroup}/linkedProviders/Microsoft.Network/networkInterfaces/{nicName}";
            IRestResponse responseDelete = MakeRestCall(uri, Method.DELETE, "2020-06-01-preview");
            PollForStatus(responseDelete, "deleting Network Interface");
        }

        /// <summary>
        /// Create a managed disk
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="edgeSubId">Edge Subscription Id of ASE device</param>
        /// <param name="diskName">Disk name</param>
        /// <param name="diskResourceGroup">Resource group containing disk</param>
        /// <param name="diskSizeGB">Disk size in GB</param>
        /// <returns></returns>
        public void CreateManagedDisk(string saasResourceGroup, string edgeSubId, string diskName, string diskResourceGroup, string diskSizeGB)
        {
            Console.WriteLine($"Creating a managed disk {diskName} of size {diskSizeGB} GB under edge resource group {diskResourceGroup}. SaasResourceGroup: {saasResourceGroup}, EdgeSubId: {edgeSubId}");
            String uri = $"/subscriptions/{SubscriptionId}/resourcegroups/{saasResourceGroup}/providers/Microsoft.AzureStack/linkedSubscriptions/{edgeSubId}/linkedResourceGroups/{diskResourceGroup}/linkedProviders/Microsoft.Compute/disks/{diskName}";
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

            IRestResponse response = MakeRestCall(uri, Method.PUT, "2020-06-01-preview", body: body);
            PollForStatus(response, $"Create disk {diskName}");
            Console.WriteLine($"Created a managed disk {diskName} of size {diskSizeGB} GB under edge resource group {diskResourceGroup}");
        }

        /// <summary>
        /// Create a nic
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="edgeSubId">Edge Subscription Id of ASE device</param>
        /// <param name="nicName">Nic name</param>
        /// <param name="nicResourceGroup">Resource group containing nic</param>
        /// <param name="vnetName">Virtual network where nic should be created</param>
        /// <param name="vnetResourceGroup">Resource group containing virtual network</param>
        /// <param name="ip">IP to be allocated to the nic</param>
        /// <returns></returns>
        public void CreateNic(string saasResourceGroup, string edgeSubId, string nicName, string nicResourceGroup, string vnetName, string vnetResourceGroup, string ip = "")
        {
            string ipConfig = String.IsNullOrEmpty(ip) ? "Dynamic" : "Static";
            Console.WriteLine($"Creating a nic {nicName} under edge resource group {nicResourceGroup} with {ipConfig} ip configuration. SaasResourceGroup: {saasResourceGroup}, EdgeSubId: {edgeSubId}. IP: {ip}");

            //Get the subnet from vnet. Current vnet has only one subnet.
            String vnetJson = GetEdgeResource("Microsoft.Network/virtualNetworks", vnetName, vnetResourceGroup, saasResourceGroup, edgeSubId);
            dynamic vnetObj = JObject.Parse(vnetJson);

            String uri = $"/subscriptions/{SubscriptionId}/resourcegroups/{saasResourceGroup}/providers/Microsoft.AzureStack/linkedSubscriptions/{edgeSubId}/linkedResourceGroups/{nicResourceGroup}/linkedProviders/Microsoft.Network/networkInterfaces/{nicName}";

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

            IRestResponse response = MakeRestCall(uri, Method.PUT, "2020-06-01-preview", body: (payload.ToString()));
            PollForStatus(response, $"Create nic {nicName}");
            Console.WriteLine($"Created nic {nicName} under edge resource group {nicResourceGroup} with {ipConfig} ip configuration");
        }

        /// <summary>
        /// Attach the specified disk to the specified VM
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="edgeSubId">Edge Subscription Id of ASE device</param>
        /// <param name="vmName">VM name</param>
        /// <param name="vmResourceGroup">Resource group containing VM</param>
        /// <param name="diskName">Disk name</param>
        /// <param name="diskResourceGroup">Resource group containing disk</param>
        /// <param name="lunId">Lun id with which disk should attach to VM</param>
        /// <returns></returns>
        public void AttachDisk(string saasResourceGroup, string edgeSubId, string vmName, string vmResourceGroup, string diskName, string diskResourceGroup, int lunId = 0)
        {
            Console.WriteLine($"Attaching disk {diskName} under edge resource group {diskResourceGroup} to vm {vmName} under edge resource group {vmResourceGroup} with lunId {lunId}");

            // Get the VM object
            String vmJson = GetEdgeResource("Microsoft.Compute/virtualMachines", vmName, vmResourceGroup, saasResourceGroup, edgeSubId);
            dynamic vmObj = JObject.Parse(vmJson);

            // Get the disk object
            String diskJson = GetEdgeResource("Microsoft.Compute/disks", diskName, diskResourceGroup, saasResourceGroup, edgeSubId);
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
            newDisk.managedDisk.id = GetLocalResourceId((String)diskObj.id);

            // Add the disk to the list of existing disks
            JArray dataDisks = (JArray)vmObj.properties.storageProfile.dataDisks;
            dataDisks.Add((JObject)newDisk);

            // Create payload to attach the disk
            dynamic payload = new JObject();
            payload.properties = new JObject();
            payload.properties.storageProfile = new JObject();
            payload.properties.storageProfile.dataDisks = dataDisks;

            // Make request to attach the disk
            IRestResponse response = MakeRestCall((String)vmObj.id, Method.PATCH, "2020-06-01-preview", body: (payload.ToString()));
            PollForStatus(response, $"Attach disk {diskName} to VM {vmName}");
            Console.WriteLine($"Attached the disk {diskName} in edge resource group {diskResourceGroup} to the VM {vmName} in edge resource group {vmResourceGroup}");
        }

        /// <summary>
        /// Detach the specified disk from the specified VM
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="edgeSubId">Edge Subscription Id of ASE device</param>
        /// <param name="vmName">VM name</param>
        /// <param name="vmResourceGroup">Resource group containing VM</param>
        /// <param name="diskName">Disk name</param>
        /// <param name="diskResourceGroup">Resource group containign disk</param>
        /// <returns></returns>
        public void DetachDisk(string saasResourceGroup, string edgeSubId, string vmName, string vmResourceGroup, string diskName, string diskResourceGroup)
        {
            Console.WriteLine($"Detaching disk {diskName} under edge resource group {diskResourceGroup} from vm {vmName} under edge resource group {vmResourceGroup}");

            // Get the VM object
            String vmJson = GetEdgeResource("Microsoft.Compute/virtualMachines", vmName, vmResourceGroup, saasResourceGroup, edgeSubId);
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
                Console.WriteLine($"Disk {diskName} in edge resource group {diskResourceGroup} is not part of VM disks currently. Current VM object: {vmObj.ToString()}");
                return;
            }

            // Create payload for removing the disk
            dynamic payload = new JObject();
            payload.properties = new JObject();
            payload.properties.storageProfile = new JObject();
            payload.properties.storageProfile.dataDisks = dataDisks;

            // Make request to attach the disk
            IRestResponse response = MakeRestCall((String)vmObj.id, Method.PATCH, "2020-06-01-preview", body: (payload.ToString()));
            PollForStatus(response, $"Detach disk {diskName} from VM {vmName}");
            Console.WriteLine($"Detached the disk {diskName} in edge resource group {diskResourceGroup} to the VM {vmName} in edge resource group {vmResourceGroup}");
        }

        /// <summary>
        /// Resize a disk
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="edgeSubId">Edge Subscription Id of ASE device</param>
        /// <param name="diskName">Disk name</param>
        /// <param name="diskResourceGroup">Resource group containign disk</param>
        /// <param name="newSizeInGb">Disk size in GB</param>
        /// <returns></returns>
        public void ResizeDisk(string saasResourceGroup, string edgeSubId, string diskName, string diskResourceGroup, int newSizeInGb)
        {
            Console.WriteLine($"Resizing the disk {diskName} in edge resource group {diskResourceGroup} to {newSizeInGb} GB");

            // Get the disk object
            String diskJson = GetEdgeResource("Microsoft.Compute/disks", diskName, diskResourceGroup, saasResourceGroup, edgeSubId);
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
            IRestResponse response = MakeRestCall((String)diskObj.id, Method.PATCH, "2020-06-01-preview", body: (payload.ToString()));
            PollForStatus(response, $"Resize disk {diskName}");
            Console.WriteLine($"Resized disk {diskName} in edge resource group {diskResourceGroup} to size {newSizeInGb} GB");
        }

        /// <summary>
        /// Resize a VM
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="edgeSubId">Edge Subscription Id of ASE device</param>
        /// <param name="vmName">VM name</param>
        /// <param name="vmResourceGroup">Resource group containing VM</param>
        /// <param name="newVMSize">VM size e.g. Standard_D1_V1</param>
        /// <returns></returns>
        public void ResizeVM(string saasResourceGroup, string edgeSubId, string vmName, string vmResourceGroup, string newVMSize)
        {
            Console.WriteLine($"Resizing the VM {vmName} in edge resource group {vmResourceGroup} to {newVMSize}");

            // Get the vm object
            String vmJson = GetEdgeResource("Microsoft.Compute/virtualMachines", vmName, vmResourceGroup, saasResourceGroup, edgeSubId);
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
            IRestResponse response = MakeRestCall((String)vmObj.id, Method.PATCH, "2020-06-01-preview", body: (payload.ToString()));
            PollForStatus(response, $"Resize vm {vmName}");
            Console.WriteLine($"Resized VM {vmName} in edge resource group {vmResourceGroup} to size {newVMSize}");
        }

        /// <summary>
        /// Check if the specified disk is attahced to the specified VM
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="edgeSubId">Edge Subscription Id of ASE device</param>
        /// <param name="vmName">VM name</param>
        /// <param name="vmResourceGroup">Resource group containing VM</param>
        /// <param name="diskName">Disk name</param>
        /// <param name="diskResourceGroup">Resource group containign disk</param>
        /// <param name="sleepTimeSec"> Sleep time in seconds before checking</param>
        /// <returns>bool</returns>
        public bool IsDiskAttachedToVM(string saasResourceGroup, string edgeSubId, string vmName, string vmResourceGroup, string diskName, string diskResourceGroup, int sleepTimeSec = 0)
        {

            Console.WriteLine($"Check if disk {diskName} in resource group {diskResourceGroup} is attached to VM {vmName} in resource group {vmResourceGroup}. Sleeping for {sleepTimeSec} seconds before checking");
            System.Threading.Thread.Sleep(sleepTimeSec * 1000);

            // Get the vm object
            String vmJson = GetEdgeResource("Microsoft.Compute/virtualMachines", vmName, vmResourceGroup, saasResourceGroup, edgeSubId);
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
        /// <param name="edgeSubId">Edge Subscription Id of ASE device</param>
        /// <param name="diskName">Disk name</param>
        /// <param name="diskResourceGroup">Resource group containign disk</param>
        /// <param name="expectedDiskSizeGb">Disk size in GB</param>
        /// <param name="sleepTimeSec">Sleep time in seconds before checking</param>
        /// <returns>bool</returns>
        public bool IsDiskSizeExpected(string saasResourceGroup, string edgeSubId, string diskName, string diskResourceGroup, int expectedDiskSizeGb, int sleepTimeSec = 0)
        {
            Console.WriteLine($"Verifying the disk {diskName} in resource group {diskResourceGroup} is of size {expectedDiskSizeGb} GB. Sleeping for {sleepTimeSec} seconds before checking");
            System.Threading.Thread.Sleep(sleepTimeSec * 1000);

            // Get the disk object
            String diskJson = GetEdgeResource("Microsoft.Compute/disks", diskName, diskResourceGroup, saasResourceGroup, edgeSubId);
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
        /// <param name="edgeSubId">Edge Subscription Id of ASE device</param>
        /// <param name="vmName">VM name</param>
        /// <param name="vmResourceGroup">Resource group containing VM</param>
        /// <param name="expectedVMsize">Expected VM size</param>
        /// <param name="sleepTimeSec">Sleep time in seconds before checking</param>
        /// <returns>bool</returns>
        public bool IsVMSizeExpected(string saasResourceGroup, string edgeSubId, string vmName, string vmResourceGroup, string expectedVMsize, int sleepTimeSec = 0)
        {
            Console.WriteLine($"Verifying if the VM {vmName} in resource group {vmResourceGroup} is of size {expectedVMsize}. Sleeping for {sleepTimeSec} seconds before checking");
            System.Threading.Thread.Sleep(sleepTimeSec * 1000);

            // Get the vm object
            String vmJson = GetEdgeResource("Microsoft.Compute/virtualMachines", vmName, vmResourceGroup, saasResourceGroup, edgeSubId);
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
        /// <param name="edgeSubId">Edge Subscription Id of ASE device</param>
        /// <param name="vmName">VM name</param>
        /// <param name="vmResourceGroup">Resource group containing VM</param>
        /// <param name="nicName">Nic name</param>
        /// <param name="nicResourceGroup">Resource group containing nic</param>
        /// <param name="primary">Whether nic should be attached as primary or not</param>
        /// <returns></returns>
        public void AttachNic(string saasResourceGroup, string edgeSubId, string vmName, string vmResourceGroup, string nicName, string nicResourceGroup, bool primary = false)
        {
            Console.WriteLine($"Attaching nic {nicName} under edge resource group {nicResourceGroup} to vm {vmName} under edge resource group {vmResourceGroup} with primary set to {primary}");

            // Get the VM object
            String vmJson = GetEdgeResource("Microsoft.Compute/virtualMachines", vmName, vmResourceGroup, saasResourceGroup, edgeSubId);
            dynamic vmObj = JObject.Parse(vmJson);

            // Get the nic object
            String nicJson = GetEdgeResource("Microsoft.Network/networkInterfaces", nicName, nicResourceGroup, saasResourceGroup, edgeSubId);
            dynamic nicObj = JObject.Parse(nicJson);

            // Create nic object structure
            dynamic newNic = new JObject();
            newNic.id = GetLocalResourceId((String)nicObj.id);
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
            IRestResponse response = MakeRestCall((String)vmObj.id, Method.PATCH, "2020-06-01-preview", body: (payload.ToString()));
            PollForStatus(response, $"Attach nic {nicName} to VM {vmName}");
            Console.WriteLine($"Attached the nic {nicName} in edge resource group {nicResourceGroup} to the VM {vmName} in edge resource group {vmResourceGroup} with primary set to {primary}");
        }

        /// <summary>
        /// Set the specified nic to the specified VM as primary nic
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="edgeSubId">Edge Subscription Id of ASE device</param>
        /// <param name="vmName">VM name</param>
        /// <param name="vmResourceGroup">Resource group containing VM</param>
        /// <param name="nicName">Nic name</param>
        /// <param name="nicResourceGroup">Resource group containing nic</param>
        /// <returns></returns>
        public void SetNicAsPrimary(string saasResourceGroup, string edgeSubId, string vmName, string vmResourceGroup, string nicName, string nicResourceGroup)
        {
            Console.WriteLine($"Setting nic {nicName} under edge resource group {nicResourceGroup} to vm {vmName} under edge resource group {vmResourceGroup} as primary");

            // Get the VM object
            String vmJson = GetEdgeResource("Microsoft.Compute/virtualMachines", vmName, vmResourceGroup, saasResourceGroup, edgeSubId);
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
            IRestResponse response = MakeRestCall((String)vmObj.id, Method.PATCH, "2020-06-01-preview", body: (payload.ToString()));
            PollForStatus(response, $"Set nic {nicName} to VM {vmName} as primary");
            Console.WriteLine($"Set the nic {nicName} in edge resource group {nicResourceGroup} to the VM {vmName} in edge resource group {vmResourceGroup} as primary");
        }

        /// <summary>
        /// Detach the specified nic from the specified VM
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="edgeSubId">Edge Subscription Id of ASE device</param>
        /// <param name="vmName">VM name</param>
        /// <param name="vmResourceGroup">Resource group containing VM</param>
        /// <param name="nicName">Nic name</param>
        /// <param name="nicResourceGroup">Resource group containing nic</param>
        /// <returns></returns>
        public void DetachNic(string saasResourceGroup, string edgeSubId, string vmName, string vmResourceGroup, string nicName, string nicResourceGroup)
        {
            Console.WriteLine($"Detaching nic {nicName} under edge resource group {nicResourceGroup} from vm {vmName} under edge resource group {vmResourceGroup}");

            // Get the VM object
            String vmJson = GetEdgeResource("Microsoft.Compute/virtualMachines", vmName, vmResourceGroup, saasResourceGroup, edgeSubId);
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
                Console.WriteLine($"Nic {nicName} in edge resource group {nicResourceGroup} is not part of VM nics currently. Current VM object: {vmObj.ToString()}");
                return;
            }

            // Create payload for removing the nic
            dynamic payload = new JObject();
            payload.properties = new JObject();
            payload.properties.networkProfile = new JObject();
            payload.properties.networkProfile.networkInterfaces = networkInterfaces;

            // Make request to attach the nic
            IRestResponse response = MakeRestCall((String)vmObj.id, Method.PATCH, "2020-06-01-preview", body: (payload.ToString()));
            PollForStatus(response, $"Detach nic {nicName} from VM {vmName}");
            Console.WriteLine($"Detached the nic {nicName} in edge resource group {nicResourceGroup} to the VM {vmName} in edge resource group {vmResourceGroup}");
        }

        /// <summary>
        /// Check if the specified nic is attahced to the specified VM
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="edgeSubId">Edge Subscription Id of ASE device</param>
        /// <param name="vmName">VM name</param>
        /// <param name="vmResourceGroup">Resource group containing VM</param>
        /// <param name="nicName">Nic name</param>
        /// <param name="nicResourceGroup">Resource group containing nic</param>
        /// <param name="sleepTimeSec">Sleep time in seconds before checking</param>
        /// <returns>True if nic is attached to VM</returns>
        public bool IsNicAttachedToVM(string saasResourceGroup, string edgeSubId, string vmName, string vmResourceGroup, string nicName, string nicResourceGroup, int sleepTimeSec = 0)
        {

            Console.WriteLine($"Check if nic {nicName} in resource group {nicResourceGroup} is attached to VM {vmName} in resource group {vmResourceGroup}. Sleeping for {sleepTimeSec} seconds before checking");
            System.Threading.Thread.Sleep(sleepTimeSec * 1000);

            // Get the vm object
            String vmJson = GetEdgeResource("Microsoft.Compute/virtualMachines", vmName, vmResourceGroup, saasResourceGroup, edgeSubId);
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
        /// <param name="edgeSubId">Edge Subscription Id of ASE device</param>
        /// <param name="nicName">Nic name</param>
        /// <param name="nicResourceGroup">Resource group containing nic</param>
        /// <param name="staticIp">Static IP address</param>
        /// <returns></returns>
        public void SetNicIp(string saasResourceGroup, string edgeSubId, string nicName, string nicResourceGroup, string staticIp = "")
        {
            string ipConfig = String.IsNullOrEmpty(staticIp) ? "Dynamic" : "Static";
            Console.WriteLine($"Setting the ip configuration of nic {nicName} in edge resource group {nicResourceGroup} to {ipConfig}. IP: {staticIp}");

            // Get the nic object
            String nicJson = GetEdgeResource("Microsoft.Network/networkInterfaces", nicName, nicResourceGroup, saasResourceGroup, edgeSubId);
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
            IRestResponse response = MakeRestCall((String)nicObj.id, Method.PUT, "2020-06-01-preview", body: (payload.ToString()));
            PollForStatus(response, $"Set ip config of nic {nicName} to {ipConfig}. IP: {staticIp}");
            Console.WriteLine($"Set the ip config of nic {nicName} in edge resource group {nicResourceGroup} to {ipConfig} successfully");
        }

        /// <summary>
        /// Check if specifid nic has specified ip
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="edgeSubId">Edge Subscription Id of ASE device</param>
        /// <param name="nicName">Nic name</param>
        /// <param name="nicResourceGroup">Resource group containing nic</param>
        /// <param name="ip">IP address for nic</param>
        /// <param name="sleepTimeInSec">Sleep time in seconds before checking</param>
        /// <returns>True if the specified IP is assigned to the nic</returns>
        public bool HasNicIp(string saasResourceGroup, string edgeSubId, string nicName, string nicResourceGroup, string ip, int sleepTimeInSec = 0)
        {
            Console.WriteLine($"Checking if nic {nicName} in edge resource group {nicResourceGroup} has ip set to {ip}. Sleeping for {sleepTimeInSec} seconds before checking");
            Thread.Sleep(sleepTimeInSec * 1000);

            // Get the nic object
            String nicJson = GetEdgeResource("Microsoft.Network/networkInterfaces", nicName, nicResourceGroup, saasResourceGroup, edgeSubId);
            dynamic nicObj = JObject.Parse(nicJson);

            if (nicObj.properties.ipConfigurations[0].properties.privateIPAddress == ip)
            {
                return true;
            }
            Console.WriteLine($"Nic {nicName} in edge resource group {nicResourceGroup} has ip set to {nicObj.properties.ipConfigurations[0].properties.privateIPAddress}");
            return false;
        }

        /// <summary>
        /// Check if specifid nic has static ip configuration
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="edgeSubId">Edge Subscription Id of ASE device</param>
        /// <param name="nicName">Nic name</param>
        /// <param name="nicResourceGroup">Resource group containing nic</param>
        /// <param name="sleepTimeInSec">Sleep time in seconds before checking</param>
        /// <returns>True if Nic has static ip configuration</returns>
        public bool IsNicStatic(string saasResourceGroup, string edgeSubId, string nicName, string nicResourceGroup, int sleepTimeInSec = 0)
        {
            Console.WriteLine($"Checking if nic {nicName} in edge resource group {nicResourceGroup} has static ip configuration. Sleeping for {sleepTimeInSec} seconds before checking");
            Thread.Sleep(sleepTimeInSec * 1000);

            // Get the nic object
            String nicJson = GetEdgeResource("Microsoft.Network/networkInterfaces", nicName, nicResourceGroup, saasResourceGroup, edgeSubId);
            dynamic nicObj = JObject.Parse(nicJson);

            if (((String)nicObj.properties.ipConfigurations[0].properties.privateIPAllocationMethod).Equals("Static", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            Console.WriteLine($"Nic {nicName} in edge resource group {nicResourceGroup} has ip set to {nicObj.properties.ipConfigurations[0].properties.privateIPAllocationMethod}");
            return false;
        }

        /// <summary>
        /// Check if the specified nic is attahced to the specified VM as a primary nic
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="edgeSubId">Edge Subscription Id of ASE device</param>
        /// <param name="vmName">VM name</param>
        /// <param name="vmResourceGroup">Resource group containing VM</param>
        /// <param name="nicName">Nic name</param>
        /// <param name="nicResourceGroup">Resource group containing nic</param>
        /// <param name="sleepTimeSec">Sleep time in seconds before checking</param>
        /// <returns>True if Nic is a primary nic</returns>
        public bool IsNicPrimary(string saasResourceGroup, string edgeSubId, string vmName, string vmResourceGroup, string nicName, string nicResourceGroup, int sleepTimeSec = 0)
        {

            Console.WriteLine($"Check if nic {nicName} in resource group {nicResourceGroup} is attached to VM {vmName} in resource group {vmResourceGroup} as primary. Sleeping for {sleepTimeSec} seconds before checking");
            System.Threading.Thread.Sleep(sleepTimeSec * 1000);

            // Get the vm object
            String vmJson = GetEdgeResource("Microsoft.Compute/virtualMachines", vmName, vmResourceGroup, saasResourceGroup, edgeSubId);
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
        /// Get all the resources inside an edge resource group
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="edgeSubId">Edge Subscription Id of ASE device</param>
        /// <param name="edgeResourceGroup">Resource Group on device</param>
        /// <returns>Returns all resources in edge resource group</returns>
        public string GetAllResourcesInLinkedResourceGroup(string saasResourceGroup, string edgeSubId, string edgeResourceGroup)
        {
            Console.WriteLine($"Getting all resources inside edge resource group {edgeResourceGroup}. SaasResourceGroup: {saasResourceGroup}, EdgeSubId: {edgeSubId}");

            String uri = $"/subscriptions/{SubscriptionId}/resourcegroups/{saasResourceGroup}/providers/Microsoft.AzureStack/linkedSubscriptions/{edgeSubId}/linkedResourceGroups/{edgeResourceGroup}/resources";

            IRestResponse response = MakeRestCallWithRetry(uri, "2020-06-01-preview");
            return response.Content;
        }

        /// <summary>
        /// Convert a remote resource id to a local resource id
        /// </summary>
        /// <param name="remoteResourceId">Resource Id of a resource on ASE device when seen from cloud</param>
        /// <returns>Local resource Id i.e. Resource Id of a resource on ASE device when seen from device itself</returns>
        public String GetLocalResourceId(string remoteResourceId)
        {
            Console.WriteLine($"Remote resource Id: {remoteResourceId}");
            string localLinkedResourceId = remoteResourceId.Split(new String[] { "Microsoft.AzureStack" }, StringSplitOptions.RemoveEmptyEntries)[1];
            string localResourceId = localLinkedResourceId.Replace("linked", "");
            Console.WriteLine($"Local resource Id: {localResourceId}");
            return localResourceId;
        }

        /// <summary>
        /// Get the default virtual network on ASE device
        /// </summary>
        /// <param name="deviceName">ASE Device name</param>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <returns>returns a tuple (Virtual network name, Virtual network resource group, Subnet name)</returns>
        public (String, String, String) GetDefaultVirtualNetwork(string deviceName, string saasResourceGroup)
        {
            var edgeSubId = GetEdgeSubscriptionId(deviceName, saasResourceGroup);
            var vnets = GetAllResourcesByTypeOnDevice(saasResourceGroup, edgeSubId, "Microsoft.Network/virtualNetworks");
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
        /// Poll for both the deployments: deployment triggered from Saas and depolyment triggered locally
        /// </summary>
        /// <param name="saasResourceGroup">Saas Resource Group containing ASE device</param>
        /// <param name="edgeSubId">Edge subscription id</param>
        /// <param name="deploymentUri"> Deployment uri triggered from Saas </param>
        /// <returns>None</returns>
        private void PollForDeployments(string saasResourceGroup, string edgeSubId, string deploymentUri)
        {
            // We put a try catch here because we do not want the caller to fail due to failures in polling.
            // This is just for info purpose
            try
            {
                // Check main deployment status
                IRestResponse response = MakeRestCallWithRetry(deploymentUri, "2020-06-01-preview");
                dynamic resp = JObject.Parse(response.Content);
                string deploymentName = resp.Name;
                string status = resp.properties.provisioningState;
                Console.WriteLine($"Deployment {deploymentName} provisioning state: {status}");

                // Check the inner deployment triggered on device
                string innerDeploymentName = resp.properties.dependencies[0].resourceName;
                string innerDeploymentResourceType = resp.properties.dependencies[0].resourceType;
                // We should have a better mechanism of figuring out resource group for deployment. May be parse the id field in future.
                string innerDeploymentResourceGroup = resp.properties.dependencies[0].dependsOn[0].resourceName;
                string innerDeploymentOutput = GetEdgeResource(innerDeploymentResourceType, innerDeploymentName, innerDeploymentResourceGroup, saasResourceGroup, edgeSubId);
                Console.WriteLine($"Deployment {innerDeploymentName} content: {innerDeploymentOutput}");
            }
            catch (Exception exc)
            {
                Console.WriteLine($"Got error while polling deployments: {exc}");
            }
        }

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
        /// <param name="apiVersion">Saas api version</param>
        /// <param name="edgeApiVersion">Resource specific api version</param>
        /// <param name="body">Request body</param>
        /// <param name="doNotThrowException">does not throw exception when set to true and fails silently</param>
        /// <returns>Rest response</returns>
        private IRestResponse MakeRestCall(string uri, Method method, string apiVersion = "", string edgeApiVersion = "", object body = null, bool doNotThrowException = false)
        {
            Console.WriteLine($"MakeRestCall Uri: {uri}, Method: {method}");
            IRestRequest request = new RestRequest(uri, method);
            if (!String.IsNullOrEmpty(apiVersion))
                request.AddQueryParameter("api-version", apiVersion);
            if (!String.IsNullOrEmpty(edgeApiVersion))
                request.AddQueryParameter("linked-api-version", edgeApiVersion);
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
        /// <param name="apiVersion">Saas api version</param>
        /// <param name="edgeApiVersion">Resource specific api version</param>
        /// <param name="maxRetries">Max retries</param>
        /// <param name="retryFrequency">Time gap in seconds for rest call retires</param>
        /// <returns>Rest response</returns>
        private IRestResponse MakeRestCallWithRetry(string uri, string apiVersion = "", string edgeApiVersion = "", int maxRetries = 8, int retryFrequency = 15)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                IRestResponse response = MakeRestCall(uri, Method.GET, apiVersion: apiVersion, edgeApiVersion: edgeApiVersion, doNotThrowException: true);
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

        #endregion
    }
}