import { Test, type TestingModule } from '@nestjs/testing';
import { getRepositoryToken } from '@nestjs/typeorm';

import { SubdomainManagerService } from 'src/engine/core-modules/domain/subdomain-manager/services/subdomain-manager.service';
import { type PublicDomainEntity } from 'src/engine/core-modules/public-domain/public-domain.entity';
import { PublicDomainService } from 'src/engine/core-modules/public-domain/public-domain.service';
import { TwentyConfigService } from 'src/engine/core-modules/twenty-config/twenty-config.service';
import { WorkspaceEntity } from 'src/engine/core-modules/workspace/workspace.entity';

describe('SubdomainManagerService', () => {
  let service: SubdomainManagerService;
  let workspaceRepository: {
    findOne: jest.Mock;
    find: jest.Mock;
  };
  let publicDomainService: {
    findByDomain: jest.Mock;
  };
  let twentyConfigService: {
    get: jest.Mock;
  };

  beforeEach(async () => {
    workspaceRepository = {
      findOne: jest.fn().mockResolvedValue(null),
      find: jest.fn().mockResolvedValue([]),
    };
    publicDomainService = {
      findByDomain: jest.fn().mockResolvedValue(null),
    };
    twentyConfigService = {
      get: jest.fn((key: string) => {
        if (key === 'FRONTEND_URL') {
          return 'https://twenty.com';
        }
        if (key === 'DEFAULT_SUBDOMAIN') {
          return 'app';
        }

        return undefined;
      }),
    };

    const module: TestingModule = await Test.createTestingModule({
      providers: [
        SubdomainManagerService,
        {
          provide: getRepositoryToken(WorkspaceEntity),
          useValue: workspaceRepository,
        },
        {
          provide: PublicDomainService,
          useValue: publicDomainService,
        },
        {
          provide: TwentyConfigService,
          useValue: twentyConfigService,
        },
      ],
    }).compile();

    service = module.get<SubdomainManagerService>(SubdomainManagerService);
  });

  describe('isSubdomainAvailable', () => {
    it('should return true when neither a workspace subdomain nor a public domain matches', async () => {
      const result = await service.isSubdomainAvailable('acme');

      expect(result).toBe(true);
      expect(workspaceRepository.findOne).toHaveBeenCalledWith({
        where: { subdomain: 'acme' },
        withDeleted: true,
      });
      expect(publicDomainService.findByDomain).toHaveBeenCalledWith(
        'acme.twenty.com',
      );
    });

    it('should return false when workspace with subdomain already exists', async () => {
      workspaceRepository.findOne.mockResolvedValue({
        id: 'workspace-id',
        subdomain: 'acme',
      } as WorkspaceEntity);

      const result = await service.isSubdomainAvailable('acme');

      expect(result).toBe(false);
      expect(publicDomainService.findByDomain).not.toHaveBeenCalled();
    });

    it('should return false when subdomain.baseHostname is already registered as a public domain', async () => {
      publicDomainService.findByDomain.mockResolvedValue({
        id: 'public-domain-id',
        domain: 'acme.twenty.com',
      } as PublicDomainEntity);

      const result = await service.isSubdomainAvailable('acme');

      expect(result).toBe(false);
      expect(publicDomainService.findByDomain).toHaveBeenCalledWith(
        'acme.twenty.com',
      );
    });

    it('should return true and skip public domain check when FRONTEND_URL is not configured', async () => {
      twentyConfigService.get.mockImplementation((key: string) => {
        if (key === 'FRONTEND_URL') {
          return undefined;
        }

        return undefined;
      });

      const result = await service.isSubdomainAvailable('acme');

      expect(result).toBe(true);
      expect(publicDomainService.findByDomain).not.toHaveBeenCalled();
    });
  });
});
