using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Reflection;

namespace cloudscript
{
    class SampleCreateImage
    {
        public static void CreateImage(CloudLib client, string deviceName, string saaResourceGroup, string imageName, string imageResourceGroup, string blobUri, string osType)
        {
            // check if image exists. If yes, skip creation
            var edgeSubId = client.GetEdgeSubscriptionId(deviceName, saaResourceGroup);
            try
            {
                var resource = client.GetEdgeResource("Microsoft.Compute/images", imageName, imageResourceGroup, saaResourceGroup, edgeSubId);
                Console.WriteLine($"Image {imageName} already exists under edge resource group {imageResourceGroup}");
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

            var template = InitializeImageTemplate(imageName, imageResourceGroup, blobUri, osType);
            client.DeployTemplate(saaResourceGroup, deviceName, template);
            Console.WriteLine($"Created image {imageName}  under edge resource group {imageResourceGroup}");
        }


        private static string InitializeImageTemplate(string imageName, string imageResourceGroup, string blobUri, string osType)
        {
            var outPutDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase);
            Console.WriteLine($"out directory: {outPutDirectory}");
            var imageTemplateFilePath = Path.Combine(outPutDirectory, @"Templates\imageTemplate.json");
            Console.WriteLine($"template path: {new Uri(imageTemplateFilePath).LocalPath}");
            dynamic imageTemplate = JObject.Parse(File.ReadAllText((new Uri(imageTemplateFilePath)).LocalPath));

            imageTemplate.properties.parameters.rgName.value = imageResourceGroup;

            var innerDeployment = imageTemplate.properties.template.resources[1];
            var randomSuffix = Guid.NewGuid().ToString().Split('-')[0];

            innerDeployment.name = $"imageDeployment-{imageName}-{randomSuffix}";
            innerDeployment.properties.parameters.sourceBlobUri.value = blobUri;
            innerDeployment.properties.parameters.targetStorageAccountName.value = $"sa-{imageName}-{randomSuffix}";
            innerDeployment.properties.parameters.targetContainerName.value = $"vhds-{imageName}-{randomSuffix}";
            innerDeployment.properties.parameters.ingestionJobName.value = $"ingestionJob-{imageName}-{randomSuffix}";
            innerDeployment.properties.parameters.imageName.value = imageName;
            innerDeployment.properties.parameters.osType.value = osType;

            return imageTemplate.ToString();
        }
    }
}
