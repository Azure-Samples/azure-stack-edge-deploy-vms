using CreateVmSample.Common;

using Microsoft.Azure.Management.Profiles.hybrid_2019_03_01.Compute;
using Microsoft.Azure.Management.Profiles.hybrid_2019_03_01.Network;
using Microsoft.Azure.Management.Profiles.hybrid_2019_03_01.ResourceManager;
using Microsoft.Azure.Management.Profiles.hybrid_2019_03_01.ResourceManager.Models;
using Microsoft.Rest.Azure;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CreateVmSample
{
    class Program
    {
        static void Main(string[] args)
        {

            // Tenant Id  of DBE
            // For DBE tenant id is same for all devices
            var tenantId = "c0257de7-538f-415c-993a-1b87a031879d";

            // User Name
            // To make a ARM connection with DBE, the user name is constant
            var userName = "EdgeArmUser";

            // User Password
            // To make a ARM connection with DBE, the password for the above user
            // TODO: Please replace this value with the appropriate value for your device.
            var password = "Password1";

            // Arm Subscription Id
            // After the device is registered with the portal. Arm on the device will be configured.
            // You can run Get-ArmSubscriptionId to get the local device arm subscription Id.
            // Or you can make a Cloud Managed Rest Call to retrive this value.
            var localArmSubscriptionId = "383843f2-12f2-4231-9bef-77598b1eb610";

            // Location
            // For DBE the location is fixed, dbelocal
            var location = "dbelocal";

            // Appliance machine name or host name
            // you can get this value from the DBE local use
            var hostName = "<ApplianceName>";

            // Local Arm Authority URL
            // This is the authority/authenticating website URL
            // You can also find this value from the local UI
            // Make sure that the name is resonvable
            var authenticationAuthorityUrl = $"https://login.dbe-{hostName.ToLower()}.microsoftdatabox.com/adfs/";

            // Arm Management endpoint
            // ARM uses this endpoint to make all ARM request.
            // After you have configured your device you can find this value in local UI
            // Make sure the name is resolvable
            var managementEndpointUrl = $"https://management.dbe-{hostName.ToLower()}.microsoftdatabox.com/";

            // Arm template json file path
            // You can use one of the json shared in the VM creation template JSON
            var templateJson = "<Template File Name>";

            // Arm Template Json Parameter file.
            // You can either pre-populate the parameter file with values or we can programatically fill in the values
            var templateParameters = "<>";


            // The example assumes that the VHD has been uploaded to the device.
            
            // template parameters.
            // These values are required to create VM.

            // resource group to create. All the related resources are created in this group
            var resourceGroupName = "testVM10";

            // Depoyment job name
            var deploymentName = "testVM_Deployment10";

            // Name of the VM
            string vmName = "VMT10";

            // VM Admin user name
            string adminUserName = "admin";

            // VM Admin User password
            string adminUserPassword = "P@ssword1";

            // Name of the image to use for creating VM
            string imageName = "";

            // Size of the VM
            string vmSize = "";

            // VM's NIC Name
            string nicName = "nic10";

            // VM's IP Config Name
            string ipConfigName = "ipconfignic10";

            // Virtual Network Name
            // On ASE device when you configure/enable compute on a given network. The device automatically crates a VNet
            // You can query the VNET and provide it here, or leave it as blank and the sample will query the device and populate these values
            string vnetName = "";
            string vnetResourceGroup = "";
            string subNetName = "";


            // construct a credentials object.
            // If you have changed the domain name of the device you below constructor to consruct the credentials object
            // var clientCreds = new AseServiceClientCredentials(userName, password, tenantId, managementEndpointUrl, authenticationAuthorityUrl);
            var clientCreds = new AseServiceClientCredentials(userName, password, tenantId, hostName);

            // Once you have created a credentials obejct you can now interact with any Azure SDK component and operate with the device
            
            // In this example, we will use the ARM templates to create a VM resource.

            // Create an instance of Resource Management Client.
            var resourceClient = new ResourceManagementClient(new Uri(clientCreds.AudienceResourceUrl), clientCreds)
            {
                SubscriptionId = localArmSubscriptionId
            };

            // if the template assums the resource group to be present, then create the resource group
            var resourceGroup = resourceClient.ResourceGroups.CreateOrUpdate(resourceGroupName, new ResourceGroup(location));

            // Lets get the template
            var deploymentTemplate = GetJsonFileContents(templateJson);

            // Lets get the template prameter
            var deploymentTemplateParameters = GetJsonFileContents(templateParameters);

            // If code defines the parameter lets replace the template parameter values with the one in template.
            deploymentTemplateParameters = InitializeDeploymentTemplateParameters(deploymentTemplateParameters,
                clientCreds,
                localArmSubscriptionId,
                resourceGroupName,
                vmName,
                adminUserName,
                adminUserPassword,
                imageName,
                vmSize,
                nicName,
                ipConfigName,
                vnetName,
                vnetResourceGroup,
                subNetName);

            DeploymentExtended deploymentResult = null;
            var deployment = new Deployment();

            deployment.Properties = new DeploymentProperties()
            {
                Mode = DeploymentMode.Incremental,
                Template = deploymentTemplate,
                Parameters = deploymentTemplateParameters["parameters"].ToObject<JObject>()
            };

            try
            {

                deploymentResult = resourceClient.Deployments.CreateOrUpdate(resourceGroupName, deploymentName, deployment);
                Utilities.Log($"Template deployment {deploymentName} provisioningState: {deploymentResult.Properties.ProvisioningState}");
            }
            catch (CloudException ex)
            {
                Utilities.Log($"Received template deployment exception. Ex: {ex}");
                if(ex.Body != null)
                {
                    Utilities.Log($"{ex.Body.Code} <=> {ex.Body.Message}");

                    if(ex.Body.Details != null)
                    {
                        foreach(var d in ex.Body.Details)
                        {
                            Utilities.Log($"\t{d.Code} <==>{d.Message}");
                        }
                    }
                }
                throw;
            }
        }

        /// <summary>
        /// Initialize VM deployment template parameter values
        /// </summary>
        /// <param name="deploymentTemplateParameters">JSon object with all the deployment template</param>
        /// <param name="clientCreds">Credential Object, used to query device</param>
        /// <param name="subscriptionId">local arm subscription id</param>
        /// <param name="resourceGroupName">resourceGroup Name</param>
        /// <param name="vmName">VM Name</param>
        /// <param name="adminUserName">Admin user name on the VM</param>
        /// <param name="adminUserPassword">Admin user password on the VM</param>
        /// <param name="imageName">Image name to use for creating VM</param>
        /// <param name="vmSize">VM Size</param>
        /// <param name="nicName">NIC Name</param>
        /// <param name="ipConfigName">IPConfig Name</param>
        /// <param name="vnetName">VNet Name</param>
        /// <param name="vnetResourceGroup">VNet Resource Group Name</param>
        /// <param name="subNetName">Sub Net Name</param>
        /// <returns></returns>
        private static JObject InitializeDeploymentTemplateParameters(JObject deploymentTemplateParameters, 
            AseServiceClientCredentials clientCreds, 
            string subscriptionId,
            string resourceGroupName,
            string vmName,
            string adminUserName,
            string adminUserPassword,
            string imageName,
            string vmSize,
            string nicName,
            string ipConfigName,
            string vnetName,
            string vnetResourceGroup,
            string subNetName)
        {
            // Connect to the device's Network Management Client
            var networkClient = new NetworkManagementClient(new Uri(clientCreds.AudienceResourceUrl), clientCreds)
            {
                SubscriptionId = subscriptionId
            };


            // Compute Management client. Will use to get Compute realted information
            var computeClient = new ComputeManagementClient(new Uri(clientCreds.AudienceResourceUrl), clientCreds)
            {
                SubscriptionId = subscriptionId
            };

            // Update resource group name
            //if(!string.IsNullOrEmpty(resourceGroupName))
            //{
            //    ((dynamic)deploymentTemplateParameters).parameters.rgName.value = resourceGroupName;
            //}

            // Update value for parameter VMName
            if (!string.IsNullOrEmpty(vmName))
            {
                ((dynamic)deploymentTemplateParameters).parameters.vmName.value = vmName;
            }

            // Update value for parameter adminUserName
            if (!string.IsNullOrEmpty(adminUserName))
            {
                ((dynamic)deploymentTemplateParameters).parameters.adminUsername.value = adminUserName;
            }

            // Update value  for parameter adminUserPassword
            if (!string.IsNullOrEmpty(adminUserPassword))
            {
                ((dynamic)deploymentTemplateParameters).parameters.Password.value = adminUserPassword;
            }

            // Update value for parameter imageName
            if (!string.IsNullOrEmpty(imageName))
            {
                ((dynamic)deploymentTemplateParameters).parameters.imageName.value = imageName;
            }
            else
            {
                // Query the device to get a list of image name
                var images = computeClient.Images.List();
                if(images.Any())
                {
                    // lets pick the fist image for the sample
                    var img = images.First();
                    ((dynamic)deploymentTemplateParameters).parameters.imageName.value = img.Name;
                    ((dynamic)deploymentTemplateParameters).parameters.imageRG.value = GetResourceGroup(img.Id);
                }
            }

            // Update value for parameter vmSize
            if (!string.IsNullOrEmpty(vmSize))
            {
                ((dynamic)deploymentTemplateParameters).parameters.vmSize.value = vmSize;
            }
            else
            {
                ((dynamic)deploymentTemplateParameters).parameters.vmSize.value = "Standard_D1_v2";
            }

            // Update value for parameter nicName
            if (!string.IsNullOrEmpty(nicName))
            {
                ((dynamic)deploymentTemplateParameters).parameters.nicName.value = nicName;
            }

            // Update value for parameter ipConfigName
            if (!string.IsNullOrEmpty(ipConfigName))
            {
                ((dynamic)deploymentTemplateParameters).parameters.IPConfigName.value = ipConfigName;
            }



            // Update value for parameter vnetName and related values
            if (string.IsNullOrEmpty(vnetName))
            {
                // Query all the virtual network
                var allNetworks = networkClient.VirtualNetworks.ListAll();

                foreach (var vn in allNetworks)
                {
                    Utilities.Log($"{vn.Name} {vn.ProvisioningState} {vn.Location}");
                }

                // Use the first network for the sample
                var vNet = allNetworks.FirstOrDefault();
                if(vNet == null)
                {
                    throw new Exception("VNet is not configured.");
                }

                // Update the parameter values
                ((dynamic)deploymentTemplateParameters).parameters.vnetName.value = vNet.Name;
                ((dynamic)deploymentTemplateParameters).parameters.vnetRG.value = GetResourceGroup(vNet.Id);
                ((dynamic)deploymentTemplateParameters).parameters.subnetName.value = vNet.Subnets.First().Name;

            }


            return deploymentTemplateParameters;
        }

        /// <summary>
        /// Function returns the resource group name.
        /// </summary>
        /// <param name="id">Resource ID</param>
        /// <returns></returns>
        private static string GetResourceGroup(string id)
        {
            var keyValue = id.Split(new char[] { '/' },StringSplitOptions.RemoveEmptyEntries);
            var count = 0;
            while(count < keyValue.Length)
            {
                if(keyValue[count].Equals("resourcegroups",StringComparison.OrdinalIgnoreCase))
                {
                    return keyValue[count + 1];
                }
                count = count + 2;
            }

            throw new Exception("Resource Group not found");
        }

        /// <summary>
        /// Read the test JSON file
        /// </summary>
        /// <param name="pathToJson">JSON file path</param>
        /// <returns></returns>
        private static JObject GetJsonFileContents(string pathToJson)
        {
            JObject templatefileContent;
            using (var file = File.OpenText(pathToJson))
            {
                using (var reader = new JsonTextReader(file))
                {
                    templatefileContent = (JObject)JToken.ReadFrom(reader);
                    return templatefileContent;
                }
            }
        }
    }
}
