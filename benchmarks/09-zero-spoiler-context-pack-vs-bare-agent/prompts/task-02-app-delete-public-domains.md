# Bug Report: Deleting an Application Orphans Its Public Domains and External DNS Hostnames

Implement the fix in `C:/Work/bench-pilot/repos/twenty-ctx-exp` (`packages/twenty-server`).

## Problem Description
Custom applications installed in a workspace can own registered public domains (`publicDomain` rows linked by `applicationId` and `workspaceId`).

Currently, when a workspace application is deleted (by `universalIdentifier` and `workspaceId`) in `twenty-server`, any public domains owned by that application are left orphaned in external DNS infrastructure or removed without running Twenty's full public-domain teardown lifecycle.

## Required Behavior
Update the workspace application deletion flow (taking `universalIdentifier` and `workspaceId`) so that:
1. If no application with `universalIdentifier` exists in `workspaceId`, throw the existing `APPLICATION_NOT_FOUND` exception without deleting any public domains or calling external DNS teardown.
2. When deleting an existing application in `workspaceId`, remove all public domains owned by that application in `workspaceId` through Twenty's canonical public-domain removal lifecycle — including tearing down each domain's external custom hostname (`{ isPublicDomain: true }`) and recomputing the relevant workspace caches — while leaving public domains and DNS hostnames belonging to other applications or other workspaces untouched.
