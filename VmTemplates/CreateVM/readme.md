This template creates a virtual machine from the existing image and virtual network.
--------------------------------------------------------------------------------------------------------------------------------------------------------------------
** Refer to this section if you are using a build older than Nov release**
--------------------------------------------------------------------------------------------------------------------------------------------------------------------
Pre-requisites:
1) Create an image and Virtual Network from the template CreateImageAndVnet.json

https://github.com/Azure-Samples/azure-stack-edge-deploy-vms/blob/users/niachary/autovnettemplates/VmTemplates/CreateImage/readme.md

Deployment Steps:
1) Configure the parameter file with appropriate values for the parameters.
2) Give the same name for the image and the vnet as created by the template CreateImageAndVnet.json
3) Assign the value "" to the parameter privateIPAddress if you want to dynamically allocate IP address to the VM from DHCP.
   Otherwise give the value of the static IP adress to the parameter privateIPAddress.

Command:
```powershell
$templateFile = "Path_to_template_file"
$templateParameterFile = "Path_to_template_parameter_file"
$RGName = "Resource_group_name"

New-AzureRmResourceGroupDeployment `
    -ResourceGroupName $RGName `
    -TemplateFile $templateFile `
    -TemplateParameterFile $templateParameterFile `
    -Name "Deployment2"
```
--------------------------------------------------------------------------------------------------------------------------------
** Refer to this section if you are using a build for and after Nov release**
---------------------------------------------------------------------------------------------------------------------------------
1) Create an image from the template CreateImage.json

2) Run this command to get the VNet and Subnet name for existing Virtual Network

Command:
Get-AzureRmVirtualNetwork

Output:
```
PS C:\Users\niachary> Get-AzureRmVirtualNetwork


Name                   : ASEVNET
ResourceGroupName      : ASERG
Location               : dbelocal
Id                     : /subscriptions/b7fcb312-915d-4fd0-aff9-87035c3e5a3a/resourceGroups/ASERG/providers/Microsoft.Network/virtualNetworks/ASEVNET
Etag                   : W/"c790df21-d690-4cd9-b6b2-a729e9ad743a"
ResourceGuid           : cf7d24ac-a0e3-4813-9f32-bd2883fc19ce
ProvisioningState      : Succeeded
Tags                   :
AddressSpace           : {
                           "AddressPrefixes": [
                             "10.178.68.0/22"
                           ]
                         }
DhcpOptions            : null
Subnets                : [
                           {
                             "Name": "ASEVNETsubNet",
                             "Etag": "W/\"c790df21-d690-4cd9-b6b2-a729e9ad743a\"",
                             "Id": "/subscriptions/b7fcb312-915d-4fd0-aff9-87035c3e5a3a/resourceGroups/ASERG/providers/Microsoft.Network/virtualNetworks/ASEVNET/subnets/ASEVNETsubNet",
                             "AddressPrefix": "10.178.68.0/22",

```
3) Pass the VNet name, Subnet Name and Vnet resource group name in the CreateVM.parameters.json section as so:

"vnetName": {
      "value": "ASEVNET"
    },
    "subnetName": {
      "value": "ASEVNETsubNet"
    },
    "vnetRG": {
      "value": "aserg"
    },

Deployment Steps:
1) Configure the parameter file with appropriate values for the parameters.
2) Give the same name for the image as given in createImage.json
3) Give the vnet Name, subnet Name and Vnet resource group name as mentioned above.
3) Assign the value "" to the parameter privateIPAddress if you want to dynamically allocate IP address to the VM from DHCP.
   Otherwise give the value of the static IP adress to the parameter privateIPAddress.

Command:
```powershell
$templateFile = "Path_to_template_file"
$templateParameterFile = "Path_to_template_parameter_file"
$RGName = "Resource_group_name"

New-AzureRmResourceGroupDeployment `
    -ResourceGroupName $RGName `
    -TemplateFile $templateFile `
    -TemplateParameterFile $templateParameterFile `
    -Name "Deployment2"
```
