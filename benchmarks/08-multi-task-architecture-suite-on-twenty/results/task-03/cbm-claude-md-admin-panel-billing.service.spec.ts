import { Test, type TestingModule } from '@nestjs/testing';
import { getRepositoryToken } from '@nestjs/typeorm';

import { WorkspaceActivationStatus } from 'twenty-shared/workspace';
import { type Repository } from 'typeorm';

import { AdminPanelBillingService } from 'src/engine/core-modules/admin-panel/services/admin-panel-billing.service';
import {
  BillingException,
  BillingExceptionCode,
} from 'src/engine/core-modules/billing/billing.exception';
import { type BillingCreditGrantEntity } from 'src/engine/core-modules/billing/entities/billing-credit-grant.entity';
import { BillingCustomerEntity } from 'src/engine/core-modules/billing/entities/billing-customer.entity';
import { BillingPriceEntity } from 'src/engine/core-modules/billing/entities/billing-price.entity';
import { BillingCreditGrantType } from 'src/engine/core-modules/billing/enums/billing-credit-grant-type.enum';
import { BillingCreditGrantService } from 'src/engine/core-modules/billing/services/billing-credit-grant.service';
import { BillingCreditService } from 'src/engine/core-modules/billing/services/billing-credit.service';
import { BillingSubscriptionService } from 'src/engine/core-modules/billing/services/billing-subscription.service';
import { BillingUsageService } from 'src/engine/core-modules/billing/services/billing-usage.service';
import { type EmailingDomainEntity } from 'src/engine/core-modules/emailing-domain/emailing-domain.entity';
import { EmailingDomainWorkspaceCleanupJob } from 'src/engine/core-modules/emailing-domain/jobs/emailing-domain-workspace-cleanup.job';
import { EmailingDomainService } from 'src/engine/core-modules/emailing-domain/services/emailing-domain.service';
import { MessageQueue } from 'src/engine/core-modules/message-queue/message-queue.constants';
import { type MessageQueueService } from 'src/engine/core-modules/message-queue/services/message-queue.service';
import { getQueueToken } from 'src/engine/core-modules/message-queue/utils/get-queue-token.util';
import { TwentyConfigService } from 'src/engine/core-modules/twenty-config/twenty-config.service';
import { WorkspaceService } from 'src/engine/core-modules/workspace/services/workspace.service';
import { WorkspaceEntity } from 'src/engine/core-modules/workspace/workspace.entity';
import { getWorkspaceScopedRepositoryToken } from 'src/engine/twenty-orm/workspace-scoped-repository/get-workspace-scoped-repository-token.util';

const WORKSPACE_ID = '20202020-0000-4000-8000-000000000001';
const ACTOR_USER_ID = '20202020-0000-4000-8000-000000000002';

describe('AdminPanelBillingService.suspendWorkspace', () => {
  let service: AdminPanelBillingService;
  let workspaceRepository: jest.Mocked<Pick<Repository<WorkspaceEntity>, 'findOne'>>;
  let workspaceService: jest.Mocked<Pick<WorkspaceService, 'suspendWorkspace'>>;
  let billingSubscriptionService: jest.Mocked<
    Pick<BillingSubscriptionService, 'cancelSubscription'>
  >;
  let billingCreditGrantService: jest.Mocked<
    Pick<BillingCreditGrantService, 'listGrants'>
  >;
  let billingCreditService: jest.Mocked<Pick<BillingCreditService, 'revokeGrant'>>;
  let emailingDomainService: jest.Mocked<
    Pick<EmailingDomainService, 'getEmailingDomains'>
  >;
  let deleteCascadeMessageQueueService: jest.Mocked<
    Pick<MessageQueueService, 'add'>
  >;
  let twentyConfigService: jest.Mocked<Pick<TwentyConfigService, 'get'>>;

  beforeEach(async () => {
    workspaceRepository = {
      findOne: jest.fn(),
    };
    workspaceService = {
      suspendWorkspace: jest.fn().mockResolvedValue(true),
    };
    billingSubscriptionService = {
      cancelSubscription: jest.fn().mockResolvedValue(undefined),
    };
    billingCreditGrantService = {
      listGrants: jest.fn().mockResolvedValue([]),
    };
    billingCreditService = {
      revokeGrant: jest.fn().mockResolvedValue({} as BillingCreditGrantEntity),
    };
    emailingDomainService = {
      getEmailingDomains: jest.fn().mockResolvedValue([]),
    };
    deleteCascadeMessageQueueService = {
      add: jest.fn().mockResolvedValue(undefined),
    };
    twentyConfigService = {
      get: jest.fn(),
    };

    const module: TestingModule = await Test.createTestingModule({
      providers: [
        AdminPanelBillingService,
        {
          provide: getWorkspaceScopedRepositoryToken(BillingCustomerEntity),
          useValue: {},
        },
        {
          provide: getRepositoryToken(BillingPriceEntity),
          useValue: {},
        },
        {
          provide: getRepositoryToken(WorkspaceEntity),
          useValue: workspaceRepository,
        },
        {
          provide: BillingSubscriptionService,
          useValue: billingSubscriptionService,
        },
        {
          provide: BillingUsageService,
          useValue: {},
        },
        {
          provide: BillingCreditService,
          useValue: billingCreditService,
        },
        {
          provide: BillingCreditGrantService,
          useValue: billingCreditGrantService,
        },
        {
          provide: TwentyConfigService,
          useValue: twentyConfigService,
        },
        {
          provide: WorkspaceService,
          useValue: workspaceService,
        },
        {
          provide: EmailingDomainService,
          useValue: emailingDomainService,
        },
        {
          provide: getQueueToken(MessageQueue.deleteCascadeQueue),
          useValue: deleteCascadeMessageQueueService,
        },
      ],
    }).compile();

    service = module.get(AdminPanelBillingService);
  });

  it('should throw BILLING_SUBSCRIPTION_EVENT_WORKSPACE_NOT_FOUND when workspace does not exist or is soft-deleted', async () => {
    workspaceRepository.findOne.mockResolvedValueOnce(null);

    await expect(
      service.suspendWorkspace({
        workspaceId: WORKSPACE_ID,
        actorUserId: ACTOR_USER_ID,
      }),
    ).rejects.toMatchObject({
      name: BillingException.name,
      code: BillingExceptionCode.BILLING_SUBSCRIPTION_EVENT_WORKSPACE_NOT_FOUND,
    });

    workspaceRepository.findOne.mockResolvedValueOnce({
      id: WORKSPACE_ID,
      deletedAt: new Date(),
    } as WorkspaceEntity);

    await expect(
      service.suspendWorkspace({
        workspaceId: WORKSPACE_ID,
        actorUserId: ACTOR_USER_ID,
      }),
    ).rejects.toMatchObject({
      name: BillingException.name,
      code: BillingExceptionCode.BILLING_SUBSCRIPTION_EVENT_WORKSPACE_NOT_FOUND,
    });

    expect(workspaceService.suspendWorkspace).not.toHaveBeenCalled();
    expect(billingSubscriptionService.cancelSubscription).not.toHaveBeenCalled();
    expect(deleteCascadeMessageQueueService.add).not.toHaveBeenCalled();
  });

  it('should suspend workspace, cancel subscription, revoke active credit grants, and enqueue emailing domain cleanup when billing is enabled', async () => {
    const workspace = {
      id: WORKSPACE_ID,
      activationStatus: WorkspaceActivationStatus.ACTIVE,
      deletedAt: null,
    } as WorkspaceEntity;

    workspaceRepository.findOne.mockResolvedValue(workspace);
    twentyConfigService.get.mockReturnValue(true as never);

    const now = Date.now();
    const activeGrant = {
      id: 'grant-active',
      amountMicro: 100_000,
      type: BillingCreditGrantType.PROMOTIONAL,
      effectiveAt: new Date(now - 60_000),
      expiresAt: new Date(now + 60_000),
      revokedAt: null,
      sourceGrantId: null,
      reason: 'Promo',
      createdAt: new Date(now - 60_000),
    } as BillingCreditGrantEntity;
    const revokedGrant = {
      id: 'grant-revoked',
      amountMicro: 50_000,
      type: BillingCreditGrantType.PROMOTIONAL,
      effectiveAt: new Date(now - 60_000),
      expiresAt: null,
      revokedAt: new Date(now - 10_000),
      sourceGrantId: null,
      reason: null,
      createdAt: new Date(now - 60_000),
    } as BillingCreditGrantEntity;
    const expiredGrant = {
      id: 'grant-expired',
      amountMicro: 50_000,
      type: BillingCreditGrantType.PROMOTIONAL,
      effectiveAt: new Date(now - 120_000),
      expiresAt: new Date(now - 60_000),
      revokedAt: null,
      sourceGrantId: null,
      reason: null,
      createdAt: new Date(now - 120_000),
    } as BillingCreditGrantEntity;

    billingCreditGrantService.listGrants.mockResolvedValue([
      activeGrant,
      revokedGrant,
      expiredGrant,
    ]);
    emailingDomainService.getEmailingDomains.mockResolvedValue([
      { domain: 'acme.com' } as EmailingDomainEntity,
      { domain: 'mail.acme.com' } as EmailingDomainEntity,
    ]);

    const result = await service.suspendWorkspace({
      workspaceId: WORKSPACE_ID,
      actorUserId: ACTOR_USER_ID,
    });

    expect(result).toBe(true);
    expect(workspaceRepository.findOne).toHaveBeenCalledWith({
      where: { id: WORKSPACE_ID },
      withDeleted: true,
    });
    expect(workspaceService.suspendWorkspace).toHaveBeenCalledWith(WORKSPACE_ID);
    expect(billingSubscriptionService.cancelSubscription).toHaveBeenCalledWith(
      WORKSPACE_ID,
    );
    expect(billingCreditService.revokeGrant).toHaveBeenCalledTimes(1);
    expect(billingCreditService.revokeGrant).toHaveBeenCalledWith({
      workspaceId: WORKSPACE_ID,
      grantId: 'grant-active',
      revokedByUserId: ACTOR_USER_ID,
    });
    expect(emailingDomainService.getEmailingDomains).toHaveBeenCalledWith(
      workspace,
    );
    expect(deleteCascadeMessageQueueService.add).toHaveBeenCalledWith(
      EmailingDomainWorkspaceCleanupJob.name,
      {
        workspaceId: WORKSPACE_ID,
        domains: ['acme.com', 'mail.acme.com'],
      },
      {
        workspaceId: WORKSPACE_ID,
        isWorkspaceJob: true,
      },
    );
  });

  it('should suspend workspace and enqueue emailing domain cleanup without billing calls when billing is disabled', async () => {
    const workspace = {
      id: WORKSPACE_ID,
      activationStatus: WorkspaceActivationStatus.ACTIVE,
      deletedAt: null,
    } as WorkspaceEntity;

    workspaceRepository.findOne.mockResolvedValue(workspace);
    twentyConfigService.get.mockReturnValue(false as never);
    emailingDomainService.getEmailingDomains.mockResolvedValue([
      { domain: 'acme.com' } as EmailingDomainEntity,
    ]);

    const result = await service.suspendWorkspace({
      workspaceId: WORKSPACE_ID,
      actorUserId: ACTOR_USER_ID,
    });

    expect(result).toBe(true);
    expect(workspaceService.suspendWorkspace).toHaveBeenCalledWith(WORKSPACE_ID);
    expect(billingSubscriptionService.cancelSubscription).not.toHaveBeenCalled();
    expect(billingCreditGrantService.listGrants).not.toHaveBeenCalled();
    expect(billingCreditService.revokeGrant).not.toHaveBeenCalled();
    expect(deleteCascadeMessageQueueService.add).toHaveBeenCalledWith(
      EmailingDomainWorkspaceCleanupJob.name,
      {
        workspaceId: WORKSPACE_ID,
        domains: ['acme.com'],
      },
      {
        workspaceId: WORKSPACE_ID,
        isWorkspaceJob: true,
      },
    );
  });
});
