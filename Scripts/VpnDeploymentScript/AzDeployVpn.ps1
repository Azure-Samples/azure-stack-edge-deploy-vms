Param(
    [Parameter(Mandatory = $true)]
    [ValidateSet(
    'eastus',
    'westus2',
    'southcentralus',
    'centraluseuap')]
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
    [string]$Priority
)


function Azurevpndeploymentstages()
{
    <#template deployment#>
    $deploymentInfo=Azurevpndeployment $AzureDeploymentName $ResourceGroupName $PSScriptRoot\parameters.json $PSScriptRoot\template.json


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
    $parameterTags = Get-Content $PSScriptRoot\parameters.json | ConvertFrom-Json

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

$serviceTagAndRegionList = @()
$serviceTagAndRegionList += "AzureCloud."+$Location
$serviceTagAndRegionList += "AzureActiveDirectory"
$serviceTagAndRegionList += "AzureActiveDirectoryDomainServices"

Import-Module $PSScriptRoot\AzVpnDeploymentHelper.psm1 -Force

Azurevpndeploymentstages  $AzureDeploymentName

AzurecustomizeRouteTableAndFirewallRules 
exit

