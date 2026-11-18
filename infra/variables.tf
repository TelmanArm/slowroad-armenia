variable "aws_region" {
  type    = string
  default = "eu-north-1"
}

variable "availability_zone" {
  type    = string
  default = "eu-north-1a"
}

variable "project" {
  type    = string
  default = "slowroad"
}

variable "environment" {
  type    = string
  default = "dev"
}

variable "instance_name" {
  type    = string
  default = "slowroad-app"
}

variable "instance_type" {
  type    = string
  default = "t3.micro"
}

variable "key_name" {
  type    = string
  default = "web_server_key"
}

variable "ssh_allowed_cidr" {
  type        = string
  description = "Your IP for SSH, e.g. 1.2.3.4/32"
  default     = "0.0.0.0/0"
}
variable "backup_bucket" {
  type        = string
  description = "Existing S3 bucket holding Postgres dumps"
  default     = "slowroad-backups-449916777624-eu-north-1-an"
}
