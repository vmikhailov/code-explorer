import { GUARDS_METADATA } from '@nestjs/common/constants';
import { Test, type TestingModule } from '@nestjs/testing';

import { PermissionFlagType } from 'twenty-shared/constants';

import { LoginTokenService } from 'src/engine/core-modules/auth/token/services/login-token.service';
import { WorkspaceDomainsService } from 'src/engine/core-modules/domain/workspace-domains/services/workspace-domains.service';
import { TwoFactorAuthenticationMethodEntity } from 'src/engine/core-modules/two-factor-authentication/entities/two-factor-authentication-method.entity';
import { TwoFactorAuthenticationRecoveryService } from 'src/engine/core-modules/two-factor-authentication/services/two-factor-authentication-recovery.service';
import { TwoFactorAuthenticationResolver } from 'src/engine/core-modules/two-factor-authentication/two-factor-authentication.resolver';
import { TwoFactorAuthenticationService } from 'src/engine/core-modules/two-factor-authentication/two-factor-authentication.service';
import { UserWorkspaceService } from 'src/engine/core-modules/user-workspace/user-workspace.service';
import { UserService } from 'src/engine/core-modules/user/services/user.service';
import { type WorkspaceEntity } from 'src/engine/core-modules/workspace/workspace.entity';
import { AccessPolicyService } from 'src/engine/guards/access-policy/access-policy.service';
import { ACCESS_POLICY_GUARD_CONFIG_KEY } from 'src/engine/guards/constants/access-policy-guard-config-key.constant';
import { getWorkspaceScopedRepositoryToken } from 'src/engine/twenty-orm/workspace-scoped-repository/get-workspace-scoped-repository-token.util';

const WORKSPACE_ID = '20202020-1c25-4d02-bf25-6aeccf7ea419';
const TARGET_USER_ID = '20202020-9e3b-46d4-a556-88b9ddc2b034';
const TARGET_USER_WORKSPACE_ID = '20202020-3957-45c8-95c6-3990297a0311';

describe('TwoFactorAuthenticationResolver', () => {
  let resolver: TwoFactorAuthenticationResolver;
  let twoFactorAuthenticationService: {
    revokeRecoveryCodes: jest.Mock;
  };
  let userWorkspaceService: {
    getUserWorkspaceForUserOrThrow: jest.Mock;
  };
  let twoFactorAuthenticationMethodRepository: {
    delete: jest.Mock;
  };

  const workspace = { id: WORKSPACE_ID } as WorkspaceEntity;

  beforeEach(async () => {
    twoFactorAuthenticationService = {
      revokeRecoveryCodes: jest.fn().mockResolvedValue(1),
    };
    userWorkspaceService = {
      getUserWorkspaceForUserOrThrow: jest.fn().mockResolvedValue({
        id: TARGET_USER_WORKSPACE_ID,
        userId: TARGET_USER_ID,
        workspaceId: WORKSPACE_ID,
      }),
    };
    twoFactorAuthenticationMethodRepository = {
      delete: jest.fn().mockResolvedValue({ affected: 1 }),
    };

    const module: TestingModule = await Test.createTestingModule({
      providers: [
        TwoFactorAuthenticationResolver,
        {
          provide: TwoFactorAuthenticationService,
          useValue: twoFactorAuthenticationService,
        },
        {
          provide: TwoFactorAuthenticationRecoveryService,
          useValue: {},
        },
        {
          provide: LoginTokenService,
          useValue: {},
        },
        {
          provide: UserService,
          useValue: {},
        },
        {
          provide: UserWorkspaceService,
          useValue: userWorkspaceService,
        },
        {
          provide: WorkspaceDomainsService,
          useValue: {},
        },
        {
          provide: getWorkspaceScopedRepositoryToken(
            TwoFactorAuthenticationMethodEntity,
          ),
          useValue: twoFactorAuthenticationMethodRepository,
        },
        {
          provide: AccessPolicyService,
          useValue: {},
        },
      ],
    }).compile();

    resolver = module.get<TwoFactorAuthenticationResolver>(
      TwoFactorAuthenticationResolver,
    );
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  describe('resetTwoFactorAuthenticationMethodsForUser', () => {
    it('deletes all 2FA methods for the target userWorkspace and revokes all recovery codes including redeemed ones', async () => {
      const result = await resolver.resetTwoFactorAuthenticationMethodsForUser(
        { userId: TARGET_USER_ID },
        workspace,
      );

      expect(result).toEqual({ success: true });
      expect(
        userWorkspaceService.getUserWorkspaceForUserOrThrow,
      ).toHaveBeenCalledWith({
        userId: TARGET_USER_ID,
        workspaceId: WORKSPACE_ID,
      });
      expect(
        twoFactorAuthenticationMethodRepository.delete,
      ).toHaveBeenCalledWith(WORKSPACE_ID, {
        userWorkspaceId: TARGET_USER_WORKSPACE_ID,
      });
      expect(
        twoFactorAuthenticationService.revokeRecoveryCodes,
      ).toHaveBeenCalledWith({
        workspaceId: WORKSPACE_ID,
        userId: TARGET_USER_ID,
        includeRedeemed: true,
      });
    });

    it('propagates an error when the target user does not belong to the workspace without deleting methods or revoking codes', async () => {
      userWorkspaceService.getUserWorkspaceForUserOrThrow.mockRejectedValueOnce(
        new Error('User workspace not found'),
      );

      await expect(
        resolver.resetTwoFactorAuthenticationMethodsForUser(
          { userId: TARGET_USER_ID },
          workspace,
        ),
      ).rejects.toThrow('User workspace not found');

      expect(
        twoFactorAuthenticationMethodRepository.delete,
      ).not.toHaveBeenCalled();
      expect(
        twoFactorAuthenticationService.revokeRecoveryCodes,
      ).not.toHaveBeenCalled();
    });

    it('guards the mutation with TWO_FACTOR_AUTHENTICATION_RECOVERY_ACCESS_POLICY requiring PermissionFlagType.SECURITY', () => {
      const guards = Reflect.getMetadata(
        GUARDS_METADATA,
        TwoFactorAuthenticationResolver.prototype
          .resetTwoFactorAuthenticationMethodsForUser,
      );

      expect(guards).toHaveLength(1);

      const accessPolicy = Reflect.getMetadata(
        ACCESS_POLICY_GUARD_CONFIG_KEY,
        guards[0],
      );

      expect(accessPolicy).toEqual({
        authPrincipals: {
          userSession: {
            standard: true,
            impersonated: false,
            playground: false,
            workspaceAgnostic: false,
          },
          apiKey: false,
          oauthClient: false,
          application: false,
        },
        requiredPermissionFlags: [PermissionFlagType.SECURITY],
      });
    });
  });
});
