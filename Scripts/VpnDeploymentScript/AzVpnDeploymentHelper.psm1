$script:logfile = "vpndeployment.log"

function _log($msg, $lvl) {    
    $output = "[$lvl] $msg" | timestamp
    $output | Out-File -Append $script:logfile
}

function Log($msg) {
    Write-Host($msg)
    _log $msg "INF"
}

function LogOnly($msg) {
    _log $msg "INF"
}

function LogGreen($msg) {
    Write-Host($msg) -ForegroundColor Green
    _log $msg "INF"
}

function LogError($msg) {
    Write-Host($msg) -ForegroundColor Red
    _log $msg "ERROR"
}

function LogWarning($msg) {
    Write-Host($msg) -ForegroundColor Yellow
    _log $msg "WARNING"
}

filter timestamp {
    "$(get-date -format g): $_"
}

# TODO: Handle this list better as Gateway Manager has subset of IPs in this range.
$exclusionList = @(
    "13.92.0.0/16", # East US Gateway Manager
    "52.191.128.0/18", # West US2 Gateway Manager
    "13.84.0.0/15", # South Central US Gateway Manager
    "20.39.0.0/19","52.138.64.0/20","52.138.88.0/21" # East US 2 EUAP Gateway Manager
)

function AddFirewallAppRule()
{
    [CmdletBinding()]
    Param
    (
        [Parameter(Mandatory=$true)]
        [String]
        $appRuleName,
        
        [Parameter(Mandatory=$true)]
        [String[]]
        $protocol,

        [Parameter(Mandatory=$true)]
        [String[]]
        $targetFqdn
    )
   
    $protocol = "http:80","https:443"
    $firewallAppRule = New-AzFirewallApplicationRule -Name $appRuleName  -Protocol $protocol -SourceAddress * -TargetFqdn $targetFqdn 
    return $firewallAppRule
}

function AddFirewallAppRuleCollection()
{
    [CmdletBinding()]
    Param
    (
        [Parameter(Mandatory=$true)]
        [String]
        $appRuleCollectionName,
        
        [Parameter(Mandatory=$true)]
        [String]
        $priority,

        [Parameter(Mandatory=$true)]
        [Microsoft.Azure.Commands.Network.Models.PSAzureFirewallApplicationRule]
        $firewallappRule
    )

    $firewallAppRuleCollection = New-AzFirewallApplicationRuleCollection -Name $appRuleCollectionName -Priority $priority  -Rule $firewallappRule -ActionType "Allow"
    return $firewallAppRuleCollection
}

function AddFirewallNetworkRule()
{
    [CmdletBinding()]
    Param
    (
        [Parameter(Mandatory=$true)]
        [String]
        $networkRuleName,
        
        [Parameter(Mandatory=$true)]
        [String[]]
        $protocol,

        [Parameter(Mandatory=$true)]
        [String[]]
        $destinationAddress
    )

    $rule = New-AzFirewallNetworkRule -Name $networkRuleName  -Protocol $protocol -SourceAddress * -DestinationAddress $destinationAddress -DestinationPort *
    return $rule
}

function AddFirewallNetworkRuleCollection()
{
    [CmdletBinding()]
    Param
    (
        [Parameter(Mandatory=$true)]
        [String]
        $networkRuleCollectionName,
        
        [Parameter(Mandatory=$true)]
        [String]
        $priority,

        [Parameter(Mandatory=$true)]
        [Microsoft.Azure.Commands.Network.Models.PSAzureFirewallNetworkRule]
        $firewallNetworkRule
    )
    $firewallNetworkRuleCollection = New-AzFirewallNetworkRuleCollection -Name $networkRuleCollectionName -Priority $priority  -Rule $firewallNetworkRule -ActionType "Allow"
    return $firewallNetworkRuleCollection
}


function SetFirewallAppRule()
{
    [CmdletBinding()]
    Param
    (
        [Parameter(Mandatory=$true)]
        [Microsoft.Azure.Commands.Network.Models.PSAzureFirewall]
        $azFirewall,
        
        [Parameter(Mandatory=$true)]
        [Microsoft.Azure.Commands.Network.Models.PSAzureFirewallApplicationRuleCollection]
        $appRuleCollection
    )

    Log "updating az firewall take few mins..."
    $azFirewall.ApplicationRuleCollections = $appRuleCollection
    $azFirewall | Set-AzFirewall
}

function SetFirewallNetworkRule()
{
    [CmdletBinding()]
    Param
    (
        [Parameter(Mandatory=$true)]
        [Microsoft.Azure.Commands.Network.Models.PSAzureFirewall]
        $azFirewall,
        
        [Parameter(Mandatory=$true)]
        [Microsoft.Azure.Commands.Network.Models.PSAzureFirewallNetworkRuleCollection]
        $networkRuleCollection
    )

    Log "updating az firewall take few mins..."
    $azFirewall.NetworkRuleCollections = $networkRuleCollection
    $azFirewall | Set-AzFirewall
}

function ApplyFirewallAppRule()
{

    [CmdletBinding()]
    Param
    (

        [Parameter(Mandatory = $true)]
        [String]$AzureAppRuleFilePath,

        [Parameter(Mandatory = $true)]
        [String]$ResourceGroupName,

        [Parameter(Mandatory = $true)]
        [String]$FirewallName,

        [Parameter(Mandatory = $true)]
        [String]$AppRuleCollectionName,

        [Parameter(Mandatory = $true)]
        [String]$Priority
    )

    $appRuleCollectionTags = Get-Content $AzureAppRuleFilePath | ConvertFrom-Json

    $existingFirewallAppRuleHash = @{}
    $FirewallAppRuleCollection = $null

    $firewall = Get-AzFirewall -Name $FirewallName -ResourceGroupName $ResourceGroupName

    if($firewall -eq $null)
    {
        LogError "Get-AzFirewall -Name $FirewallName -ResourceGroupName $ResourceGroupName"
        return
    }

    if ($firewall.ApplicationRuleCollections -ne $null)
    {
        foreach ($appRuleCollection in $firewall.ApplicationRuleCollections)
        {
            if($appRuleCollection.Name -eq $AppRuleCollectionName)
            {
                $FirewallAppRuleCollection = $appRuleCollection
                break
            }
        }
    }

    if($FirewallAppRuleCollection -ne $null)
    {
        foreach ($firewallAppRule in $FirewallAppRuleCollection.Rules)
        {
            $existingFirewallAppRuleHash[$firewallAppRule.Name] = $true
        }
    }

    $routeId = 0
    $existingRouteId = 0

    $count = $appRuleCollectionTags.Rules.Count
    Log "Total Address Prefixes present: $count on $($appRuleCollectionTags.Name)"
    $protocols = "http:80","https:443"

    foreach ($rule in $appRuleCollectionTags.Rules)
    {
        if ($existingFirewallAppRuleHash.ContainsKey($rule.Name))
        {
            LogOnly "Firewall App Rule $($rule.Name) for $AppRuleCollectionName already present"
            $existingRouteId++
        }
        else
        {
            LogOnly "Adding firewall app rule $($rule.Name) for $AppRuleCollectionName"
            Write-Host -NoNewLine "..."
            <#$firewallAppRule = AddFirewallAppRule $rule.Name $rule.Protocols $rule.TargetFqdns#>
            $firewallAppRule = AddFirewallAppRule $rule.Name $protocols $rule.TargetFqdns
                
            if($FirewallAppRuleCollection -eq $null)
            {
                $FirewallAppRuleCollection = AddFirewallAppRuleCollection $AppRuleCollectionName $Priority $firewallAppRule
            }
            else
            {
                $FirewallAppRuleCollection.Rules += $firewallAppRule
            }

            $existingFirewallAppRuleHash[$rule.Name] = $true
        }

        $routeId++
    }

    Log "Total FirewallNetworkRules:$routeId, Existing FirewallNetworkRules: $existingRouteId, New FirewallNetworkRules Added: $($routeId-$existingRouteId)"
    SetFirewallAppRule $firewall $FirewallAppRuleCollection     
    LogGreen "Total FirewallNetworkRules:$routeId, Existing FirewallNetworkRules: $existingRouteId, New FirewallNetworkRules Added: $($routeId-$existingRouteId)"
}

function ApplyFirewallNetworkRule()
{
    [CmdletBinding()]
    Param
    (
        [Parameter(Mandatory = $true)]
        [string[]]$ServiceTagAndRegionList,

        [Parameter(Mandatory = $true)]
        [string]$AzureIPRangesFilePath,

        [Parameter(Mandatory = $true)]
        [String]$ResourceGroupName,

        [Parameter(Mandatory = $true)]
        [String]$FirewallName,

        [Parameter(Mandatory = $true)]
        [String]$NetworkRuleCollectionName,

        [Parameter(Mandatory = $true)]
        [String]$Priority
    )

    [hashtable]$serviceTagAndRegionHash = @{}

    foreach ($serviceTagAndRegion in $ServiceTagAndRegionList)
    {
        $serviceTagAndRegionHash[$serviceTagAndRegion.ToUpper()] = $true
    }

    $serviceTags = Get-Content $AzureIPRangesFilePath | ConvertFrom-Json
    $filteredServiceTags = @()

    foreach ($serviceTag in $serviceTags.Values)
    {
        if ($serviceTagAndRegionHash.ContainsKey($serviceTag.Name.ToUpper()))
        {
            $filteredServiceTags += $serviceTag
        }
    }


    $exitingFirewallRuleHash = @{}
    $FirewallNetworkRuleCollection = $null

    $firewall = Get-AzFirewall -Name $FirewallName -ResourceGroupName $ResourceGroupName

    if($firewall -eq $null)
    {
        LogError "Get-AzFirewall -Name $FirewallName -ResourceGroupName $ResourceGroupName"
        return
    }

    if ($firewall.NetworkRuleCollections -ne $null)
    {
        foreach ($networkRuleCollection in $firewall.NetworkRuleCollections)
        {
            if($networkRuleCollection.Name -eq $NetworkRuleCollectionName)
            {
                $FirewallNetworkRuleCollection = $networkRuleCollection
                break
            }
        }
    }

    if($FirewallNetworkRuleCollection -ne $null)
    {
        foreach ($firewallRule in $FirewallNetworkRuleCollection.Rules)
        {
            $exitingFirewallRuleHash[$firewallRule.Name] = $true
        }
    }

    $routeId = 0
    $existingRouteId = 0

    foreach ($serviceTag in $filteredServiceTags)
    {
        $count = $serviceTag.Properties.AddressPrefixes.Count

        Log "Total Address Prefixes present: $count on $($serviceTag.Name)"

        foreach ($addrPrefix in $serviceTag.Properties.AddressPrefixes)
        {
            if ($exclusionList -match $addrPrefix)
            {
                Log "Excluding Firewall Network Rule: $addrPrefix for $($serviceTag.Name)"
                continue
            }

            $isValid = IsValidIPv4CidrFormat $addrPrefix

            if($isValid -eq $false)
            {
                # Exclude ipv6 routes
                Log "Excluding invalid ip or ipv6 routes: $addrPrefix"
                continue
            }

            $networkRuleName = $addrPrefix + $NetworkRuleCollectionName
            if ($exitingFirewallRuleHash.ContainsKey($networkRuleName))
            {
                Log "Firewall Network Rule $addrPrefix for $($serviceTag.Name) already present"
                $existingRouteId++
            }
            else
            {
                Log "Adding firewall network rule $addrPrefix for $($serviceTag.Name)"
            
                $firewallRule = AddFirewallNetworkRule $networkRuleName "Any" $addrPrefix

                
                if($FirewallNetworkRuleCollection -eq $null)
                {
                    $FirewallNetworkRuleCollection = AddFirewallNetworkRuleCollection $NetworkRuleCollectionName $Priority $firewallRule
                }
                else
                {
                    $FirewallNetworkRuleCollection.Rules += $firewallRule
                }


                $exitingFirewallRuleHash[$networkRuleName] = $true
            }

            $routeId++
        }
    }
    Log "Total FirewallNetworkRules:$routeId, Existing FirewallNetworkRules: $existingRouteId, New FirewallNetworkRules Added: $($routeId-$existingRouteId)"
    SetFirewallNetworkRule $firewall $FirewallNetworkRuleCollection
    LogGreen "Total FirewallNetworkRules:$routeId, Existing FirewallNetworkRules: $existingRouteId, New FirewallNetworkRules Added: $($routeId-$existingRouteId)"
}


function AddRouteTableEntry()
{
    [CmdletBinding()]
    Param
    (
        [Parameter(Mandatory=$true)]
        [Microsoft.Azure.Commands.Network.Models.PSRouteTable]
        $routeTable,
        
        [Parameter(Mandatory=$true)]
        [String]
        $routeName,

        [Parameter(Mandatory=$true)]
        [String]
        $addrPrefix,

        [Parameter(Mandatory=$true)]
        [String]
        $nextHopIp
    )

    Add-AzRouteConfig -RouteTable $routeTable -Name $routeName -AddressPrefix $addrPrefix -NextHopType VirtualAppliance -NextHopIpAddress $nextHopIp | Set-AzRouteTable | Out-Null
}

function AddRouteTableEntryForVirtualNetworkGatewayType()
{
    [CmdletBinding()]
    Param
    (
        [Parameter(Mandatory=$true)]
        [Microsoft.Azure.Commands.Network.Models.PSRouteTable]
        $routeTable,
        
        [Parameter(Mandatory=$true)]
        [String]
        $routeName,

        [Parameter(Mandatory=$true)]
        [String]
        $addrPrefix
    )

    Add-AzRouteConfig -RouteTable $routeTable -Name $routeName -AddressPrefix $addrPrefix -NextHopType VirtualNetworkGateway | Set-AzRouteTable | Out-Null
}

function IsValidIPv4CidrFormat()
{
    [CmdletBinding()]
    Param
    (       
        [Parameter(Mandatory=$true)]
        [String]
        $cidr
    )

    $cidrParts = $cidr.Split("/");

    Log $cidrParts[0]
    $a=[ipaddress]$cidrParts[0]
    if($a.AddressFamily -eq "InterNetwork")
    {
        return $true
    }
    return $false;
}

function ApplyRoutesForRouteTable()
{
    [CmdletBinding()]
    Param
    (
        [Parameter(Mandatory = $true)]
        [string[]]$ServiceTagAndRegionList,

        [Parameter(Mandatory = $true)]
        [string]$AzureIPRangesFilePath,

        [Parameter(Mandatory = $true)]
        [String]$ResourceGroupName,

        [Parameter(Mandatory = $true)]
        [String]$RouteTableName,

        [Parameter(Mandatory = $true)]
        [String]$FirewallIPv4,

        [Parameter(Mandatory = $true)]
        [String]$RouteNamePrefix,

        [Parameter(Mandatory = $false)]
        [String[]]$vngRoutes = $null
    )

    [hashtable]$serviceTagAndRegionHash = @{}

    foreach ($serviceTagAndRegion in $ServiceTagAndRegionList)
    {
        $serviceTagAndRegionHash[$serviceTagAndRegion.ToUpper()] = $true
    }

    $serviceTags = Get-Content $AzureIPRangesFilePath | ConvertFrom-Json
    $filteredServiceTags = @()

    foreach ($serviceTag in $serviceTags.Values)
    {
        if ($serviceTagAndRegionHash.ContainsKey($serviceTag.Name.ToUpper()))
        {
            $filteredServiceTags += $serviceTag
        }
    }

    $routeTable = Get-AzRouteTable -Name $RouteTableName -ResourceGroupName $ResourceGroupName
    
    if($routeTable -eq $null)
    {
        LogError "Get-AzRouteTable -Name $RouteTableName -ResourceGroupName $ResourceGroupName"
        return
    }

    $exitingRoutesHash = @{}
    if ($routeTable.Routes -ne $null)
    {
        foreach ($route in $routeTable.Routes)
        {
            $routeKey = $route.AddressPrefix + "_" + $route.NextHopType + "_" + $route.NextHopIpAddress
            $exitingRoutesHash[$routeKey] = $true
        }
    }

    $routeId = 1
    $existingRouteId = 1
    foreach ($serviceTag in $filteredServiceTags)
    {
        $count = $serviceTag.Properties.AddressPrefixes.Count

        foreach ($addrPrefix in $serviceTag.Properties.AddressPrefixes)
        {
            if ($exclusionList -match $addrPrefix)
            {
                Log "Excluding routes: $addrPrefix"
                continue
            }

            $isValid = IsValidIPv4CidrFormat $addrPrefix

            if($isValid -eq $false)
            {
                # Exclude ipv6 routes
                Log "Excluding invalid ip or ipv6 routes: $addrPrefix"
                continue
            }

            $routeKey = $addrPrefix + "_VirtualAppliance_" + $FirewallIPv4
            if ($exitingRoutesHash.ContainsKey($routeKey))
            {
                Log "Route $addrPrefix for $($serviceTag.Name) already present"
                $existingRouteId++
            }
            else
            {
                Log "Adding route $addrPrefix for $($serviceTag.Name)"
            
                AddRouteTableEntry $routeTable ($RouteNamePrefix + $routeId) $addrPrefix $FirewallIPv4

                $exitingRoutesHash[$routeKey] = $true
            }

            $routeId++
        }
    }
    LogGreen "Total Routes:$routeId, Existing Routes: $existingRouteId, New Routes Added: $($routeId-$existingRouteId) "
    
    Log "Additional routes getting added"    
    AddRouteTableEntry $routeTable "mcreus0.cdn.mscr.io" "204.79.197.219/32" $FirewallIPv4
    AddRouteTableEntry $routeTable "www.office.com" "13.107.7.190/32" $FirewallIPv4

    if ($vngRoutes -ne $null)
    {
        $additionalRoute = 1
        foreach($vngRoute in $vngRoutes)
        {
            AddRouteTableEntryForVirtualNetworkGatewayType $routeTable ("vngRoute" + $additionalRoute) $vngRoute 
            $additionalRoute++
        }
    }
}

function Azurevpndeployment()
{
    [CmdletBinding()]
    Param
    (
        [Parameter(Mandatory=$true)]
        [String]
        $deploymentName,
        [Parameter(Mandatory=$true)]
        [String]
        $resourceGroupName,
        [Parameter(Mandatory=$true)]
        [String]
        $templateParamFilePath,
        [Parameter(Mandatory=$true)]
        [String]
        $templateFilePath
       
    )

    Log "validating vpn deployment parameters"
    $testOutput=Test-AzResourceGroupDeployment -ResourceGroupName $resourceGroupName -Mode Incremental -TemplateParameterFile $templateParamFilePath -TemplateFile $templateFilePath
    if($testOutput -ne $null)
    {
        LogError $testOutput.Code
        LogError $testOutput.Message
        LogError $testOutput.Details
        return $testOutput
    }

    Log "Starting vpn deployment"
    Log $templateParamFilePath
    Log $templateFilePath
    $deployment=New-AzResourceGroupDeployment -Name $deploymentName -ResourceGroupName $resourceGroupName -Mode Incremental -TemplateParameterFile $templateParamFilePath -TemplateFile $templateFilePath -DeploymentDebugLogLevel All -AsJob 
    return $deployment
}

function WaitForAzurevpndeployment()
{
    [CmdletBinding()]
    Param
    (
        [Parameter(Mandatory=$true)]
        [String]
        $deploymentName,
        [Parameter(Mandatory=$true)]
        [String]
        $resourceGroupName
    )
    Log "Waiting for vpn deployment completion...."
    Do
    {
        Start-Sleep -s 60
        $deploymentStatus=Get-AzResourceGroupDeployment -DeploymentName $deploymentName -ResourceGroupName $ResourceGroupName
        Log "vpn deployment status: $($deploymentStatus.ProvisioningState)"
    } while($deploymentStatus.ProvisioningState -eq "Running")

    return $deploymentStatus
}


function GetFirewallPrivateIp()
{
    [CmdletBinding()]
    Param
    (
        [Parameter(Mandatory=$true)]
        [String]
        $firewallPublicIpName,

        [Parameter(Mandatory=$true)]
        [String]
        $firewallName,
        
        [Parameter(Mandatory=$true)]
        [String]
        $resourceGroupName
    )

    $firewallPublicIp = Get-AzPublicIpAddress -Name $firewallPublicIpName -ResourceGroupName $resourceGroupName

    if($firewallPublicIp -eq $null)
    {
        LogError "Get-AzPublicIpAddress -Name $firewallPublicIpName -ResourceGroupName $resourceGroupName"
        return
    }
    $ipConfigId = $firewallPublicIp.IpConfiguration.Id

    $firewall = Get-AzFirewall -Name $firewallName -ResourceGroupName $resourceGroupName

    if($firewall -eq $null)
    {
        LogError "Get-AzFirewall -Name $FirewallName -ResourceGroupName $ResourceGroupName"
        return
    }
    
    $firewallPrivateIp = $null
    foreach($ipConfig in $firewall.IpConfigurations)
    {
        if($ipConfig.Id -eq $ipConfigId)
        {
            $firewallPrivateIp = $ipConfig.PrivateIPAddress
            break
        }
    }

    return $firewallPrivateIp
}

