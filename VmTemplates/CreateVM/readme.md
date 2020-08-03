This template creates a virtual machine from the existing image and virtual network.

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

