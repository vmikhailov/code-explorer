resource "aws_s3_bucket" "this" {
  bucket = "${var.name}-bucket"
  tags   = var.tags
}

resource "aws_sns_topic" "notifications" {
  name = "${var.name}-notifications"
  tags = var.tags
}
