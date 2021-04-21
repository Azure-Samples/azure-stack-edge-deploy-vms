
namespace CloudManagedVM
{
    class SampleDeployments
    {
        public static void DeploySubscriptionLevelTemplate(CloudLib client, string deviceName, string saaResourceGroup, string templateFilePath, string templateParamsFilePath, string location = "dbelocal")
        {
            client.DeployTemplateAtSubscriptionLevel(saaResourceGroup, deviceName, templateFilePath, templateParamsFilePath, location);
        }

        public static void DeployResourceGroupLevelTemplate(CloudLib client, string deviceName, string saaResourceGroup, string templateFilePath, string templateParamsFilePath, string linkedResourceGroup)
        {
            client.DeployTemplateAtResourceGroupLevel(saaResourceGroup, deviceName, templateFilePath, templateParamsFilePath, linkedResourceGroup);
        }
    }
}
