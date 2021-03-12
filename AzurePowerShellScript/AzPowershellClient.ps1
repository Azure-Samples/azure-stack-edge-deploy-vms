
[CmdletBinding(DefaultParametersetName='None')]
Param(
	[Parameter(Mandatory=$True)]
    [String]$VNetAddressSpace,
	[String]$NicPrivateIp,
	[Parameter(Mandatory=$True)]
	[String]$VHDPath,
	[Parameter(Mandatory=$True)]
	[String]$VHDFile,
	[Parameter(Mandatory=$True)]
	[String]$StorageAccountName,
	[String]$DiskSizeGb,
	[Parameter(Mandatory=$True)]
	[ValidateSet("Standard_D1_v2", "Standard_D2_v2", "Standard_D3_v2", "Standard_D4_v2", "Standard_D5_v2", "Standard_D11_v2", 
				 "Standard_D12_v2", "Standard_D13_v2", "Standard_D14_v2", "Standard_DS1_v2", "Standard_DS2_v2", "Standard_DS3_v2", 
				 "Standard_DS4_v2", "Standard_DS5_v2", "Standard_DS11_v2", "Standard_DS12_v2", "Standard_DS13_v2", "Standard_DS14_v2")]
	[String]$VMSize,
	[Parameter(Mandatory=$True)]
	[String]$VMUserName,
	[Parameter(Mandatory=$True)]
	[String]$VMPassword,
	[Parameter(Mandatory=$True)]
	[ValidateSet("Windows", "Linux")]
	[String]$OS,
	[string]$AzCopy10Path
)
Import-Module .\AzHelper.psm1 -Force

$date=get-date
$timeStamp = $date.toString('yyMMddhhmmss')
$script:ResourceGroupName = "rg" + $timeStamp
$script:VnetName = "vnet" + $timeStamp
$script:DeviceLocation = "DBELocal"
$script:ContainerName = "vmimages"
$script:DiskName = "ld" + + $timeStamp
$script:ImageName = "ig" + + $timeStamp
$script:Nic = "nic" + $timeStamp
$script:ipConfigName = "ip" + $timeStamp
$script:VmName = "VM" + $timeStamp
$script:ComputerName = "COM" + $timeStamp
$script:OSDiskName = "osld" + $timeStamp

LogGreen "Setting AZCOPY_DEFAULT_SERVICE_API_VERSION to 2017-11-09"
[Environment]::SetEnvironmentVariable("AZCOPY_DEFAULT_SERVICE_API_VERSION", "2017-11-09")

try{
	NewResourceGroup $script:ResourceGroupName $script:DeviceLocation
	NewStorageAccount $script:StorageAccountName $script:ResourceGroupName $script:DeviceLocation
	GetStorageAccount $script:StorageAccountName $script:ResourceGroupName
	UploadVhd $script:ResourceGroupName $script:StorageAccountName $script:ContainerName $VHDPath $VHDFile $AzCopy10Path
	CreateManagedDisk $script:ResourceGroupName $script:StorageAccountName $script:DiskName $script:ContainerName $script:DeviceLocation $VHDFile
	CreateImage $script:ResourceGroupName $script:ImageName $script:DiskName $script:DeviceLocation $OS
	CreateNetworkInterface $script:Nic $script:ipConfigName $script:ResourceGroupName $script:VnetName $script:DeviceLocation $VNetAddressSpace $NicPrivateIp
	CreateVM $script:VmName $script:ComputerName $script:OSDiskName $script:Nic $script:ResourceGroupName $script:DeviceLocation $script:ImageName $OS $VMSize $VMUserName $VMPassword
}
catch
{
	throw $_
}
