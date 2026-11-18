import 'reflect-metadata';

import {
  type ExecutionContext,
  RequestMethod,
  type Type,
} from '@nestjs/common';
import {
  GUARDS_METADATA,
  METHOD_METADATA,
  PATH_METADATA,
  ROUTE_ARGS_METADATA,
} from '@nestjs/common/constants';
import { RouteParamtypes } from '@nestjs/common/enums/route-paramtypes.enum';
import { Test, type TestingModule } from '@nestjs/testing';
import { getDataSourceToken, getRepositoryToken } from '@nestjs/typeorm';
import { type Request, type Response } from 'express';

import { ApiKeyEntity } from 'src/engine/core-modules/api-key/api-key.entity';
import { ApiKeyModule } from 'src/engine/core-modules/api-key/api-key.module';
import { ApiKeyResolver } from 'src/engine/core-modules/api-key/api-key.resolver';
import { ApiKeyService } from 'src/engine/core-modules/api-key/services/api-key.service';
import { AppTokenEntity } from 'src/engine/core-modules/app-token/app-token.entity';
import { ApplicationLookupService } from 'src/engine/core-modules/application/application-lookup/application-lookup.service';
import { ApplicationEntity } from 'src/engine/core-modules/application/application.entity';
import { ApplicationExceptionCode } from 'src/engine/core-modules/application/application.exception';
import { ApplicationModule } from 'src/engine/core-modules/application/application.module';
import { ApplicationService } from 'src/engine/core-modules/application/application.service';
import { ApplicationOAuthModule } from 'src/engine/core-modules/application/application-oauth/application-oauth.module';
import { OAuthRegistrationController } from 'src/engine/core-modules/application/application-oauth/controllers/oauth-registration.controller';
import { OAuthService } from 'src/engine/core-modules/application/application-oauth/oauth.service';
import { ApplicationRegistrationLookupService } from 'src/engine/core-modules/application/application-registration/application-registration-lookup/application-registration-lookup.service';
import { ApplicationRegistrationEntity } from 'src/engine/core-modules/application/application-registration/application-registration.entity';
import { ApplicationRegistrationModule } from 'src/engine/core-modules/application/application-registration/application-registration.module';
import { ApplicationRegistrationService } from 'src/engine/core-modules/application/application-registration/application-registration.service';
import { ApplicationRegistrationSourceType } from 'src/engine/core-modules/application/application-registration/enums/application-registration-source-type.enum';
import { ApplicationVariableEntity } from 'src/engine/core-modules/application/application-variable/application-variable.entity';
import { AuthResolver } from 'src/engine/core-modules/auth/auth.resolver';
import { type AuthContextUser } from 'src/engine/core-modules/auth/types/auth-context.type';
import { DnsManagerService } from 'src/engine/core-modules/dns-manager/services/dns-manager.service';
import { DomainServerConfigService } from 'src/engine/core-modules/domain/domain-server-config/services/domain-server-config.service';
import { SubdomainManagerService } from 'src/engine/core-modules/domain/subdomain-manager/services/subdomain-manager.service';
import { SubdomainManagerModule } from 'src/engine/core-modules/domain/subdomain-manager/subdomain-manager.module';
import { FileStorageService } from 'src/engine/core-modules/file-storage/services/file-storage.service';
import { PublicDomainEntity } from 'src/engine/core-modules/public-domain/public-domain.entity';
import { PublicDomainModule } from 'src/engine/core-modules/public-domain/public-domain.module';
import { PublicDomainService } from 'src/engine/core-modules/public-domain/public-domain.service';
import {
  ThrottlerException,
  ThrottlerExceptionCode,
} from 'src/engine/core-modules/throttler/throttler.exception';
import { ThrottlerService } from 'src/engine/core-modules/throttler/throttler.service';
import { TwentyConfigService } from 'src/engine/core-modules/twenty-config/twenty-config.service';
import { UsageLimitQuotaService } from 'src/engine/core-modules/usage-limit/services/usage-limit-quota.service';
import { UsageLimitStockService } from 'src/engine/core-modules/usage-limit/services/usage-limit-stock.service';
import { UsageLimitService } from 'src/engine/core-modules/usage-limit/services/usage-limit.service';
import { UsageLimitEntity } from 'src/engine/core-modules/usage-limit/usage-limit.entity';
import { UsageLimitModule } from 'src/engine/core-modules/usage-limit/usage-limit.module';
import { UsageOperationType } from 'src/engine/core-modules/usage/enums/usage-operation-type.enum';
import { UsageResourceType } from 'src/engine/core-modules/usage/enums/usage-resource-type.enum';
import { UsageUnit } from 'src/engine/core-modules/usage/enums/usage-unit.enum';
import { UserWorkspaceEntity } from 'src/engine/core-modules/user-workspace/user-workspace.entity';
import { WorkspaceEntity } from 'src/engine/core-modules/workspace/workspace.entity';
import { WorkspaceExceptionCode } from 'src/engine/core-modules/workspace/workspace.exception';
import { ACCESS_POLICY_GUARD_CONFIG_KEY } from 'src/engine/guards/constants/access-policy-guard-config-key.constant';
import { NoPermissionGuard } from 'src/engine/guards/no-permission.guard';
import { PublicEndpointGuard } from 'src/engine/guards/public-endpoint.guard';
import { type AccessPolicy } from 'src/engine/guards/types/access-policy.type';
import { AgentEntity } from 'src/engine/metadata-modules/ai/ai-agent/entities/agent.entity';
import { CommandMenuItemEntity } from 'src/engine/metadata-modules/command-menu-item/entities/command-menu-item.entity';
import { FrontComponentEntity } from 'src/engine/metadata-modules/front-component/entities/front-component.entity';
import { LogicFunctionEntity } from 'src/engine/metadata-modules/logic-function/logic-function.entity';
import { ObjectMetadataEntity } from 'src/engine/metadata-modules/object-metadata/object-metadata.entity';
import { RoleTargetEntity } from 'src/engine/metadata-modules/role-target/role-target.entity';
import { RoleEntity } from 'src/engine/metadata-modules/role/role.entity';
import { SettingsMenuItemEntity } from 'src/engine/metadata-modules/settings-menu-item/entities/settings-menu-item.entity';
import { WorkspaceEventBroadcaster } from 'src/engine/subscriptions/workspace-event-broadcaster/workspace-event-broadcaster.service';
import { provideWorkspaceScopedRepository } from 'src/engine/twenty-orm/workspace-scoped-repository/provide-workspace-scoped-repository';
import { WorkspaceCacheService } from 'src/engine/workspace-cache/services/workspace-cache.service';

// ---------------------------------------------------------------------------
// Shared In-Memory TypeORM Repository + NestJS Metadata Invocation Helpers
// ---------------------------------------------------------------------------

type RowRecord = Record<string, unknown>;

function matchesOperator(actual: unknown, expected: unknown): boolean {
  if (
    expected !== null &&
    typeof expected === 'object' &&
    '_type' in (expected as Record<string, unknown>)
  ) {
    const op = expected as unknown as {
      _type: string;
      _value: unknown;
    };

    switch (op._type) {
      case 'not':
        return !matchesOperator(actual, op._value);
      case 'isNull':
        return actual === null || actual === undefined;
      case 'in':
        return Array.isArray(op._value) && op._value.includes(actual);
      case 'equal':
        return actual === op._value;
      case 'ilike':
        return (
          typeof actual === 'string' &&
          typeof expected !== 'undefined' &&
          typeof op._value === 'string' &&
          actual.toLowerCase() === op._value.toLowerCase()
        );
      case 'raw': {
        const params = (op as unknown as { _objectLiteralParameters?: Record<string, unknown> })._objectLiteralParameters;
        if (params && typeof actual === 'string') {
          const lowerActual = actual.toLowerCase();
          for (const val of Object.values(params)) {
            if (Array.isArray(val)) {
              return val.some((x) => String(x).toLowerCase() === lowerActual);
            }
            if (typeof val === 'string') {
              return val.toLowerCase() === lowerActual;
            }
          }
        }
        return false;
      }
      default:
        return actual === op._value;
    }
  }

  if (
    expected !== null &&
    typeof expected === 'object' &&
    !Array.isArray(expected) &&
    !(expected instanceof Date)
  ) {
    if (actual === null || typeof actual !== 'object') {
      return false;
    }

    return matchesClause(
      actual as RowRecord,
      expected as Record<string, unknown>,
      true,
    );
  }

  return actual === expected;
}

function matchesClause(
  row: RowRecord,
  clause: Record<string, unknown> | undefined,
  withDeleted: boolean,
): boolean {
  if (!withDeleted && row.deletedAt !== null && row.deletedAt !== undefined) {
    const explicitlyFiltersDeletedAt =
      clause !== undefined &&
      Object.prototype.hasOwnProperty.call(clause, 'deletedAt');

    if (!explicitlyFiltersDeletedAt) {
      return false;
    }
  }

  if (!clause) {
    return true;
  }

  for (const [key, expected] of Object.entries(clause)) {
    if (expected === undefined) {
      continue;
    }
    if (!matchesOperator(row[key], expected)) {
      return false;
    }
  }

  return true;
}

function matchesWhere(
  row: RowRecord,
  where: unknown,
  withDeleted = false,
): boolean {
  if (!where) {
    return matchesClause(row, undefined, withDeleted);
  }
  if (Array.isArray(where)) {
    return where.some((clause) =>
      matchesClause(row, clause as Record<string, unknown>, withDeleted),
    );
  }

  return matchesClause(row, where as Record<string, unknown>, withDeleted);
}

function createInMemoryRepository<T extends RowRecord>(initialRows: T[] = []) {
  const rows: T[] = initialRows.map((r) => ({ ...r }));

  const findMatching = (optionsOrWhere?: RowRecord, isDirectWhere = false) => {
    const withDeleted = isDirectWhere
      ? false
      : Boolean(optionsOrWhere?.withDeleted);
    const where = isDirectWhere ? optionsOrWhere : optionsOrWhere?.where;

    return rows.filter((row) => matchesWhere(row, where, withDeleted));
  };

  const knownColumns = new Set<string>(['id', 'workspaceId', 'spenderType', 'spenderId', 'resourceType', 'operationType', 'unit', 'period', 'maxAmount', 'isInstanceOverride']);
  for (const r of rows) {
    for (const k of Object.keys(r)) knownColumns.add(k);
  }

  return {
    rows,
    find: jest.fn(async (options?: RowRecord) =>
      findMatching(options).map((r) => ({ ...r })),
    ),
    findBy: jest.fn(async (where: RowRecord) =>
      findMatching(where, true).map((r) => ({ ...r })),
    ),
    findOne: jest.fn(async (options: RowRecord) => {
      const match = findMatching(options)[0];

      return match ? { ...match } : null;
    }),
    findOneBy: jest.fn(async (where: RowRecord) => {
      const match = findMatching(where, true)[0];

      return match ? { ...match } : null;
    }),
    findOneOrFail: jest.fn(async (options: RowRecord) => {
      const match = findMatching(options)[0];

      if (!match) {
        throw new Error('EntityNotFoundError');
      }

      return { ...match };
    }),
    count: jest.fn(async (options?: RowRecord) => findMatching(options).length),
    exists: jest.fn(
      async (options?: RowRecord) => findMatching(options).length > 0,
    ),
    existsBy: jest.fn(
      async (where: RowRecord) => findMatching(where, true).length > 0,
    ),
    insert: jest.fn(async (entityOrEntities: T | T[]) => {
      const list = Array.isArray(entityOrEntities)
        ? entityOrEntities
        : [entityOrEntities];

      for (const item of list) {
        rows.push({ ...item });
      }

      return { identifiers: list.map((i) => ({ id: i.id })), raw: [] };
    }),
    save: jest.fn(async (entity: T) => {
      const idx = rows.findIndex((r) => r.id && r.id === entity.id);

      if (idx >= 0) {
        rows[idx] = { ...rows[idx], ...entity };

        return { ...rows[idx] };
      }
      const created = { ...entity };

      rows.push(created);

      return { ...created };
    }),
    create: jest.fn((partial: Partial<T>) => ({ ...partial }) as T),
    update: jest.fn(
      async (criteria: RowRecord | string, partial: Partial<T>) => {
        const where =
          typeof criteria === 'string' ? { id: criteria } : criteria;
        let affected = 0;

        for (const row of rows) {
          if (matchesWhere(row, where, true)) {
            Object.assign(row, partial);
            affected += 1;
          }
        }

        return { affected, raw: [], generatedMaps: [] };
      },
    ),
    delete: jest.fn(async (criteria: RowRecord | string) => {
      const where = typeof criteria === 'string' ? { id: criteria } : criteria;
      let affected = 0;

      for (let i = rows.length - 1; i >= 0; i -= 1) {
        if (matchesWhere(rows[i], where, true)) {
          rows.splice(i, 1);
          affected += 1;
        }
      }

      return { affected, raw: [] };
    }),
    softDelete: jest.fn(async (criteria: RowRecord | string) => {
      const where = typeof criteria === 'string' ? { id: criteria } : criteria;
      let affected = 0;

      for (const row of rows) {
        if (matchesWhere(row, where, false)) {
          (row as RowRecord).deletedAt = new Date();
          affected += 1;
        }
      }

      return { affected, raw: [], generatedMaps: [] };
    }),
    remove: jest.fn(async (entityOrEntities: T | T[]) => {
      const list = Array.isArray(entityOrEntities)
        ? entityOrEntities
        : [entityOrEntities];

      for (const item of list) {
        const idx = rows.findIndex(
          (r) =>
            (item.id && r.id === item.id) ||
            (item.oAuthClientId && r.oAuthClientId === item.oAuthClientId),
        );

        if (idx >= 0) {
          rows.splice(idx, 1);
        }
      }

      return entityOrEntities;
    }),
    target: 'MockEntity',
    manager: {
      connection: {
        driver: {
          prepareHydratedValue: (v: unknown) => v,
        },
      },
    },
    metadata: {
      get columns() {
        for (const r of rows) {
          for (const k of Object.keys(r)) knownColumns.add(k);
        }
        return Array.from(knownColumns).map((k) => ({ propertyName: k, databaseName: k }));
      },
    },
    createQueryBuilder: jest.fn(() => {
      let currentPredicates: Array<(r: T) => boolean> = [];

      const qb = {
        where: (expr: string | RowRecord, params?: RowRecord) => {
          currentPredicates = [];
          if (typeof expr === 'object' && expr !== null) {
            currentPredicates.push((r) => matchesWhere(r, expr, true));
            return qb;
          }

          return qb.andWhere(expr, params);
        },
        andWhere: (expr: string, params?: RowRecord) => {
          if (params) {
            for (const [paramKey, paramVal] of Object.entries(params)) {
              if (expr.toLowerCase().includes('lower(')) {
                currentPredicates.push((r) =>
                  Object.values(r).some((v) =>
                    Array.isArray(paramVal)
                      ? typeof v === 'string' &&
                        paramVal
                          .map((x) => String(x).toLowerCase())
                          .includes(v.toLowerCase())
                      : typeof v === 'string' &&
                        v.toLowerCase() === String(paramVal).toLowerCase(),
                  ),
                );
              } else {
                currentPredicates.push((r) =>
                  Array.isArray(paramVal)
                    ? paramVal.includes(r[paramKey])
                    : r[paramKey] === paramVal ||
                      Object.values(r).includes(paramVal),
                );
              }
            }
          }

          return qb;
        },
        withDeleted: () => qb,
        select: () => qb,
        getOne: async () =>
          rows.find((r) => currentPredicates.every((p) => p(r))) ?? null,
        getMany: async () =>
          rows.filter((r) => currentPredicates.every((p) => p(r))),
        getRawMany: async () =>
          rows.filter((r) => currentPredicates.every((p) => p(r))),
        getCount: async () =>
          rows.filter((r) => currentPredicates.every((p) => p(r))).length,
        getExists: async () =>
          rows.some((r) => currentPredicates.every((p) => p(r))),
        delete: () => qb,
        from: () => qb,
        returning: () => qb,
        execute: async () => {
          const deleted: T[] = [];

          for (let i = rows.length - 1; i >= 0; i -= 1) {
            if (currentPredicates.every((p) => p(rows[i]))) {
              deleted.unshift({ ...rows[i] });
              rows.splice(i, 1);
            }
          }

          return { affected: deleted.length, raw: deleted };
        },
      };

      return qb;
    }),
  };
}

function createAutoLeafMock(): Record<string, jest.Mock> {
  return new Proxy(
    {},
    {
      get(target: Record<string | symbol, unknown>, prop: string | symbol) {
        if (
          prop === 'then' ||
          prop === 'onModuleInit' ||
          prop === 'onModuleDestroy' ||
          prop === 'beforeApplicationShutdown' ||
          prop === 'onApplicationBootstrap' ||
          prop === 'onApplicationShutdown' ||
          typeof prop === 'symbol'
        ) {
          return undefined;
        }
        if (!(prop in target)) {
          target[prop] = jest.fn();
        }

        return target[prop];
      },
    },
  ) as Record<string, jest.Mock>;
}

function getExtraClassProvidersFromModule(
  moduleClass: Type<unknown>,
  excludedClasses: Set<unknown>,
): Array<Type<unknown>> {
  const rawProviders: unknown[] =
    Reflect.getMetadata('providers', moduleClass) ?? [];

  return rawProviders.filter(
    (p): p is Type<unknown> =>
      typeof p === 'function' &&
      !excludedClasses.has(p) &&
      !p.name.endsWith('CronJob') &&
      !p.name.endsWith('CronCommand') &&
      !p.name.endsWith('HealthIndicator') &&
      !p.name.endsWith('Health'),
  );
}

async function invokeGraphqlMethod<TResult>(
  instance: object,
  classRef: Type<unknown>,
  methodName: string,
  context: {
    gqlArgs: Record<string, unknown>;
    user?: Partial<AuthContextUser>;
    workspace?: Partial<WorkspaceEntity>;
  },
): Promise<TResult> {
  const fn = (instance as Record<string, (...a: unknown[]) => Promise<TResult>>)[
    methodName
  ];

  if (typeof fn !== 'function') {
    throw new Error(
      `Missing GraphQL method ${classRef.name}.${methodName}`,
    );
  }

  const routeArgs: Record<
    string,
    { index: number; data?: unknown; factory?: (...a: unknown[]) => unknown }
  > = Reflect.getMetadata(ROUTE_ARGS_METADATA, classRef, methodName) ?? {};

  const execCtx = {
    getType: () => 'http',
    switchToHttp: () => ({
      getRequest: () => ({
        user: context.user,
        workspace: context.workspace,
      }),
    }),
  } as unknown as ExecutionContext;

  const args: unknown[] = [];

  for (const [key, meta] of Object.entries(routeArgs)) {
    if (key.includes('__customRouteArgs__') && typeof meta.factory === 'function') {
      args[meta.index] = meta.factory(meta.data, execCtx);
    } else if (typeof meta.data === 'string' && meta.data.length > 0) {
      if (meta.data in context.gqlArgs) {
        args[meta.index] = context.gqlArgs[meta.data];
      } else if (meta.data === 'input') {
        args[meta.index] = context.gqlArgs;
      } else {
        args[meta.index] = context.gqlArgs[meta.data];
      }
    } else {
      args[meta.index] = context.gqlArgs;
    }
  }

  if (Object.keys(routeArgs).length === 0) {
    args.push(context.gqlArgs);
  }

  return await fn.apply(instance, args);
}

function getMethodGuards(
  classRef: Type<unknown>,
  methodName: string,
): Array<Type<unknown>> {
  const fn = (classRef.prototype as Record<string, unknown>)[methodName];

  if (typeof fn !== 'function') {
    return [];
  }

  return (Reflect.getMetadata(GUARDS_METADATA, fn) ?? []) as Array<
    Type<unknown>
  >;
}

function findAccessPolicyOnMethod(
  classRef: Type<unknown>,
  methodName: string,
): AccessPolicy | undefined {
  for (const guard of getMethodGuards(classRef, methodName)) {
    const policy = Reflect.getMetadata(
      ACCESS_POLICY_GUARD_CONFIG_KEY,
      guard,
    ) as AccessPolicy | undefined;

    if (policy) {
      return policy;
    }
  }

  return undefined;
}

// ---------------------------------------------------------------------------
// Acceptance Suite: 20 Behavioral Assertions (5 per Feature)
// ---------------------------------------------------------------------------

describe('State Ownership & Domain Boundary Benchmark Acceptance Suite', () => {
  beforeAll(() => {
    jest.useRealTimers();
  });

  // =========================================================================
  // FEATURE 1: Scoped Usage Limit Cleanup on API Key Revocation (5 assertions)
  // =========================================================================
  describe('Feature 1: Scoped Usage Limit Cleanup on API Key Revocation', () => {
    const WORKSPACE_ID = '11111111-1111-4111-8111-111111111111';
    const OTHER_WORKSPACE_ID = '99999999-9999-4999-8999-999999999999';
    const API_KEY_ID = '22222222-2222-4222-8222-222222222222';
    const OTHER_API_KEY_ID = '33333333-3333-4333-8333-333333333333';

    const buildFeature1Harness = async (seed: {
      apiKeys?: Array<Record<string, unknown>>;
      usageLimits?: Array<Record<string, unknown>>;
    }) => {
      const apiKeyRepo = createInMemoryRepository(seed.apiKeys ?? []);
      const usageLimitRepo = createInMemoryRepository(seed.usageLimits ?? []);
      const cacheInvalidateMock = jest.fn().mockResolvedValue(undefined);
      const dropQuotaCounterMock = jest.fn().mockResolvedValue(undefined);
      const dropStockCounterMock = jest.fn().mockResolvedValue(undefined);

      const excluded = new Set<unknown>([
        ApiKeyResolver,
        ApiKeyService,
        UsageLimitService,
        UsageLimitQuotaService,
        UsageLimitStockService,
        WorkspaceCacheService,
      ]);
      const extraApiKeyProviders = getExtraClassProvidersFromModule(
        ApiKeyModule,
        excluded,
      ).filter(
        (p) => !p.name.endsWith('Resolver') && !p.name.endsWith('Command'),
      );
      const extraUsageLimitProviders = getExtraClassProvidersFromModule(
        UsageLimitModule,
        excluded,
      ).filter(
        (p) => !p.name.endsWith('Resolver') && !p.name.endsWith('Command'),
      );

      const moduleRef: TestingModule = await Test.createTestingModule({
        providers: [
          ApiKeyResolver,
          ApiKeyService,
          UsageLimitService,
          ...extraApiKeyProviders,
          ...extraUsageLimitProviders,
          {
            provide: getRepositoryToken(ApiKeyEntity),
            useValue: apiKeyRepo,
          },
          provideWorkspaceScopedRepository(ApiKeyEntity),
          {
            provide: getRepositoryToken(UsageLimitEntity),
            useValue: usageLimitRepo,
          },
          provideWorkspaceScopedRepository(UsageLimitEntity),
          {
            provide: getRepositoryToken(ApplicationEntity),
            useValue: createInMemoryRepository([]),
          },
          provideWorkspaceScopedRepository(ApplicationEntity),
          {
            provide: getRepositoryToken(UserWorkspaceEntity),
            useValue: createInMemoryRepository([]),
          },
          provideWorkspaceScopedRepository(UserWorkspaceEntity),
          {
            provide: getRepositoryToken(AgentEntity),
            useValue: createInMemoryRepository([]),
          },
          provideWorkspaceScopedRepository(AgentEntity),
          {
            provide: getRepositoryToken(LogicFunctionEntity),
            useValue: createInMemoryRepository([]),
          },
          provideWorkspaceScopedRepository(LogicFunctionEntity),
          {
            provide: getRepositoryToken(RoleEntity),
            useValue: createInMemoryRepository([]),
          },
          provideWorkspaceScopedRepository(RoleEntity),
          {
            provide: getRepositoryToken(RoleTargetEntity),
            useValue: createInMemoryRepository([]),
          },
          provideWorkspaceScopedRepository(RoleTargetEntity),
          {
            provide: getRepositoryToken(WorkspaceEntity),
            useValue: createInMemoryRepository([]),
          },
          {
            provide: WorkspaceCacheService,
            useValue: {
              ...createAutoLeafMock(),
              invalidateAndRecompute: cacheInvalidateMock,
            },
          },
          {
            provide: UsageLimitQuotaService,
            useValue: {
              ...createAutoLeafMock(),
              dropLimitCounter: dropQuotaCounterMock,
            },
          },
          {
            provide: UsageLimitStockService,
            useValue: {
              ...createAutoLeafMock(),
              dropLimitCounter: dropStockCounterMock,
            },
          },
        ],
      })
        .useMocker(() => createAutoLeafMock())
        .compile();

      return {
        apiKeyService: moduleRef.get(ApiKeyService),
        apiKeyRepo,
        usageLimitRepo,
        cacheInvalidateMock,
        dropQuotaCounterMock,
        dropStockCounterMock,
      };
    };

    const makeQuotaLimit = (overrides: Record<string, unknown> = {}) => ({
      id: 'ul-quota-1',
      workspaceId: WORKSPACE_ID,
      resourceType: UsageResourceType.AI,
      operationType: UsageOperationType.AI_CHAT_TOKEN,
      spenderType: 'apiKey',
      spenderId: API_KEY_ID,
      limitKind: 'quota',
      periodCount: 1,
      periodUnit: 'week',
      unit: UsageUnit.CREDIT,
      limitValue: 1000,
      burstValue: null,
      isInstanceOverride: false,
      ...overrides,
    });

    it('1.1 revokes an active API key and deletes all apiKey-scoped usage limits (including operator instance overrides) while leaving other keys and workspaces intact', async () => {
      const { apiKeyService, usageLimitRepo } = await buildFeature1Harness({
        apiKeys: [
          {
            id: API_KEY_ID,
            workspaceId: WORKSPACE_ID,
            name: 'CI Bot Key',
            expiresAt: new Date('2030-01-01T00:00:00Z'),
            revokedAt: null,
          },
        ],
        usageLimits: [
          makeQuotaLimit({ id: 'ul-regular', isInstanceOverride: false }),
          makeQuotaLimit({
            id: 'ul-operator-override',
            operationType: UsageOperationType.WEB_SEARCH,
            isInstanceOverride: true,
          }),
          makeQuotaLimit({
            id: 'ul-other-key',
            spenderId: OTHER_API_KEY_ID,
          }),
          makeQuotaLimit({
            id: 'ul-other-ws',
            workspaceId: OTHER_WORKSPACE_ID,
          }),
          makeQuotaLimit({
            id: 'ul-app-spender',
            spenderType: 'application',
            spenderId: API_KEY_ID,
          }),
        ],
      });

      const revoked = await apiKeyService.revoke(API_KEY_ID, WORKSPACE_ID);

      expect(revoked).not.toBeNull();
      expect(revoked?.revokedAt).toBeInstanceOf(Date);
      expect(usageLimitRepo.rows.map((r) => r.id)).toEqual([
        'ul-other-key',
        'ul-other-ws',
        'ul-app-spender',
      ]);
    });

    it('1.2 invalidates and recomputes the workspace usageLimits cache when scoped usage limits are removed on API key revocation', async () => {
      const { apiKeyService, cacheInvalidateMock } = await buildFeature1Harness({
        apiKeys: [
          {
            id: API_KEY_ID,
            workspaceId: WORKSPACE_ID,
            name: 'Integration Key',
            expiresAt: new Date('2030-01-01T00:00:00Z'),
            revokedAt: null,
          },
        ],
        usageLimits: [makeQuotaLimit({ id: 'ul-cached-1' })],
      });

      await apiKeyService.revoke(API_KEY_ID, WORKSPACE_ID);

      const invalidatedKeys = cacheInvalidateMock.mock.calls
        .filter(([wsId]: [string]) => wsId === WORKSPACE_ID)
        .flatMap(([, keys]: [string, string[]]) => keys);

      expect(invalidatedKeys).toContain('usageLimits');
    });

    it('1.3 drops active usage-limit counters via UsageLimitQuotaService.dropLimitCounter when quota limits are removed on API key revocation', async () => {
      const { apiKeyService, dropQuotaCounterMock } =
        await buildFeature1Harness({
          apiKeys: [
            {
              id: API_KEY_ID,
              workspaceId: WORKSPACE_ID,
              name: 'Quota Key',
              expiresAt: new Date('2030-01-01T00:00:00Z'),
              revokedAt: null,
            },
          ],
          usageLimits: [
            makeQuotaLimit({ id: 'ul-q-1' }),
            makeQuotaLimit({
              id: 'ul-q-2',
              operationType: UsageOperationType.WEB_SEARCH,
              isInstanceOverride: true,
            }),
          ],
        });

      await apiKeyService.revoke(API_KEY_ID, WORKSPACE_ID);

      expect(dropQuotaCounterMock).toHaveBeenCalledTimes(2);
      expect(dropQuotaCounterMock).toHaveBeenCalledWith(
        expect.objectContaining({
          workspaceId: WORKSPACE_ID,
          usageLimit: expect.objectContaining({ id: 'ul-q-1' }),
        }),
      );
      expect(dropQuotaCounterMock).toHaveBeenCalledWith(
        expect.objectContaining({
          workspaceId: WORKSPACE_ID,
          usageLimit: expect.objectContaining({ id: 'ul-q-2' }),
        }),
      );
    });

    it('1.4 revokes cleanly when the API key has zero scoped usage limits without affecting unrelated limits', async () => {
      const { apiKeyService, usageLimitRepo, dropQuotaCounterMock } =
        await buildFeature1Harness({
          apiKeys: [
            {
              id: API_KEY_ID,
              workspaceId: WORKSPACE_ID,
              name: 'No-Limits Key',
              expiresAt: new Date('2030-01-01T00:00:00Z'),
              revokedAt: null,
            },
          ],
          usageLimits: [
            makeQuotaLimit({ id: 'ul-unrelated', spenderId: OTHER_API_KEY_ID }),
          ],
        });

      const revoked = await apiKeyService.revoke(API_KEY_ID, WORKSPACE_ID);

      expect(revoked?.revokedAt).toBeInstanceOf(Date);
      expect(usageLimitRepo.rows).toHaveLength(1);
      expect(dropQuotaCounterMock).not.toHaveBeenCalled();
    });

    it('1.5 returns null for missing API key and leaves an already-revoked API key and usage limits untouched', async () => {
      const originalRevokedAt = new Date('2026-01-15T12:00:00.000Z');
      const {
        apiKeyService,
        usageLimitRepo,
        dropQuotaCounterMock,
      } = await buildFeature1Harness({
        apiKeys: [
          {
            id: API_KEY_ID,
            workspaceId: WORKSPACE_ID,
            name: 'Already Revoked Key',
            expiresAt: new Date('2030-01-01T00:00:00Z'),
            revokedAt: originalRevokedAt,
          },
        ],
        usageLimits: [makeQuotaLimit({ id: 'ul-keep-on-noop' })],
      });

      const missing = await apiKeyService.revoke(
        '00000000-0000-4000-8000-000000000000',
        WORKSPACE_ID,
      );
      expect(missing).toBeNull();

      const alreadyRevoked = await apiKeyService.revoke(
        API_KEY_ID,
        WORKSPACE_ID,
      );
      expect(alreadyRevoked).not.toBeNull();
      expect((alreadyRevoked?.revokedAt as Date)?.toISOString()).toBe(
        originalRevokedAt.toISOString(),
      );
      expect(usageLimitRepo.rows).toHaveLength(1);
      expect(dropQuotaCounterMock).not.toHaveBeenCalled();
    });
  });

  // =========================================================================
  // FEATURE 2: RFC 7592 Dynamic OAuth Client De-Registration (5 assertions)
  // =========================================================================
  describe('Feature 2: DELETE /oauth/register/:clientId', () => {
    const findDeleteRouteHandler = (): {
      controllerClass: Type<unknown>;
      handlerName: string;
    } => {
      const moduleControllers = (
        (Reflect.getMetadata('controllers', ApplicationOAuthModule) as
          | Array<Type<unknown>>
          | undefined) ?? []
      ).filter((c) => c !== OAuthRegistrationController);
      const candidates: Array<Type<unknown>> = [
        OAuthRegistrationController,
        ...moduleControllers,
      ];

      for (const controllerClass of candidates) {
        const ctrlPath = String(
          Reflect.getMetadata(PATH_METADATA, controllerClass) ?? '',
        ).replace(/^\/+|\/+$/g, '');
        const proto = controllerClass.prototype as unknown as Record<
          string,
          unknown
        >;

        for (const name of Object.getOwnPropertyNames(proto)) {
          if (name === 'constructor') {
            continue;
          }
          const candidate = proto[name];

          if (typeof candidate !== 'function') {
            continue;
          }
          const httpMethod = Reflect.getMetadata(METHOD_METADATA, candidate);
          const routePath = Reflect.getMetadata(PATH_METADATA, candidate);

          if (httpMethod !== RequestMethod.DELETE || typeof routePath !== 'string') {
            continue;
          }

          const cleanRoute = routePath.replace(/^\/+|\/+$/g, '');
          const fullPath = ctrlPath ? `${ctrlPath}/${cleanRoute}` : cleanRoute;

          if (
            cleanRoute === 'register/:clientId' ||
            fullPath === 'oauth/register/:clientId'
          ) {
            return { controllerClass, handlerName: name };
          }
        }
      }

      throw new Error(
        'Missing @Delete("register/:clientId") handler in ApplicationOAuthModule controllers',
      );
    };

    interface MockResponse {
      statusCode: number;
      headers: Record<string, string>;
      status: jest.Mock<MockResponse, [number]>;
      setHeader: jest.Mock<MockResponse, [string, string]>;
      header: jest.Mock<MockResponse, [string, string]>;
      set: jest.Mock<MockResponse, [string | Record<string, string>, string?]>;
      getStatus: () => number;
    }

    const createMockRes = (): MockResponse => {
      const headers: Record<string, string> = {};
      let statusCode = 200;
      const res: MockResponse = {
        statusCode: 200,
        headers,
        status: jest.fn((code: number): MockResponse => {
          statusCode = code;
          res.statusCode = code;

          return res;
        }),
        setHeader: jest.fn((k: string, v: string): MockResponse => {
          headers[k.toLowerCase()] = String(v);

          return res;
        }),
        header: jest.fn((k: string, v: string): MockResponse => {
          headers[k.toLowerCase()] = String(v);

          return res;
        }),
        set: jest.fn(
          (k: string | Record<string, string>, v?: string): MockResponse => {
            if (typeof k === 'string') {
              headers[k.toLowerCase()] = String(v ?? '');
            } else {
              for (const [hk, hv] of Object.entries(k)) {
                headers[hk.toLowerCase()] = String(hv);
              }
            }

            return res;
          },
        ),
        getStatus: () => statusCode,
      };

      return res;
    };

    const invokeDeleteRegisterEndpoint = async (
      controller: unknown,
      clientId: string,
      ip = '203.0.113.42',
    ) => {
      const { handlerName } = findDeleteRouteHandler();
      const fn = (
        controller as unknown as Record<
          string,
          (...a: unknown[]) => Promise<unknown>
        >
      )[handlerName];
      const httpCodeMeta = Reflect.getMetadata('__httpCode__', fn) as
        | number
        | undefined;
      const res = createMockRes();

      if (typeof httpCodeMeta === 'number') {
        res.status(httpCodeMeta);
      }

      const req = { ip } as unknown as Request;
      const routeArgs: Record<
        string,
        { index: number; data?: string }
      > =
        Reflect.getMetadata(
          ROUTE_ARGS_METADATA,
          OAuthRegistrationController,
          handlerName,
        ) ?? {};

      const args: unknown[] = [];

      for (const [key, meta] of Object.entries(routeArgs)) {
        const paramType = Number(key.split(':')[0]);

        if (paramType === RouteParamtypes.PARAM) {
          args[meta.index] =
            meta.data === 'clientId' || !meta.data
              ? meta.data
                ? clientId
                : { clientId }
              : undefined;
        } else if (paramType === RouteParamtypes.REQUEST) {
          args[meta.index] = req;
        } else if (paramType === RouteParamtypes.RESPONSE) {
          args[meta.index] = res as unknown as Response;
        }
      }

      const body = await fn.apply(controller, args);

      return { status: res.getStatus(), headers: res.headers, body };
    };

    const buildFeature2Harness = async (options: {
      registrations?: Array<Record<string, unknown>>;
      rateLimited?: boolean;
    }) => {
      const regRepo = createInMemoryRepository(options.registrations ?? []);
      const throttleMock = options.rateLimited
        ? jest
            .fn()
            .mockRejectedValue(
              new ThrottlerException(
                'Rate limited',
                ThrottlerExceptionCode.LIMIT_REACHED,
              ),
            )
        : jest.fn().mockResolvedValue(undefined);

      const excluded = new Set<unknown>([
        OAuthRegistrationController,
        OAuthService,
        ApplicationRegistrationService,
        ApplicationRegistrationLookupService,
      ]);
      const extraOAuthProviders = getExtraClassProvidersFromModule(
        ApplicationOAuthModule,
        excluded,
      ).filter((p) => !p.name.endsWith('Resolver'));
      const extraRegProviders = getExtraClassProvidersFromModule(
        ApplicationRegistrationModule,
        excluded,
      ).filter((p) => !p.name.endsWith('Resolver'));

      const moduleControllers =
        (Reflect.getMetadata('controllers', ApplicationOAuthModule) as
          | Array<Type<unknown>>
          | undefined) ?? [];

      const moduleRef: TestingModule = await Test.createTestingModule({
        providers: [
          OAuthRegistrationController,
          ...moduleControllers.filter((c) => c !== OAuthRegistrationController),
          OAuthService,
          ApplicationRegistrationService,
          ApplicationRegistrationLookupService,
          ...extraOAuthProviders,
          ...extraRegProviders,
          {
            provide: getRepositoryToken(ApplicationRegistrationEntity),
            useValue: regRepo,
          },
          {
            provide: getRepositoryToken(AppTokenEntity),
            useValue: createInMemoryRepository([]),
          },
          {
            provide: getRepositoryToken(ApplicationEntity),
            useValue: createInMemoryRepository([]),
          },
          provideWorkspaceScopedRepository(ApplicationEntity),
          {
            provide: getRepositoryToken(UserWorkspaceEntity),
            useValue: createInMemoryRepository([]),
          },
          {
            provide: ThrottlerService,
            useValue: {
              ...createAutoLeafMock(),
              tokenBucketThrottleOrThrow: throttleMock,
            },
          },
        ],
      })
        .useMocker(() => createAutoLeafMock())
        .compile();

      const { controllerClass } = findDeleteRouteHandler();

      return {
        controller: moduleRef.get(controllerClass, { strict: false }),
        regRepo,
        throttleMock,
      };
    };

    it('2.1 deletes an OAUTH_ONLY dynamic registration, sets Cache-Control/Pragma headers, and returns HTTP 204 with empty body', async () => {
      const { controller, regRepo, throttleMock } = await buildFeature2Harness({
        registrations: [
          {
            id: 'reg-oauth-1',
            oAuthClientId: 'client-oauth-1',
            sourceType: ApplicationRegistrationSourceType.OAUTH_ONLY,
            name: 'MCP Dynamic Client',
            deletedAt: null,
          },
        ],
      });

      const res = await invokeDeleteRegisterEndpoint(
        controller,
        'client-oauth-1',
        '198.51.100.7',
      );

      expect(res.status).toBe(204);
      expect(res.body).toBeUndefined();
      expect(res.headers['cache-control']).toBe('no-store');
      expect(res.headers.pragma).toBe('no-cache');
      expect(
        regRepo.rows.filter((r) => !r.deletedAt && r.oAuthClientId === 'client-oauth-1'),
      ).toHaveLength(0);
      expect(throttleMock).toHaveBeenCalledWith(
        'oauth-register:198.51.100.7',
        1,
        expect.any(Number),
        expect.any(Number),
      );
    });

    it('2.2 returns HTTP 404 invalid_client when clientId does not exist', async () => {
      const { controller } = await buildFeature2Harness({ registrations: [] });

      const res = await invokeDeleteRegisterEndpoint(
        controller,
        'non-existent-client-id',
      );

      expect(res.status).toBe(404);
      expect(res.body).toEqual({
        error: 'invalid_client',
        error_description: 'Client not found',
      });
    });

    it('2.3 refuses to delete non-OAUTH_ONLY registrations (NPM / LOCAL / TARBALL), returning 404 invalid_client and keeping them intact', async () => {
      const { controller, regRepo } = await buildFeature2Harness({
        registrations: [
          {
            id: 'reg-npm-1',
            oAuthClientId: 'client-npm-1',
            sourceType: ApplicationRegistrationSourceType.NPM,
            name: 'Marketplace App',
            deletedAt: null,
          },
          {
            id: 'reg-local-1',
            oAuthClientId: 'client-local-1',
            sourceType: ApplicationRegistrationSourceType.LOCAL,
            name: 'Workspace App',
            ownerWorkspaceId: 'ws-1',
            deletedAt: null,
          },
        ],
      });

      const npmRes = await invokeDeleteRegisterEndpoint(
        controller,
        'client-npm-1',
      );
      const localRes = await invokeDeleteRegisterEndpoint(
        controller,
        'client-local-1',
      );

      expect(npmRes.status).toBe(404);
      expect(npmRes.body).toEqual({
        error: 'invalid_client',
        error_description: 'Client not found',
      });
      expect(localRes.status).toBe(404);
      expect(
        regRepo.rows.filter((r) => !r.deletedAt),
      ).toHaveLength(2);
    });

    it('2.4 enforces IP rate limiting and returns HTTP 429 rate_limit_exceeded without deleting client', async () => {
      const { controller, regRepo } = await buildFeature2Harness({
        rateLimited: true,
        registrations: [
          {
            id: 'reg-oauth-2',
            oAuthClientId: 'client-oauth-2',
            sourceType: ApplicationRegistrationSourceType.OAUTH_ONLY,
            deletedAt: null,
          },
        ],
      });

      const res = await invokeDeleteRegisterEndpoint(
        controller,
        'client-oauth-2',
      );

      expect(res.status).toBe(429);
      expect(res.body).toEqual({
        error: 'rate_limit_exceeded',
        error_description:
          'Too many registration requests, please try again later',
      });
      expect(regRepo.rows.filter((r) => !r.deletedAt)).toHaveLength(1);
    });

    it('2.5 guards the DELETE register/:clientId route with PublicEndpointGuard and NoPermissionGuard', () => {
      const { controllerClass, handlerName } = findDeleteRouteHandler();
      const guards = getMethodGuards(controllerClass, handlerName);

      expect(guards).toContain(PublicEndpointGuard);
      expect(guards).toContain(NoPermissionGuard);
    });
  });

  // =========================================================================
  // FEATURE 3: Public Domain Teardown on Application Deletion (5 assertions)
  // =========================================================================
  describe('Feature 3: Public Domain Teardown on Application Deletion', () => {
    const WS_ID = '55555555-5555-4555-8555-555555555555';
    const OTHER_WS_ID = '66666666-6666-4666-8666-666666666666';
    const APP_ID = '77777777-7777-4777-8777-777777777777';
    const APP_UID = 'com.acme.portal-app';
    const OTHER_APP_ID = '88888888-8888-4888-8888-888888888888';

    const buildFeature3Harness = async (seed: {
      applications?: Array<Record<string, unknown>>;
      publicDomains?: Array<Record<string, unknown>>;
    }) => {
      const applicationRepo = createInMemoryRepository(seed.applications ?? []);
      const publicDomainRepo = createInMemoryRepository(
        seed.publicDomains ?? [],
      );
      const deleteHostnameMock = jest.fn().mockResolvedValue(undefined);
      const cacheInvalidateMock = jest.fn().mockResolvedValue(undefined);
      const broadcastMock = jest.fn().mockResolvedValue(undefined);

      const resolveRepoForTarget = (target: unknown) => {
        if (target === ApplicationEntity) return applicationRepo;
        if (target === PublicDomainEntity) return publicDomainRepo;

        return createInMemoryRepository([]);
      };

      const queryRunnerMock = {
        isTransactionActive: false,
        connect: jest.fn().mockResolvedValue(undefined),
        startTransaction: jest.fn(async () => {
          queryRunnerMock.isTransactionActive = true;
        }),
        commitTransaction: jest.fn(async () => {
          queryRunnerMock.isTransactionActive = false;
        }),
        rollbackTransaction: jest.fn(async () => {
          queryRunnerMock.isTransactionActive = false;
        }),
        release: jest.fn().mockResolvedValue(undefined),
        manager: {
          find: jest.fn(
            async (entityClass: unknown, options?: Record<string, unknown>) =>
              resolveRepoForTarget(entityClass).find(options),
          ),
          findBy: jest.fn(
            async (entityClass: unknown, where: Record<string, unknown>) =>
              resolveRepoForTarget(entityClass).findBy(where),
          ),
          update: jest.fn(
            async (
              entityClass: unknown,
              criteria: Record<string, unknown> | string,
              partial: Record<string, unknown>,
            ) => resolveRepoForTarget(entityClass).update(criteria, partial),
          ),
          delete: jest.fn(
            async (
              entityClass: unknown,
              criteria: Record<string, unknown> | string,
            ) => resolveRepoForTarget(entityClass).delete(criteria),
          ),
          getRepository: jest.fn((entityClass: unknown) =>
            resolveRepoForTarget(entityClass),
          ),
        },
      };

      const excluded = new Set<unknown>([
        ApplicationService,
        ApplicationLookupService,
        PublicDomainService,
        DnsManagerService,
        FileStorageService,
        WorkspaceCacheService,
        WorkspaceEventBroadcaster,
      ]);
      const extraAppProviders = getExtraClassProvidersFromModule(
        ApplicationModule,
        excluded,
      ).filter(
        (p) => !p.name.endsWith('Resolver') && !p.name.endsWith('Command'),
      );
      const extraPdProviders = getExtraClassProvidersFromModule(
        PublicDomainModule,
        excluded,
      ).filter(
        (p) => !p.name.endsWith('Resolver') && !p.name.endsWith('Command'),
      );

      const moduleRef: TestingModule = await Test.createTestingModule({
        providers: [
          ApplicationService,
          ApplicationLookupService,
          PublicDomainService,
          ...extraAppProviders,
          ...extraPdProviders,
          {
            provide: getDataSourceToken(),
            useValue: {
              createQueryRunner: () => queryRunnerMock,
            },
          },
          {
            provide: getRepositoryToken(ApplicationEntity),
            useValue: applicationRepo,
          },
          provideWorkspaceScopedRepository(ApplicationEntity),
          {
            provide: getRepositoryToken(PublicDomainEntity),
            useValue: publicDomainRepo,
          },
          provideWorkspaceScopedRepository(PublicDomainEntity),
          {
            provide: getRepositoryToken(ApplicationRegistrationEntity),
            useValue: createInMemoryRepository([]),
          },
          {
            provide: getRepositoryToken(WorkspaceEntity),
            useValue: createInMemoryRepository([{ id: WS_ID }]),
          },
          {
            provide: getRepositoryToken(LogicFunctionEntity),
            useValue: createInMemoryRepository([]),
          },
          provideWorkspaceScopedRepository(LogicFunctionEntity),
          {
            provide: getRepositoryToken(AgentEntity),
            useValue: createInMemoryRepository([]),
          },
          provideWorkspaceScopedRepository(AgentEntity),
          {
            provide: getRepositoryToken(FrontComponentEntity),
            useValue: createInMemoryRepository([]),
          },
          provideWorkspaceScopedRepository(FrontComponentEntity),
          {
            provide: getRepositoryToken(CommandMenuItemEntity),
            useValue: createInMemoryRepository([]),
          },
          provideWorkspaceScopedRepository(CommandMenuItemEntity),
          {
            provide: getRepositoryToken(SettingsMenuItemEntity),
            useValue: createInMemoryRepository([]),
          },
          provideWorkspaceScopedRepository(SettingsMenuItemEntity),
          {
            provide: getRepositoryToken(ObjectMetadataEntity),
            useValue: createInMemoryRepository([]),
          },
          provideWorkspaceScopedRepository(ObjectMetadataEntity),
          {
            provide: getRepositoryToken(ApplicationVariableEntity),
            useValue: createInMemoryRepository([]),
          },
          provideWorkspaceScopedRepository(ApplicationVariableEntity),
          {
            provide: DnsManagerService,
            useValue: {
              ...createAutoLeafMock(),
              deleteHostnameSilently: deleteHostnameMock,
            },
          },
          {
            provide: FileStorageService,
            useValue: {
              ...createAutoLeafMock(),
              deleteApplicationFileRows: jest.fn().mockResolvedValue(undefined),
              deleteApplicationFilesFromStorage: jest
                .fn()
                .mockResolvedValue(undefined),
              invalidateStorageStock: jest.fn().mockResolvedValue(undefined),
            },
          },
          {
            provide: WorkspaceCacheService,
            useValue: {
              ...createAutoLeafMock(),
              invalidateAndRecompute: cacheInvalidateMock,
            },
          },
          {
            provide: WorkspaceEventBroadcaster,
            useValue: {
              ...createAutoLeafMock(),
              broadcast: broadcastMock,
            },
          },
        ],
      })
        .useMocker(() => createAutoLeafMock())
        .compile();

      return {
        applicationService: moduleRef.get(ApplicationService),
        applicationRepo,
        publicDomainRepo,
        deleteHostnameMock,
        cacheInvalidateMock,
      };
    };

    it('3.1 deletes the target application and removes all publicDomain records owned by that application while keeping other applications and workspaces intact', async () => {
      const { applicationService, applicationRepo, publicDomainRepo } =
        await buildFeature3Harness({
          applications: [
            {
              id: APP_ID,
              universalIdentifier: APP_UID,
              workspaceId: WS_ID,
              name: 'Portal App',
              deletedAt: null,
            },
            {
              id: OTHER_APP_ID,
              universalIdentifier: 'com.acme.other-app',
              workspaceId: WS_ID,
              name: 'Other App',
              deletedAt: null,
            },
          ],
          publicDomains: [
            {
              id: 'pd-1',
              domain: 'portal.acme.com',
              applicationId: APP_ID,
              workspaceId: WS_ID,
              isValidated: true,
            },
            {
              id: 'pd-2',
              domain: 'docs.acme.com',
              applicationId: APP_ID,
              workspaceId: WS_ID,
              isValidated: false,
            },
            {
              id: 'pd-other-app',
              domain: 'status.acme.com',
              applicationId: OTHER_APP_ID,
              workspaceId: WS_ID,
              isValidated: true,
            },
            {
              id: 'pd-other-ws',
              domain: 'other.example.com',
              applicationId: APP_ID,
              workspaceId: OTHER_WS_ID,
              isValidated: true,
            },
          ],
        });

      await applicationService.delete(APP_UID, WS_ID);

      expect(applicationRepo.rows.map((a) => a.id)).toEqual([OTHER_APP_ID]);
      expect(publicDomainRepo.rows.map((d) => d.id)).toEqual([
        'pd-other-app',
        'pd-other-ws',
      ]);
    });

    it('3.2 tears down external custom hostnames via DnsManagerService.deleteHostnameSilently({ isPublicDomain: true }) for each public domain of the deleted application', async () => {
      const { applicationService, deleteHostnameMock } =
        await buildFeature3Harness({
          applications: [
            {
              id: APP_ID,
              universalIdentifier: APP_UID,
              workspaceId: WS_ID,
              name: 'Portal App',
              deletedAt: null,
            },
          ],
          publicDomains: [
            {
              id: 'pd-1',
              domain: 'portal.acme.com',
              applicationId: APP_ID,
              workspaceId: WS_ID,
              isValidated: true,
            },
            {
              id: 'pd-2',
              domain: 'help.acme.com',
              applicationId: APP_ID,
              workspaceId: WS_ID,
              isValidated: false,
            },
          ],
        });

      await applicationService.delete(APP_UID, WS_ID);

      expect(deleteHostnameMock).toHaveBeenCalledTimes(2);
      expect(deleteHostnameMock).toHaveBeenCalledWith('portal.acme.com', {
        isPublicDomain: true,
      });
      expect(deleteHostnameMock).toHaveBeenCalledWith('help.acme.com', {
        isPublicDomain: true,
      });
    });

    it('3.3 never tears down DNS hostnames belonging to other applications or other workspaces', async () => {
      const { applicationService, deleteHostnameMock } =
        await buildFeature3Harness({
          applications: [
            {
              id: APP_ID,
              universalIdentifier: APP_UID,
              workspaceId: WS_ID,
              name: 'Portal App',
              deletedAt: null,
            },
          ],
          publicDomains: [
            {
              id: 'pd-target',
              domain: 'portal.acme.com',
              applicationId: APP_ID,
              workspaceId: WS_ID,
            },
            {
              id: 'pd-other-app',
              domain: 'keep-app.acme.com',
              applicationId: OTHER_APP_ID,
              workspaceId: WS_ID,
            },
            {
              id: 'pd-other-ws',
              domain: 'keep-ws.acme.com',
              applicationId: APP_ID,
              workspaceId: OTHER_WS_ID,
            },
          ],
        });

      await applicationService.delete(APP_UID, WS_ID);

      expect(deleteHostnameMock).toHaveBeenCalledTimes(1);
      expect(deleteHostnameMock).toHaveBeenCalledWith('portal.acme.com', {
        isPublicDomain: true,
      });
    });

    it('3.4 deletes an application with zero public domains cleanly while preserving flatApplicationMaps cache invalidation', async () => {
      const {
        applicationService,
        applicationRepo,
        deleteHostnameMock,
        cacheInvalidateMock,
      } = await buildFeature3Harness({
        applications: [
          {
            id: APP_ID,
            universalIdentifier: APP_UID,
            workspaceId: WS_ID,
            name: 'Headless App',
            deletedAt: null,
          },
        ],
        publicDomains: [],
      });

      await applicationService.delete(APP_UID, WS_ID);

      expect(applicationRepo.rows).toHaveLength(0);
      expect(deleteHostnameMock).not.toHaveBeenCalled();
      expect(cacheInvalidateMock).toHaveBeenCalledWith(
        WS_ID,
        expect.arrayContaining(['flatApplicationMaps']),
      );
    });

    it('3.5 throws APPLICATION_NOT_FOUND when universalIdentifier does not exist without deleting public domains or calling DNS teardown', async () => {
      const { applicationService, publicDomainRepo, deleteHostnameMock } =
        await buildFeature3Harness({
          applications: [],
          publicDomains: [
            {
              id: 'pd-keep',
              domain: 'portal.acme.com',
              applicationId: APP_ID,
              workspaceId: WS_ID,
            },
          ],
        });

      await expect(
        applicationService.delete('com.acme.missing', WS_ID),
      ).rejects.toMatchObject({
        code: ApplicationExceptionCode.APPLICATION_NOT_FOUND,
      });
      expect(publicDomainRepo.rows).toHaveLength(1);
      expect(deleteHostnameMock).not.toHaveBeenCalled();
    });
  });

  // =========================================================================
  // FEATURE 4: Subdomain Collision Protection vs PublicDomain (5 assertions)
  // =========================================================================
  describe('Feature 4: Subdomain Collision Protection Against Registered Public Domains', () => {
    const buildFeature4Harness = async (seed: {
      publicDomainUrl?: string;
      defaultSubdomain?: string;
      workspaces?: Array<{ subdomain: string; deletedAt?: Date | null }>;
      publicDomains?: Array<{ domain: string; workspaceId?: string }>;
    }) => {
      const workspaceRepo = createInMemoryRepository(
        (seed.workspaces ?? []).map((w, idx) => ({
          id: `ws-${idx}`,
          subdomain: w.subdomain,
          deletedAt: w.deletedAt ?? null,
        })),
      );
      const publicDomainRepo = createInMemoryRepository(
        (seed.publicDomains ?? []).map((pd, idx) => ({
          id: `pd-${idx}`,
          domain: pd.domain.toLowerCase(),
          workspaceId: pd.workspaceId ?? 'ws-owner',
          isValidated: true,
        })),
      );

      const configValues: Record<string, unknown> = {
        PUBLIC_DOMAIN_URL: seed.publicDomainUrl,
        DEFAULT_SUBDOMAIN: seed.defaultSubdomain ?? 'app',
        FRONTEND_URL: 'https://twenty.com',
        SERVER_URL: 'https://api.twenty.com',
        IS_MULTIWORKSPACE_ENABLED: true,
      };

      const excludedF4 = new Set<unknown>([
        SubdomainManagerService,
        PublicDomainService,
        DomainServerConfigService,
        TwentyConfigService,
      ]);
      const importedSubdomainModules = (
        (Reflect.getMetadata('imports', SubdomainManagerModule) as
          | Array<unknown>
          | undefined) ?? []
      ).filter((m): m is Type<unknown> => typeof m === 'function');
      const extraSubdomainProviders = [
        ...getExtraClassProvidersFromModule(SubdomainManagerModule, excludedF4),
        ...importedSubdomainModules.flatMap((mod) =>
          getExtraClassProvidersFromModule(mod, excludedF4),
        ),
      ].filter(
        (p, idx, arr) =>
          arr.indexOf(p) === idx &&
          !p.name.endsWith('Resolver') &&
          !p.name.endsWith('Command'),
      );

      const moduleRef: TestingModule = await Test.createTestingModule({
        providers: [
          AuthResolver,
          SubdomainManagerService,
          PublicDomainService,
          DomainServerConfigService,
          ...extraSubdomainProviders,
          {
            provide: getRepositoryToken(WorkspaceEntity),
            useValue: workspaceRepo,
          },
          {
            provide: getRepositoryToken(PublicDomainEntity),
            useValue: publicDomainRepo,
          },
          provideWorkspaceScopedRepository(PublicDomainEntity),
          {
            provide: getRepositoryToken(ApplicationEntity),
            useValue: createInMemoryRepository([]),
          },
          provideWorkspaceScopedRepository(ApplicationEntity),
          {
            provide: TwentyConfigService,
            useValue: {
              ...createAutoLeafMock(),
              get: jest.fn((key: string) => configValues[key]),
            },
          },
        ],
      })
        .useMocker(() => createAutoLeafMock())
        .compile();

      return {
        subdomainManagerService: moduleRef.get(SubdomainManagerService),
        authResolver: moduleRef.get(AuthResolver),
      };
    };

    it('4.1 marks subdomain unavailable when <subdomain>.<publicBaseHostname> matches a registered PublicDomain and returns 3 collision-free suggestions', async () => {
      const { authResolver } = await buildFeature4Harness({
        publicDomainUrl: 'https://Twenty.App:443',
        publicDomains: [{ domain: 'acme.twenty.app' }],
        workspaces: [],
      });

      const availability =
        await authResolver.checkWorkspaceSubdomainAvailability('acme');

      expect(availability.isValid).toBe(true);
      expect(availability.available).toBe(false);
      expect(availability.suggestedSubdomains).toHaveLength(3);
      expect(availability.suggestedSubdomains).not.toContain('acme');
      expect(availability.suggestedSubdomain).toBe(
        availability.suggestedSubdomains[0],
      );
      expect(availability.suggestedSubdomain).toBe('acme-2');
    });

    it('4.2 throws WorkspaceException(SUBDOMAIN_ALREADY_TAKEN) in validateSubdomainOrThrow when derived hostname collides with a PublicDomain', async () => {
      const { subdomainManagerService } = await buildFeature4Harness({
        publicDomainUrl: 'https://twenty.app',
        publicDomains: [{ domain: 'acme-portal.twenty.app' }],
      });

      await expect(
        subdomainManagerService.validateSubdomainOrThrow('acme-portal'),
      ).rejects.toMatchObject({
        code: WorkspaceExceptionCode.SUBDOMAIN_ALREADY_TAKEN,
      });
    });

    it('4.3 skips candidates taken by either Workspace.subdomain or PublicDomain in findAvailableSubdomain', async () => {
      const { subdomainManagerService } = await buildFeature4Harness({
        publicDomainUrl: 'https://twenty.app',
        publicDomains: [
          { domain: 'acme.twenty.app' },
          { domain: 'acme-3.twenty.app' },
        ],
        workspaces: [{ subdomain: 'acme-2' }],
      });

      const chosen = await subdomainManagerService.findAvailableSubdomain('acme');

      expect(chosen).toBe('acme-4');
    });

    it('4.4 keeps non-colliding valid subdomains available when <subdomain>.<publicBaseHostname> is not registered', async () => {
      const { subdomainManagerService } = await buildFeature4Harness({
        publicDomainUrl: 'https://twenty.app',
        publicDomains: [{ domain: 'acme.twenty.app' }],
      });

      const availability =
        await subdomainManagerService.getSubdomainAvailability('globex');

      expect(availability).toEqual({
        isValid: true,
        available: true,
        suggestedSubdomain: 'globex',
        suggestedSubdomains: ['globex'],
      });
      await expect(
        subdomainManagerService.validateSubdomainOrThrow('globex'),
      ).resolves.toBeUndefined();
    });

    it('4.5 skips public-domain hostname collision checks when PUBLIC_DOMAIN_URL is unset or invalid', async () => {
      const unsetHarness = await buildFeature4Harness({
        publicDomainUrl: '',
        publicDomains: [{ domain: 'acme.twenty.app' }],
      });
      const invalidUrlHarness = await buildFeature4Harness({
        publicDomainUrl: 'not-a-valid-url',
        publicDomains: [{ domain: 'acme.twenty.app' }],
      });

      const unsetRes =
        await unsetHarness.subdomainManagerService.getSubdomainAvailability(
          'acme',
        );
      const invalidRes =
        await invalidUrlHarness.subdomainManagerService.getSubdomainAvailability(
          'acme',
        );

      expect(unsetRes.available).toBe(true);
      expect(invalidRes.available).toBe(true);
    });
  });
});
