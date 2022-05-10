---
title: Sync data from Azure Stack Edge local share to IoT Edge
description: Use an IoT Edge module to access data from an Azure Stack Edge local share. 
services: databox
author: alkohli

ms.service: databox
ms.subservice: pod
ms.topic: sample
ms.date: 05/10/2022
ms.author: alkohli

# Customer intent: As an IT pro, I want to quickly sync data from Azure Stack Edge local share to IoT Edge.
---

# Sync data from Azure Stack Edge local share to IoT Edge

One liner > This example uses an IoT Edge module to access data from an Azure Stack Edge local share. You can use this example with all Azure Stack Edge SKUs.

Key concepts

- Provide users with more context on the tools and services used in the sample. Explain some of the code that is being used and how services interact with each other.

Usage notes

- You'll need to provide...

License

- License considerations...

## Prerequisites

Before you begin, make sure you have:

- Windows client.
- Azure subscription to use for your Azure Stack Edge resources.
- Resource group to use to manage the resources.
- Azure PowerShell.
- Script - `MyScript.py` - stored in a convenient location.

### Install Azure PowerShell

1. Install PowerShell v6 or later. For guidance, see [Install Azure PowerShell](/powershell/azure/install-az-ps?view=azps-4.7.0).

2. Install the Az.Resources and Az.StackEdge modules in PowerShell. You must run PowerShell as Administrator.

   1. If AzureRM is installed, uninstall it:

      ```powershell
      PS C:\> Uninstall-AzureRm
      ```

   1. Install the Az.Resources and Az.StackEdge modules:

      ```powershell
      PS C:\> Install-Module Az.Resources
      PS C:\> Install-Module Az.StackEdge
      ```

      > [!NOTE]
      > If AzureRM is installed, you'll need to uninstall it.

### Download the scripts

1. Go to the [repo that stores the scripts in Azure Samples](https://github.com/Azure-Samples/azure-stack-edge-deploy-vms/).

1. Download or clone the zip file for the scripts.

   ![Screenshot showing the download zip file](./azure-stack-edge-order-download-clone-scripts.png)

    Extract the files from the zip, and note where you saved the scripts.

## Run the script

1. Open Azure PowerShell as Administrator.
1. Set your execution policy to **Unrestricted**. This is needed because the script is an unsigned script.

   ```azurepowershell
   Set-ExecutionPolicy Unrestricted
   ```

1. Change directories to the directory where you stored the script. For example:

   ```azurepowershell
   cd scripts
   ```

1. Run the script. To run `MyScript.py`, you would type the following:

   ```azurepowershell
   & '.\MyScript.py'
   ```
1. With an **Unrestricted** execution policy, you'll see the following text. Type `R` to run the script.

   ```azurepowershell
   Security warning
   Run only scripts that you trust. While scripts from the internet can be useful, this script can potentially harm your computer.
   If you trust this script, use the Unblock-File cmdlet to allow the script to run without this warning message. Do you want to
   run C:\scripts\New-AzStackEdgeMultiOrder.ps1?
   [D] Do not run  [R] Run once  [S] Suspend  [?] Help (default is "D"): R
   ```

### MyScript.py

Use this script to ....

#### Parameter information

- `SKU` indicates the configuration of Azure Stack Edge device:
  | Azure Stack Edge SKU | Value |
  | -------------------- | ---------------- |
  | Azure Stack Edge Pro - 1GPU | `EdgeP_Base` |
  | Azure Stack Edge Pro - 2GPU | `EdgeP_High` |
  | Azure Stack Edge Pro - single node | `EdgePR-Base` |
  | Azure Stack Edge Pro - single node with UPS | `EdgePR_Base_UPS` |
  | Azure Stack Edge Mini R | `EdgeMR_Mini` |
  
   > [!NOTE]
   > Azure Stack Edge Pro with FPGA is now deprecated.

## Step 1. Create and configure an Edge local share

1. Create an Edge local share, and name it `myshare1`.

   Use these steps to [create an Edge local share](https://docs.microsoft.com/azure/databox-online/azure-stack-edge-gpu-manage-shares#add-a-local-share).

   Use these steps to [mount the share you just created](https://docs.microsoft.com/azure/databox-online/azure-stack-edge-gpu-manage-shares#mount-a-share).

1. Connect to the local share.

   Use these steps to [connect to the local share](https://docs.microsoft.com/azure/databox-online/azure-stack-edge-gpu-deploy-add-shares#connect-to-the-share).

1. From the [IotEdge](https://github.com/Azure-Samples/azure-stack-edge-deploy-vms/IotEdgeScript/) folder, add the `HelloWorld.txt` file to the local share.

   Example here...

## Step 2. Create an app and container registry to read from the local share

1. Create a simple app to read from the local share.

   Example that creates the app...

1. Create an azure container registry and push the app into your container image. Use the following steps to [create an Azure container registry](https://docs.microsoft.com/azure/container-registry/container-registry-get-started-portal).

1. Starting with a file, [build and push the image into the container registry](https://docs.microsoft.com/azure/container-registry/container-registry-quickstart-task-cli#build-and-push-image-from-a-dockerfile).

   - In my deployment I will be mapping it to /app.
   - Use the following script to push the app into your container registry.

      `~/helloworld$ cat Dockerfile
       FROM python:3
       ADD myscript.py /
       CMD [ "python", "./myscript.py" ]`

   The result is a container image that includes the registry with your app.

   The myscript.py script.

   `~/helloworld$ cat myscript.py
    while True:
    with open('/app/helloworld.txt') as f:
    contents = f.readlines()
    print(contents)`

## Step 3. Create a deployment

1. Create a deployment that assigns your Edge local share to a deployment module.

   - Remember to [specify the mount option](https://microsoft.github.io/iotedge-k8s-doc/bp/storage/ase.html).

1. 3.1 > screenshots and details TBD

1. Erika’s deployment looks like this:

```json

{
"modulesContent": {
"$edgeAgent": {
"properties.desired": {
"modules": {
"testhelloworld": {
"settings": {
"image": "ehdregistry.azurecr.io/helloworld:latest",
"createOptions": "{\"HostConfig\":{\"Mounts\":[{\"Target\":\"/app\",\"Source\":\"myshare1\",\"Type\":\"volume\"}]}}"
},
"type": "docker",
"version": "1.0",
"status": "running",
"restartPolicy": "always"
}
},
"runtime": {
"settings": {
"minDockerVersion": "v1.25",
"registryCredentials": {
"ehdregistry": {
"address": "ehdregistry.azurecr.io",
"password": "",
"username": ""
}
}
},
"type": "docker"
},
"schemaVersion": "1.1",
"systemModules": {
"edgeAgent": {
"settings": {
"image": "mcr.microsoft.com/azureiotedge-agent:1.1",
"createOptions": ""
},
"type": "docker"
},
"edgeHub": {
"settings": {
"image": "mcr.microsoft.com/azureiotedge-hub:1.1",
"createOptions": "{\"HostConfig\":{\"PortBindings\":{\"443/tcp\":[{\"HostPort\":\"443\"}],\"5671/tcp\":[{\"HostPort\":\"5671\"}],\"8883/tcp\":[{\"HostPort\":\"8883\"}]}}}"
},
"type": "docker",
"status": "running",
"restartPolicy": "always"
}
}
}
},
"$edgeHub": {
"properties.desired": {
"routes": {
"route": "FROM /messages/* INTO $upstream"
},
"schemaVersion": "1.1",
"storeAndForwardConfiguration": {
"timeToLiveSecs": 7200
}
}
},
"testhelloworld": {
"properties.desired": {}
}
}
}
```

## Step 4. Review sample output from container logs

Here's a sample output:

  `kubectl logs --tail=2 testhelloworld-57758b5f57-rt6jw -c testhell
   oworld -n iotedge
   ['hello world']
   ['hello world']`
