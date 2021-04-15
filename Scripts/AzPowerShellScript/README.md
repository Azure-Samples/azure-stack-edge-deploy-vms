---
page_type: sample
languages:
- azurepowershell
products:
- azure-stack
- azure-virtual-machines

---

# Deploying VMs using the Az cmdlets

This script creates a VM on your Azure Stack Edge device using Az cmdlets via Powershell. 
The VM created will have 1 data disk and 1 network card which can be configure by passing appropriate arguments to the script.

<!-- 
Guidelines on README format: https://review.docs.microsoft.com/help/onboard/admin/samples/concepts/readme-template?branch=master

Guidance on onboarding samples to docs.microsoft.com/samples: https://review.docs.microsoft.com/help/onboard/admin/samples/process/onboarding?branch=master

Taxonomies for products and languages: https://review.docs.microsoft.com/new-hope/information-architecture/metadata/taxonomies?branch=master
-->

## Contents

| File/folder       | Description                                |
|-------------------|--------------------------------------------|
| `AzHelper.psm1`             | A helper file for Az VM creation.                        |
| `CreateVM.ps1`      | The powershell script to create a virtual machine.      |

## Prerequisites

The detailed steps for VM deployment using Az cmdlets is published at <paste link after document publish>

## Setup

1. Open Powershell client version 7 and above in administrative mode
2. Connect to Azure Stack edge device using Az cmdlets <paste link after document publish>
3. Ensure that storage account name is properly configured in the `\etc\hosts` file <paste link after document publish>

## Running the sample
The script can be used to fine tune the VM configuration:
| Parameter       | Description                                |
|-------------------|--------------------------------------------|
| `-ResourceGroupName` | The resource group which will be created and used while creating the VM |
| `-VmName` | The VM name which will be used while creating the VM |
| `-VMUserName` | The user account which will be created on the VM |
| `-VMPassword` | The password associated with the username |
| `-OS` | `Linux` or `Windows` |
| `-VHDPath`| The folder path where the VM vhd is located |
| `-VHDFile` | The file name for the VM vhd |
| `-StorageAccountName` | The storage account group which will be created for the VM vhd |
| `-VMSize` | The VM size <VM size web link> |
| `-DiskSizeGb` | Size of the data disk created with the VM |
| `-VNetAddressSpace` | Optional argument which specifies the network address space |
| `-NicPrivateIp` | Optional argument which specifies static IP. Skip if using DHCP IP |
| `-AzCopy10Path` | The path to azcopy execuatble which will be used to copy the VM vhd |

A sample run using some of the parameters defined above:

```
.\CreateVM.ps1 -ResourceGroupName rg2 `
				-VmName vm2 `
				-VMUserName admin `
				-VMPassword Password1 `
				-OS "Linux" `
				-VHDPath "\\hcsfs\scratch\bvt_vhds_2" `
				-VHDFile "ubuntu18.04waagent.vhd" `
				-StorageAccountName sa2 `
				-VMSize "Standard_D1_v2" `
				-DiskSizeGb 10 `
				-AzCopy10Path "C:\Users\Admin\Documents\azcopy.exe"
```

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
