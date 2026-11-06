include "root" {
  path = find_in_parent_folders()
}

terraform {
  source = "../reusable-module"
}

dependency "network" {
  config_path = "../root-stack"
}

inputs = {
  name = "tg-demo"
}
