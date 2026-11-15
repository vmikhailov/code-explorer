import {
  BillingException,
  BillingExceptionCode,
} from 'src/engine/core-modules/billing/billing.exception';
import { type BillingCreditGrantEntity } from 'src/engine/core-modules/billing/entities/billing-credit-grant.entity';
import { BillingCreditGrantType } from 'src/engine/core-modules/billing/enums/billing-credit-grant-type.enum';
import { EmailingDomainWorkspaceCleanupJob } from 'src/engine/core-modules/emailing-domain/jobs/emailing-domain-workspace-cleanup.job';
import { AdminPanelBillingService } from 'src/engine/core-modules/admin-panel/services/admin-panel-billing.service';

const WORKSPACE_ID = '20202020-0000-4000-8000-000000000001';
const ACTOR_USER_ID = '20202020-0000-4000-8000-000000000002';

const buildGrant = (
  overrides: Partial<BillingCreditGrantEntity> & { id: string },
): BillingCreditGrantEntity =>
  ({
    id: overrides.id,
    workspaceId: WORKSPACE_ID,
    amountMicro: 100_000,
    type: BillingCreditGrantType.PROMOTIONAL,
    effectiveAt: new Date(Date.now() - 60_000),
    expiresAt: null,
    revokedAt: null,
    revokedByUserId: null,
    sourceGrantId: null,
    reason: 'test',
    createdAt: new Date(Date.now() - 60_000),
    updatedAt: new Date(Date.now() - 60_000),
    ...overrides,
  }) as BillingCreditGrantEntity;

const buildService = ({
  workspace = { id: WORKSPACE_ID, deletedAt: null },
  isBillingEnabled = true,
  grants = [],
  emailingDomains = [{ domain: 'acme.com' }, { domain: 'mail.acme.com' }],
}: {
  workspace?: { id: string; deletedAt: Date | null } | null;
  isBillingEnabled?: boolean;
  grants?: BillingCreditGrantEntity[];
  emailingDomains?: { domain: string }[];
} = {}) => {
  const findOneWorkspace = jest.fn(async () => workspace);
  const cancelSubscription = jest.fn(async () => undefined);
  const listGrants = jest.fn(async () => grants);
  const revokeGrant = jest.fn(async ({ grantId }: { grantId: string }) =>
    buildGrant({ id: grantId, revokedAt: new Date() }),
  );
  const suspendWorkspace = jest.fn(async () => true);
  const findEmailingDomains = jest.fn(async () => emailingDomains);
  const addDeleteCascadeJob = jest.fn(async () => undefined);

  const service = new AdminPanelBillingService(
    {} as never,
    {} as never,
    { findOne: findOneWorkspace } as never,
    { cancelSubscription } as never,
    {} as never,
    { revokeGrant } as never,
    { listGrants } as never,
    {
      get: jest.fn((key: string) =>
        key === 'IS_BILLING_ENABLED' ? isBillingEnabled : undefined,
      ),
    } as never,
    { suspendWorkspace } as never,
    { find: findEmailingDomains } as never,
    { add: addDeleteCascadeJob } as never,
  );

  return {
    service,
    findOneWorkspace,
    cancelSubscription,
    listGrants,
    revokeGrant,
    suspendWorkspace,
    findEmailingDomains,
    addDeleteCascadeJob,
  };
};

describe('AdminPanelBillingService.suspendWorkspace', () => {
  it('should throw BILLING_SUBSCRIPTION_EVENT_WORKSPACE_NOT_FOUND when workspace is not found', async () => {
    const {
      service,
      cancelSubscription,
      revokeGrant,
      suspendWorkspace,
      addDeleteCascadeJob,
    } = buildService({ workspace: null });

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

    expect(cancelSubscription).not.toHaveBeenCalled();
    expect(revokeGrant).not.toHaveBeenCalled();
    expect(suspendWorkspace).not.toHaveBeenCalled();
    expect(addDeleteCascadeJob).not.toHaveBeenCalled();
  });

  it('should cancel subscription, revoke active credit grants, suspend workspace, and enqueue emailing domain cleanup when billing is enabled', async () => {
    const activeGrant1 = buildGrant({ id: 'grant-active-1' });
    const activeGrant2 = buildGrant({
      id: 'grant-active-2',
      expiresAt: new Date(Date.now() + 3_600_000),
    });
    const revokedGrant = buildGrant({
      id: 'grant-revoked',
      revokedAt: new Date(Date.now() - 10_000),
    });
    const expiredGrant = buildGrant({
      id: 'grant-expired',
      expiresAt: new Date(Date.now() - 10_000),
    });

    const {
      service,
      cancelSubscription,
      listGrants,
      revokeGrant,
      suspendWorkspace,
      findEmailingDomains,
      addDeleteCascadeJob,
    } = buildService({
      isBillingEnabled: true,
      grants: [activeGrant1, activeGrant2, revokedGrant, expiredGrant],
    });

    await expect(
      service.suspendWorkspace({
        workspaceId: WORKSPACE_ID,
        actorUserId: ACTOR_USER_ID,
      }),
    ).resolves.toBe(true);

    expect(cancelSubscription).toHaveBeenCalledTimes(1);
    expect(cancelSubscription).toHaveBeenCalledWith(WORKSPACE_ID);

    expect(listGrants).toHaveBeenCalledTimes(1);
    expect(listGrants).toHaveBeenCalledWith(WORKSPACE_ID);

    expect(revokeGrant).toHaveBeenCalledTimes(2);
    expect(revokeGrant).toHaveBeenCalledWith({
      workspaceId: WORKSPACE_ID,
      grantId: 'grant-active-1',
      revokedByUserId: ACTOR_USER_ID,
    });
    expect(revokeGrant).toHaveBeenCalledWith({
      workspaceId: WORKSPACE_ID,
      grantId: 'grant-active-2',
      revokedByUserId: ACTOR_USER_ID,
    });

    expect(suspendWorkspace).toHaveBeenCalledTimes(1);
    expect(suspendWorkspace).toHaveBeenCalledWith(WORKSPACE_ID);

    expect(findEmailingDomains).toHaveBeenCalledTimes(1);
    expect(findEmailingDomains).toHaveBeenCalledWith(WORKSPACE_ID);

    expect(addDeleteCascadeJob).toHaveBeenCalledTimes(1);
    expect(addDeleteCascadeJob).toHaveBeenCalledWith(
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

  it('should skip billing cancellation and credit grant revocation when billing is disabled, while still suspending workspace and cleaning up emailing domains', async () => {
    const {
      service,
      cancelSubscription,
      listGrants,
      revokeGrant,
      suspendWorkspace,
      findEmailingDomains,
      addDeleteCascadeJob,
    } = buildService({
      isBillingEnabled: false,
      grants: [buildGrant({ id: 'grant-active-1' })],
      emailingDomains: [{ domain: 'acme.com' }],
    });

    await expect(
      service.suspendWorkspace({
        workspaceId: WORKSPACE_ID,
        actorUserId: ACTOR_USER_ID,
      }),
    ).resolves.toBe(true);

    expect(cancelSubscription).not.toHaveBeenCalled();
    expect(listGrants).not.toHaveBeenCalled();
    expect(revokeGrant).not.toHaveBeenCalled();

    expect(suspendWorkspace).toHaveBeenCalledTimes(1);
    expect(suspendWorkspace).toHaveBeenCalledWith(WORKSPACE_ID);

    expect(findEmailingDomains).toHaveBeenCalledTimes(1);
    expect(findEmailingDomains).toHaveBeenCalledWith(WORKSPACE_ID);

    expect(addDeleteCascadeJob).toHaveBeenCalledTimes(1);
    expect(addDeleteCascadeJob).toHaveBeenCalledWith(
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
