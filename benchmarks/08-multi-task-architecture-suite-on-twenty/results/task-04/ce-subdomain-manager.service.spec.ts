import { Test, type TestingModule } from '@nestjs/testing';
import { getRepositoryToken } from '@nestjs/typeorm';

import { type Repository } from 'typeorm';

import { SubdomainManagerService } from 'src/engine/core-modules/domain/subdomain-manager/services/subdomain-manager.service';
import { type PublicDomainEntity } from 'src/engine/core-modules/public-domain/public-domain.entity';
import { PublicDomainService } from 'src/engine/core-modules/public-domain/public-domain.service';
import { TwentyConfigService } from 'src/engine/core-modules/twenty-config/twenty-config.service';
import { WorkspaceEntity } from 'src/engine/core-modules/workspace/workspace.entity';

describe('SubdomainManagerService', () => {
  let service: SubdomainManagerService;
  let workspaceRepository: jest.Mocked<
    Pick<Repository<WorkspaceEntity>, 'findOne'>
  >;
  let publicDomainService: jest.Mocked<
    Pick<PublicDomainService, 'findByDomain'>
  >;
  let twentyConfigService: jest.Mocked<Pick<TwentyConfigService, 'get'>>;

  beforeEach(async () => {
    workspaceRepository = {
      findOne: jest.fn(),
    };
    publicDomainService = {
      findByDomain: jest.fn(),
    };
    twentyConfigService = {
      get: jest.fn(),
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
      workspaceRepository.findOne.mockResolvedValue(null);
      publicDomainService.findByDomain.mockResolvedValue(null);
      twentyConfigService.get.mockReturnValue('https://twenty.com');

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

    it('should return false when the subdomain is already registered as a public domain', async () => {
      workspaceRepository.findOne.mockResolvedValue(null);
      publicDomainService.findByDomain.mockResolvedValue({
        id: 'public-domain-id',
        domain: 'acme.twenty.com',
      } as PublicDomainEntity);
      twentyConfigService.get.mockReturnValue('https://twenty.com');

      const result = await service.isSubdomainAvailable('acme');

      expect(result).toBe(false);
      expect(publicDomainService.findByDomain).toHaveBeenCalledWith(
        'acme.twenty.com',
      );
    });

    it('should return false when a workspace with the subdomain already exists', async () => {
      workspaceRepository.findOne.mockResolvedValue({
        id: 'workspace-id',
        subdomain: 'acme',
      } as WorkspaceEntity);

      const result = await service.isSubdomainAvailable('acme');

      expect(result).toBe(false);
      expect(publicDomainService.findByDomain).not.toHaveBeenCalled();
    });

    it('should return true without checking public domains when FRONTEND_URL is not defined', async () => {
      workspaceRepository.findOne.mockResolvedValue(null);
      twentyConfigService.get.mockReturnValue(undefined);

      const result = await service.isSubdomainAvailable('acme');

      expect(result).toBe(true);
      expect(publicDomainService.findByDomain).not.toHaveBeenCalled();
    });
  });
});
