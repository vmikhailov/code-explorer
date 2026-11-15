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

  const connectedAccountId = '20202020-1c25-4d02-bf25-6aeccf7ea419';

  beforeEach(async () => {
    userVarsService = {
      get: jest.fn(),
      set: jest.fn(),
      delete: jest.fn(),
    };

    const module: TestingModule = await Test.createTestingModule({
      providers: [
        TimelineMessagingResolver,
        {
          provide: AccountsToReconnectService,
          useValue: {
            removeAccountToReconnect: jest.fn(),
            addAccountToReconnectByKey: jest.fn(),
          },
        },
        {
          provide: UserVarsService,
          useValue: userVarsService,
        },
        {
          provide: GetMessagesService,
          useValue: {
            getMessagesFromObjectRecord: jest.fn(),
            searchMessagesFromObjectRecord: jest.fn(),
          },
        },
        {
          provide: TimelineMessagingService,
          useValue: {
            getMessageThreadChannel: jest.fn(),
          },
        },
        {
          provide: AccessPolicyService,
          useValue: {},
        },
      ],
    }).compile();

    resolver = module.get<TimelineMessagingResolver>(TimelineMessagingResolver);
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  describe('triggerReconnectAccountBanner', () => {
    it('should add a new connectedAccountId under ACCOUNTS_TO_RECONNECT_INSUFFICIENT_PERMISSIONS when none exist', async () => {
      userVarsService.get.mockResolvedValue(undefined);

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
      expect(userVarsService.set).toHaveBeenCalledWith({
        userId: mockUser.id,
        workspaceId: mockWorkspace.id,
        key: AccountsToReconnectKeys.ACCOUNTS_TO_RECONNECT_INSUFFICIENT_PERMISSIONS,
        value: [connectedAccountId],
      });
    });

    it('should append connectedAccountId when other accounts are already present', async () => {
      const existingAccountId = '30303030-1c25-4d02-bf25-6aeccf7ea419';

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
});
