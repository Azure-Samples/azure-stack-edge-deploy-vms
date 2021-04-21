using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace CloudManagedVM
{
    class SampleVMCreate
    {
        public static void CreateVMWithSubscriptionLevelTemplate(CloudLib client, string deviceName, string saaResourceGroup, string templateFilePath, string templateParamsFilePath)
        {
            var template = InitializeVMTemplate(templateFilePath, templateParamsFilePath, client, deviceName, saaResourceGroup);
            client.DeployTemplate(saaResourceGroup, deviceName, template);
        }

        public static void CreateVMWithResourceGroupLevelTemplate(CloudLib client, string deviceName, string saaResourceGroup, string templateFilePath, string templateParamsFilePath, string linkedResourceGroup)
        {
            client.DeployTemplateAtResourceGroupLevel(saaResourceGroup, deviceName, templateFilePath, templateParamsFilePath, linkedResourceGroup);
        }

        private static string InitializeVMTemplate(string templateFilePath, string templateParamsFilePath, CloudLib client, string deviceName, string saasResourceGroup)
        {
            dynamic vmTemplate = JObject.Parse(File.ReadAllText(templateFilePath));
            dynamic vmTemplateParams = JObject.Parse(File.ReadAllText(templateParamsFilePath));

            vmTemplate.properties.parameters.rgName.value = vmTemplateParams.parameters.rgName.value;

            var innerDeployment = vmTemplate.properties.template.resources[1];
            var randomSuffix = Guid.NewGuid().ToString().Split('-')[0];

            innerDeployment.name = $"vmDeployment-{randomSuffix}";
            innerDeployment.properties.parameters.vmName.value = vmTemplateParams.parameters.vmName.value;
            innerDeployment.properties.parameters.adminUsername.value = vmTemplateParams.parameters.adminUsername.value;
            innerDeployment.properties.parameters.Password.value = vmTemplateParams.parameters.Password.value;
            innerDeployment.properties.parameters.imageName.value = vmTemplateParams.parameters.imageName.value;
            innerDeployment.properties.parameters.imageRG.value = vmTemplateParams.parameters.imageRG.value;
            innerDeployment.properties.parameters.vmSize.value = vmTemplateParams.parameters.vmSize.value;

            if (String.IsNullOrEmpty(vmTemplateParams.parameters.vnetName.value) ||
                String.IsNullOrEmpty(vmTemplateParams.parameters.vnetRG.value) ||
                String.IsNullOrEmpty(vmTemplateParams.parameters.subnetName.value))
            {
                var (vnetName, vnetRG, subnetName) = client.GetDefaultVirtualNetwork(deviceName, saasResourceGroup);
                innerDeployment.properties.parameters.vnetName.value = vnetName;
                innerDeployment.properties.parameters.vnetRG.value = vnetRG;
                innerDeployment.properties.parameters.subnetName.value = subnetName;
            }
            else
            {
                innerDeployment.properties.parameters.vnetName.value = vmTemplateParams.parameters.vnetName.value;
                innerDeployment.properties.parameters.vnetRG.value = vmTemplateParams.parameters.vnetRG.value;
                innerDeployment.properties.parameters.subnetName.value = vmTemplateParams.parameters.subnetName.value;
            }

            string nicName = vmTemplateParams.parameters.nicName.value;
            innerDeployment.properties.parameters.nicName.value = String.IsNullOrEmpty(nicName) ? $"nic-{randomSuffix}" : nicName;

            string ipconfigName = vmTemplateParams.parameters.nicIPConfigName.value;
            innerDeployment.properties.parameters.IPConfigName.value = String.IsNullOrEmpty(ipconfigName) ? $"ipconfig-{randomSuffix}" : ipconfigName;
            
            innerDeployment.properties.parameters.privateIPAddress.value = vmTemplateParams.parameters.privateIPAddress.value;

            return vmTemplate.ToString();
        }
    }
}
