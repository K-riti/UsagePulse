resource "azurerm_monitor_action_group" "operations" {
  name                = "${var.prefix}-ops-ag"
  short_name          = "upops"
  resource_group_name = azurerm_resource_group.usagepulse.name

  email_receiver {
    name                    = "operations-email"
    email_address           = var.operations_alert_email
    use_common_alert_schema = true
  }
}

resource "azurerm_monitor_action_rule_action_group" "operations" {
  name                = "${var.prefix}-ops-route"
  resource_group_name = azurerm_resource_group.usagepulse.name
  scope {
	resource_ids = [azurerm_resource_group.usagepulse.id]
  }
  action_group_id = azurerm_monitor_action_group.operations.id
}

resource "azurerm_monitor_metric_alert" "servicebus_queue_depth" {
  name                = "${var.prefix}-sb-queue-depth"
  resource_group_name = azurerm_resource_group.usagepulse.name
  scopes              = [azurerm_servicebus_namespace.usagepulse.id]
  description         = "Alerts when usage work queue depth exceeds threshold."
  severity            = 2
  frequency           = "PT1M"
  window_size         = "PT5M"

  criteria {
	metric_namespace = "Microsoft.ServiceBus/namespaces"
	metric_name      = "ActiveMessages"
	aggregation      = "Average"
	operator         = "GreaterThan"
	threshold        = var.servicebus_queue_depth_alert_threshold

	dimension {
	  name     = "EntityName"
	  operator = "Include"
	  values   = [azurerm_servicebus_queue.usage_work.name]
	}
  }

  action {
	action_group_id = azurerm_monitor_action_group.operations.id
  }
}

resource "azurerm_monitor_metric_alert" "eventhub_incoming_messages" {
  name                = "${var.prefix}-eh-ingress-volume"
  resource_group_name = azurerm_resource_group.usagepulse.name
  scopes              = [azurerm_eventhub_namespace.usagepulse.id]
  description         = "Alerts when ingress volume exceeds threshold."
  severity            = 3
  frequency           = "PT1M"
  window_size         = "PT5M"

  criteria {
	metric_namespace = "Microsoft.EventHub/namespaces"
	metric_name      = "IncomingMessages"
	aggregation      = "Total"
	operator         = "GreaterThan"
	threshold        = var.eventhub_incoming_messages_alert_threshold
  }

  action {
	action_group_id = azurerm_monitor_action_group.operations.id
  }
}
