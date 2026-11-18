variable "dataset_name" {
  type    = string
  default = "player360"
}

locals {
  bucket_name = "ds-${var.dataset_name}"
}

resource "aws_s3_bucket" "dataset" {
  bucket = local.bucket_name
}

output "dataset_bucket_arn" {
  value = aws_s3_bucket.dataset.arn
}
