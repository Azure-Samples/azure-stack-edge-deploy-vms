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
    [string]$templateFilePath = ".\Templates\imageTemplate.json",

    [Parameter(Mandatory=$True)]
    [ValidateNotNullOrEmpty()]
    # Template Params file path
    [string]$templateParamsFilePath = ".\Templates\imageTemplate.Params.json",

    [Parameter(Mandatory=$True)]
    [ValidateNotNullOrEmpty()]
    # Cloud Managed VM dll and its dependencies path
    [string]$dllPath = ".\"
)

function InitializeImageTemplateParameters($templateFilePath, $templateParamsFilePath)
{
    Write-Host "Initializing the image template $templateFilePath with parameters"

    # Update template with parameter values
    $template = Get-Content $templateFilePath | ConvertFrom-Json
    $templateParams = Get=Content $templateParamsFilePath | ConvertFrom-Json

    $template.properties.parameters.rgName.value = $templateParams.parameters.rgName.value
    $ingestionJobParams = $template.properties.template.resources[1].properties.parameters
    $ingestionJobParams.sourceBlobUri.value = $templateParams.parameters.sourceBlobUri.value
    $ingestionJobParams.imageName.value = $templateParams.parameters.imageName.value
    $ingestionJobParams.osType.value = $templateParams.parameters.osType.value

    $suffix = (New-Guid).Guid.Split('-')[0]

    # Storage account name should not have capital letters
    $saName = $templateParams.parameters.targetStorageAccountName.value
    $ingestionJobParams.targetStorageAccountName.value = If ([String]::IsNullOrEmpty($saName)) {"sa" + $suffix} Else {$saName}
    
    $containerName = $templateParams.parameters.targetContainerName.value
    $ingestionJobParams.targetContainerName.value = If ([String]::IsNullOrEmpty($containerName)) {"vmimages" + $suffix} Else {$containerName}

    $ingestionJobName = $templateParams.parameters.ingestionJobName.value
    $ingestionJobParams.ingestionJobName.value = If ([String]::IsNullOrEmpty($ingestionJobName)) {"ingestionJob" + $suffix} Else {$ingestionJobName}

    $template.properties.template.resources[1].name = "imageDeployment" + $suffix
    
    $template = $template | ConvertTo-Json -depth 30 | % { [System.Text.RegularExpressions.Regex]::Unescape($_) }
    return $template
}

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

$template = InitializeImageTemplateParameters $templateFilePath $templateParamsFilePath
$client.DeployTemplate($saasResourceGroup, $deviceName, $template)
