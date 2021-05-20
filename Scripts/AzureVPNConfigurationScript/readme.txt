Install-Module -Name Az -AllowClobber -Scope CurrentUser 
Import-Module Az.Accounts 
Connect-AzAccount 
Set-AzContext -Subscription "DataBox_Edge_Test" 

To connect to fairfax:
Connect-AzAccount -Environment AzureUSGovernment

P2S vs S2S:

This deployment script by default sets up things for P2S VPN. If the intended deployment is for S2S VPN, please pass "S2S" switch to AzDeployVPN.ps1 script.

*****Make sure to change the values on parameters.json or parameters-p2s.json before deployment.******

Start Deployment:
.\AzDeployVpn.ps1 -Location eastus -AzureAppRuleFilePath "appRule.json" -AzureIPRangesFilePath "ServiceTags_Public_20191216.json"  -ResourceGroupName "devtestrg4" -AzureDeploymentName "dbetestdeployment20" -NetworkRuleCollectionName "testnrc20" -Priority 115 -AppRuleCollectionName "testarc20"


To debug failures:

Get-AzResourceGroupDeployment -DeploymentName $deploymentName -ResourceGroupName $ResourceGroupName

Get-AzResourceGroupDeploymentOperation -ResourceGroupName $ResourceGroupName -DeploymentName $AzureDeploymentName
