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
        [Parameter(Mandatory=$false)]
        [String]
        $name=$script:ResourceGroupName,

        [Parameter(Mandatory=$false)]
        [String]
        $location=$script:DeviceLocation
    )

    try
    {
        LogGreen "New-AzureRmResourceGroup -Name $name -Location $location -Force"
        $out1 = New-AzureRmResourceGroup -Name $name -Location $location -Force
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
        [Parameter(Mandatory=$false)]
        [String]
        $saname=$script:StorageAccountName,


        [Parameter(Mandatory=$false)]
        [String]
        $rgname=$script:ResourceGroupName,

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
        LogGreen "New-AzureRmStorageAccount -Name $saname  -ResourceGroupName $rgname -SkuName $sku -Location $location"
        $out = New-AzureRmStorageAccount -Name $saname  -ResourceGroupName $rgname -SkuName $sku -Location $location
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
        [Parameter(Mandatory=$false)]
        [String]
        $name = $script:StorageAccountName,

        [Parameter(Mandatory=$false)]
        [String]
        $rgname=$script:ResourceGroupName
    )
    LogGreen "Get-AzureRmStorageAccount -name $name -resourcegroupname $rgname"
    Start-Sleep -s 1
    $out = Get-AzureRmStorageAccount -name $name -resourcegroupname $rgname
    $out
}

function UploadVhd
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

        $key = (Get-AzureRmStorageAccountKey -ResourceGroupName $rgname -Name $saname)[0].Value

        $endPoint = (Get-AzureRmStorageAccount -name $saname -resourcegroupname $rgname).PrimaryEndpoints.Blob
        $endPoint = $endPoint.trim()

        $script:AzCopy = $AzCopy10Path

        LogGreen "`nNew-AzureStorageContext -StorageAccountName $saname -StorageAccountKey $key -Endpoint $endPoint"
        $storCont = New-AzureStorageContext -StorageAccountName $saname -StorageAccountKey $key -Endpoint $endPoint

        LogGreen "`nNew-AzureStorageAccountSASToken -Service Blob,File,Queue,Table -ResourceType Container,Service,Object -Permission "acdlrw" -Context $storCont -Protocol HttpsOnly"
        $storSAS =  New-AzureStorageAccountSASToken -Service Blob,File,Queue,Table -ResourceType Container,Service,Object -Permission "acdlrw" -Context $storCont -Protocol HttpsOnly

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
        [Parameter(Mandatory=$false)]
        [String]
        $rgname=$script:ResourceGroupName,

        [Parameter(Mandatory=$false)]
        [String]
        $saname=$script:StorageAccountName,

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
        $endPoint = (Get-AzureRmStorageAccount -name $saname -resourcegroupname $rgname).PrimaryEndpoints.Blob
        $endPoint = $endPoint.trim()

        LogGreen "`n $diskConfig = New-AzureRmDiskConfig -Location $DeviceLocation -CreateOption Import -SourceUri "$endPoint$ContainerName/$VHDFile""
    
        $DiskConfig = New-AzureRmDiskConfig -Location $DeviceLocation -CreateOption Import -SourceUri "$endPoint$ContainerName/$VHDFile"
    
        LogGreen "`n $diskConfig"
    
        LogGreen "`n New-AzureRmDisk -ResourceGroupName $rgname -DiskName $diskname -Disk"
    
        New-AzureRmDisk -ResourceGroupName $rgname -DiskName $diskname -Disk $DiskConfig

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
        LogGreen "`n  $script:DiskConfig = New-AzureRmDiskConfig -Location ${deviceLocation} -DiskSizeGB ${diskSizeGB} -AccountType StandardLRS -CreateOption Empty -OsType ${OsType}"
    
        LogGreen "`n OS Type is $OsType"

        $script:DiskConfig = New-AzureRmDiskConfig -Location $deviceLocation -DiskSizeGB $diskSizeGB -AccountType StandardLRS -CreateOption Empty -OsType $OsType
    
        LogGreen "`n $diskConfig"
    
        LogGreen "`n New-AzureRmDisk -ResourceGroupName $rgname -DiskName $diskname -Disk"
    
        New-AzureRmDisk -ResourceGroupName $rgname -DiskName $diskname -Disk $script:DiskConfig
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
        [Parameter(Mandatory=$false)]
        [String]
        $rgname=$script:ResourceGroupName,

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
        $imageConfig = New-AzureRmImageConfig -Location $DeviceLocation
        $ManagedDiskId = (Get-AzureRmDisk -Name $diskname -ResourceGroupName $rgname).Id
        Set-AzureRmImageOsDisk -Image $imageConfig -OsType $OS -OsState 'Generalized' -ManagedDiskId $ManagedDiskId
    
        LogGreen "`n New-AzureRmImage -Image $imageConfig -ImageName $imagename  -ResourceGroupName $rgname"
        New-AzureRmImage -Image $imageConfig -ImageName $imagename  -ResourceGroupName $rgname

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
                
        [Parameter(Mandatory=$false)]
        [String]
        $rgname=$script:ResourceGroupName,

        [Parameter(Mandatory=$false)]
        [String]
        $vnetName,

        [Parameter(Mandatory=$true)]
        [String]
        $script:DeviceLocation,

        [Parameter(Mandatory=$true)]
        [String]$VNetAddressSpace,

        [Parameter(Mandatory=$false)]
        [String]$ip
    )
   
    #CreateVSwitch -Session $Session -netAdapter $netAdapter 
    try
    {
        if (!(Get-AzureRmVirtualNetwork))
        {
            LogGreen "Creating virtual network and subnet"
            
            $vnetCfg = New-AzureRmVirtualNetworkSubnetConfig -Name subnet123 -AddressPrefix $VNetAddressSpace
            New-AzureRmVirtualNetwork -ResourceGroupName $rgname -Name $vnetName -Location $script:DeviceLocation -AddressPrefix $VNetAddressSpace -Subnet $vnetCfg
            LogGreen "`nCreated virtual network"
        }
        else
        {
            LogGreen "`n Using existing Virtual network Resource"
        }

        LogGreen "`n Creating a new Newtork Interface"
        $ipconfig
        $subNetId = (Get-AzureRmVirtualNetwork).Subnets[0].Id 
        if([String]::IsNullOrEmpty($ip))
        {
            $ipconfig = New-AzureRmNetworkInterfaceIpConfig -Name $ipcfgName -SubnetId $subNetId
        }
        else
        {
            $ipConfig = New-AzureRmNetworkInterfaceIpConfig -Name $ipcfgName -SubnetId $subNetId -PrivateIpAddress $ip
        }
        $Nic = New-AzureRmNetworkInterface -Name $nicName -ResourceGroupName $rgname -Location $script:DeviceLocation -IpConfiguration $ipConfig

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
        [Parameter(Mandatory=$false)]
        [String]$vmName=$script:VmName,

        [Parameter(Mandatory=$false)]
        [String]$computerName=$script:ComputerName,

        [Parameter(Mandatory=$false)]
        [String]$osDiskName=$script:OSDiskName,

        [Parameter(Mandatory=$false)]
        [String]$network=$script:Nic,

        [Parameter(Mandatory=$false)]
        [String]$rgname=$script:ResourceGroupName,

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

        LogGreen "`n New-AzureRmVMConfig -VMName $vmName -VMSize $VMSize"
        $VirtualMachine = New-AzureRmVMConfig -VMName $vmName -VMSize $VMSize

        if($OS.Contains("Windows"))
        {
            LogGreen "`n Set-AzureRmVMOperatingSystem -VM $VirtualMachine -$OS -ComputerName $computerName -Credential $Credential"
            $VirtualMachine = Set-AzureRmVMOperatingSystem -VM $VirtualMachine -Windows -ComputerName $computerName -Credential $Credential
    
            LogGreen "`n $VirtualMachine = Set-AzureRmVMOSDisk -VM $VirtualMachine -Name $osDiskName -Caching $OSDiskCaching -CreateOption $OSCreateOption -$OS -StorageAccountType StandardLRS"
            $VirtualMachine = Set-AzureRmVMOSDisk -VM $VirtualMachine -Name $osDiskName -Caching $OSDiskCaching -CreateOption $OSCreateOption -Windows -StorageAccountType StandardLRS
        }
        else
        {
            LogGreen "`n Set-AzureRmVMOperatingSystem -VM $VirtualMachine -$OS -ComputerName $computerName -Credential $Credential"
            $VirtualMachine = Set-AzureRmVMOperatingSystem -VM $VirtualMachine -Linux -ComputerName $computerName -Credential $Credential
    
            LogGreen "`n $VirtualMachine = Set-AzureRmVMOSDisk -VM $VirtualMachine -Name $osDiskName -Caching $OSDiskCaching -CreateOption $OSCreateOption -$OS -StorageAccountType StandardLRS"
            $VirtualMachine = Set-AzureRmVMOSDisk -VM $VirtualMachine -Name $osDiskName -Caching $OSDiskCaching -CreateOption $OSCreateOption -Linux -StorageAccountType StandardLRS
        }

        $nic = (Get-AzureRmNetworkInterface -Name $network -ResourceGroupName $rgname).Id

        LogGreen "`n Add-AzureRmVMNetworkInterface -VM $VirtualMachine -Id $nic.Id"
        $VirtualMachine = Add-AzureRmVMNetworkInterface -VM $VirtualMachine -Id $nic

        $image = (Get-AzureRmImage -ResourceGroupName $rgname -ImageName $script:ImageName).Id

        LogGreen "`n Set-AzureRmVMSourceImage -VM $VirtualMachine -Id $image"
        $VirtualMachine = Set-AzureRmVMSourceImage -VM $VirtualMachine -Id $image

        LogGreen "`n New-AzureRmVM -ResourceGroupName $rgname -Location $script:DeviceLocation -VM $VirtualMachine -Verbose"
        Measure-Command {$result = New-AzureRmVM -ResourceGroupName $rgname -Location $script:DeviceLocation -VM $VirtualMachine -Verbose}
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
    LogGreen "`n Remove-AzureRmVM -name $vmName -ResourceGroupName $rgname -Force"
    Remove-AzureRmVM -name $vmName -ResourceGroupName $rgname -Force 
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

    LogGreen "`n Remove-AzureRmVirtualNetworkSubnetConfig -Name $network -VirtualNetwork $vnetName -force"
    Remove-AzureRmVirtualNetworkSubnetConfig -Name $network -VirtualNetwork $vnetName -force
    
    LogGreen "`n Remove-AzureRmVirtualNetwork -name $vnetName -ResourceGroupName $rgname -force"
    Remove-AzureRmVirtualNetwork -name $vnetName -ResourceGroupName $rgname -force

    LogGreen "`n  Remove-AzureRmNetworkInterfaceIpConfig -Name $ipcfgName -NetworkInterface $network -force"
    Remove-AzureRmNetworkInterfaceIpConfig -Name $ipcfgName -NetworkInterface $network -force

    LogGreen "`n Remove-AzureRmNetworkInterface -name $network -ResourceGroupName $rgname -Force"
    Remove-AzureRmNetworkInterface -name $network -ResourceGroupName $rgname -Force
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

    LogGreen "`n Remove-AzureRmdisk -name $diskName -ResourceGroupName $rgname -Force"
    Remove-AzureRmdisk -name $diskName -ResourceGroupName $rgname -Force
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

    LogGreen "`n Remove-AzureRmdisk -name $osDiskName -ResourceGroupName $rgname -Force"
    Remove-AzureRmdisk -name $osDiskName -ResourceGroupName $rgname -Force
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
    LogGreen "`n Remove-AzureRmStorageAccount -Name $saName -ResourceGroupName $rgname -Force"   
    Remove-AzureRmStorageAccount -Name $saName -ResourceGroupName $rgname -Force
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
    LogGreen "`n Remove-AzureRmResourceGroup -Name $rgname -Force"
    Remove-AzureRmResourceGroup -Name $rgname -Force
}

function CleanupDisks
{
    LogGreen "`n Get-AzureRmdisk | Remove-AzureRmDisk -force"
    Get-AzureRmdisk | Remove-AzureRmDisk -force
}

function CleanupDiskImage
{
    LogGreen "`n Get-AzureRmImage | Remove-AzureRmImage -Force"
    Get-AzureRmImage | Remove-AzureRmImage -Force
}

function CleanupStorageAccounts
{
    LogGreen "`n Get-AzureRmImage | Remove-AzureRmImage -Force"
    Get-AzureRmStorageAccount | Remove-AzureRmStorageAccount -Force
}

function CleanupVNets
{
    LogGreen "`n Get-AzureRmVirtualNetwork | Remove-AzureRmVirtualNetwork -Force"
    Get-AzureRmVirtualNetwork | Remove-AzureRmVirtualNetwork -Force
}

function CleanupNetworkInterfaces
{
    LogGreen "`n Get-AzureRmNetworkInterface | Remove-AzureRmNetworkInterface -Force"
    Get-AzureRmNetworkInterface | Remove-AzureRmNetworkInterface -Force
}

function CleanupAllResources
{
    LogGreen "`n Get-AzureRmResource | Remove-AzureRmResource -Force"
    Get-AzureRmResource | Remove-AzureRmResource -Force
}

function CleanupAllResourceGroups
{
    LogGreen "`n AzureRmResourceGroup | Remove-AzureRmResourceGroup -Force"
    Get-AzureRmResourceGroup | Remove-AzureRmResourceGroup -Force
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

    LogGreen "`n New-AzureRmVMConfig -VMName $vmName -VMSize "Standard_D1_v2""
    $VirtualMachine = New-AzureRmVMConfig -VMName $vmName -VMSize "Standard_D1_v2"

    if($islinux)
    {
        LogGreen "`n $disk = Get-AzureRmDisk -Name $diskName -ResourceGroupName $rgname"
        $disk = Get-AzureRmDisk -Name $diskName -ResourceGroupName $rgname

        LogGreen "`n $VirtualMachine = Set-AzureRmVMOSDisk -VM $VirtualMachine -ManagedDiskId $disk.Id -CreateOption Attach -Linux"
        $VirtualMachine = Set-AzureRmVMOSDisk -VM $VirtualMachine -ManagedDiskId $disk.Id -CreateOption Attach -Linux
    }else
    {
        LogGreen "`n $disk = Get-AzureRmDisk -Name $diskName -ResourceGroupName $rgname"
        $disk = Get-AzureRmDisk -Name $diskName -ResourceGroupName $rgname

        LogGreen "`n $VirtualMachine = Set-AzureRmVMOSDisk -VM $VirtualMachine -ManagedDiskId $disk.Id -CreateOption Attach -Windows"
        $VirtualMachine = Set-AzureRmVMOSDisk -VM $VirtualMachine -ManagedDiskId $disk.Id -CreateOption Attach -Windows
    }
    $nic = (Get-AzureRmNetworkInterface -Name $network -ResourceGroupName $rgname).Id

    LogGreen "`n Add-AzureRmVMNetworkInterface -VM $VirtualMachine -Id $nic.Id"
    $VirtualMachine = Add-AzureRmVMNetworkInterface -VM $VirtualMachine -Id $nic

    LogGreen "`n New-AzureRmVM -ResourceGroupName $rgname -Location $script:DeviceLocation -VM $VirtualMachine -Verbose"
    New-AzureRmVM -ResourceGroupName $rgname -Location $script:DeviceLocation -VM $VirtualMachine -Verbose
}