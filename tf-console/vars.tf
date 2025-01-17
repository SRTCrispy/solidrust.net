variable "env" {
  description = "The name of the environment"
}

variable "project" {
  description = "The name of the project"
  default = "solidrust"
}

variable "bucket" {
  description = "The name of the bucket."
  type        = string
}

variable "region" {
  description = "(Optional) If specified, the AWS region this bucket should reside in. Otherwise, the region used by the callee."
  type        = string
  default     = "us-west-2"
}

variable "domain_name" {
  description = "The domain name for the bucket."
  type        = string
}

variable "vpc_region_us_west_2_prefix" {
  description = "the network CIDR prefix for the VPC"
}

variable "vpc_region_us_west_1_prefix" {
  description = "the network CIDR prefix for the VPC"
}

variable "vpc_region_us_east_1_prefix" {
  description = "the network CIDR prefix for the VPC"
}

variable "vpc_region_us_east_2_prefix" {
  description = "the network CIDR prefix for the VPC"
}

variable "vpc_region_ap_southeast_2_prefix" {
  description = "the network CIDR prefix for the VPC"
}
