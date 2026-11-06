variable "name" {
  type        = string
  description = "Name prefix for created resources."
}

variable "tags" {
  type    = map(string)
  default = {}
}
