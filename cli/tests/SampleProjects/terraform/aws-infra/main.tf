terraform {
  required_version = ">= 1.5.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

provider "aws" {
  region = "us-east-1"
}

variable "workgroup_name" {
  type    = string
  default = "default-wg"
}

variable "allowed_origin" {
  type    = string
  default = "*"
}

resource "aws_s3_bucket" "assets" {
  bucket = "example-assets-bucket"
}

resource "aws_athena_workgroup" "team" {
  name = var.workgroup_name
}

resource "aws_lambda_function" "api" {
  function_name = "p360-api"
  role          = "arn:aws:iam::123456789012:role/lambda"
  handler       = "index.handler"
  runtime       = "python3.12"

  environment {
    variables = {
      ATHENA_WG      = aws_athena_workgroup.team.name
      ALLOWED_ORIGIN = var.allowed_origin
      ASSETS_BUCKET  = aws_s3_bucket.assets.bucket
    }
  }
}

output "athena_workgroup_name" {
  value = aws_athena_workgroup.team.name
}

output "lambda_arn" {
  value = aws_lambda_function.api.arn
}
