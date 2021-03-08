provider "azurerm" {
  features {}
  subscription_id = var.subscription

}

resource "azurerm_resource_group" "main" {
  name     = "${var.prefix}-rg"
  location = var.location
}

resource "azurerm_storage_account" "main" {
  name                     = "${var.prefix}storageacct"
  resource_group_name      = azurerm_resource_group.main.name
  location                 = azurerm_resource_group.main.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
}

resource "azurerm_application_insights" "main" {
  name                = "${var.prefix}-appinsights"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  application_type    = "web"
}

resource "azurerm_app_service_plan" "main" {
  name                = "${var.prefix}-asp"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  kind                = "FunctionApp"
  
  sku {
    tier = "Dynamic"
    size = "Y1"
  }
}

resource "azurerm_function_app" "main" {
  name                      = "${var.prefix}-func"
  resource_group_name       = azurerm_resource_group.main.name
  location                  = azurerm_resource_group.main.location
  app_service_plan_id       = azurerm_app_service_plan.main.id
  storage_account_name      = azurerm_storage_account.main.name
  storage_account_access_key = azurerm_storage_account.main.primary_access_key
  version                   = "~3"
  https_only                = true
    
  site_config {
    ftps_state          = "Disabled"
    use_32_bit_worker_process = true
  }

  identity {
    type = "SystemAssigned"
  }

  app_settings = {
    AppInsights_InstrumentationKey = azurerm_application_insights.main.instrumentation_key
    SERVICEBUS_CONNECTION_STRING = azurerm_servicebus_queue_authorization_rule.main.primary_connection_string
    Vault_URI = azurerm_key_vault.main.vault_uri
  }
}

resource "azurerm_servicebus_namespace" "main" {
  name                = "${var.prefix}sb"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "Standard"

}

resource "azurerm_servicebus_queue" "main" {
  name                = "whosoff"
  resource_group_name = azurerm_resource_group.main.name
  namespace_name      = azurerm_servicebus_namespace.main.name

  enable_partitioning = false
  default_message_ttl = "PT60S"
  dead_lettering_on_message_expiration = true
  requires_duplicate_detection = true
  duplicate_detection_history_time_window = "PT10S"
}

resource "azurerm_servicebus_queue_authorization_rule" "main" {
  name                = "SendAndListen"
  namespace_name      = azurerm_servicebus_namespace.main.name
  queue_name          = azurerm_servicebus_queue.main.name
  resource_group_name = azurerm_resource_group.main.name

  listen = true
  send   = true
  manage = false
}

data "azurerm_client_config" "current" {}

resource "azurerm_key_vault" "main" {
  name                        = "${var.prefix}-kv"
  location                    = azurerm_resource_group.main.location
  resource_group_name         = azurerm_resource_group.main.name
  enabled_for_disk_encryption = true
  tenant_id                   = data.azurerm_client_config.current.tenant_id
  soft_delete_retention_days  = 7
  purge_protection_enabled    = false

  sku_name = "standard"

  
}

resource "azurerm_key_vault_access_policy" "main" {
  key_vault_id = azurerm_key_vault.main.id
  tenant_id    = data.azurerm_client_config.current.tenant_id
  object_id    = azurerm_function_app.main.identity[0].principal_id

  secret_permissions = [
    "get",
    "set"
  ]
}