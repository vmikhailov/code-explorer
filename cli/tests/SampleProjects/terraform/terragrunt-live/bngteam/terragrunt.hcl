include "root" {
  path = find_in_parent_folders()
}

terraform {
  source = "../../aws-infra"
}

inputs = {
  workgroup_name = "bng-team-wg"
  allowed_origin = "https://team.bng.internal"
}
