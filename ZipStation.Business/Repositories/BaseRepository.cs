using MongoDB.Bson;
using MongoDB.Driver;
using ZipStation.Models.Entities;
using ZipStation.Models.Responses;
using ZipStation.Models.SearchProfiles;

namespace ZipStation.Business.Repositories;

public interface IBaseRepository<T> where T : BaseEntity
{
    Task<T?> GetAsync(string id);
    Task<T> CreateAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task<T> RemoveAsync(string id);
    Task<T> UpsertAsync(T entity);
    Task<long> CountAsync();
    Task<PaginatedResponse<T>> GetPaginatedResults(FilterDefinition<T> filter, BaseSearchProfile searchProfile);
    Task<PaginatedResponse<T>> GetPaginatedResults(IAggregateFluent<T> aggregation, BaseSearchProfile searchProfile);
    IMongoCollection<T> GetCollection();
}

public class BaseRepository<T> : IBaseRepository<T> where T : BaseEntity
{
    protected readonly IMongoCollection<T> _Collection;

    public BaseRepository(IMongoDatabase database, string collectionName)
    {
        _Collection = database.GetCollection<T>(collectionName);
    }

    public IMongoCollection<T> GetCollection() => _Collection;

    public async Task<long> CountAsync()
    {
        var filter = Builders<T>.Filter.Eq(e => e.IsVoid, false);
        return await _Collection.CountDocumentsAsync(filter);
    }

    public async Task<T?> GetAsync(string id)
    {
        var filter = Builders<T>.Filter.Eq(e => e.Id, id)
                   & Builders<T>.Filter.Eq(e => e.IsVoid, false);
        return await _Collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<T> CreateAsync(T entity)
    {
        if (string.IsNullOrEmpty(entity.Id))
            entity.Id = ObjectId.GenerateNewId().ToString();

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (entity.CreatedOnDateTime == 0)
            entity.CreatedOnDateTime = now;
        entity.UpdatedOnDateTime = now;
        entity.IsVoid = false;
        await _Collection.InsertOneAsync(entity);
        return entity;
    }

    public async Task<T> UpdateAsync(T entity)
    {
        entity.UpdatedOnDateTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var filter = Builders<T>.Filter.Eq(e => e.Id, entity.Id);
        await _Collection.ReplaceOneAsync(filter, entity);
        return entity;
    }

    public async Task<T> RemoveAsync(string id)
    {
        var entity = await GetAsync(id);
        if (entity == null) return null!;

        entity.IsVoid = true;
        entity.UpdatedOnDateTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var filter = Builders<T>.Filter.Eq(e => e.Id, id);
        await _Collection.ReplaceOneAsync(filter, entity);
        return entity;
    }

    public async Task<T> UpsertAsync(T entity)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        entity.UpdatedOnDateTime = now;

        if (entity.CreatedOnDateTime == 0)
            entity.CreatedOnDateTime = now;

        var filter = Builders<T>.Filter.Eq(e => e.Id, entity.Id);
        var options = new ReplaceOptions { IsUpsert = true };
        await _Collection.ReplaceOneAsync(filter, entity, options);
        return entity;
    }

    public async Task<PaginatedResponse<T>> GetPaginatedResults(
        FilterDefinition<T> filter,
        BaseSearchProfile searchProfile)
    {
        var voidFilter = searchProfile.IsVoid.HasValue
            ? Builders<T>.Filter.Eq(e => e.IsVoid, searchProfile.IsVoid.Value)
            : Builders<T>.Filter.Eq(e => e.IsVoid, false);

        var combinedFilter = filter & voidFilter;

        if (!string.IsNullOrWhiteSpace(searchProfile.Query))
        {
            var textFilter = Builders<T>.Filter.Text(searchProfile.Query);
            combinedFilter &= textFilter;
        }

        var totalCount = await _Collection.CountDocumentsAsync(combinedFilter);

        var findFluent = _Collection.Find(combinedFilter);

        if (searchProfile.OrderByBestMatch && !string.IsNullOrWhiteSpace(searchProfile.Query))
        {
            findFluent = findFluent.Sort(Builders<T>.Sort.MetaTextScore("TextMatchScore"));
        }
        else if (!string.IsNullOrWhiteSpace(searchProfile.OrderByFieldName))
        {
            var sortDefinition = searchProfile.OrderByAscending
                ? Builders<T>.Sort.Ascending(searchProfile.OrderByFieldName)
                : Builders<T>.Sort.Descending(searchProfile.OrderByFieldName);
            findFluent = findFluent.Sort(sortDefinition);
        }
        else
        {
            findFluent = findFluent.SortByDescending(e => e.CreatedOnDateTime);
        }

        var skip = (searchProfile.Page - 1) * searchProfile.ResultsPerPage;
        var results = await findFluent
            .Skip(skip)
            .Limit(searchProfile.ResultsPerPage)
            .ToListAsync();

        return new PaginatedResponse<T>
        {
            TotalResultCount = totalCount,
            Results = results
        };
    }

    public async Task<PaginatedResponse<T>> GetPaginatedResults(
        IAggregateFluent<T> aggregation,
        BaseSearchProfile searchProfile)
    {
        var countFacet = AggregateFacet.Create("count",
            PipelineDefinition<T, AggregateCountResult>.Create(new[]
            {
                PipelineStageDefinitionBuilder.Count<T>()
            }));

        var skip = (searchProfile.Page - 1) * searchProfile.ResultsPerPage;

        var sortStage = !string.IsNullOrWhiteSpace(searchProfile.OrderByFieldName)
            ? (searchProfile.OrderByAscending
                ? Builders<T>.Sort.Ascending(searchProfile.OrderByFieldName)
                : Builders<T>.Sort.Descending(searchProfile.OrderByFieldName))
            : Builders<T>.Sort.Descending(nameof(BaseEntity.CreatedOnDateTime));

        var dataFacet = AggregateFacet.Create("data",
            PipelineDefinition<T, T>.Create(new IPipelineStageDefinition[]
            {
                PipelineStageDefinitionBuilder.Sort(sortStage),
                PipelineStageDefinitionBuilder.Skip<T>(skip),
                PipelineStageDefinitionBuilder.Limit<T>(searchProfile.ResultsPerPage)
            }));

        var facetResults = await aggregation
            .Facet(countFacet, dataFacet)
            .ToListAsync();

        var facet = facetResults.FirstOrDefault();
        var count = facet?.Facets
            .First(x => x.Name == "count")
            .Output<AggregateCountResult>()
            .FirstOrDefault()?.Count ?? 0;

        var data = facet?.Facets
            .First(x => x.Name == "data")
            .Output<T>() ?? new List<T>();

        return new PaginatedResponse<T>
        {
            TotalResultCount = count,
            Results = data.ToList()
        };
    }
}
