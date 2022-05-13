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

# Customer intent: As an IT pro, I want to quickly use an IoT Edge module to access data from an Azure Stack Edge local share.
---

# Use an IoT Edge module to access data from an Azure Stack Edge local share

**Applies to:** Azure Stack Edge Pro 2, Azure Stack Edge Pro GPU, Azure Stack Edge Pro R, Azure Stack Edge Mini R

This example shows how to use an IoT Edge module to access data from an Azure Stack Edge local share.

## Prerequisites

Before you begin, make sure you have:

- An Azure Stack Edge device that's activated. For more information, see [Activate Azure Stack Edge Pro with GPU](https://docs.microsoft.com/azure/databox-online/azure-stack-edge-gpu-deploy-activate).
- A Windows client that can connect to your Azure Stack Edge device.
- An Azure subscription to use for your Azure Stack Edge resources.
- Get the sample code.

### Get the sample code

1. Go to [Azure Stack Edge deploy VMs in Azure-Samples](https://github.com/Azure-Samples/azure-stack-edge-deploy-vms), and clone or download the zip file for code.

1. Download or clone the zip file to the local system. Extract files from the zip folder, and note where you save the file.

   ![Screenshot of the clone repo dialog in github.](media/readme/clone-or-download-the-zip-file-5.png)

   Contents of the script file:

   ```python
   ~/helloworld$ cat myscript.py
    while True:
    with open('/app/helloworld.txt') as f:
    contents = f.readlines()
    print(contents)`
   ```

## Step 1. Create and configure an Edge local share

1. Create an Edge local share, and name it `myshare1`.

   Use these steps to [create an Edge local share](https://docs.microsoft.com/azure/databox-online/azure-stack-edge-gpu-manage-shares#add-a-local-share).

   Use these steps to [mount the share you just created](https://docs.microsoft.com/azure/databox-online/azure-stack-edge-gpu-manage-shares#mount-a-share).

1. Connect to the local share.

   Use these steps to [connect to the local share](https://docs.microsoft.com/azure/databox-online/azure-stack-edge-gpu-deploy-add-shares#connect-to-the-share).

1. From the [IotEdgeScript](https://github.com/Azure-Samples/azure-stack-edge-deploy-vms/IotEdgeScript/) folder, add the `HelloWorld.txt` file to the local share.

## Step 2. Create an app and corresponding container to read from the local share

Create a container image using the code. Output is an IoT Edge container that will enable you to read from the local share.

1. Create an Azure Container Registry. Use the following steps to [create an Azure container registry](https://docs.microsoft.com/azure/container-registry/container-registry-get-started-portal).

1. Starting with a file, [build and push the image into the container registry](https://docs.microsoft.com/azure/container-registry/container-registry-quickstart-task-cli#build-and-push-image-from-a-dockerfile).

   ```python
   ~/helloworld$ cat Dockerfile
   FROM python:3
   ADD myscript.py /
   CMD [ "python", "./myscript.py" ]`
   ```

The result is an IoT Edge container.

## Step 3. Create a deployment using the IoT Edge module

You'll now create a deployment using the IoT Edge module that you created in the earlier step.

1. On an Azure Stack Edge device that's activated, make sure that the IoT Edge service is enabled.

   ![Screenshot that shows the healthy status of the IoT Edge service.](media/readme/iot-edge-service-status-1.png)

1. Configure the Edge compute role. When this role is configured, the **Properties** would show the IoT Hub resource associated with your Azure Stack Edge device.

   ![Screenshot that shows properties of the IoT Edge compute role.](media/readme/iot-edge-compute-role-properties-2.png)

1. Go to the IoT Hub resource and deploy the IoT Edge module using the steps described in this article: [Configure and run a module on GPU on Azure Stack Edge Pro device](https://docs.microsoft.com/azure/databox-online/azure-stack-edge-gpu-configure-gpu-modules), with the following differences:

   1. On the **Module settings** tab, the image URI would be the information from your Azure Container Registry.

      ![Screenshot that shows the image URI for the IoT Edge module.](media/readme/iot-edge-module-image-uri-3.png)

   1. Provide the container create option as shown here:

      ```python
      "{"HostConfig":{"Mounts":[
      {"Target":"/app","Source":"myshare1","Type":"volume"}]}}"
      ```

   1. **Add** the module. The module should show as running.

   1. Select **Review + Create**. The deployment options that you have selected are displayed. Review the options.

      In your deployment, remember to [specify the mount option](https://microsoft.github.io/iotedge-k8s-doc/bp/storage/ase.html).

      In our example, the deployment file looks like this:

      ```python
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

      ![Screenshot that shows the helloworld IoT Edge custom module is running.](media/readme/helloworld-iot-edge-custom-module-is-running-4.png)

## Step 4. Review output from the container logs

Here's a sample output:

   ```python
   kubectl logs --tail=2
   helloworld-57758b5f57-rt6jw -c 
   helloworld -n iotedge
   ['hello world']
   ['hello world']`
   ```
