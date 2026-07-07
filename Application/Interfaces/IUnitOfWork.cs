using Domain.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface IUnitOfWork
    {
        IRepository<T> Repository<T>() where T : BaseEntity;
        
        Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default);
    }
}
