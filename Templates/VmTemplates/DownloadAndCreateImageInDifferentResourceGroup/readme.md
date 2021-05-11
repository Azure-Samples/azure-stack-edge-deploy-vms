This template downloads a VHD from an Azure storage account to the Azure Stack Edge appliance into the default resource group. Then it creates an image in a different resource group from this downloaded VHD.

**Pre-requisites:**

Upload a valid VHD into an Azure storage account as a page blob. We only support the creation of Generation 1 VMs. The VHD must be Fixed type and end in .vhd extension.


**Deployment Steps:**

Configure the parameter file with appropriate values:
    
* sourceBlobUri = the blob SAS uri for the above uploaded blob in Azure.
* targetStorageAccountName = the name of the storage account on the Azure Stack Edge where the VHD will get downloaded to. This can stay constant throughout multiple deployments.
* targetContainerName = the name of the container that will get created inside the storage account on the Azure Stack Edge to hold the VHD. This should change for each deployment because the container will get deleted as part of the template.
* ingestionJobName = the name of the download job. This should change for each deployment.
* imageName = the name of the image to create. This should change for each deployment.
* osType = Linux or Windows for the VHD that is provided. This should change based on the source blob.
* defaultResourceGroup = the resource group for the storage account and download job resources.
* imageResourceGroup = the resource group for the image to be created.
* vhdName = the name of the vhd that will be downloaded with the .vhd extension, ie ubuntu.vhd.


Command:
```powershell
$templateFile = "path_to_template_file"
$templateParameterFile = "path_to_template_parameter_file"
$defaultResourceGroupName = "default_resource_group_name"
$imageResourceGroupName = "image_resource_group_name"

# Create the default resource group if it doesn't exist
New-AzureRmResourceGroup -Name $defaultResourceGroupName -Location dbelocal
# Create the image resource group if it doesn't exist
New-AzureRmResourceGroup -Name $imageResourceGroupName -Location dbelocal

New-AzureRmResourceGroupDeployment `
    -ResourceGroupName $defaultResourceGroupName `
    -TemplateFile $templateFile `
    -TemplateParameterFile $templateParameterFile `
    -Name "Deployment1"

