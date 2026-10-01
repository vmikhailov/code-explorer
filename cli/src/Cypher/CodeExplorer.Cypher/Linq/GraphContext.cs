using CodeExplorer.Cypher.Linq.Entities;

namespace CodeExplorer.Cypher.Linq;

public class GraphContext
{
    private readonly ICypherQueryExecutor _executor;
    private readonly CypherQueryProvider _provider;

    public GraphContext(ICypherQueryExecutor executor)
    {
        _executor = executor;
        _provider = new CypherQueryProvider(executor);
    }

    public ICypherQueryExecutor Executor => _executor;

    public IQueryable<ServiceEntity> Services => Set<ServiceEntity>();
    public IQueryable<AppEntity> Apps => Set<AppEntity>();
    public IQueryable<WorkerEntity> Workers => Set<WorkerEntity>();
    public IQueryable<CliToolEntity> CliTools => Set<CliToolEntity>();
    public IQueryable<DatabaseEntity> Databases => Set<DatabaseEntity>();
    public IQueryable<TopicEntity> Topics => Set<TopicEntity>();
    public IQueryable<ExternalServiceEntity> ExternalServices => Set<ExternalServiceEntity>();
    public IQueryable<LibraryEntity> Libraries => Set<LibraryEntity>();
    public IQueryable<EndpointEntity> Endpoints => Set<EndpointEntity>();
    public IQueryable<GraphEntity> Nodes => Set<GraphEntity>();

    public IQueryable<TEntity> Set<TEntity>() where TEntity : GraphEntity, new()
    {
        return new CypherQueryable<TEntity>(_provider);
    }
}
