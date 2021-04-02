[CmdletBinding()]
Param(
    [Parameter(Mandatory=$True)]
    [ValidateNotNullOrEmpty()]
    # VM name to be created
    [string]$vmName,

    [Parameter(Mandatory=$True)]
    [ValidateNotNullOrEmpty()]
    # VM resource group for above VM
    [string]$vmResourceGroup,

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
    # username for VM to be created
    [string]$username,

    [Parameter(Mandatory=$True)]
    [ValidateNotNullOrEmpty()]
    # Password for abive username of the VM
    [string]$password,

    [Parameter(Mandatory=$False)]
    # VM size
    [string]$vmSize = "Standard_D1_v2",

    [Parameter(Mandatory=$False)]
    # Ip address of the VM. If empty, VM will get a DHCP ip
    [string]$ipAddress = "",

    [Parameter(Mandatory=$False)]
    # Virtual network name on ASE device
    [string]$vnetName = "",

    [Parameter(Mandatory=$False)]
    # Edge Resource group for virtual network on ASE device
    [string]$vnetResourceGroup = "",

    [Parameter(Mandatory=$False)]
    # subnet name for the above virtual network on ASE device
    [string]$subnetName = ""
)


function InitializeVMTemplateParameters($templateFilePath, $vmName, $vmResourceGroup, $imageName, $imageRG, $username, $password, $vmSize, $ipAddress, $vnetName, $vnetResourceGroup, $subnetName)
{
    Write-Host "Initializing the VM template $templateFilePath with parameters. Using image $imageName under resource group $imageRG to create it"

    # Update template with parameter values
    $template = Get-Content $templateFilePath | ConvertFrom-Json
    

    $template.properties.parameters.rgName.value = $vmResourceGroup
    $vmParams = $template.properties.template.resources[1].properties.parameters

    $vmParams.vmName.value = $vmName
    $vmParams.adminUsername.value = $username
    $vmParams.Password.value = $password
    $vmParams.imageName.value = $imageName
    $vmParams.imageRG.value = $imageRG
    $vmParams.vmSize.value = $vmSize
    $vmParams.vnetName.value = $vnetName
    $vmParams.vnetRG.value = $vnetResourceGroup
    $vmParams.subnetName.value = $subnetName

    $suffix = (new-guid).Guid.Split('-')[0]
    $vmParams.nicName.value = "nic" + $suffix
    $vmParams.IPConfigName.value = "ipconfig" + $suffix
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

$dllPath = ".\"
$templateFilePath = ".\Templates\vmTemplate.json"
LoadDlls $dllPath
$client = New-Object cloudscript.CloudLib($saasAccessKey, $tenantId, $clientId, $subscriptionId)

if ([string]::IsNullOrEmpty($vnetName))
{
    $vnet = $client.GetDefaultVirtualNetwork($deviceName, $saasResourceGroup)
}


$template = InitializeVMTemplateParameters $templateFilePath $vmName $vmResourceGroup $imageName $imageResourceGroup `
                                           $username $password $vmSize $ipAddress $vnet.Item1 $vnet.Item2 $vnet.Item3
$client.DeployTemplate($saasResourceGroup, $deviceName, $template)