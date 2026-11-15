import { Test, type TestingModule } from '@nestjs/testing';

import { type AuthContextUser } from 'src/engine/core-modules/auth/types/auth-context.type';
import { GetMessagesService } from 'src/engine/core-modules/messaging/services/get-messages.service';
import { TimelineMessagingService } from 'src/engine/core-modules/messaging/services/timeline-messaging.service';
import { TimelineMessagingResolver } from 'src/engine/core-modules/messaging/timeline-messaging.resolver';
import { UserVarsService } from 'src/engine/core-modules/user/user-vars/services/user-vars.service';
import { type WorkspaceEntity } from 'src/engine/core-modules/workspace/workspace.entity';
import { AccessPolicyService } from 'src/engine/guards/access-policy/access-policy.service';
import { AccountsToReconnectService } from 'src/modules/connected-account/services/accounts-to-reconnect.service';
import { AccountsToReconnectKeys } from 'src/modules/connected-account/types/accounts-to-reconnect-key-value.type';

describe('TimelineMessagingResolver', () => {
  let resolver: TimelineMessagingResolver;
  let accountsToReconnectService: AccountsToReconnectService;
  let userVarsService: {
    get: jest.Mock;
    set: jest.Mock;
    delete: jest.Mock;
  };

  const mockUser = {
    id: 'user-id',
  } as AuthContextUser;

  const mockWorkspace = {
    id: 'workspace-id',
  } as WorkspaceEntity;

  const connectedAccountId = '20202020-0000-4000-8000-000000000001';

  beforeEach(async () => {
    userVarsService = {
      get: jest.fn(),
      set: jest.fn(),
      delete: jest.fn(),
    };

    const module: TestingModule = await Test.createTestingModule({
      providers: [
        TimelineMessagingResolver,
        AccountsToReconnectService,
        {
          provide: UserVarsService,
          useValue: userVarsService,
        },
        {
          provide: GetMessagesService,
          useValue: {},
        },
        {
          provide: TimelineMessagingService,
          useValue: {},
        },
        {
          provide: AccessPolicyService,
          useValue: {},
        },
      ],
    }).compile();

    resolver = module.get<TimelineMessagingResolver>(TimelineMessagingResolver);
    accountsToReconnectService = module.get<AccountsToReconnectService>(
      AccountsToReconnectService,
    );
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  describe('triggerReconnectAccountBanner', () => {
    it('should add connectedAccountId under ACCOUNTS_TO_RECONNECT_INSUFFICIENT_PERMISSIONS when not yet present', async () => {
      userVarsService.get.mockResolvedValue(undefined);
      const addAccountSpy = jest.spyOn(
        accountsToReconnectService,
        'addAccountToReconnectByKey',
      );

      const result = await resolver.triggerReconnectAccountBanner(
        mockUser,
        mockWorkspace,
        { connectedAccountId },
      );

      expect(result).toBe(true);
      expect(addAccountSpy).toHaveBeenCalledWith(
        AccountsToReconnectKeys.ACCOUNTS_TO_RECONNECT_INSUFFICIENT_PERMISSIONS,
        mockUser.id,
        mockWorkspace.id,
        connectedAccountId,
      );
      expect(userVarsService.get).toHaveBeenCalledWith({
        userId: mockUser.id,
        workspaceId: mockWorkspace.id,
        key: AccountsToReconnectKeys.ACCOUNTS_TO_RECONNECT_INSUFFICIENT_PERMISSIONS,
      });
      expect(userVarsService.set).toHaveBeenCalledWith({
        userId: mockUser.id,
        workspaceId: mockWorkspace.id,
        key: AccountsToReconnectKeys.ACCOUNTS_TO_RECONNECT_INSUFFICIENT_PERMISSIONS,
        value: [connectedAccountId],
      });
    });

    it('should append connectedAccountId to existing accounts when not already present', async () => {
      const existingAccountId = '20202020-0000-4000-8000-000000000002';

      userVarsService.get.mockResolvedValue([existingAccountId]);

      const result = await resolver.triggerReconnectAccountBanner(
        mockUser,
        mockWorkspace,
        { connectedAccountId },
      );

      expect(result).toBe(true);
      expect(userVarsService.set).toHaveBeenCalledWith({
        userId: mockUser.id,
        workspaceId: mockWorkspace.id,
        key: AccountsToReconnectKeys.ACCOUNTS_TO_RECONNECT_INSUFFICIENT_PERMISSIONS,
        value: [existingAccountId, connectedAccountId],
      });
    });

    it('should idempotently skip updating user vars when connectedAccountId is already present', async () => {
      userVarsService.get.mockResolvedValue([connectedAccountId]);

      const result = await resolver.triggerReconnectAccountBanner(
        mockUser,
        mockWorkspace,
        { connectedAccountId },
      );

      expect(result).toBe(true);
      expect(userVarsService.get).toHaveBeenCalledWith({
        userId: mockUser.id,
        workspaceId: mockWorkspace.id,
        key: AccountsToReconnectKeys.ACCOUNTS_TO_RECONNECT_INSUFFICIENT_PERMISSIONS,
      });
      expect(userVarsService.set).not.toHaveBeenCalled();
    });
  });

  describe('dismissReconnectAccountBanner', () => {
    it('should remove connectedAccountId from accounts to reconnect and return true', async () => {
      const removeAccountSpy = jest
        .spyOn(accountsToReconnectService, 'removeAccountToReconnect')
        .mockResolvedValue();

      const result = await resolver.dismissReconnectAccountBanner(
        mockUser,
        mockWorkspace,
        { connectedAccountId },
      );

      expect(result).toBe(true);
      expect(removeAccountSpy).toHaveBeenCalledWith(
        mockUser.id,
        mockWorkspace.id,
        connectedAccountId,
      );
    });
  });
});
