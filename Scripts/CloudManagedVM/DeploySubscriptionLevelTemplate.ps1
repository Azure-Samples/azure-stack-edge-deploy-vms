[CmdletBinding()]
Param(
    [Parameter(Mandatory=$True)]
    [ValidateNotNullOrEmpty()]
    # Resource group containing azure device
    [string]$saasResourceGroup,

    [Parameter(Mandatory=$True)]
    [ValidateNotNullOrEmpty()]
    # Azure ASE device name
    [string]$deviceName,

    [Parameter(Mandatory=$True)]
    [ValidateNotNullOrEmpty()]
    # Azure subscription id
    [string]$subscriptionId,

    [Parameter(Mandatory=$True)]
    [ValidateNotNullOrEmpty()]
    # Access key for azure subscription access
    # Please refer to https://docs.microsoft.com/en-us/azure/databox-online/azure-stack-edge-gpu-deploy-virtual-machine-cli-python on how to obtain
    # tenantId and clientId
    [string]$saasAccessKey,

    [Parameter(Mandatory=$True)]
    [ValidateNotNullOrEmpty()]
    # Tenant id
    [string]$tenantId,

    [Parameter(Mandatory=$True)]
    [ValidateNotNullOrEmpty()]
    # Client id
    [string]$clientId,

    [Parameter(Mandatory=$True)]
    [ValidateNotNullOrEmpty()]
    # Template file path
    [string]$templateFilePath,

    [Parameter(Mandatory=$True)]
    [ValidateNotNullOrEmpty()]
    # Template Params file path
    [string]$templateParamsFilePath,

    [Parameter(Mandatory=$False)]
    [ValidateNotNullOrEmpty()]
    # Cloud Managed VM dll and its dependencies path
    [string]$dllPath = ".\",

    [Parameter(Mandatory=$True)]
    [ValidateNotNullOrEmpty()]
    # Location
    [string]$location = "dbelocal"
)

function LoadDlls($dllPath)
{
    $dllFiles = ls $dllPath*.dll
    foreach ($f in $dllFiles)
    {
        [Reflection.Assembly]::LoadFile($f.FullName)
    }
}


LoadDlls $dllPath
$client = New-Object CloudManagedVM.CloudLib($saasAccessKey, $tenantId, $clientId, $subscriptionId)

$client.DeployTemplateAtSubscriptionLevel($saasResourceGroup, $deviceName, $templateFilePath, $templateParamsFilePath, $location)
