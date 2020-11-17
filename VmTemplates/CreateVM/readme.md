This template creates a virtual machine from the existing image and virtual network.
--------------------------------------------------------------------------------------------------------------------------------------------------------------------
** Refer to this section if you are using a build older than Nov release**
--------------------------------------------------------------------------------------------------------------------------------------------------------------------
Pre-requisites:
1) Create an image and Virtual Network from the template CreateImageAndVnet.json

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

Run this command to get the VNet and Subnet name for existing Virtual Network

Get-AzureRmVirtualNetwork

Pass the VNet name, Subnet Name and Vnet resource group name in the parameter section as so:

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
