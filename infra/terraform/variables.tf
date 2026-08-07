variable "prefix" {
  description = "Project prefix used for resource naming."
  type        = string
  default     = "usagepulse"
}

variable "resource_group_name" {
  description = "Azure resource group for UsagePulse resources."
  type        = string
  default     = "rg-usagepulse"
}

variable "location" {
  description = "Azure region."
  type        = string
  default     = "eastus2"
}

variable "servicebus_queue_depth_alert_threshold" {
  description = "Average active message count threshold for usage work queue alerts."
  type        = number
  default     = 1000
}

variable "eventhub_incoming_messages_alert_threshold" {
  description = "Total incoming Event Hubs messages threshold for ingress alerts."
  type        = number
  default     = 100000
}

variable "operations_alert_email" {
  description = "Operations team email that receives Azure Monitor alerts."
  type        = string
  default     = "platform-ops@example.com"
}
