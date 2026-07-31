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
