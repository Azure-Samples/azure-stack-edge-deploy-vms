Param(
    [Parameter(Mandatory = $true)]
    [ValidateSet(
    'eastus',
    'westus2',
    'southcentralus',
    'centraluseuap',
    'eastus2euap',
    'westus',
    "usdodcentral",
    "usdodeast",
    "usgovarizona",
    "usgoviowa",
    "usgovtexas",
    "usgovvirginia")]
    [string]$Location,

    [Parameter(Mandatory = $true)]
    [string]$AzureDeploymentName,

    [Parameter(Mandatory = $true)]
    [string]$AzureAppRuleFilePath,

    [Parameter(Mandatory = $true)]
    [string]$AzureIPRangesFilePath,

    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory = $true)]
    [string]$NetworkRuleCollectionName,

    [Parameter(Mandatory = $true)]
    [string]$AppRuleCollectionName,

    [Parameter(Mandatory = $true)]
    [string]$Priority,

    [Parameter(Mandatory = $false)]
    [switch]$S2S = $false
)

function Check-FilePath($filePath)
{
    if (-not (Test-Path -Path $filePath))
    {
        LogError "error: File not found: $filePath"
        exit 1
    }
}

function Update-ParametersLocation($location)
{
    $paramtersFileName = GetParametersFileName 
    Check-FilePath $paramtersFileName

    $json = Get-Content $paramtersFileName | ConvertFrom-Json
    $json.parameters.location.value = $location
    ConvertTo-Json -InputObject $json | Out-File $paramtersFileName
}

function Azurevpndeploymentstages()
{
    <#template deployment#>

    $templateFileName = GetTemplateFileName
    $paramtersFileName = GetParametersFileName 

    $deploymentInfo=Azurevpndeployment $AzureDeploymentName $ResourceGroupName $paramtersFileName $templateFileName


    Log "vpn deployment: $AzureDeploymentName started and status: $($deploymentInfo.State)"


    if($deploymentInfo.State -eq "Running")
    {
        $deploymentStatus = WaitForAzurevpndeployment $AzureDeploymentName $ResourceGroupName

        if($deploymentStatus.ProvisioningState -ne "Succeeded")
        {
            Log "Deployment error: $deploymentInfo"
            $errorDetails=Get-AzResourceGroupDeploymentOperation -ResourceGroupName $ResourceGroupName -DeploymentName $AzureDeploymentName
            LogError "error: $errorDetails"
            return $deploymentStatus
        }
        Log "output:$($deploymentStatus.outputs.firewall.value)"
    }
    else
    {
        Log "Deployment error: $deploymentInfo"
        $errorDetails=Get-AzResourceGroupDeploymentOperation -ResourceGroupName $ResourceGroupName -DeploymentName $AzureDeploymentName
        LogError "error: $errorDetails"
        return $deploymentInfo
    }

}

function AzurecustomizeRouteTableAndFirewallRules()
{
    $paramtersFileName = GetParametersFileName 

    $parameterTags = Get-Content $paramtersFileName | ConvertFrom-Json

    $firewallPublicIpName = $parameterTags.parameters.publicIPAddresses_firewall_public_ip_name.value
    $firewallName = $parameterTags.parameters.azureFirewalls_firewall_name.value
    $routeTableName = $parameterTags.parameters.routeTables_routetable_name.value
    $iotTNetworkAddressSpace = $parameterTags.parameters.DbeIOTNetworkAddressSpace.value
    $customerNetworkAddressSpace = $parameterTags.parameters.CustomerNetworkAddressSpace.value

    $virtualNetworkGatewayRoutesForRouteTable = @()
    $virtualNetworkGatewayRoutesForRouteTable += $iotTNetworkAddressSpace
    $virtualNetworkGatewayRoutesForRouteTable += $customerNetworkAddressSpace

    Log "firewallName: $firewallName, firewallPublicIpName: $firewallPublicIpName, ResourceGroupName: $ResourceGroupName"


    $firewallIPv4 = GetFirewallPrivateIp $firewallPublicIpName $firewallName $ResourceGroupName

    #Add app firewall rules
    ApplyFirewallAppRule $AzureAppRuleFilePath $ResourceGroupName $firewallName $AppRuleCollectionName $Priority

    #Add network firewall rules
    ApplyFirewallNetworkRule $serviceTagAndRegionList $AzureIPRangesFilePath $ResourceGroupName $firewallName $NetworkRuleCollectionName $Priority

    Log "Route table name: $routeTableName, firewallPrivateIPv4:$firewallIPv4"

    $routeNamePrefix = "route_"
    #Add routes to route table
    ApplyRoutesForRouteTable $serviceTagAndRegionList $AzureIPRangesFilePath $ResourceGroupName $routeTableName $firewallIPv4 $routeNamePrefix $virtualNetworkGatewayRoutesForRouteTable
}

function GetParametersFileName()
{
    # Additional parameters are reported as error by Azure deployment cmdlet if they are not referred by the template. Hence, using different param file as well for P2S.
    $paramtersFileName = "$PSScriptRoot\parameters-p2s.json" 

    if ($S2S)
    {
        $paramtersFileName = "$PSScriptRoot\parameters.json"
    }

    return $paramtersFileName
}

function GetTemplateFileName()
{
    # There are some differences between S2S template and P2S template. TODO: Check the possibilty of sharing common template stuff.
    $templateFileName = "$PSScriptRoot\template-p2s.json"

    if ($S2S)
    {
        $templateFileName = "$PSScriptRoot\template.json"
    }

    return $templateFileName
}

Check-FilePath $AzureAppRuleFilePath
Check-FilePath $AzureIPRangesFilePath

Update-ParametersLocation $Location

$serviceTagAndRegionList = @()
$serviceTagAndRegionList += "AzureCloud."+$Location
$serviceTagAndRegionList += "AzureActiveDirectory"
$serviceTagAndRegionList += "AzureActiveDirectoryDomainServices"

Import-Module $PSScriptRoot\AzVpnDeploymentHelper.psm1 -Force

Azurevpndeploymentstages  $AzureDeploymentName

AzurecustomizeRouteTableAndFirewallRules 
exit
