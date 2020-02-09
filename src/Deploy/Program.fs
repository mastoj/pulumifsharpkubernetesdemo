module Program

open Pulumi.FSharp
open Pulumi.Azure.Core
open Pulumi.Azure.ContainerService
open Pulumi.Azure.ContainerService.Inputs
open Pulumi.Azure.AD
open Pulumi.Azure.Network
open Pulumi.Random
open Pulumi.Tls

[<RequireQualifiedAccess>]
module Pulumi =
    let createResourceGroup name = ResourceGroup(name)

    let createPassword() =
        RandomPassword("password", 
            RandomPasswordArgs(
                Length = input 20,
                Special = input true
            )
        )

    let createPrivateKey() =
        PrivateKey("ssh-key",
            PrivateKeyArgs(
                Algorithm = input "RSA",
                RsaBits = input 4096
            ))

    let createApplication name = Application(name)

    let createServicePrincipal name (app: Application) =
        ServicePrincipal(name,
            ServicePrincipalArgs(ApplicationId = io app.ApplicationId))

    let createServicePrincipalPassword name (password: RandomPassword) (servicePrincipal: ServicePrincipal) =
        ServicePrincipalPassword(name,
            ServicePrincipalPasswordArgs(
                ServicePrincipalId = io servicePrincipal.Id,
                Value = io password.Result,
                EndDate = input "2099-01-01T00:00:00Z"
            ))

    let createVnet name (resourceGroup: ResourceGroup) =
        VirtualNetwork(name,
            VirtualNetworkArgs(
                ResourceGroupName = io resourceGroup.Name,
                AddressSpaces = inputList [ input "10.2.0.0/16" ]
            ))

    let createSubnet name (vnet: VirtualNetwork) (resourceGroup: ResourceGroup) =
        Subnet(name, 
            SubnetArgs(
                ResourceGroupName = io resourceGroup.Name,
                VirtualNetworkName = io vnet.Name,
                AddressPrefix = input "10.2.1.0/24"
            ))

    let createCluster name (subnet: Subnet) (privateKey: PrivateKey) (app: Application) (servicePrincipalPassword: ServicePrincipalPassword) (resourceGroup: ResourceGroup) nodeCount =
        let defaultNodePoolArgs =
            KubernetesClusterDefaultNodePoolArgs(
                Name = input "aksagentpool",
                NodeCount = input nodeCount,
                VmSize = input "Standard_B2s",
                OsDiskSizeGb = input 30,
                VnetSubnetId = io subnet.Id
            )

        let linuxProfileArgs = 
            let keyArgs = KubernetesClusterLinuxProfileSshKeyArgs(KeyData = io privateKey.PublicKeyOpenssh)
            KubernetesClusterLinuxProfileArgs(
                AdminUsername = input "aksuser",
                SshKey = input keyArgs
            )

        let servicePrincipalArgs =
            KubernetesClusterServicePrincipalArgs(
                ClientId = io app.ApplicationId,
                ClientSecret = io servicePrincipalPassword.Value
            )

        let rbacArgs =
            KubernetesClusterRoleBasedAccessControlArgs(Enabled = input true)

        let networkProfileArgs =
            KubernetesClusterNetworkProfileArgs(
                NetworkPlugin = input "azure",
                DnsServiceIp = input "10.2.2.254",
                ServiceCidr = input "10.2.2.0/24",
                DockerBridgeCidr = input "172.17.0.1/16"
            )

        KubernetesCluster(name,
            KubernetesClusterArgs(
                ResourceGroupName = io resourceGroup.Name,
                DefaultNodePool = input defaultNodePoolArgs,
                DnsPrefix = input "pulfskubaks",
                LinuxProfile = input linuxProfileArgs,
                ServicePrincipal = input servicePrincipalArgs,
                KubernetesVersion = input "1.16.4",
                RoleBasedAccessControl = input rbacArgs,
                NetworkProfile = input networkProfileArgs
            ))

let infra () =
    let resourceGroup = Pulumi.createResourceGroup "pulfskub"
    let password = Pulumi.createPassword()
    let privateKey = Pulumi.createPrivateKey()
    let app = Pulumi.createApplication "pulfskubaks"
    let servicePrincipal = Pulumi.createServicePrincipal "pulfskubsp" app
    let servicePrincipalPassword = Pulumi.createServicePrincipalPassword "pulfskubsppassword" password servicePrincipal
    let vnet = Pulumi.createVnet "pulfskubvnet" resourceGroup
    let subnet = Pulumi.createSubnet "pulfskubsubnet" vnet resourceGroup

    let cluster =
        Pulumi.createCluster
            "pulfskubcluster"
            subnet
            privateKey
            app
            servicePrincipalPassword
            resourceGroup
            3

    // Export the kubeconfig string for the storage account
    dict [("kubeconfig", cluster.KubeConfigRaw :> obj)]

[<EntryPoint>]
let main _ =
  Deployment.run infra
