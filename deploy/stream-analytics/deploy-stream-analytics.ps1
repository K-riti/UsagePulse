param(
	[Parameter(Mandatory = $true)]
	[string]$ResourceGroupName,

	[Parameter(Mandatory = $true)]
	[string]$JobName,

	[Parameter(Mandatory = $true)]
	[string]$EventHubNamespace,

	[Parameter(Mandatory = $true)]
	[string]$EventHubName,

	[Parameter(Mandatory = $true)]
	[string]$EventHubPolicyName,

	[Parameter(Mandatory = $true)]
	[string]$EventHubPolicyKey,

	[Parameter(Mandatory = $true)]
	[string]$CosmosAccountName,

	[Parameter(Mandatory = $true)]
	[string]$CosmosDatabaseName,

	[Parameter(Mandatory = $true)]
	[string]$CosmosContainerName,

	[Parameter(Mandatory = $true)]
	[string]$CosmosAccountKey,

	[Parameter(Mandatory = $true)]
	[string]$TransformationQueryPath
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $TransformationQueryPath)) {
	throw "Transformation query file not found: $TransformationQueryPath"
}

$jobExists = az stream-analytics job show --resource-group $ResourceGroupName --name $JobName --query "name" -o tsv 2>$null
if (-not $jobExists) {
	throw "Stream Analytics job '$JobName' does not exist in resource group '$ResourceGroupName'."
}

$query = Get-Content -Path $TransformationQueryPath -Raw

$inputExists = az stream-analytics input show --resource-group $ResourceGroupName --job-name $JobName --name EventHubInput --query "name" -o tsv 2>$null
if ($inputExists) {
	az stream-analytics input update --resource-group $ResourceGroupName --job-name $JobName --name EventHubInput --datasource type=Microsoft.ServiceBus/EventHub namespace=$EventHubNamespace eventHubName=$EventHubName sharedAccessPolicyName=$EventHubPolicyName sharedAccessPolicyKey=$EventHubPolicyKey consumerGroupName='$Default' --serialization type=Json encoding=UTF8 | Out-Null
}
else {
	az stream-analytics input eventhub create --resource-group $ResourceGroupName --job-name $JobName --name EventHubInput --consumer-group-name '$Default' --event-hub-name $EventHubName --service-bus-namespace $EventHubNamespace --shared-access-policy-name $EventHubPolicyName --shared-access-policy-key $EventHubPolicyKey --serialization type=Json encoding=UTF8 | Out-Null
}

$outputExists = az stream-analytics output show --resource-group $ResourceGroupName --job-name $JobName --name CosmosAggregateOutput --query "name" -o tsv 2>$null
if ($outputExists) {
	az stream-analytics output update --resource-group $ResourceGroupName --job-name $JobName --name CosmosAggregateOutput --datasource type=Microsoft.Storage/DocumentDB accountName=$CosmosAccountName accountKey=$CosmosAccountKey database=$CosmosDatabaseName collectionName=$CosmosContainerName documentId=usagepulse-window partitionKey=TenantId --serialization type=Json format=LineSeparated | Out-Null
}
else {
	az stream-analytics output cosmosdb create --resource-group $ResourceGroupName --job-name $JobName --name CosmosAggregateOutput --account-name $CosmosAccountName --account-key $CosmosAccountKey --database $CosmosDatabaseName --container $CosmosContainerName --document-id usagepulse-window --partition-key TenantId --serialization type=Json format=LineSeparated | Out-Null
}

az stream-analytics transformation update --resource-group $ResourceGroupName --job-name $JobName --name Transformation --streaming-units 3 --saql $query | Out-Null

$jobState = az stream-analytics job show --resource-group $ResourceGroupName --name $JobName --query "jobState" -o tsv
if ($jobState -ne "Running") {
	az stream-analytics job start --resource-group $ResourceGroupName --name $JobName --output-start-mode JobStartTime | Out-Null
}

Write-Host "Stream Analytics job '$JobName' configured. Current state: $jobState"
