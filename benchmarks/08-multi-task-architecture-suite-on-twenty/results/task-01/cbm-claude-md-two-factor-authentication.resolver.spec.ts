import { GUARDS_METADATA } from '@nestjs/common/constants';
import { Test, type TestingModule } from '@nestjs/testing';
import { getRepositoryToken } from '@nestjs/typeorm';

import { PermissionFlagType } from 'twenty-shared/constants';

import { AppTokenEntity } from 'src/engine/core-modules/app-token/app-token.entity';
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
import { getWorkspaceScopedRepositoryToken } from 'src/engine/twenty-orm/workspace-scoped-repository/get-workspace-scoped-repository-token.util';

const WORKSPACE_ID = 'workspace-id';
const TARGET_USER_ID = 'target-user-id';
const TARGET_USER_WORKSPACE_ID = 'target-user-workspace-id';

describe('TwoFactorAuthenticationResolver', () => {
  let resolver: TwoFactorAuthenticationResolver;
  let twoFactorAuthenticationService: TwoFactorAuthenticationService;
  let userWorkspaceService: {
    getUserWorkspaceForUserOrThrow: jest.Mock;
  };
  let twoFactorAuthenticationMethodRepository: {
    delete: jest.Mock;
    findOne: jest.Mock;
  };
  let appTokenRepository: {
    update: jest.Mock;
  };

  beforeEach(async () => {
    userWorkspaceService = {
      getUserWorkspaceForUserOrThrow: jest.fn().mockResolvedValue({
        id: TARGET_USER_WORKSPACE_ID,
        userId: TARGET_USER_ID,
        workspaceId: WORKSPACE_ID,
      }),
    };

    twoFactorAuthenticationMethodRepository = {
      delete: jest.fn().mockResolvedValue({ affected: 1 }),
      findOne: jest.fn(),
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
          useValue: {
            assertEnrollmentNotReservedForRecoveryOrThrow: jest.fn(),
            getRecoveryStatus: jest.fn(),
            generateRecoveryCode: jest.fn(),
            revokeRecoveryCode: jest.fn(),
          },
        },
        {
          provide: LoginTokenService,
          useValue: {
            verifyLoginToken: jest.fn(),
          },
        },
        {
          provide: UserService,
          useValue: {
            findUserByEmailOrThrow: jest.fn(),
          },
        },
        {
          provide: UserWorkspaceService,
          useValue: userWorkspaceService,
        },
        {
          provide: WorkspaceDomainsService,
          useValue: {
            getWorkspaceByOriginOrDefaultWorkspace: jest.fn(),
          },
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
          useValue: {
            tokenBucketThrottleOrThrow: jest.fn(),
          },
        },
        {
          provide: CacheStorageNamespace.EngineAuthSession,
          useValue: {
            setIfAbsent: jest.fn(),
          },
        },
        {
          provide: getRepositoryToken(AppTokenEntity),
          useValue: appTokenRepository,
        },
        {
          provide: getWorkspaceScopedRepositoryToken(
            TwoFactorAuthenticationMethodEntity,
          ),
          useValue: twoFactorAuthenticationMethodRepository,
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
    jest.restoreAllMocks();
  });

  describe('resetTwoFactorAuthenticationMethodsForUser', () => {
    it('should delete all 2FA methods for the target userWorkspace and revoke recovery codes including redeemed ones', async () => {
      const revokeRecoveryCodesSpy = jest.spyOn(
        twoFactorAuthenticationService,
        'revokeRecoveryCodes',
      );

      const result = await resolver.resetTwoFactorAuthenticationMethodsForUser(
        { userId: TARGET_USER_ID },
        { id: WORKSPACE_ID } as WorkspaceEntity,
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
      expect(revokeRecoveryCodesSpy).toHaveBeenCalledWith({
        workspaceId: WORKSPACE_ID,
        userId: TARGET_USER_ID,
        includeRedeemed: true,
      });
    });

    it('should propagate the error and skip deletion when the target user workspace does not exist', async () => {
      const revokeRecoveryCodesSpy = jest.spyOn(
        twoFactorAuthenticationService,
        'revokeRecoveryCodes',
      );

      userWorkspaceService.getUserWorkspaceForUserOrThrow.mockRejectedValueOnce(
        new Error('User workspace not found'),
      );

      await expect(
        resolver.resetTwoFactorAuthenticationMethodsForUser(
          { userId: TARGET_USER_ID },
          { id: WORKSPACE_ID } as WorkspaceEntity,
        ),
      ).rejects.toThrow('User workspace not found');

      expect(
        twoFactorAuthenticationMethodRepository.delete,
      ).not.toHaveBeenCalled();
      expect(revokeRecoveryCodesSpy).not.toHaveBeenCalled();
    });

    it('should be guarded by the SECURITY recovery access policy', () => {
      const resetGuards = Reflect.getMetadata(
        GUARDS_METADATA,
        TwoFactorAuthenticationResolver.prototype
          .resetTwoFactorAuthenticationMethodsForUser,
      );
      const revokeGuards = Reflect.getMetadata(
        GUARDS_METADATA,
        TwoFactorAuthenticationResolver.prototype
          .revokeTwoFactorAuthenticationRecoveryCode,
      );

      expect(resetGuards).toHaveLength(1);
      expect(revokeGuards).toHaveLength(1);

      const accessPolicyService = {
        checkAccessPolicy: jest.fn().mockResolvedValue(true),
      };
      const context = {} as never;

      new resetGuards[0](accessPolicyService).canActivate(context);

      expect(accessPolicyService.checkAccessPolicy).toHaveBeenCalledWith(
        context,
        {
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
        },
      );
    });
  });
});
