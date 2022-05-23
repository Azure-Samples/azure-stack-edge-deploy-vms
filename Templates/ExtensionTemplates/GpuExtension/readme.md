This template deploys GPU extension for Linux and Windows VMs
--------------------------------------------------------------------------------------------------------------------------------------------------------------------
**If you are using release older than 2205 please make sure to use addGPUExtLinuxVM.parameters.json for Linux VMs and addGPUExtWindowsVM.parameters.json for Windows VMs**

**If you are using release 2205 and further please make sure to use addGPUExtLinuxVM2205.parameters.json for Linux VMs and addGPUExtWindowsVM2205.parameters.json for Windows VMs**
--------------------------------------------------------------------------------------------------------------------------------------------------------------------
Deployment Steps:
1) Configure the parameter file with appropriate values for the parameter 'vmName'.

Command:
```powershell
$templateFile = "Path_to_template_file"
$templateParameterFile = "Path_to_template_parameter_file"
$RGName = "Resource_group_name"

New-AzureRmResourceGroupDeployment `
    -ResourceGroupName $RGName `
    -TemplateFile $templateFile `
    -TemplateParameterFile $templateParameterFile `
    -Name "<DeploymentName>"
```