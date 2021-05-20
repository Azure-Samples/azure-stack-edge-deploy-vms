This template downloads a VHD from an Azure storage account to the Azure Stack Edge appliance and creates a managed disk out of this downloaded VHD.

**Pre-requisites:**

Upload a valid VHD into an Azure storage account as a page blob. We only support the creation of Generation 1 VMs. The VHD must be Fixed type and end in .vhd extension.


**Deployment Steps:**

Configure the parameter file with appropriate values:
    
* sourceBlobUri = the blob SAS uri for the above uploaded blob in Azure.
* targetStorageAccountName = the name of the storage account on the Azure Stack Edge where the VHD will get downloaded to. This can stay constant throughout multiple deployments.
* targetContainerName = the name of the container that will get created inside the storage account on the Azure Stack Edge to hold the VHD. This should change for each deployment because the container will get deleted as part of the template.
* ingestionJobName = the name of the download job. This should change for each deployment.
* dataDiskName = the name of the managed disk to be created. This should change for each deployment.


Command:
```powershell
$templateFile = "path_to_template_file"
$templateParameterFile = "path_to_template_parameter_file"
$resourceGroupName = "resource_group_name"

# Create the resource group if it doesn't exist
New-AzureRmResourceGroup -Name $resourceGroupName -Location dbelocal

New-AzureRmResourceGroupDeployment `
    -ResourceGroupName $resourceGroupName `
    -TemplateFile $templateFile `
    -TemplateParameterFile $templateParameterFile `
    -Name "Deployment1"

