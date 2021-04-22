
namespace CloudManagedVM
{
    class SampleDeployments
    {
        public static void DeploySubscriptionLevelTemplate(string accessToken, string subscriptionId, string deviceName, string saaResourceGroup, string templateFilePath, string templateParamsFilePath, string location = "dbelocal")
        {
            var client = new CloudLib(accessToken, subscriptionId);
            client.DeployTemplateAtSubscriptionLevel(saaResourceGroup, deviceName, templateFilePath, templateParamsFilePath, location);
        }

        public static void DeployResourceGroupLevelTemplate(string accessToken, string subscriptionId, string deviceName, string saaResourceGroup, string templateFilePath, string templateParamsFilePath, string linkedResourceGroup)
        {
            var client = new CloudLib(accessToken, subscriptionId);
            client.DeployTemplateAtResourceGroupLevel(saaResourceGroup, deviceName, templateFilePath, templateParamsFilePath, linkedResourceGroup);
        }
    }
}
