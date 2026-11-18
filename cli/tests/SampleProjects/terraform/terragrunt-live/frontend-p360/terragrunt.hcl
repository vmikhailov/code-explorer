include "root" {
  path = find_in_parent_folders()
}

terraform {
  source = "../../aws-infra"
}

dependency "team_outputs" {
  config_path = "../bngteam"
}

inputs = {
  workgroup_name = dependency.team_outputs.outputs.athena_workgroup_name
  allowed_origin = include.root.locals.default_origin
}
