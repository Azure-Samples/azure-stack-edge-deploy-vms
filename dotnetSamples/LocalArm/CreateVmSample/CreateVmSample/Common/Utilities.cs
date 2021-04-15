using Microsoft.Azure.Management.Profiles.hybrid_2019_03_01.Compute.Models;
using Microsoft.Azure.Management.Profiles.hybrid_2019_03_01.Network.Models;
using Microsoft.Azure.Management.Profiles.hybrid_2019_03_01.ResourceManager.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreateVmSample.Common
{
    public static class Utilities
    {
        public static bool IsRunningMocked { get; set; }
        public static Action<string> LoggerMethod { get; set; }
        public static Func<string> PauseMethod { get; set; }

        public static string ProjectPath { get; set; }

        static Utilities()
        {
            LoggerMethod = Console.WriteLine;
            PauseMethod = Console.ReadLine;
            ProjectPath = ".";
        }

        public static void Log(string message)
        {
            LoggerMethod.Invoke(message);
        }

        public static void Log(object obj)
        {
            if (obj != null)
            {
                LoggerMethod.Invoke(obj.ToString());
            }
            else
            {
                LoggerMethod.Invoke("(null)");
            }
        }

        public static void Log()
        {
            Utilities.Log("");
        }

        public static string ReadLine()
        {
            return PauseMethod.Invoke();
        }

        // Print resource group info.
        public static void PrintResourceGroup(ResourceGroup resource)
        {
            StringBuilder info = new StringBuilder();
            info.Append("Resource Group: ").Append(resource.Id)
                    .Append("\n\tName: ").Append(resource.Name)
                    .Append("\n\tLocation: ").Append(resource.Location)
                    .Append("\n\tTags: ").Append(resource.Tags.ToString());
            Log(info.ToString());
        }

        public static void PrintVirtualMachine(VirtualMachine virtualMachine)
        {
            var storageProfile = new StringBuilder().Append("\n\tStorageProfile: ");
            if (virtualMachine.StorageProfile.ImageReference != null)
            {
                storageProfile.Append("\n\t\tImageReference:");
                storageProfile.Append("\n\t\t\tPublisher: ").Append(virtualMachine.StorageProfile.ImageReference.Publisher);
                storageProfile.Append("\n\t\t\tOffer: ").Append(virtualMachine.StorageProfile.ImageReference.Offer);
                storageProfile.Append("\n\t\t\tSKU: ").Append(virtualMachine.StorageProfile.ImageReference.Sku);
                storageProfile.Append("\n\t\t\tVersion: ").Append(virtualMachine.StorageProfile.ImageReference.Version);
            }

            if (virtualMachine.StorageProfile.OsDisk != null)
            {
                storageProfile.Append("\n\t\tOSDisk:");
                storageProfile.Append("\n\t\t\tOSType: ").Append(virtualMachine.StorageProfile.OsDisk.OsType);
                storageProfile.Append("\n\t\t\tName: ").Append(virtualMachine.StorageProfile.OsDisk.Name);
                storageProfile.Append("\n\t\t\tCaching: ").Append(virtualMachine.StorageProfile.OsDisk.Caching);
                storageProfile.Append("\n\t\t\tCreateOption: ").Append(virtualMachine.StorageProfile.OsDisk.CreateOption);
                storageProfile.Append("\n\t\t\tDiskSizeGB: ").Append(virtualMachine.StorageProfile.OsDisk.DiskSizeGB);
                if (virtualMachine.StorageProfile.OsDisk.Image != null)
                {
                    storageProfile.Append("\n\t\t\tImage Uri: ").Append(virtualMachine.StorageProfile.OsDisk.Image.Uri);
                }
                if (virtualMachine.StorageProfile.OsDisk.Vhd != null)
                {
                    storageProfile.Append("\n\t\t\tVhd Uri: ").Append(virtualMachine.StorageProfile.OsDisk.Vhd.Uri);
                }
                if (virtualMachine.StorageProfile.OsDisk.EncryptionSettings != null)
                {
                    storageProfile.Append("\n\t\t\tEncryptionSettings: ");
                    storageProfile.Append("\n\t\t\t\tEnabled: ").Append(virtualMachine.StorageProfile.OsDisk.EncryptionSettings.Enabled);
                    storageProfile.Append("\n\t\t\t\tDiskEncryptionKey Uri: ").Append(virtualMachine
                            .StorageProfile
                            .OsDisk
                            .EncryptionSettings
                            .DiskEncryptionKey.SecretUrl);
                    storageProfile.Append("\n\t\t\t\tKeyEncryptionKey Uri: ").Append(virtualMachine
                            .StorageProfile
                            .OsDisk
                            .EncryptionSettings
                            .KeyEncryptionKey.KeyUrl);
                }
            }

            if (virtualMachine.StorageProfile.DataDisks != null)
            {
                var i = 0;
                foreach (var disk in virtualMachine.StorageProfile.DataDisks)
                {
                    storageProfile.Append("\n\t\tDataDisk: #").Append(i++);
                    storageProfile.Append("\n\t\t\tName: ").Append(disk.Name);
                    storageProfile.Append("\n\t\t\tCaching: ").Append(disk.Caching);
                    storageProfile.Append("\n\t\t\tCreateOption: ").Append(disk.CreateOption);
                    storageProfile.Append("\n\t\t\tDiskSizeGB: ").Append(disk.DiskSizeGB);
                    storageProfile.Append("\n\t\t\tLun: ").Append(disk.Lun);
                    if (disk.ManagedDisk != null)
                    {
                        storageProfile.Append("\n\t\t\tManaged Disk Id: ").Append(disk.ManagedDisk.Id);
                    }
                    if (disk.Vhd.Uri != null)
                    {
                        storageProfile.Append("\n\t\t\tVhd Uri: ").Append(disk.Vhd.Uri);
                    }
                    if (disk.Image != null)
                    {
                        storageProfile.Append("\n\t\t\tImage Uri: ").Append(disk.Image.Uri);
                    }
                }
            }
            StringBuilder osProfile;
            if (virtualMachine.OsProfile != null)
            {
                osProfile = new StringBuilder().Append("\n\tOSProfile: ");

                osProfile.Append("\n\t\tComputerName:").Append(virtualMachine.OsProfile.ComputerName);
                if (virtualMachine.OsProfile.WindowsConfiguration != null)
                {
                    osProfile.Append("\n\t\t\tWindowsConfiguration: ");
                    osProfile.Append("\n\t\t\t\tProvisionVMAgent: ")
                            .Append(virtualMachine.OsProfile.WindowsConfiguration.ProvisionVMAgent);
                    osProfile.Append("\n\t\t\t\tEnableAutomaticUpdates: ")
                            .Append(virtualMachine.OsProfile.WindowsConfiguration.EnableAutomaticUpdates);
                    osProfile.Append("\n\t\t\t\tTimeZone: ")
                            .Append(virtualMachine.OsProfile.WindowsConfiguration.TimeZone);
                }
                if (virtualMachine.OsProfile.LinuxConfiguration != null)
                {
                    osProfile.Append("\n\t\t\tLinuxConfiguration: ");
                    osProfile.Append("\n\t\t\t\tDisablePasswordAuthentication: ")
                            .Append(virtualMachine.OsProfile.LinuxConfiguration.DisablePasswordAuthentication);
                }
            }
            else
            {
                osProfile = new StringBuilder().Append("\n\tOSProfile: null");
            }


            var networkProfile = new StringBuilder().Append("\n\tNetworkProfile: ");
            foreach (var networkInterfaceId in virtualMachine.NetworkProfile.NetworkInterfaces)
            {
                networkProfile.Append("\n\t\tId:").Append(networkInterfaceId);
            }

            Utilities.Log(new StringBuilder().Append("Virtual Machine: ").Append(virtualMachine.Id)
                    .Append("Name: ").Append(virtualMachine.Name)
                    .Append("\n\tLocation: ").Append(virtualMachine.Location)
                    .Append("\n\tHardwareProfile: ")
                    .Append("\n\t\tSize: ").Append(virtualMachine.HardwareProfile.VmSize)
                    .Append(storageProfile)
                    .Append(osProfile)
                    .Append(networkProfile)
                    .ToString());
        }
        public static void PrintNetworkInterface(NetworkInterface resource)
        {
            var info = new StringBuilder();
            info.Append("NetworkInterface: ").Append(resource.Id)
                    .Append("Name: ").Append(resource.Name)
                    .Append("\n\tLocation: ").Append(resource.Location)
                    .Append("\n\tTags: ").Append(FormatDictionary(resource.Tags))
                    .Append("\n\tInternal DNS name label: ").Append(resource.DnsSettings.InternalDnsNameLabel)
                    .Append("\n\tInternal FQDN: ").Append(resource.DnsSettings.InternalFqdn)
                    .Append("\n\tInternal domain name suffix: ").Append(resource.DnsSettings.InternalDomainNameSuffix)
                    .Append("\n\tApplied DNS servers: ").Append(FormatCollection(resource.DnsSettings.AppliedDnsServers))
                    .Append("\n\tDNS server IPs: ");

            // Output dns servers
            foreach (var dnsServerIp in resource.DnsSettings.DnsServers)
            {
                info.Append("\n\t\t").Append(dnsServerIp);
            }

            info.Append("\n\t IP forwarding enabled: ").Append(resource.EnableIPForwarding)
                    .Append("\n\tAccelerated networking enabled: ").Append(resource.EnableAcceleratedNetworking)
                    .Append("\n\tMAC Address:").Append(resource.MacAddress);
            Utilities.Log(info.ToString());
        }
        private static string FormatDictionary(IDictionary<string, string> dictionary)
        {
            if (dictionary == null)
            {
                return string.Empty;
            }

            var outputString = new StringBuilder();

            foreach (var entity in dictionary)
            {
                outputString.AppendLine($"{entity.Key}: {entity.Value}");
            }

            return outputString.ToString();
        }

        private static string FormatCollection(IEnumerable<string> collection)
        {
            return string.Join(", ", collection);
        }
    }
}
