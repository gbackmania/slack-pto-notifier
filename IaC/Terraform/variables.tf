variable "prefix" {
  type = string  
  description = "The prefix used for all resources in this example"
}

variable "location" {
  type = string
  description = "The Azure location where all resources in this example should be created"
}

variable subscription {
  type = string
  description = "The Azure subscription id"
  default = "00000000-0000-0000-0000-000000000000"
}