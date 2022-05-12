---
title: Use an IoT Edge module to access data from an Azure Stack Edge local share.
description: Use an IoT Edge module to access data from an Azure Stack Edge local share.
services: databox
author: alkohli

ms.service: databox
ms.subservice: pod
ms.topic: sample
ms.date: 05/12/2022
ms.author: alkohli

# Customer intent: As an IT pro, I want to quickly use an IoT Edge module to access data from Azure Stack Edge local share.
---

# Use an IoT Edge module to access data from an Azure Stack Edge local share

This example shows how to use an IoT Edge module to access data from an Azure Stack Edge local share. You can use this example with all Azure Stack Edge SKUs.

## Prerequisites

Before you begin, make sure you have:

- Windows client.
- Azure subscription to use for your Azure Stack Edge resources.
- Resource group to use to manage the resources.
- Azure PowerShell.
- Download the `MyScript.py` script and store it in a convenient location on your local system.

### Install Azure PowerShell

1. Install PowerShell v6 or later. For guidance, see [Install Azure PowerShell](/powershell/azure/install-az-ps?view=azps-4.7.0).

1. Install the Az.Resources and Az.StackEdge modules in PowerShell. You must run PowerShell as Administrator.

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
      > If AzureRM is installed, uninstall it.

### Download the script

1. Go to the [folder that hosts the script](https://github.com/Azure-Samples/azure-stack-edge-deploy-vms/scripts/IotEdgeScript).

1. Download or clone the zip file to the local system. Extract files from the zip, and note where you save the file.

   Contents of the script file:

   `~/helloworld$ cat myscript.py
    while True:
    with open('/app/helloworld.txt') as f:
    contents = f.readlines()
    print(contents)`

## Step 1. Create and configure an Edge local share

1. Create an Edge local share, and name it `myshare1`.

   Use these steps to [create an Edge local share](https://docs.microsoft.com/azure/databox-online/azure-stack-edge-gpu-manage-shares#add-a-local-share).

   Use these steps to [mount the share you just created](https://docs.microsoft.com/azure/databox-online/azure-stack-edge-gpu-manage-shares#mount-a-share).

1. Connect to the local share.

   Use these steps to [connect to the local share](https://docs.microsoft.com/azure/databox-online/azure-stack-edge-gpu-deploy-add-shares#connect-to-the-share).

1. From the [IotEdgeScript](https://github.com/Azure-Samples/azure-stack-edge-deploy-vms/IotEdgeScript/) folder, add the `HelloWorld.txt` file to the local share.

## Step 2. Create an app and corresponding container to read from the local share

Create a container image then add the code/app created from the code. Output is an IoT Edge container that will enable you to read from the local share.

1. Create an Azure Container Registry. Use the following steps to [create an Azure container registry](https://docs.microsoft.com/azure/container-registry/container-registry-get-started-portal).

1. Starting with a file, [build and push the image into the container registry](https://docs.microsoft.com/azure/container-registry/container-registry-quickstart-task-cli#build-and-push-image-from-a-dockerfile).

1. Use the following script to push the app into your container registry.

      `~/helloworld$ cat Dockerfile
       FROM python:3
       ADD myscript.py /
       CMD [ "python", "./myscript.py" ]`

The result is a container image that includes the registry with your app.

## Step 3. Create a deployment using the IoT Edge module

You'll now create a deployment using the IoT Edge module that you created in the earlier step.

1. On an Azure Stack Edge device that is activated, make sure that the IoT Edge service is enabled.

   ![Screenshot that shows the healthy status of the IoT Edge service.](media/readme/iot-edge-service-status-1.png)

1. Configure the Edge compute role. When this role is configured, the **Properties** would show the IoT Hub resource associated with your Azure Stack Edge device.

   ![Screenshot that shows properties of the IoT Edge compute role.](media/readme/iot-edge-compute-role-properties-2.png)

1. Go to the IoT Hub resource and deploy the IoT Edge module using the steps described in this article: [Configure and run a module on GPU on Azure Stack Edge Pro device](https://docs.microsoft.com/azure/databox-online/azure-stack-edge-gpu-configure-gpu-modules), with the following differences:

   1. On the **Module settings** tab, the image URI would be the information from your Azure Container Registry.

      ![Screenshot that shows the image URI for the IoT Edge module.](media/README/iot-edge-module-image-uri-3.png)

   1. Provide the container create option as shown here:

      ```json
      "{\"HostConfig\":{\"Mounts\":[
      {\"Target\":\"/app\",\"Source\":\"myshare1\",\"Type\":\"volume\"}]}}"
      ```

   1. **Add** the module. The module should show as running.

   1. Select **Review+Create**. The deployment options that you have selected are displayed. Review the options.

      In your deployment, remember to [specify the mount option](https://microsoft.github.io/iotedge-k8s-doc/bp/storage/ase.html).

      In our example, the deployment file looks like this:

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
      $edgeHub": {
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

      The module should be deployed in a couple minutes. Refresh and the module status should update to running, as shown below.

      ![Screenshot that shows that the helloworld IoT Edge custom module is running.](media/README/helloworld-iot-edge-custom-module-is-running-4.png)

## Step 4. Review output from the container logs

Here's a sample output:

      `kubectl logs --tail=2 testhelloworld-57758b5f57-rt6jw -c testhell
       oworld -n iotedge
       ['hello world']
       ['hello world']`
