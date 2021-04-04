using System;
using System.IO;
using System.Security.Cryptography;
using Pulumi;
using Azure = Pulumi.Azure;
class MyStack : Stack
{
    public MyStack()
    {
        // Create an Azure Resource Group
        var resourceGroup = new Azure.Core.ResourceGroup("SampleVMs");
        var exampleNetworkSecurityGroup = new Azure.Network.NetworkSecurityGroup("exampleNetworkSecurityGroup", new Azure.Network.NetworkSecurityGroupArgs
        {
            Location = resourceGroup.Location,
            ResourceGroupName = resourceGroup.Name,
        });
        var exampleNetworkSecurityRuleInbound = new Azure.Network.NetworkSecurityRule("exampleNetworkSecurityRule", new Azure.Network.NetworkSecurityRuleArgs
        {
            Priority = 100,
            Direction = "Inbound",
            Access = "Allow",
            Protocol = "*",
            SourcePortRange = "*",
            DestinationPortRange = "22",
            SourceAddressPrefix = "*",
            DestinationAddressPrefix = "*",
            ResourceGroupName = resourceGroup.Name,
            NetworkSecurityGroupName = exampleNetworkSecurityGroup.Name,
        });
        var exampleVirtualNetwork = new Azure.Network.VirtualNetwork("testVMSVirtualNetwork", new Azure.Network.VirtualNetworkArgs
        {
            AddressSpaces = 
            {
                "10.0.0.0/16",
            },
            Location = resourceGroup.Location,
            ResourceGroupName = resourceGroup.Name,
            Subnets = 
            {
                new Azure.Network.Inputs.VirtualNetworkSubnetArgs
                {
                    Name = "subnet3",
                    AddressPrefix = "10.0.3.0/24",
                    SecurityGroup = exampleNetworkSecurityGroup.Id,
                },
            }
        });
        
        for (int i = 0; i < 2; i++)
        {
            var publicIP = new Azure.Network.PublicIp("publicIP"+ i, new Azure.Network.PublicIpArgs
            {
                ResourceGroupName = resourceGroup.Name,
                AllocationMethod = "Dynamic"
            });
            var exampleNetworkInterface = new Azure.Network.NetworkInterface("exampleNetworkInterface"+ i, new Azure.Network.NetworkInterfaceArgs
            {
                Location = resourceGroup.Location,
                ResourceGroupName = resourceGroup.Name,
                IpConfigurations = 
                {
                    new Azure.Network.Inputs.NetworkInterfaceIpConfigurationArgs
                    {
                        Name = "internal",
                        SubnetId = exampleVirtualNetwork.Subnets.First().Apply(x => x.Id),
                        PrivateIpAddressAllocation = "Dynamic",
                        PublicIpAddressId = publicIP.Id
                    },
                },
            });
            var mainVirtualMachine = new Azure.Compute.VirtualMachine("mainVirtualMachine" + i, new Azure.Compute.VirtualMachineArgs
            {
                Name = "TestVM" + i,
                Location = resourceGroup.Location,
                ResourceGroupName = resourceGroup.Name,
                NetworkInterfaceIds = 
                {
                    exampleNetworkInterface.Id,
                },
                VmSize = "Standard_B1s",
                StorageImageReference = new Azure.Compute.Inputs.VirtualMachineStorageImageReferenceArgs
                {
                    Publisher = "Canonical",
                    Offer = "UbuntuServer",
                    Sku = "18.04-LTS",
                    Version = "latest",
                },
                StorageOsDisk = new Azure.Compute.Inputs.VirtualMachineStorageOsDiskArgs
                {
                    Name = "myosdisk" + i,
                    Caching = "ReadWrite",
                    CreateOption = "FromImage",
                    ManagedDiskType = "Standard_LRS",
                },
                
                OsProfileLinuxConfig = new Azure.Compute.Inputs.VirtualMachineOsProfileLinuxConfigArgs
                {
                    DisablePasswordAuthentication = false,
                },
                OsProfile = new Azure.Compute.Inputs.VirtualMachineOsProfileArgs
                {
                    AdminUsername = "randomAdminUser",
                    AdminPassword = Password.Generate(32, 12),
                    ComputerName = "testpc" + i,
                    CustomData = File.ReadAllText(Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile) + "\\Downloads\\AWS EastUS Policy.yaml")
                }
            });
        }
        var @internal = new Azure.Network.Subnet("internal", new Azure.Network.SubnetArgs
        {
            ResourceGroupName = resourceGroup.Name,
            VirtualNetworkName = exampleVirtualNetwork.Name,
            AddressPrefixes = 
            {
                "10.0.2.0/24",
            },
        });
    }

    [Output]
    public Output<string> ConnectionString { get; set; }
}

public static class Password
{
    private static readonly char[] Punctuations = "!@#$%^&*()_-+=[{]};:>|./?".ToCharArray();

    public static string Generate(int length, int numberOfNonAlphanumericCharacters)
    {
        if (length < 1 || length > 128)
        {
            throw new ArgumentException(nameof(length));
        }

        if (numberOfNonAlphanumericCharacters > length || numberOfNonAlphanumericCharacters < 0)
        {
            throw new ArgumentException(nameof(numberOfNonAlphanumericCharacters));
        }

        using (var rng = RandomNumberGenerator.Create())
        {
            var byteBuffer = new byte[length];

            rng.GetBytes(byteBuffer);

            var count = 0;
            var characterBuffer = new char[length];

            for (var iter = 0; iter < length; iter++)
            {
                var i = byteBuffer[iter] % 87;

                if (i < 10)
                {
                    characterBuffer[iter] = (char)('0' + i);
                }
                else if (i < 36)
                {
                    characterBuffer[iter] = (char)('A' + i - 10);
                }
                else if (i < 62)
                {
                    characterBuffer[iter] = (char)('a' + i - 36);
                }
                else
                {
                    characterBuffer[iter] = Punctuations[i - 62];
                    count++;
                }
            }

            if (count >= numberOfNonAlphanumericCharacters)
            {
                return new string(characterBuffer);
            }

            int j;
            var rand = new Random();

            for (j = 0; j < numberOfNonAlphanumericCharacters - count; j++)
            {
                int k;
                do
                {
                    k = rand.Next(0, length);
                }
                while (!char.IsLetterOrDigit(characterBuffer[k]));

                characterBuffer[k] = Punctuations[rand.Next(0, Punctuations.Length)];
            }

            return new string(characterBuffer);
        }
    }
}
