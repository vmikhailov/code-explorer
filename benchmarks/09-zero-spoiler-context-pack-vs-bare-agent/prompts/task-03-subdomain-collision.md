# Bug Report: Workspace Subdomain Validation & Suggestions Allow Collisions With Registered Public Domains

Implement the fix in `C:/Work/bench-pilot/repos/twenty-ctx-exp` (`packages/twenty-server`).

## Problem Description
In multi-workspace mode, workspaces choose a `subdomain`, and Twenty also allows registering custom `PublicDomain` hostnames. When the server configuration setting `PUBLIC_DOMAIN_URL` is a valid URL (for example `https://twenty.app` or `https://Twenty.App:443`), a workspace `<subdomain>` derives the public hostname `<subdomain>.<publicBaseHostname>` (case-insensitive, stripping any port).

Currently, Twenty's workspace subdomain validation, availability check (`checkWorkspaceSubdomainAvailability`), and available-subdomain suggestion generator only check collisions against existing `Workspace.subdomain` rows — allowing a workspace to claim or be suggested a subdomain whose derived `<subdomain>.<publicBaseHostname>` collides with an already-registered `PublicDomain`!

## Required Behavior
Update Twenty's workspace subdomain validation, availability check, and suggestion generation logic so that whenever `PUBLIC_DOMAIN_URL` is configured as a valid URL with a non-empty hostname:
1. Any subdomain whose derived `<subdomain>.<publicBaseHostname>` (case-insensitive) matches a registered `PublicDomain` is treated as already taken:
   - Subdomain validation throws `SUBDOMAIN_ALREADY_TAKEN`.
   - Subdomain availability checks mark `available: false` (while keeping `isValid: true` for syntactically valid subdomains) and return `3` collision-free `suggestedSubdomains` (`<subdomain>-2`, `<subdomain>-3`, ... skipping any candidate taken by either `Workspace.subdomain` or a registered `PublicDomain`), with `suggestedSubdomain` set to the first suggestion.
   - Automatic available-subdomain selection skips candidates colliding with either `Workspace.subdomain` or a registered `PublicDomain`.
2. Non-colliding valid subdomains remain `available: true` with `suggestedSubdomain: <subdomain>` and `suggestedSubdomains: [<subdomain>]`.
3. When `PUBLIC_DOMAIN_URL` is unset, empty, or not a valid URL, skip the public-domain hostname collision check without throwing.
