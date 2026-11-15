import { GUARDS_METADATA } from '@nestjs/common/constants';
import { Test, type TestingModule } from '@nestjs/testing';
import { getRepositoryToken } from '@nestjs/typeorm';

import { PermissionFlagType } from 'twenty-shared/constants';
import { IsNull } from 'typeorm';

import {
  AppTokenEntity,
  AppTokenType,
} from 'src/engine/core-modules/app-token/app-token.entity';
import { LoginTokenService } from 'src/engine/core-modules/auth/token/services/login-token.service';
import { CacheStorageNamespace } from 'src/engine/core-modules/cache-storage/types/cache-storage-namespace.enum';
import { WorkspaceDomainsService } from 'src/engine/core-modules/domain/workspace-domains/services/workspace-domains.service';
import { SecretEncryptionService } from 'src/engine/core-modules/secret-encryption/secret-encryption.service';
import { ThrottlerService } from 'src/engine/core-modules/throttler/throttler.service';
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

const WORKSPACE_ID = 'workspace-id';
const TARGET_USER_ID = 'target-user-id';
const TARGET_USER_WORKSPACE_ID = 'target-user-workspace-id';

describe('TwoFactorAuthenticationResolver', () => {
  let resolver: TwoFactorAuthenticationResolver;
  let twoFactorAuthenticationService: TwoFactorAuthenticationService;
  let twoFactorAuthenticationRecoveryService: {
    assertEnrollmentNotReservedForRecoveryOrThrow: jest.Mock;
    getRecoveryStatus: jest.Mock;
    generateRecoveryCode: jest.Mock;
    revokeRecoveryCode: jest.Mock;
  };
  let userWorkspaceService: {
    getUserWorkspaceForUserOrThrow: jest.Mock;
  };
  let twoFactorAuthenticationMethodRepository: {
    findOne: jest.Mock;
    delete: jest.Mock;
  };
  let appTokenRepository: {
    update: jest.Mock;
  };

  const workspace = {
    id: WORKSPACE_ID,
    displayName: 'Apple',
  } as WorkspaceEntity;

  beforeEach(async () => {
    twoFactorAuthenticationRecoveryService = {
      assertEnrollmentNotReservedForRecoveryOrThrow: jest.fn(),
      getRecoveryStatus: jest.fn(),
      generateRecoveryCode: jest.fn(),
      revokeRecoveryCode: jest.fn(),
    };
    userWorkspaceService = {
      getUserWorkspaceForUserOrThrow: jest.fn().mockResolvedValue({
        id: TARGET_USER_WORKSPACE_ID,
        userId: TARGET_USER_ID,
        workspaceId: WORKSPACE_ID,
      }),
    };
    twoFactorAuthenticationMethodRepository = {
      findOne: jest.fn(),
      delete: jest.fn().mockResolvedValue({ affected: 1 }),
    };
    appTokenRepository = {
      update: jest.fn().mockResolvedValue({ affected: 1 }),
    };

    const module: TestingModule = await Test.createTestingModule({
      providers: [
        TwoFactorAuthenticationResolver,
        TwoFactorAuthenticationService,
        {
          provide: TwoFactorAuthenticationRecoveryService,
          useValue: twoFactorAuthenticationRecoveryService,
        },
        {
          provide: LoginTokenService,
          useValue: { verifyLoginToken: jest.fn() },
        },
        {
          provide: UserService,
          useValue: { findUserByEmailOrThrow: jest.fn() },
        },
        {
          provide: UserWorkspaceService,
          useValue: userWorkspaceService,
        },
        {
          provide: WorkspaceDomainsService,
          useValue: { getWorkspaceByOriginOrDefaultWorkspace: jest.fn() },
        },
        {
          provide: getWorkspaceScopedRepositoryToken(
            TwoFactorAuthenticationMethodEntity,
          ),
          useValue: twoFactorAuthenticationMethodRepository,
        },
        {
          provide: getRepositoryToken(AppTokenEntity),
          useValue: appTokenRepository,
        },
        {
          provide: SecretEncryptionService,
          useValue: {
            encryptVersioned: jest.fn(),
            decryptVersionedOrThrow: jest.fn(),
          },
        },
        {
          provide: ThrottlerService,
          useValue: { tokenBucketThrottleOrThrow: jest.fn() },
        },
        {
          provide: CacheStorageNamespace.EngineAuthSession,
          useValue: { setIfAbsent: jest.fn() },
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
    twoFactorAuthenticationService = module.get<TwoFactorAuthenticationService>(
      TwoFactorAuthenticationService,
    );
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  describe('resetTwoFactorAuthenticationMethodsForUser', () => {
    it('deletes all 2FA methods for the target userWorkspace and revokes all recovery codes including redeemed ones', async () => {
      const revokeRecoveryCodesSpy = jest.spyOn(
        twoFactorAuthenticationService,
        'revokeRecoveryCodes',
      );

      const result = await resolver.resetTwoFactorAuthenticationMethodsForUser(
        { userId: TARGET_USER_ID },
        workspace,
      );

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
      expect(revokeRecoveryCodesSpy).toHaveBeenCalledWith({
        workspaceId: WORKSPACE_ID,
        userId: TARGET_USER_ID,
        includeRedeemed: true,
      });
      expect(appTokenRepository.update).toHaveBeenCalledWith(
        {
          workspaceId: WORKSPACE_ID,
          userId: TARGET_USER_ID,
          type: AppTokenType.TwoFactorAuthenticationRecoveryCode,
          revokedAt: IsNull(),
        },
        { revokedAt: expect.any(Date) },
      );
      expect(result).toEqual({ success: true });
    });

    it('propagates errors when the target userWorkspace cannot be found and does not delete methods or revoke recovery codes', async () => {
      const revokeRecoveryCodesSpy = jest.spyOn(
        twoFactorAuthenticationService,
        'revokeRecoveryCodes',
      );

      userWorkspaceService.getUserWorkspaceForUserOrThrow.mockRejectedValue(
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
      expect(revokeRecoveryCodesSpy).not.toHaveBeenCalled();
      expect(appTokenRepository.update).not.toHaveBeenCalled();
    });

    it('is guarded by AccessPolicyGuard requiring PermissionFlagType.SECURITY', () => {
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
