"""Create and manage virtual machines with managed disks.

This script expects that the following environment vars are set:

AZURE_TENANT_ID: your Azure Active Directory tenant id or domain
AZURE_CLIENT_ID: your Azure Active Directory Application Client ID
AZURE_CLIENT_SECRET: your Azure Active Directory Application Secret
AZURE_SUBSCRIPTION_ID: your Azure Subscription Id
AZURE_RESOURCE_LOCATION: your resource location
ARM_ENDPOINT: your cloud's resource manager endpoint
VHD_FILE_PATH: the path to the vhd for the vm
ADDRESS_PREFIXES: the address prefix
PRIVATE_IP_ADDRESS: the private ip address
"""
import logging
import os
import random
import traceback
import uuid
import sys

from azure.common.client_factory import get_client_from_cli_profile
from azure.common.credentials import ServicePrincipalCredentials
from azure.mgmt.compute import ComputeManagementClient
from azure.mgmt.compute.models import DiskCreateOption
from azure.mgmt.network import NetworkManagementClient
from azure.mgmt.resource import ResourceManagementClient
from azure.mgmt.storage import StorageManagementClient
from azure.profiles import KnownProfiles
from azure.storage.blob import PublicAccess, BlockBlobService, PageBlobService
from haikunator import Haikunator
from msrestazure.azure_active_directory import UserPassCredentials
from msrestazure.azure_cloud import get_cloud_from_metadata_endpoint
from msrestazure.azure_exceptions import CloudError
from urllib.parse import urlparse

haikunator = Haikunator()

# Azure Datacenter
LOCATION = os.environ['AZURE_RESOURCE_LOCATION']
ADDRESSPREFIXES = os.environ['ADDRESS_PREFIXES']
PRIVATEIP = os.environ['PRIVATE_IP_ADDRESS']

# Custom Image File Path
vhd_file_path = os.environ['VHD_FILE_PATH']
vhd_file_name = 'ubuntu13.vhd'
container_name = 'vmimages'

# Resource Group
postfix = random.randint(100, 500)
GROUP_NAME ='azure-sample-group-virtual-machines{}'.format(postfix)

# Network
VNET_NAME = 'azure-sample-vnet{}'.format(postfix)
SUBNET_NAME = 'azure-sample-subnet{}'.format(postfix)

# VM
OS_DISK_NAME = 'azure-sample-osdisk{}'.format(postfix)
STORAGE_ACCOUNT_NAME = "aseaccount"

IP_CONFIG_NAME = 'azure-sample-ip-config{}'.format(postfix)
NIC_NAME = 'azure-sample-nic{}'.format(postfix)
USERNAME = 'userlogin'
PASSWORD = str(uuid.uuid4())
VM_NAME = 'VmName{}'.format(postfix)

VM_REFERENCE = {
    'linux': {
        'publisher': 'Canonical',
        'offer': 'UbuntuServer',
        'sku': '16.04-LTS',
        'version': 'latest'
    },
    'windows': {
        'publisher': 'MicrosoftWindowsServer',
        'offer': 'WindowsServer',
        'sku': '2012-R2-Datacenter',
        'version': 'latest'
    }
}

# Azure clients
resource_client = None
compute_client = None
storage_client = None
network_client = None
mystack_cloud = None

def authenticate_service_principal_token():
    #
    # Create all clients with an Application (service principal) token provider
    #

    global mystack_cloud, resource_client, compute_client, storage_client, network_client

    mystack_cloud = get_cloud_from_metadata_endpoint(
        os.environ['ARM_ENDPOINT'])

    subscription_id = os.environ['AZURE_SUBSCRIPTION_ID']
    credentials = ServicePrincipalCredentials(
        client_id=os.environ['AZURE_CLIENT_ID'],
        secret=os.environ['AZURE_CLIENT_SECRET'],
        tenant=os.environ['AZURE_TENANT_ID'],
        cloud_environment=mystack_cloud
    )

    # By Default, use AzureStack supported profile
    KnownProfiles.default.use(KnownProfiles.v2019_03_01_hybrid)

    resource_client = ResourceManagementClient(
        credentials, subscription_id, base_url=mystack_cloud.endpoints.resource_manager)
    compute_client = ComputeManagementClient(
        credentials, subscription_id, base_url=mystack_cloud.endpoints.resource_manager)
    storage_client = StorageManagementClient(
        credentials, subscription_id, base_url=mystack_cloud.endpoints.resource_manager)
    network_client = NetworkManagementClient(
        credentials, subscription_id, base_url=mystack_cloud.endpoints.resource_manager)
    
def authenticate_cli():
    #
    # Create all clients with Azure CLI's active subscription
    #

    global mystack_cloud, resource_client, compute_client, storage_client, network_client

    mystack_cloud = get_cloud_from_metadata_endpoint(
        os.environ['ARM_ENDPOINT'])

    resource_client = get_client_from_cli_profile(ResourceManagementClient)
    compute_client = get_client_from_cli_profile(ComputeManagementClient)
    storage_client = get_client_from_cli_profile(StorageManagementClient)
    network_client = get_client_from_cli_profile(NetworkManagementClient)

def run_example():
    """Virtual Machine management example."""

    global mystack_cloud, resource_client, compute_client, storage_client, network_client

    arm_url = mystack_cloud.endpoints.resource_manager
    storage_endpoint_suffix = arm_url.replace(arm_url.split(".")[0], "").strip('./')
    logging.basicConfig(level=logging.ERROR)

    ###########
    # Prepare #
    ###########
    # Create Resource group
    print('\nCreate Resource Group')
    resource_client.resource_groups.create_or_update(
        GROUP_NAME, {'location': LOCATION})

    try:
        # Create a storage account
        print('\nCreate a storage account')
        storage_async_operation = storage_client.storage_accounts.create(
            GROUP_NAME,
            STORAGE_ACCOUNT_NAME,
            {
                'sku': {'name': 'standard_lrs'},
                'kind': 'storage',
                'location': LOCATION
            }
        )
        storage_async_operation.wait()

        # Upload VHD to blob storage
        ############################

        # Get storage account keys
        storage_keys = storage_client.storage_accounts.list_keys(
            GROUP_NAME, STORAGE_ACCOUNT_NAME)

        connection_string = (
            'DefaultEndpointsProtocol={protocol};'
            'AccountName={account_name};'
            'AccountKey={account_key};'
            'EndpointSuffix={blob_endpoint};'
        ).format(
            protocol=urlparse(arm_url).scheme,
            account_name=STORAGE_ACCOUNT_NAME,
            account_key=storage_keys.keys[0].value,
            blob_endpoint=storage_endpoint_suffix,
        )

        # Create a container called 'vmimages'.
        # Set the permission so the blobs are public.
        # Upload the created file, use vhd_file_name for the blob name.
        print("\nUploading to Azure Stack Storage as blob:\n\t" + vhd_file_name)
        blob_client = PageBlobService(connection_string=connection_string)
        container_client = blob_client.create_container(container_name, public_access='container', fail_on_exist=False)
        blob_client.create_blob_from_path(container_name, vhd_file_name, vhd_file_path)

        # List the blobs in the container
        print("\nListing blobs...")
        blob_list = blob_client.list_blobs(container_name)
        for blob in blob_list:
            print("\t" + blob.name)

        # Construct the blob uri so it can be used to create the VM image
        blob_uri = urlparse(arm_url).scheme + '://' + blob_client.primary_endpoint + '/' + container_name + '/' + vhd_file_name

        # Create image from the VHD
        async_creation = compute_client.images.create_or_update(
            GROUP_NAME,
            'UbuntuImage',
            {
                'location': LOCATION,
                'storage_profile': {
                    'os_disk': {
                            'os_type': 'Linux',
                            'os_state': "Generalized",
                            'blob_uri': blob_uri,
                            'caching': "ReadWrite",
                    }
                }
            }
        )

        image_resource = async_creation.result()
        print("\nVM image resource id: \n\t", image_resource.id)

        # Create a NIC
        nic = create_nic(network_client)

        #############
        # VM Sample #
        #############

        # Create Linux VM
        print('\nCreating Linux Virtual Machine')

        vm_parameters = create_vm_parameters(
            nic.id, image_resource.id)
        async_vm_creation = compute_client.virtual_machines.create_or_update(
            GROUP_NAME, VM_NAME, vm_parameters)
        async_vm_creation.wait()

        # Tag the VM
        print('\nTag Virtual Machine')
        async_vm_update = compute_client.virtual_machines.create_or_update(
            GROUP_NAME,
            VM_NAME,
            {
                'location': LOCATION,
                'tags': {
                    'who-rocks': 'python',
                    'where': 'on azure'
                }
            }
        )
        async_vm_update.wait()

        # Create managed data disk
        print('\nCreate (empty) managed Data Disk')
        async_disk_creation = compute_client.disks.create_or_update(
            GROUP_NAME,
            'mydatadisk1',
            {
                'location': LOCATION,
                'disk_size_gb': 1,
                'creation_data': {
                    'create_option': DiskCreateOption.empty
                }
            }
        )
        data_disk = async_disk_creation.result()

        # Get the virtual machine by name
        print('\nGet Virtual Machine by Name')
        virtual_machine = compute_client.virtual_machines.get(
            GROUP_NAME,
            VM_NAME
        )

        # Attach data disk
        print('\nAttach Data Disk')
        virtual_machine.storage_profile.data_disks.append({
            'lun': 12,
            'name': 'mydatadisk1',
            'create_option': DiskCreateOption.attach,
            'managed_disk': {
                'id': data_disk.id
            }
        })
        async_disk_attach = compute_client.virtual_machines.create_or_update(
            GROUP_NAME,
            virtual_machine.name,
            virtual_machine
        )
        async_disk_attach.wait()

        # Detach data disk
        print('\nDetach Data Disk')
        data_disks = virtual_machine.storage_profile.data_disks
        data_disks[:] = [disk for disk in data_disks if disk.name != 'mydatadisk1']
        async_vm_update = compute_client.virtual_machines.create_or_update(
            GROUP_NAME,
            VM_NAME,
            virtual_machine
        )
        virtual_machine = async_vm_update.result()

        # Deallocating the VM (in preparation for a disk resize)
        print('\nDeallocating the VM (to prepare for a disk resize)')
        async_vm_deallocate = compute_client.virtual_machines.deallocate(
            GROUP_NAME, VM_NAME)
        async_vm_deallocate.wait()

        # Increase OS disk size by 10 GB
        print('\nUpdate OS disk size')
        os_disk_name = virtual_machine.storage_profile.os_disk.name
        os_disk = compute_client.disks.get(GROUP_NAME, os_disk_name)
        if not os_disk.disk_size_gb:
            print(
                "\tServer is not returning the OS disk size, possible bug in the server?")
            print("\tAssuming that the OS disk size is 30 GB")
            os_disk.disk_size_gb = 30

        os_disk.disk_size_gb += 10

        async_disk_update = compute_client.disks.create_or_update(
            GROUP_NAME,
            os_disk.name,
            os_disk
        )
        async_disk_update.wait()

        # Start the VM
        print('\nStart VM')
        async_vm_start = compute_client.virtual_machines.start(
            GROUP_NAME, VM_NAME)
        async_vm_start.wait()

        # Restart the VM
        print('\nRestart VM')
        async_vm_restart = compute_client.virtual_machines.restart(
            GROUP_NAME, VM_NAME)
        async_vm_restart.wait()

        # Stop the VM
        print('\nStop VM')
        async_vm_stop = compute_client.virtual_machines.power_off(
            GROUP_NAME, VM_NAME)
        async_vm_stop.wait()

        # List VMs in subscription
        print('\nList VMs in subscription')
        for vm in compute_client.virtual_machines.list_all():
            print("\tVM: {}".format(vm.name))

        # List VM in resource group
        print('\nList VMs in resource group')
        for vm in compute_client.virtual_machines.list(GROUP_NAME):
            print("\tVM: {}".format(vm.name))

        # Delete VM
        print('\nDelete VM')
        async_vm_delete = compute_client.virtual_machines.delete(
            GROUP_NAME, VM_NAME)
        async_vm_delete.wait()

        # # Create Windows VM
        # print('\nCreating Windows Virtual Machine')
        # # Recycling NIC of previous VM
        # vm_parameters = create_vm_parameters(
        #     nic.id, VM_REFERENCE['windows'])
        # async_vm_creation = compute_client.virtual_machines.create_or_update(
        #     GROUP_NAME, VM_NAME, vm_parameters)
        # async_vm_creation.wait()
    except CloudError:
        print('A VM operation failed:', traceback.format_exc(), sep='\n')
    except Exception as ex:
        print("Exception details:\n", ex)
    else:
        print('All example operations completed successfully!')
    finally:
        # Delete Resource group and everything in it
        print('\nDelete Resource Group')
        delete_async_operation = resource_client.resource_groups.delete(
            GROUP_NAME)
        delete_async_operation.wait()
        print("\nDeleted: {}".format(GROUP_NAME))

def create_nic(network_client):
    """Create a Network Interface for a VM.
    """
    # Create VNet
    print('\nCreate Vnet')
    async_vnet_creation = network_client.virtual_networks.create_or_update(
        GROUP_NAME,
        VNET_NAME,
        {
            'location': LOCATION,
            'address_space': {
                'address_prefixes': [ADDRESSPREFIXES]
            }
        }
    )
    async_vnet_creation.wait()

    # Create Subnet
    print('\nCreate Subnet')
    async_subnet_creation = network_client.subnets.create_or_update(
        GROUP_NAME,
        VNET_NAME,
        SUBNET_NAME,
        {'address_prefix': ADDRESSPREFIXES}
    )
    subnet_info = async_subnet_creation.result()

    # Create NIC
    print('\nCreate NIC')
    async_nic_creation = network_client.network_interfaces.create_or_update(
        GROUP_NAME,
        NIC_NAME,
        {
            'location': LOCATION,
            'ip_configurations': [{
                'name': IP_CONFIG_NAME,
                'subnet': {
                    'id': subnet_info.id
                },
                'private_ip_allocation_method': network_client.public_ip_addresses.models.IPAllocationMethod.static,
                'private_ip_address': PRIVATEIP
            }]
        }
    )
    return async_nic_creation.result()

def create_vm_parameters(nic_id, image_id):
    """Create the VM parameters structure.
    """

    return {
        'location': LOCATION,
        'os_profile': {
            'computer_name': VM_NAME,
            'admin_username': USERNAME,
            'admin_password': PASSWORD
        },
        'hardware_profile': {
            'vm_size': 'Standard_DS1_v2'
        },
        'storage_profile': {
            'image_reference': {
                'id': image_id
            },
        },
        'network_profile': {
            'network_interfaces': [{
                'id': nic_id,
            }]
        },
    }

if __name__ == "__main__":
    if len(sys.argv) != 2:
        print('Please provide which authentication method to use: cli or token')
        sys.exit()

    if sys.argv[1] == 'cli':
        authenticate_cli()
    elif sys.argv[1] == 'token':
        authenticate_service_principal_token()
    else:
        print('Please provide which authentication method to use: cli or token')
        sys.exit()

    run_example()
