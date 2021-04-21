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
    [string]$templateFilePath = ".\Templates\vmTemplate.json",

    [Parameter(Mandatory=$True)]
    [ValidateNotNullOrEmpty()]
    # Template Params file path
    [string]$templateParamsFilePath = ".\Templates\vmTemplate.Params.json",

    [Parameter(Mandatory=$True)]
    [ValidateNotNullOrEmpty()]
    # Cloud Managed VM dll and its dependencies path
    [string]$dllPath = ".\"
)


function InitializeVMTemplateParameters($templateFilePath, $templateParamsFilePath, $vmName, $vmResourceGroup, $imageName, $imageRG, $username, $password, $vmSize, $ipAddress, $vnetName, $vnetResourceGroup, $subnetName)
{
    Write-Host "Initializing the VM template $templateFilePath with parameters in file $templateParamsFilePath"

    # Update template with parameter values
    $template = Get-Content $templateFilePath | ConvertFrom-Json
    $templateParams = Get-Content $templateParamsFilePath | ConvertFrom-Json

    $template.properties.parameters.rgName.value = $templateParams.parameters.rgName.value
    $vmParams = $template.properties.template.resources[1].properties.parameters

    $vmParams.vmName.value = $templateParams.parameters.vmName.value
    $vmParams.adminUsername.value = $templateParams.parameters.adminUsername.value
    $vmParams.Password.value = $templateParams.parameters.Password.value
    $vmParams.imageName.value = $templateParams.parameters.imageName.value
    $vmParams.imageRG.value = $templateParams.parameters.imageRG.value
    $vmParams.vmSize.value = $templateParams.parameters.vmSize.value

    if ([String]::IsNullOrEmpty($templateParams.parameters.vnetName.value) -or
        [String]::IsNullOrEmpty($templateParams.parameters.vnetRG.value) -or
        [String]::IsNullOrEmpty($templateParams.parameters.subnetName.value))
    {
        $vnet = $client.GetDefaultVirtualNetwork($deviceName, $saasResourceGroup)
        $vmParams.vnetName.value = $vnet.Item1
        $vmParams.vnetRG.value = $vnet.Item2
        $vmParams.subnetName.value = $vnet.Item3
    } else
    {
        $vmParams.vnetName.value = $templateParams.parameters.vnetName.value
        $vmParams.vnetRG.value = $templateParams.parameters.vnetRG.value
        $vmParams.subnetName.value = $templateParams.parameters.subnetName.value
    }

    $suffix = (new-guid).Guid.Split('-')[0]

    $nicName = $templateParams.parameters.nicName.value
    $vmParams.nicName.value = If ([String]::IsNullOrEmpty($nicName)) {"nic" + $suffix} Else {$nicName}

    $ipconfigName = $templateParams.parameters.ipconfigName.value
    $vmParams.ipconfigName.value = If ([String]::IsNullOrEmpty($ipconfigName)) {"ipconfig" + $suffix} Else {$ipconfigName}

    $template.properties.template.resources[1].name = "vmDeployment" + $suffix

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

$template = InitializeVMTemplateParameters $templateFilePath $templateParamsFilePath $client $deviceName $saasResourceGroup
$client.DeployTemplate($saasResourceGroup, $deviceName, $template)