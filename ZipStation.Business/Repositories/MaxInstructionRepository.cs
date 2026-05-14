using MongoDB.Driver;
using ZipStation.Models.Entities;

namespace ZipStation.Business.Repositories;

public interface IMaxInstructionRepository : IBaseRepository<MaxInstruction>
{
    Task<List<MaxInstruction>> GetByProjectIdAsync(string projectId);
}

public class MaxInstructionRepository : BaseRepository<MaxInstruction>, IMaxInstructionRepository
{
    public MaxInstructionRepository(IMongoDatabase database, string collectionName)
        : base(database, collectionName)
    {
    }

    public async Task<List<MaxInstruction>> GetByProjectIdAsync(string projectId)
    {
        var filter = Builders<MaxInstruction>.Filter.Eq(i => i.ProjectId, projectId)
                   & Builders<MaxInstruction>.Filter.Eq(i => i.IsVoid, false);
        return await _Collection.Find(filter)
            .SortByDescending(i => i.CreatedOnDateTime)
            .ToListAsync();
    }
}
