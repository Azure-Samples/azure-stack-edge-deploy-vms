$script:logfile = "PowershellClient.log"

function _log($msg, $lvl) {    
    $output = "[$lvl] $msg" | timestamp
    $output | Out-File -Append $script:logfile
}

function Log($msg) {
    Write-Host($msg)
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

function GetHostname()
{
    [CmdletBinding()]
    Param
    (

        [Parameter(Mandatory=$true)]
        [System.Management.Automation.Runspaces.PSSession]
        $Session
    )
   
    $name = Invoke-Command -Session $Session -ScriptBlock {
        hostname
    }   

    Write-Host "Hostname: $name"
    return $name
}

function CreateVSwitch()
{
    [CmdletBinding()]
    Param
    (
        [Parameter(Mandatory=$true)]
        [System.Management.Automation.Runspaces.PSSession]
        $Session,
        
        [Parameter(Mandatory=$false)]
        [String]
        $netAdapter
    )
   
    ##Checking VMSwitch exists
    Invoke-Command -Session $Session -ScriptBlock {
        $vmSwitches = Get-HcsExternalVirtualSwitch 
        $exists = $false
        if($vmSwitches -ne $null)
        {
            foreach ($sw in $vmSwitches) 
            {
                if($sw.InterfaceAlias.ToLower() -eq  $args[0].ToLower())
                {
                    $exists = $true
                }
            }
            if($exists -eq $true)
            {
                Write-Host "vSwitch already exists"
            }
            else
            {
                Add-HcsExternalVirtualSwitch -InterfaceAlias $args[0]
                Write-Host "vSwitch Created"
            }
        } 
        else
        {
             Add-HcsExternalVirtualSwitch -InterfaceAlias $args[0]
        }
    } -ArgumentList $netAdapter       
}

function GetDbeHostNameFromIP()
{
    [CmdletBinding()]
    Param
    (
        [Parameter(Mandatory=$false)]
        [String]
        $IPAddress
    )
    $out = [System.Net.Dns]::GetHostByAddress($IPAddress)   
    return $out.HostName
}

function ShortGuid()
{
    $g = [guid]::NewGuid()
    $v = [string]$g
    $v = $v.Replace("-", "")
    $v = $v.substring(0, 6)

    return $v
}

function NewResourceGroup
{
    [CmdletBinding()]
    Param
    (
        [Parameter(Mandatory=$true)]
        [String]
        $name,

        [Parameter(Mandatory=$false)]
        [String]
        $location=$script:DeviceLocation
    )

    try
    {
        LogGreen "New-AzResourceGroup -Name $name -Location $location -Force"
        $out1 = New-AzResourceGroup -Name $name -Location $location -Force
        if ($out1.ProvisioningState -eq 'Succeeded' -and
            $out1.ResourceGroupName -eq $name)
        {
            LogGreen "Successfully created Resource Group:$name"
        }
        Start-Sleep -s 1
    }
    catch
    {
        throw $_
    }
}

function NewStorageAccount
{
    [CmdletBinding()]
    Param
    (
        [Parameter(Mandatory=$true)]
        [String]
        $saname,

        [Parameter(Mandatory=$true)]
        [String]
        $rgname,

        [Parameter(Mandatory=$false)]
        [String]
        $location=$script:DeviceLocation,

        [Parameter(Mandatory=$false)]
        [String]
        $sku ="Standard_LRS",

        [Parameter(Mandatory=$false)]
        [String]        
        $kind = "StorageV2",

        [Parameter(Mandatory=$false)]
        [String]
        $accessTier = "Hot"
    )

    Start-Sleep -s 1
    try
    {
        LogGreen "New-AzStorageAccount -Name $saname -ResourceGroupName $rgname -SkuName $sku -Location $location"
        $out = New-AzStorageAccount -Name $saname -ResourceGroupName $rgname -SkuName $sku -Location $location
        $out
        LogGreen "Created New Storage Account"
    }
    catch
    {
        throw $_
    }
}

function GetStorageAccount
{
    [CmdletBinding()]
    Param
    (
        [Parameter(Mandatory=$true)]
        [String]
        $saname,

        [Parameter(Mandatory=$true)]
        [String]
        $rgname
    )
    LogGreen "Get-AzStorageAccount -name $saname -resourcegroupname $rgname"
    Start-Sleep -s 1
    $out = Get-AzStorageAccount -name $saname -resourcegroupname $rgname
    $out
}

function UploadVhd
{
    [CmdletBinding()]
    Param
    (
        [Parameter(Mandatory=$true)]
        [String]
        $rgname,

        [Parameter(Mandatory=$true)]
        [String]
        $saname,

        [Parameter(Mandatory=$false)]
        [String]
        $containername=$script:ContainerName,
        
        [Parameter(Mandatory=$true)]
        [String]
        $VHDPath,

        [Parameter(Mandatory=$true)]
        [String]
        $VHDFile,

        [Parameter(Mandatory=$false)]
        [String]
        $AzCopy10Path
    )
    
    try
    {
        LogGreen "`n Uploading Vhd to Storage Account"
        
        $journalFile = [string]([guid]::NewGuid())
        $script:ContainerName =  $containername

        $key = (Get-AzStorageAccountKey -ResourceGroupName $rgname -Name $saname)[0].Value

        $endPoint = (Get-AzStorageAccount -name $saname -resourcegroupname $rgname).PrimaryEndpoints.Blob
        $endPoint = $endPoint.trim()

        $script:AzCopy = $AzCopy10Path

        LogGreen "`nNew-AzStorageContext -StorageAccountName $saname -StorageAccountKey $key -Endpoint $endPoint"
        $storCont = New-AzStorageContext -StorageAccountName $saname -StorageAccountKey $key -Endpoint $endPoint

        LogGreen "`nNew-AzStorageAccountSASToken -Service Blob,File,Queue,Table -ResourceType Container,Service,Object -Permission "acdlrw" -Context $storCont -Protocol HttpsOnly"
        $storSAS =  New-AzStorageAccountSASToken -Service Blob,File,Queue,Table -ResourceType Container,Service,Object -Permission "acdlrw" -Context $storCont -Protocol HttpsOnly

        Write-Host "`nSAS Token : $storSAS"

        Write-Host "`n $script:AzCopy make $endPoint$ContainerName$storSAS"
        & $script:AzCopy make $endPoint$ContainerName$storSAS

        Write-Host "`n AzCopy cp $VHDPath\$VHDFile $endPoint$ContainerName$storSAS " -ForegroundColor Green
        Write-Host "`n"
        & $script:AzCopy  cp "$VHDPath\$VHDFile" "$endPoint$ContainerName$storSAS" 

        LogGreen "VHD Upload Done"
    }
    catch
    {
        throw $_
    }
}

function CreateManagedDisk
{
    [CmdletBinding()]
    Param
    (
        [Parameter(Mandatory=$true)]
        [String]
        $rgname,

        [Parameter(Mandatory=$true)]
        [String]
        $saname,

        [Parameter(Mandatory=$false)]
        [String]
        $diskname=$script:DiskName,

        [Parameter(Mandatory=$false)]
        [String]
        $ContainerName,

        [Parameter(Mandatory=$false)]
        [String]
        $DeviceLocation,

        [Parameter(Mandatory=$false)]
        [String]
        $VHDFile
    )

    try
    {
        LogGreen "`n Creating a new managed disk"
        $endPoint = (Get-AzStorageAccount -name $saname -resourcegroupname $rgname).PrimaryEndpoints.Blob
        $endPoint = $endPoint.trim()

        LogGreen "`n $diskConfig = New-AzDiskConfig -Location $DeviceLocation -CreateOption Import -SourceUri "$endPoint$ContainerName/$VHDFile""
    
        $DiskConfig = New-AzDiskConfig -Location $DeviceLocation -CreateOption Import -SourceUri "$endPoint$ContainerName/$VHDFile"
    
        LogGreen "`n $diskConfig"
    
        LogGreen "`n New-AzDisk -ResourceGroupName $rgname -DiskName $diskname -Disk"
    
        New-AzDisk -ResourceGroupName $rgname -DiskName $diskname -Disk $DiskConfig

        LogGreen "`n Created a new managed disk"
    }
    catch
    {
        throw $_
    }
}

function CreateEmptyManagedDisk
{
    [CmdletBinding()]
    Param
    (
        [Parameter(Mandatory=$false)]
        [String]
        $rgname=$script:ResourceGroupName,

        [Parameter(Mandatory=$false)]
        [String]
        $saname=$script:StorageAccountName,

        [Parameter(Mandatory=$false)]
        [String]
        $diskname=$script:DiskName,     

        [Parameter(Mandatory=$true)]
        [System.Management.Automation.Runspaces.PSSession]
        $Session,

        [Parameter(Mandatory=$false)]
        [String]
        $deviceLocation='dbeLocal',

        [Parameter(Mandatory=$false)]
        [String]
        $OsType = 'linux',

        [Parameter(Mandatory=$false)]
        [int]
        $diskSizeGB = 5

    )
    try
    {
        $serialNumber = GetHostname -Session $Session 
        $serialNumber = "dbe-" + $serialNumber.ToLower()
        LogGreen "`n Creating a new managed disk"
        LogGreen "`n  $script:DiskConfig = New-AzDiskConfig -Location ${deviceLocation} -DiskSizeGB ${diskSizeGB} -AccountType StandardLRS -CreateOption Empty -OsType ${OsType}"
    
        LogGreen "`n OS Type is $OsType"

        $script:DiskConfig = New-AzDiskConfig -Location $deviceLocation -DiskSizeGB $diskSizeGB -AccountType StandardLRS -CreateOption Empty -OsType $OsType
    
        LogGreen "`n $diskConfig"
    
        LogGreen "`n New-AzDisk -ResourceGroupName $rgname -DiskName $diskname -Disk"
    
        New-AzDisk -ResourceGroupName $rgname -DiskName $diskname -Disk $script:DiskConfig
    }
    catch
    {
        throw $_
    }
}

function CreateImage
{
    [CmdletBinding()]
    Param
    (
        [Parameter(Mandatory=$true)]
        [String]
        $rgname,

        [Parameter(Mandatory=$false)]
        [String]
        $imagename=$script:ImageName , 

        [Parameter(Mandatory=$false)]
        [String]
        $diskname=$script:DiskName,

        [Parameter(Mandatory=$true)]
        [String]
        $DeviceLocation,

        [Parameter(Mandatory=$true)]
        [String]$OS
    )
    try
    {
        LogGreen "`n Creating a new Image out of managed disk"
        $imageConfig = New-AzImageConfig -Location $DeviceLocation
        $ManagedDiskId = (Get-AzDisk -Name $diskname -ResourceGroupName $rgname).Id
        Set-AzImageOsDisk -Image $imageConfig -OsType $OS -OsState 'Generalized' -ManagedDiskId $ManagedDiskId
    
        LogGreen "`n New-AzImage -Image $imageConfig -ImageName $imagename  -ResourceGroupName $rgname"
        New-AzImage -Image $imageConfig -ImageName $imagename  -ResourceGroupName $rgname

        LogGreen "`n Created a new Image"

    }
    catch
    {
        throw $_
    }
}

function CreateNetworkInterface
{
    [CmdletBinding()]
    Param
    (
        [Parameter(Mandatory=$false)]
        [String]
        $nicName=$script:Nic,

        [Parameter(Mandatory=$false)]
        [String]
        $ipcfgName=$script:ipConfigName,
                
        [Parameter(Mandatory=$true)]
        [String]
        $rgname,

        [Parameter(Mandatory=$false)]
        [String]
        $vnetName,

        [Parameter(Mandatory=$true)]
        [String]
        $script:DeviceLocation,

        [Parameter(Mandatory=$false)]
        [String]$VNetAddressSpace,

        [Parameter(Mandatory=$false)]
        [String]$ip
    )
   
    #CreateVSwitch -Session $Session -netAdapter $netAdapter 
    try
    {
        if (!(Get-AzVirtualNetwork))
        {
            LogGreen "Creating virtual network and subnet"
            
            $vnetCfg = New-AzVirtualNetworkSubnetConfig -Name subnet123 -AddressPrefix $VNetAddressSpace
            New-AzVirtualNetwork -ResourceGroupName $rgname -Name $vnetName -Location $script:DeviceLocation -AddressPrefix $VNetAddressSpace -Subnet $vnetCfg
            LogGreen "`nCreated virtual network"
        }
        else
        {
            LogGreen "`n Using existing Virtual network Resource"
        }

        LogGreen "`n Creating a new Newtork Interface"
        $ipconfig
        $subNetId = (Get-AzVirtualNetwork).Subnets[0].Id
        if([String]::IsNullOrEmpty($ip))
        {
            $ipconfig = New-AzNetworkInterfaceIpConfig -Name $ipcfgName -SubnetId $subNetId
        }
        else
        {
            $ipConfig = New-AzNetworkInterfaceIpConfig -Name $ipcfgName -SubnetId $subNetId -PrivateIpAddress $ip
        }
        $Nic = New-AzNetworkInterface -Name $nicName -ResourceGroupName $rgname -Location $script:DeviceLocation -IpConfiguration $ipConfig

        Write-Host "`n"
        $Nic
        Write-Host "`n"
        LogGreen "`n Created Network Interface`n"
    }
    catch
    {
        throw $_
    }
}

function CreateVM
{
    [CmdletBinding()]
    Param
    (
        [Parameter(Mandatory=$true)]
        [String]$vmName,

        [Parameter(Mandatory=$false)]
        [String]$computerName=$script:ComputerName,

        [Parameter(Mandatory=$false)]
        [String]$osDiskName=$script:OSDiskName,

        [Parameter(Mandatory=$false)]
        [String]$network=$script:Nic,

        [Parameter(Mandatory=$true)]
        [String]$rgname,

        [Parameter(Mandatory=$false)]
        [String]$script:DeviceLocation='dbeLocal',

        [Parameter(Mandatory=$false)]
        [String]$script:ImageName,

        [Parameter(Mandatory=$false)]
        [String]$OS,

        [Parameter(Mandatory=$false)]
        [String]$VMSize,

        [Parameter(Mandatory=$false)]
        [String]$VMUserName,

        [Parameter(Mandatory=$false)]
        [String]$VMPassword
    )

    try
    {
        LogGreen "`n Creating a new VM"
        $OSDiskCaching = "ReadWrite"
        $OSCreateOption = "FromImage"
        $VMLocalAdminSecurePassword = ConvertTo-SecureString $VMPassword -AsPlainText -Force   
        $Credential = New-Object System.Management.Automation.PSCredential ($VMUserName, $VMLocalAdminSecurePassword)

        LogGreen "`n New-AzVMConfig -VMName $vmName -VMSize $VMSize"
        $VirtualMachine = New-AzVMConfig -VMName $vmName -VMSize $VMSize

        if($OS.Contains("Windows"))
        {
            LogGreen "`n Set-AzVMOperatingSystem -VM $VirtualMachine -$OS -ComputerName $computerName -Credential $Credential"
            $VirtualMachine = Set-AzVMOperatingSystem -VM $VirtualMachine -Windows -ComputerName $computerName -Credential $Credential
    
            LogGreen "`n $VirtualMachine = Set-AzVMOSDisk -VM $VirtualMachine -Name $osDiskName -Caching $OSDiskCaching -CreateOption $OSCreateOption -$OS -StorageAccountType StandardLRS"
            $VirtualMachine = Set-AzVMOSDisk -VM $VirtualMachine -Name $osDiskName -Caching $OSDiskCaching -CreateOption $OSCreateOption -Windows -StorageAccountType StandardLRS
        }
        else
        {
            LogGreen "`n Set-AzVMOperatingSystem -VM $VirtualMachine -$OS -ComputerName $computerName -Credential $Credential"
            $VirtualMachine = Set-AzVMOperatingSystem -VM $VirtualMachine -Linux -ComputerName $computerName -Credential $Credential
    
            LogGreen "`n $VirtualMachine = Set-AzVMOSDisk -VM $VirtualMachine -Name $osDiskName -Caching $OSDiskCaching -CreateOption $OSCreateOption -$OS -StorageAccountType StandardLRS"
            $VirtualMachine = Set-AzVMOSDisk -VM $VirtualMachine -Name $osDiskName -Caching $OSDiskCaching -CreateOption $OSCreateOption -Linux -StorageAccountType StandardLRS
        }

        $nic = (Get-AzNetworkInterface -Name $network -ResourceGroupName $rgname).Id

        LogGreen "`n Add-AzVMNetworkInterface -VM $VirtualMachine -Id $nic.Id"
        $VirtualMachine = Add-AzVMNetworkInterface -VM $VirtualMachine -Id $nic

        $image = (Get-AzImage -ResourceGroupName $rgname -ImageName $script:ImageName).Id

        LogGreen "`n Set-AzVMSourceImage -VM $VirtualMachine -Id $image"
        $VirtualMachine = Set-AzVMSourceImage -VM $VirtualMachine -Id $image

        LogGreen "`n New-AzVM -ResourceGroupName $rgname -Location $script:DeviceLocation -VM $VirtualMachine -Verbose"
        Measure-Command {$result = New-AzVM -ResourceGroupName $rgname -Location $script:DeviceLocation -VM $VirtualMachine -Verbose}
        $result
    }
    catch
    {
        throw $_
    }
}

function DeleteVM
{ 
    [CmdletBinding()]
    Param
    (
        [Parameter(Mandatory=$true)]
        [String]
        $vmName,

        [Parameter(Mandatory=$true)]
        [String]
        $rgname
    )
 
    LogGreen "`n Deleting VM: $vmName"
    LogGreen "`n Remove-AzVM -name $vmName -ResourceGroupName $rgname -Force"
    Remove-AzVM -name $vmName -ResourceGroupName $rgname -Force
}
 
function DeleteNetworkInterface
{ 
    [CmdletBinding()]
    Param
    (
        [Parameter(Mandatory=$false)]
        [String]
        $network=$script:subnet,

        [Parameter(Mandatory=$true)]
        [String]
        $vnetName,

        [Parameter(Mandatory=$true)]
        [String]
        $ipcfgName,

        [Parameter(Mandatory=$true)]
        [String]
        $rgname
    )

    LogGreen "`n Remove-AzVirtualNetworkSubnetConfig -Name $network -VirtualNetwork $vnetName -force"
    Remove-AzVirtualNetworkSubnetConfig -Name $network -VirtualNetwork $vnetName -force
    
    LogGreen "`n Remove-AzVirtualNetwork -name $vnetName -ResourceGroupName $rgname -force"
    Remove-AzVirtualNetwork -name $vnetName -ResourceGroupName $rgname -force

    LogGreen "`n  Remove-AzNetworkInterfaceIpConfig -Name $ipcfgName -NetworkInterface $network -force"
    Remove-AzNetworkInterfaceIpConfig -Name $ipcfgName -NetworkInterface $network -force

    LogGreen "`n Remove-AzNetworkInterface -name $network -ResourceGroupName $rgname -Force"
    Remove-AzNetworkInterface -name $network -ResourceGroupName $rgname -Force
}

function DeleteDisk
{
   [CmdletBinding()]
    Param
    (
        [Parameter(Mandatory=$true)]
        [String]
        $diskName,

        [Parameter(Mandatory=$true)]
        [String]
        $rgname
    )

    LogGreen "`n Remove-Azdisk -name $diskName -ResourceGroupName $rgname -Force"
    Remove-Azdisk -name $diskName -ResourceGroupName $rgname -Force
}

function DeleteOsDisk
{
   [CmdletBinding()]
    Param
    (
        [Parameter(Mandatory=$true)]
        [String]
        $osDiskName,

        [Parameter(Mandatory=$false)]
        [String]
        $imagename ,

        [Parameter(Mandatory=$true)]
        [String]
        $rgname
    )

    LogGreen "`n Remove-Azdisk -name $osDiskName -ResourceGroupName $rgname -Force"
    Remove-Azdisk -name $osDiskName -ResourceGroupName $rgname -Force
}

function DeleteStorageAccount
{
   [CmdletBinding()]
    Param
    (
        [Parameter(Mandatory=$true)]
        [String]
        $saName,

        [Parameter(Mandatory=$true)]
        [String]
        $rgname
    )
    LogGreen "`n Remove-AzStorageAccount -Name $saName -ResourceGroupName $rgname -Force"
    Remove-AzStorageAccount -Name $saName -ResourceGroupName $rgname -Force
}

function DeleteRg
{
   [CmdletBinding()]
    Param
    (       

        [Parameter(Mandatory=$true)]
        [String]
        $rgname
    )
    LogGreen "`n Remove-AzResourceGroup -Name $rgname -Force"
    Remove-AzResourceGroup -Name $rgname -Force
}

function CleanupDisks
{
    LogGreen "`n Get-Azdisk | Remove-AzDisk -force"
    Get-Azdisk | Remove-AzDisk -force
}

function CleanupDiskImage
{
    LogGreen "`n Get-AzImage | Remove-AzImage -Force"
    Get-AzImage | Remove-AzImage -Force
}

function CleanupStorageAccounts
{
    LogGreen "`n Get-AzImage | Remove-AzImage -Force"
    Get-AzStorageAccount | Remove-AzStorageAccount -Force
}

function CleanupVNets
{
    LogGreen "`n Get-AzVirtualNetwork | Remove-AzVirtualNetwork -Force"
    Get-AzVirtualNetwork | Remove-AzVirtualNetwork -Force
}

function CleanupNetworkInterfaces
{
    LogGreen "`n Get-AzNetworkInterface | Remove-AzNetworkInterface -Force"
    Get-AzNetworkInterface | Remove-AzNetworkInterface -Force
}

function CleanupAllResources
{
    LogGreen "`n Get-AzResource | Remove-AzResource -Force"
    Get-AzResource | Remove-AzResource -Force
}

function CleanupAllResourceGroups
{
    LogGreen "`n AzResourceGroup | Remove-AzResourceGroup -Force"
    Get-AzResourceGroup | Remove-AzResourceGroup -Force
}

function CreateVmFromEmptyMd
{
    [CmdletBinding()]
    Param
    (
        [Parameter(Mandatory=$false)]
        [String]
        $vmName=$script:VmName,

        [Parameter(Mandatory=$false)]
        [String]
        $computerName=$script:ComputerName,

        [Parameter(Mandatory=$false)]
        [String]
        $diskName,

        [Parameter(Mandatory=$false)]
        [String]
        $network=$script:Nic,

        [Parameter(Mandatory=$false)]
        [String]
        $rgname=$script:ResourceGroupName,

        [Parameter(Mandatory=$true)]
        [String]$script:DbeLoginPassword,

        [Parameter(Mandatory=$false)]
        [String]
        $script:DeviceLocation='dbeLocal',

        [Parameter(Mandatory=$true)]
        [String]
        $script:ImageName,

        [Parameter(Mandatory=$false)]
        [Switch]
        $isLinux = $true
    )

    LogGreen "`n Creating a new VM"   
    $VMLocalAdminUser = "Administrator"
    $LocationName = "DeviceLocation"      
    $OSDiskCaching = "ReadWrite"
    $OSCreateOption = "FromImage"
    $VMLocalAdminSecurePassword = ConvertTo-SecureString $script:DbeLoginPassword -AsPlainText -Force   
    $Credential = New-Object System.Management.Automation.PSCredential ($VMLocalAdminUser, $VMLocalAdminSecurePassword)

    LogGreen "`n New-AzVMConfig -VMName $vmName -VMSize "Standard_D1_v2""
    $VirtualMachine = New-AzVMConfig -VMName $vmName -VMSize "Standard_D1_v2"

    if($islinux)
    {
        LogGreen "`n $disk = Get-AzDisk -Name $diskName -ResourceGroupName $rgname"
        $disk = Get-AzDisk -Name $diskName -ResourceGroupName $rgname

        LogGreen "`n $VirtualMachine = Set-AzVMOSDisk -VM $VirtualMachine -ManagedDiskId $disk.Id -CreateOption Attach -Linux"
        $VirtualMachine = Set-AzVMOSDisk -VM $VirtualMachine -ManagedDiskId $disk.Id -CreateOption Attach -Linux
    }else
    {
        LogGreen "`n $disk = Get-AzDisk -Name $diskName -ResourceGroupName $rgname"
        $disk = Get-AzDisk -Name $diskName -ResourceGroupName $rgname

        LogGreen "`n $VirtualMachine = Set-AzVMOSDisk -VM $VirtualMachine -ManagedDiskId $disk.Id -CreateOption Attach -Windows"
        $VirtualMachine = Set-AzVMOSDisk -VM $VirtualMachine -ManagedDiskId $disk.Id -CreateOption Attach -Windows
    }
    $nic = (Get-AzNetworkInterface -Name $network -ResourceGroupName $rgname).Id

    LogGreen "`n Add-AzVMNetworkInterface -VM $VirtualMachine -Id $nic.Id"
    $VirtualMachine = Add-AzVMNetworkInterface -VM $VirtualMachine -Id $nic

    LogGreen "`n New-AzVM -ResourceGroupName $rgname -Location $script:DeviceLocation -VM $VirtualMachine -Verbose"
    New-AzVM -ResourceGroupName $rgname -Location $script:DeviceLocation -VM $VirtualMachine -Verbose
}