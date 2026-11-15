import { Test, type TestingModule } from '@nestjs/testing';
import { getRepositoryToken } from '@nestjs/typeorm';

import { type Repository } from 'typeorm';

import { SubdomainManagerService } from 'src/engine/core-modules/domain/subdomain-manager/services/subdomain-manager.service';
import { PublicDomainEntity } from 'src/engine/core-modules/public-domain/public-domain.entity';
import { TwentyConfigService } from 'src/engine/core-modules/twenty-config/twenty-config.service';
import { WorkspaceEntity } from 'src/engine/core-modules/workspace/workspace.entity';

describe('SubdomainManagerService', () => {
  let service: SubdomainManagerService;
  let workspaceRepository: jest.Mocked<Pick<Repository<WorkspaceEntity>, 'findOne'>>;
  let publicDomainRepository: jest.Mocked<
    Pick<Repository<PublicDomainEntity>, 'findOne'>
  >;
  let twentyConfigService: jest.Mocked<Pick<TwentyConfigService, 'get'>>;

  beforeEach(async () => {
    workspaceRepository = {
      findOne: jest.fn(),
    };
    publicDomainRepository = {
      findOne: jest.fn(),
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
          provide: getRepositoryToken(PublicDomainEntity),
          useValue: publicDomainRepository,
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
    it('should return false when workspace with subdomain already exists', async () => {
      workspaceRepository.findOne.mockResolvedValue({
        id: 'workspace-id',
        subdomain: 'acme',
      } as WorkspaceEntity);

      const result = await service.isSubdomainAvailable('acme');

      expect(result).toBe(false);
      expect(workspaceRepository.findOne).toHaveBeenCalledWith({
        where: { subdomain: 'acme' },
        withDeleted: true,
      });
      expect(publicDomainRepository.findOne).not.toHaveBeenCalled();
    });

    it('should return false when ${subdomain}.${baseHostname} is registered as a public domain', async () => {
      workspaceRepository.findOne.mockResolvedValue(null);
      twentyConfigService.get.mockImplementation((key: string) =>
        key === 'FRONTEND_URL' ? 'https://twenty.com' : undefined,
      );
      publicDomainRepository.findOne.mockResolvedValue({
        id: 'public-domain-id',
        domain: 'acme.twenty.com',
      } as PublicDomainEntity);

      const result = await service.isSubdomainAvailable('acme');

      expect(result).toBe(false);
      expect(publicDomainRepository.findOne).toHaveBeenCalledWith({
        where: { domain: 'acme.twenty.com' },
      });
    });

    it('should return true when neither a workspace subdomain nor a public domain matches', async () => {
      workspaceRepository.findOne.mockResolvedValue(null);
      twentyConfigService.get.mockImplementation((key: string) =>
        key === 'FRONTEND_URL' ? 'https://twenty.com' : undefined,
      );
      publicDomainRepository.findOne.mockResolvedValue(null);

      const result = await service.isSubdomainAvailable('acme');

      expect(result).toBe(true);
      expect(workspaceRepository.findOne).toHaveBeenCalledWith({
        where: { subdomain: 'acme' },
        withDeleted: true,
      });
      expect(publicDomainRepository.findOne).toHaveBeenCalledWith({
        where: { domain: 'acme.twenty.com' },
      });
    });

    it('should return true without checking public domains when FRONTEND_URL is undefined', async () => {
      workspaceRepository.findOne.mockResolvedValue(null);
      twentyConfigService.get.mockReturnValue(undefined);

      const result = await service.isSubdomainAvailable('acme');

      expect(result).toBe(true);
      expect(publicDomainRepository.findOne).not.toHaveBeenCalled();
    });
  });
});
