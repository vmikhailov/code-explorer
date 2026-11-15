import { AdminPanelBillingService } from 'src/engine/core-modules/admin-panel/services/admin-panel-billing.service';
import {
  BillingException,
  BillingExceptionCode,
} from 'src/engine/core-modules/billing/billing.exception';
import { type BillingCreditGrantEntity } from 'src/engine/core-modules/billing/entities/billing-credit-grant.entity';
import { BillingCreditGrantType } from 'src/engine/core-modules/billing/enums/billing-credit-grant-type.enum';
import { EmailingDomainWorkspaceCleanupJob } from 'src/engine/core-modules/emailing-domain/jobs/emailing-domain-workspace-cleanup.job';

const WORKSPACE_ID = '20202020-0000-4000-8000-000000000001';
const ACTOR_USER_ID = '20202020-0000-4000-8000-000000000002';

const buildCreditGrant = (
  overrides: Partial<BillingCreditGrantEntity> = {},
): BillingCreditGrantEntity =>
  ({
    id: 'grant-active-1',
    workspaceId: WORKSPACE_ID,
    amountMicro: 1_000_000,
    type: BillingCreditGrantType.PROMOTIONAL,
    effectiveAt: new Date(Date.now() - 60_000),
    expiresAt: new Date(Date.now() + 86_400_000),
    revokedAt: null,
    revokedByUserId: null,
    sourceGrantId: null,
    reason: 'Promo',
    createdAt: new Date(Date.now() - 60_000),
    ...overrides,
  }) as BillingCreditGrantEntity;

const buildService = ({
  workspace = { id: WORKSPACE_ID, deletedAt: null },
  isBillingEnabled = true,
  creditGrants = [],
  emailingDomains = [{ domain: 'mail.acme.com' }],
}: {
  workspace?: { id: string; deletedAt: Date | null } | null;
  isBillingEnabled?: boolean;
  creditGrants?: BillingCreditGrantEntity[];
  emailingDomains?: Array<{ domain: string }>;
} = {}) => {
  const findOneWorkspace = jest.fn(async () => workspace);
  const getEmailingDomains = jest.fn(async () => emailingDomains);
  const cancelSubscription = jest.fn(async () => undefined);
  const revokeGrant = jest.fn(async () => undefined);
  const listGrants = jest.fn(async () => creditGrants);
  const getConfig = jest.fn((key: string) =>
    key === 'IS_BILLING_ENABLED' ? isBillingEnabled : undefined,
  );
  const suspendWorkspace = jest.fn(async () => true);
  const addCleanupJob = jest.fn(async () => undefined);

  const service = new AdminPanelBillingService(
    {} as never,
    {} as never,
    { findOne: findOneWorkspace } as never,
    { cancelSubscription } as never,
    {} as never,
    { revokeGrant } as never,
    { listGrants } as never,
    { get: getConfig } as never,
    { suspendWorkspace } as never,
    { getEmailingDomains } as never,
    { add: addCleanupJob } as never,
  );

  return {
    service,
    findOneWorkspace,
    getEmailingDomains,
    cancelSubscription,
    revokeGrant,
    listGrants,
    suspendWorkspace,
    addCleanupJob,
  };
};

describe('AdminPanelBillingService.suspendWorkspace', () => {
  it('should throw BILLING_SUBSCRIPTION_EVENT_WORKSPACE_NOT_FOUND when workspace is not found', async () => {
    const {
      service,
      suspendWorkspace,
      cancelSubscription,
      revokeGrant,
      addCleanupJob,
    } = buildService({
      workspace: null,
    });

    await expect(
      service.suspendWorkspace({
        workspaceId: WORKSPACE_ID,
        actorUserId: ACTOR_USER_ID,
      }),
    ).rejects.toThrow(BillingException);

    await expect(
      service.suspendWorkspace({
        workspaceId: WORKSPACE_ID,
        actorUserId: ACTOR_USER_ID,
      }),
    ).rejects.toMatchObject({
      code: BillingExceptionCode.BILLING_SUBSCRIPTION_EVENT_WORKSPACE_NOT_FOUND,
    });

    expect(suspendWorkspace).not.toHaveBeenCalled();
    expect(cancelSubscription).not.toHaveBeenCalled();
    expect(revokeGrant).not.toHaveBeenCalled();
    expect(addCleanupJob).not.toHaveBeenCalled();
  });

  it('should throw BILLING_SUBSCRIPTION_EVENT_WORKSPACE_NOT_FOUND when workspace is soft-deleted', async () => {
    const { service, suspendWorkspace } = buildService({
      workspace: { id: WORKSPACE_ID, deletedAt: new Date() },
    });

    await expect(
      service.suspendWorkspace({
        workspaceId: WORKSPACE_ID,
        actorUserId: ACTOR_USER_ID,
      }),
    ).rejects.toMatchObject({
      code: BillingExceptionCode.BILLING_SUBSCRIPTION_EVENT_WORKSPACE_NOT_FOUND,
    });

    expect(suspendWorkspace).not.toHaveBeenCalled();
  });

  it('should suspend workspace, cancel subscription, revoke only active credit grants, and enqueue emailing domain cleanup when billing is enabled', async () => {
    const activeGrant = buildCreditGrant({ id: 'grant-active-1' });
    const expiredGrant = buildCreditGrant({
      id: 'grant-expired-1',
      expiresAt: new Date(Date.now() - 60_000),
    });
    const alreadyRevokedGrant = buildCreditGrant({
      id: 'grant-revoked-1',
      revokedAt: new Date(Date.now() - 60_000),
    });

    const {
      service,
      suspendWorkspace,
      cancelSubscription,
      listGrants,
      revokeGrant,
      getEmailingDomains,
      addCleanupJob,
    } = buildService({
      isBillingEnabled: true,
      creditGrants: [activeGrant, expiredGrant, alreadyRevokedGrant],
      emailingDomains: [{ domain: 'mail.acme.com' }, { domain: 'news.acme.com' }],
    });

    const result = await service.suspendWorkspace({
      workspaceId: WORKSPACE_ID,
      actorUserId: ACTOR_USER_ID,
    });

    expect(result).toBe(true);
    expect(suspendWorkspace).toHaveBeenCalledTimes(1);
    expect(suspendWorkspace).toHaveBeenCalledWith(WORKSPACE_ID);
    expect(cancelSubscription).toHaveBeenCalledTimes(1);
    expect(cancelSubscription).toHaveBeenCalledWith(WORKSPACE_ID);
    expect(listGrants).toHaveBeenCalledWith(WORKSPACE_ID);
    expect(revokeGrant).toHaveBeenCalledTimes(1);
    expect(revokeGrant).toHaveBeenCalledWith({
      workspaceId: WORKSPACE_ID,
      grantId: 'grant-active-1',
      revokedByUserId: ACTOR_USER_ID,
    });
    expect(getEmailingDomains).toHaveBeenCalledWith({
      id: WORKSPACE_ID,
      deletedAt: null,
    });
    expect(addCleanupJob).toHaveBeenCalledTimes(1);
    expect(addCleanupJob).toHaveBeenCalledWith(
      EmailingDomainWorkspaceCleanupJob.name,
      {
        workspaceId: WORKSPACE_ID,
        domains: ['mail.acme.com', 'news.acme.com'],
      },
      {
        workspaceId: WORKSPACE_ID,
        isWorkspaceJob: true,
      },
    );
  });

  it('should suspend workspace and enqueue emailing domain cleanup without canceling subscription or revoking grants when billing is disabled', async () => {
    const {
      service,
      suspendWorkspace,
      cancelSubscription,
      listGrants,
      revokeGrant,
      addCleanupJob,
    } = buildService({
      isBillingEnabled: false,
      creditGrants: [buildCreditGrant({ id: 'grant-active-1' })],
      emailingDomains: [{ domain: 'mail.acme.com' }],
    });

    const result = await service.suspendWorkspace({
      workspaceId: WORKSPACE_ID,
      actorUserId: ACTOR_USER_ID,
    });

    expect(result).toBe(true);
    expect(suspendWorkspace).toHaveBeenCalledWith(WORKSPACE_ID);
    expect(cancelSubscription).not.toHaveBeenCalled();
    expect(listGrants).not.toHaveBeenCalled();
    expect(revokeGrant).not.toHaveBeenCalled();
    expect(addCleanupJob).toHaveBeenCalledWith(
      EmailingDomainWorkspaceCleanupJob.name,
      {
        workspaceId: WORKSPACE_ID,
        domains: ['mail.acme.com'],
      },
      {
        workspaceId: WORKSPACE_ID,
        isWorkspaceJob: true,
      },
    );
  });
});
