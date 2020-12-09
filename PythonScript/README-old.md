---
page_type: sample
languages:
- python
products:
- azure-stack
- azure-virtual-machines

---

# Deploying VMs usingPython

This sample shows how to create and manage a VM on your Azure Stack Edge device using Python.

<!-- 
Guidelines on README format: https://review.docs.microsoft.com/help/onboard/admin/samples/concepts/readme-template?branch=master

Guidance on onboarding samples to docs.microsoft.com/samples: https://review.docs.microsoft.com/help/onboard/admin/samples/process/onboarding?branch=master

Taxonomies for products and languages: https://review.docs.microsoft.com/new-hope/information-architecture/metadata/taxonomies?branch=master
-->

## Contents

Outline the file contents of the repository. It helps users navigate the codebase, build configuration and any related assets.

| File/folder       | Description                                |
|-------------------|--------------------------------------------|
| `example_ase.py`             | The python source code for vm creation                        |

## Prerequisites

All documentation and steps are listed here: https://docs.microsoft.com/en-us/azure/databox-online/azure-stack-edge-j-series-deploy-virtual-machine-cli-python

## Setup

Open a Powershell window in Administrator mode. 

Update these environments first:
	
	$ENV:ARM_TENANT_ID = ""
	$ENV:ARM_CLIENT_ID = ""
	$ENV:ARM_CLIENT_SECRET = ""
	$ENV:ARM_SUBSCRIPTION_ID = ""
	$ENV:AZURE_RESOURCE_LOCATION = ""
	$ENV:ARM_ENDPOINT = ""
	$ENV:VHD_FILE_PATH = ""
	$ENV:ADDRESS_PREFIXES = ""
	$ENV:PRIVATE_IP_ADDRESS = ""

## Running the sample

Run the script in "C:\Program Files (x86)\Microsoft SDKs\Azure\CLI2" with the appropriate token authentication method (cli or token)

To use the 'cli' authentication mode:

	C:\Program Files (x86)\Microsoft SDKs\Azure\CLI2> .\python.exe c:\example_ase.py cli

To use the 'token' authentication mode:

	C:\Program Files (x86)\Microsoft SDKs\Azure\CLI2> .\python.exe c:\example_ase.py token

## Key concepts

Provide users with more context on the tools and services used in the sample. Explain some of the code that is being used and how services interact with each other.

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
