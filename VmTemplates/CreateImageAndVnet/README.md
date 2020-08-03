
This template creates an image and Virtual Network required to create a Virtual Machine.

Pre-requisites:
1) Create a local storage account.
2) Upload Linux/Windows VHD to the local storage account as page blob.
3) Get the blob URI of the above uploaded blob.

Deployment Steps:
Configure the parameter file with appropriate values for imageUri(blob URI from step 3), osType, addressPrefix and subnetPrefix.

Command:

$templateFile = "Path_to_template_file"

$templateParameterFile = "Path_to_template_parameter_file"

$RGName = "Resource_group_name"

```powershell
New-AzureRmResourceGroupDeployment `
    -ResourceGroupName $RGName `
    -TemplateFile $templateFile `
    -TemplateParameterFile $templateParameterFile `
    -Name "Deployment1"
