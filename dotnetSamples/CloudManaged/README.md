# How to build the cloud library
* Open [CloudManagedVM.sln](https://github.com/Azure-Samples/azure-stack-edge-deploy-vms/blob/master/dotnetSamples/CloudManaged/CloudManagedVM/CloudManagedVM.sln) in Visual Studio 2019.
* Click on *Build* in Menu and it will build the CloudManagedVM.dll and its dependent dlls as well.
  It will also output the location of the dll built in the *Output* window of Visual Studio.
  
# How to Load the above dll(s)
* Once you have built the CloudmanagedVM.dll, copy its parent folder net472 to a desired location, say D:\
* Open powershell window and run the following commands
```
function LoadDlls($dllPath)
{
    $dllFiles = ls $dllPath*.dll
    foreach ($f in $dllFiles)
    {
        [Reflection.Assembly]::LoadFile($f.FullName)
    }
}

$dllPath = "D:\net472" # This should be path of parent folder where your dlls are copied in previous step
LoadDlls $dllPath
```
Your dlls are loaded now.

# How to start using the CloudManagedVM.dll
The first step in using the dll is to have a connection to your azure subscription authenticated.
To authenticate, you can either use a Service Prinicipal or an access token directly.
* To authenticate using Service Principal
  ```
  $client = New-Object CloudManagedVM.CloudLib($saasAccessKey, $tenantId, $clientId, $subscriptionId)
  ```
* To authenticate using an access token
  ```
  $client = New-Object CloudManagedVM.CloudLib($acessToken, $subscriptionId)
  ```
  
Once the client is initialized, you can invoke template deployments. For example,
```
$client.DeployTemplateAtSubscriptionLevel($saasResourceGroup, $deviceName, $templateFilePath, $templateParamsFilePath, $location)
```

