using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace CloudManagedVM
{
    class SampleCreateImage
    {
        public static void CreateImage(CloudLib client, string deviceName, string saaResourceGroup, string templateFilePath, string templateParamsFilePath)
        {
            var template = InitializeImageTemplate(templateFilePath, templateParamsFilePath);
            client.DeployTemplate(saaResourceGroup, deviceName, template);
        }


        private static string InitializeImageTemplate(string templateFilePath, string templateParamsFilePath)
        {
            dynamic imageTemplate = JObject.Parse(File.ReadAllText(templateFilePath));
            dynamic imageTemplateParams = JObject.Parse(File.ReadAllText(templateParamsFilePath));

            imageTemplate.properties.parameters.rgName.value = imageTemplateParams.parameters.rgName.value;

            var innerDeployment = imageTemplate.properties.template.resources[1];
            var randomSuffix = Guid.NewGuid().ToString().Split('-')[0];

            innerDeployment.name = $"imageDeployment-{randomSuffix}";
            innerDeployment.properties.parameters.sourceBlobUri.value = imageTemplateParams.parameters.sourceBlobUri.value;

            string saName = imageTemplateParams.parameters.targetStorageAccountName.value;
            innerDeployment.properties.parameters.targetStorageAccountName.value = String.IsNullOrEmpty(saName) ? $"sa-{randomSuffix}" : saName;

            string containerName = imageTemplateParams.parameters.targetContainerName.value;
            innerDeployment.properties.parameters.targetContainerName.value = String.IsNullOrEmpty(containerName) ? $"vmimages-{randomSuffix}" : containerName;

            string ingestionJobName = imageTemplateParams.parameters.ingestionJobName.value;
            innerDeployment.properties.parameters.ingestionJobName.value = String.IsNullOrEmpty(ingestionJobName) ? $"ingestionJob-{randomSuffix}" : ingestionJobName;

            innerDeployment.properties.parameters.imageName.value = imageTemplateParams.parameters.imageName.value;
            innerDeployment.properties.parameters.osType.value = imageTemplateParams.parameters.osType.value;

            return imageTemplate.ToString();
        }
    }
}
