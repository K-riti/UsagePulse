output "application_insights_connection_string" {
  value = azurerm_application_insights.usagepulse.connection_string
}

output "eventhub_namespace" {
  value = azurerm_eventhub_namespace.usagepulse.name
}

output "servicebus_namespace" {
  value = azurerm_servicebus_namespace.usagepulse.name
}

output "cosmos_endpoint" {
  value = azurerm_cosmosdb_account.usagepulse.endpoint
}

output "adx_cluster_uri" {
  value = azurerm_kusto_cluster.usagepulse.uri
}

output "aks_cluster_name" {
  value = azurerm_kubernetes_cluster.usagepulse.name
}

output "operations_action_group_id" {
  value = azurerm_monitor_action_group.operations.id
}
