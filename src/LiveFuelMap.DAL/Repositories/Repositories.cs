using System.Linq.Expressions;
using LiveFuelMap.DAL.Entities;
using LiveFuelMap.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LiveFuelMap.DAL.Repositories;

public interface IRepository<TEntity> where TEntity : class
{
    IQueryable<TEntity> Query();
    Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    void Update(TEntity entity);
    void Remove(TEntity entity);
    Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
}

public interface IStationRepository : IRepository<Station>
{
    Task<IReadOnlyList<Station>> SearchAsync(string? city, string? name, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> CountAsync(string? city, string? name, CancellationToken cancellationToken = default);
}

public interface IUnitOfWork
{
    IRepository<User> Users { get; }
    IStationRepository Stations { get; }
    IRepository<Fuel> Fuels { get; }
    IRepository<FuelPrice> FuelPrices { get; }
    IRepository<Subscription> Subscriptions { get; }
    IRepository<Comment> Comments { get; }
    IRepository<ApiToken> ApiTokens { get; }
    IRepository<ChatMessage> ChatMessages { get; }
    IRepository<DataSource> DataSources { get; }
    IRepository<ParserRun> ParserRuns { get; }
    IRepository<ApplicationLog> ApplicationLogs { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public class Repository<TEntity>(LiveFuelMapDbContext context) : IRepository<TEntity> where TEntity : class
{
    protected LiveFuelMapDbContext Context { get; } = context;
    protected DbSet<TEntity> Set => Context.Set<TEntity>();

    public IQueryable<TEntity> Query() => Set.AsQueryable();

    public ValueTask<TEntity?> FindAsync(int id, CancellationToken cancellationToken = default) =>
        Set.FindAsync([id], cancellationToken);

    public async Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await FindAsync(id, cancellationToken);

    public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        Set.AddAsync(entity, cancellationToken).AsTask();

    public void Update(TEntity entity) => Set.Update(entity);

    public void Remove(TEntity entity) => Set.Remove(entity);

    public Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        Set.AnyAsync(predicate, cancellationToken);
}

public sealed class StationRepository(LiveFuelMapDbContext context) : Repository<Station>(context), IStationRepository
{
    public async Task<IReadOnlyList<Station>> SearchAsync(string? city, string? name, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        return await ApplyFilters(Query(), city, name)
            .OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(string? city, string? name, CancellationToken cancellationToken = default) =>
        ApplyFilters(Query(), city, name).CountAsync(cancellationToken);

    private static IQueryable<Station> ApplyFilters(IQueryable<Station> query, string? city, string? name)
    {
        if (!string.IsNullOrWhiteSpace(city))
        {
            var normalizedCity = city.Trim();
            query = query.Where(x => x.City.Contains(normalizedCity));
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            var normalizedName = name.Trim();
            query = query.Where(x => x.Name.Contains(normalizedName));
        }

        return query.Where(x => x.IsActive);
    }
}

public sealed class UnitOfWork(LiveFuelMapDbContext context) : IUnitOfWork
{
    public IRepository<User> Users { get; } = new Repository<User>(context);
    public IStationRepository Stations { get; } = new StationRepository(context);
    public IRepository<Fuel> Fuels { get; } = new Repository<Fuel>(context);
    public IRepository<FuelPrice> FuelPrices { get; } = new Repository<FuelPrice>(context);
    public IRepository<Subscription> Subscriptions { get; } = new Repository<Subscription>(context);
    public IRepository<Comment> Comments { get; } = new Repository<Comment>(context);
    public IRepository<ApiToken> ApiTokens { get; } = new Repository<ApiToken>(context);
    public IRepository<ChatMessage> ChatMessages { get; } = new Repository<ChatMessage>(context);
    public IRepository<DataSource> DataSources { get; } = new Repository<DataSource>(context);
    public IRepository<ParserRun> ParserRuns { get; } = new Repository<ParserRun>(context);
    public IRepository<ApplicationLog> ApplicationLogs { get; } = new Repository<ApplicationLog>(context);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
