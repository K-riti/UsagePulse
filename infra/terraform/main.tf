terraform {
  required_version = ">= 1.6.0"
  required_providers {
	azurerm = {
	  source  = "hashicorp/azurerm"
	  version = "~> 3.117"
	}
  }
}

provider "azurerm" {
  features {}
}

resource "azurerm_resource_group" "usagepulse" {
  name     = var.resource_group_name
  location = var.location
}

resource "azurerm_log_analytics_workspace" "usagepulse" {
  name                = "${var.prefix}-law"
  location            = azurerm_resource_group.usagepulse.location
  resource_group_name = azurerm_resource_group.usagepulse.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
}

resource "azurerm_application_insights" "usagepulse" {
  name                = "${var.prefix}-appi"
  location            = azurerm_resource_group.usagepulse.location
  resource_group_name = azurerm_resource_group.usagepulse.name
  workspace_id        = azurerm_log_analytics_workspace.usagepulse.id
  application_type    = "web"
}

resource "azurerm_storage_account" "usagepulse" {
  name                     = replace("${var.prefix}st", "-", "")
  resource_group_name      = azurerm_resource_group.usagepulse.name
  location                 = azurerm_resource_group.usagepulse.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
}

resource "azurerm_eventhub_namespace" "usagepulse" {
  name                = "${var.prefix}-ehn"
  location            = azurerm_resource_group.usagepulse.location
  resource_group_name = azurerm_resource_group.usagepulse.name
  sku                 = "Standard"
  capacity            = 2
}

resource "azurerm_eventhub" "usage_events" {
  name                = "usage-events"
  namespace_name      = azurerm_eventhub_namespace.usagepulse.name
  resource_group_name = azurerm_resource_group.usagepulse.name
  partition_count     = 8
  message_retention   = 1
}

resource "azurerm_servicebus_namespace" "usagepulse" {
  name                = "${var.prefix}-sbn"
  location            = azurerm_resource_group.usagepulse.location
  resource_group_name = azurerm_resource_group.usagepulse.name
  sku                 = "Premium"
  capacity            = 1
}

resource "azurerm_servicebus_queue" "usage_work" {
  name         = "usage-events-work"
  namespace_id = azurerm_servicebus_namespace.usagepulse.id
}

resource "azurerm_servicebus_queue" "usage_dlq" {
  name         = "usage-events-dlq"
  namespace_id = azurerm_servicebus_namespace.usagepulse.id
}

resource "azurerm_cosmosdb_account" "usagepulse" {
  name                = "${var.prefix}-cosmos"
  location            = azurerm_resource_group.usagepulse.location
  resource_group_name = azurerm_resource_group.usagepulse.name
  offer_type          = "Standard"
  kind                = "GlobalDocumentDB"

  consistency_policy {
	consistency_level = "Session"
  }

  geo_location {
	location          = azurerm_resource_group.usagepulse.location
	failover_priority = 0
  }
}

resource "azurerm_cosmosdb_sql_database" "usagepulse" {
  name                = "usagepulse"
  resource_group_name = azurerm_resource_group.usagepulse.name
  account_name        = azurerm_cosmosdb_account.usagepulse.name
}

resource "azurerm_cosmosdb_sql_container" "usage_events" {
  name                  = "usage-events"
  resource_group_name   = azurerm_resource_group.usagepulse.name
  account_name          = azurerm_cosmosdb_account.usagepulse.name
  database_name         = azurerm_cosmosdb_sql_database.usagepulse.name
  partition_key_path    = "/tenantId"
  partition_key_version = 2
}

resource "azurerm_cosmosdb_sql_container" "usage_idempotency" {
  name                  = "usage-idempotency"
  resource_group_name   = azurerm_resource_group.usagepulse.name
  account_name          = azurerm_cosmosdb_account.usagepulse.name
  database_name         = azurerm_cosmosdb_sql_database.usagepulse.name
  partition_key_path    = "/id"
  partition_key_version = 2
}

resource "azurerm_cosmosdb_sql_container" "usage_stream_window" {
  name                  = "usage-stream-window"
  resource_group_name   = azurerm_resource_group.usagepulse.name
  account_name          = azurerm_cosmosdb_account.usagepulse.name
  database_name         = azurerm_cosmosdb_sql_database.usagepulse.name
  partition_key_path    = "/TenantId"
  partition_key_version = 2
}

resource "azurerm_kusto_cluster" "usagepulse" {
  name                = "${var.prefix}-adx"
  location            = azurerm_resource_group.usagepulse.location
  resource_group_name = azurerm_resource_group.usagepulse.name
  sku {
	name     = "Dev(No SLA)_Standard_D11_v2"
	capacity = 1
  }
}

resource "azurerm_kusto_database" "usagepulse" {
  name                = "usagepulse"
  resource_group_name = azurerm_resource_group.usagepulse.name
  location            = azurerm_resource_group.usagepulse.location
  cluster_name        = azurerm_kusto_cluster.usagepulse.name
  hot_cache_period    = "P7D"
  soft_delete_period  = "P31D"
}

resource "azurerm_stream_analytics_job" "usagepulse" {
  name                                     = "${var.prefix}-asa"
  location                                 = azurerm_resource_group.usagepulse.location
  resource_group_name                      = azurerm_resource_group.usagepulse.name
  compatibility_level                      = "1.2"
  data_locale                              = "en-US"
  events_late_arrival_max_delay_in_seconds = 10
  events_out_of_order_max_delay_in_seconds = 5
  output_error_policy                      = "Drop"
  streaming_units                          = 3
}

resource "azurerm_kubernetes_cluster" "usagepulse" {
  name                = "${var.prefix}-aks"
  location            = azurerm_resource_group.usagepulse.location
  resource_group_name = azurerm_resource_group.usagepulse.name
  dns_prefix          = "${var.prefix}-aks"

  default_node_pool {
	name       = "system"
	node_count = 2
	vm_size    = "Standard_D4s_v5"
  }

  identity {
	type = "SystemAssigned"
  }

  oms_agent {
	log_analytics_workspace_id = azurerm_log_analytics_workspace.usagepulse.id
  }
}
