output "bucket_id" {
  value = aws_s3_bucket.this.id
}

output "topic_arn" {
  value = aws_sns_topic.notifications.arn
}
