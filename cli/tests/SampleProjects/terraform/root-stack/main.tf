provider "aws" {
  region = var.region
}

variable "region" {
  type    = string
  default = "us-east-1"
}

module "app" {
  source = "../reusable-module"
  name   = "demo"
}

resource "aws_sqs_queue" "jobs" {
  name = "demo-jobs"
}
