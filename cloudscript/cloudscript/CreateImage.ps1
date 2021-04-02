[CmdletBinding()]
Param(
    [Parameter(Mandatory=$True)]
    [ValidateNotNullOrEmpty()]
    # Image name to be created
    [string]$imageName,

    [Parameter(Mandatory=$True)]
    [ValidateNotNullOrEmpty()]
    # Image resource group for above image
    [string]$imageResourceGroup,

    [Parameter(Mandatory=$True)]
    [ValidateNotNullOrEmpty()]
    # Blob sas uri from which image to be created
    [string]$blobUri,

    [Parameter(Mandatory=$True)]
    [ValidateSet('Windows','Linux')]
    # os type for image: Linux/Windows
    [string]$osType,

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
    [string]$saasAccessKey,

    [Parameter(Mandatory=$True)]
    [ValidateNotNullOrEmpty()]
    # Tenant id
    [string]$tenantId,

    [Parameter(Mandatory=$True)]
    [ValidateNotNullOrEmpty()]
    # Client id
    [string]$clientId
)

function InitializeImageTemplateParameters($templateFilePath, $imageName, $imageResourceGroup, $blobUri, $osType)
{
    Write-Host "Initializing the image template $templateFilePath with parameters"

    # Update template with parameter values
    $template = Get-Content $templateFilePath | ConvertFrom-Json

    $template.properties.parameters.rgName.value = $imageResourceGroup
    $ingestionJobParams = $template.properties.template.resources[1].properties.parameters
    $ingestionJobParams.sourceBlobUri.value = $blobUri
    $ingestionJobParams.imageName.value = $imageName
    $ingestionJobParams.osType.value = $osType

    $suffix = (New-Guid).Guid.Split('-')[0]
    # Storage account name should not have capital letters
    $ingestionJobParams.targetStorageAccountName.value = "sa" + $suffix
    $ingestionJobParams.targetContainerName.value = "vmimages"
    $ingestionJobParams.ingestionJobName.value = "ingestionJob" + $suffix
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

$dllPath = ".\"
$templateFilePath = ".\Templates\imageTemplate.json"
LoadDlls $dllPath
$client = New-Object cloudscript.CloudLib($saasAccessKey, $tenantId, $clientId, $subscriptionId)

$template = InitializeImageTemplateParameters $templateFilePath $imageName $imageResourceGroup $blobUri $osType
$client.DeployTemplate($saasResourceGroup, $deviceName, $template)
