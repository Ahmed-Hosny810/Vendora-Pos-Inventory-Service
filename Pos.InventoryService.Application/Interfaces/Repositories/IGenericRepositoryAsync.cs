

namespace Pos.InventoryService.Application.Interfaces.Repositories
{
    public interface IGenericRepositoryAsync<T, TKey> where T : class
    {
        Task<IReadOnlyList<T>> GetPagedResponseAsync(int pageNumber, int pageSize);
        Task<IReadOnlyList<T>> GetAllAsync();
        Task<T> GetByIdAsync(TKey id);
        Task<int> GetTotalCountAsync(CancellationToken cancellationToken);
        Task<T> AddAsync(T entity,CancellationToken cancellationToken);
        void Update(T entity);
        void Delete(T entity);
    }
}
