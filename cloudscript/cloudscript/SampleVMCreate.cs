using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Reflection;

namespace cloudscript
{
    class SampleVMCreate
    {
        public static void CreateVM(CloudLib client, string deviceName, string saaResourceGroup, string vmName, string vmResourceGroup, string imageName, string imageResourceGroup, string username, string password, string vmSize, string ipAddress, string vnetName, string vnetResourceGroup, string subnetName)
        {
            // check if VM exists. If yes, skip creation
            var edgeSubId = client.GetEdgeSubscriptionId(deviceName, saaResourceGroup);
            try
            {
                var resource = client.GetEdgeResource("Microsoft.Compute/virtualMachines", vmName, vmResourceGroup, saaResourceGroup, edgeSubId);
                Console.WriteLine($"VM {vmName} already exists under edge resource group {vmResourceGroup}");
                return;
            }
            catch (Exception exc)
            {
                Console.WriteLine($"Got exception of type {exc.GetType()} and error messsage {exc.Message}");
                dynamic error = JObject.Parse(exc.Message);
                string errorCode = Convert.ToString(error.error.code);
                if (!errorCode.Equals("NotFound", StringComparison.OrdinalIgnoreCase))
                    throw exc;
            }

            var template = InitializeVMTemplate(vmName, vmResourceGroup, imageName, imageResourceGroup, username, password, vmSize, ipAddress,
                                                vnetName, vnetResourceGroup, subnetName);
            client.DeployTemplate(saaResourceGroup, deviceName, template);
            Console.WriteLine($"Created VM {vmName}  under edge resource group {vmResourceGroup}");
        }

        private static string InitializeVMTemplate(string vmName, string vmResourceGroup, string imageName, string imageResourceGroup, string username, string password, string vmSize, string ipAddress, string vnetName, string vnetResourceGroup, string subnetName)
        {
            var outPutDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase);
            Console.WriteLine($"out directory: {outPutDirectory}");
            var vmTemplateFilePath = Path.Combine(outPutDirectory, @"Templates\vmTemplate.json");
            Console.WriteLine($"template path: {new Uri(vmTemplateFilePath).LocalPath}");
            dynamic vmTemplate = JObject.Parse(File.ReadAllText((new Uri(vmTemplateFilePath)).LocalPath));

            vmTemplate.properties.parameters.rgName.value = vmResourceGroup;

            var innerDeployment = vmTemplate.properties.template.resources[1];
            var randomSuffix = Guid.NewGuid().ToString().Split('-')[0];

            innerDeployment.name = $"vmDeployment-{vmName}-{randomSuffix}";
            innerDeployment.properties.parameters.vmName.value = vmName;
            innerDeployment.properties.parameters.adminUsername.value = username;
            innerDeployment.properties.parameters.Password.value = password;
            innerDeployment.properties.parameters.imageName.value = imageName;
            innerDeployment.properties.parameters.imageRG.value = imageResourceGroup;
            innerDeployment.properties.parameters.vmSize.value = vmSize;
            innerDeployment.properties.parameters.vnetName.value = vnetName;
            innerDeployment.properties.parameters.vnetRG.value = vnetResourceGroup;
            innerDeployment.properties.parameters.subnetName.value = subnetName;
            innerDeployment.properties.parameters.nicName.value = $"nic-{vmName}-{randomSuffix}";
            innerDeployment.properties.parameters.IPConfigName.value = $"ipconfig-{vmName}-{randomSuffix}";
            innerDeployment.properties.parameters.privateIPAddress.value = ipAddress;

            return vmTemplate.ToString();
        }
    }
}
