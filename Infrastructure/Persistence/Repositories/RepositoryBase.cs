using Application.Interfaces;
using Domain.Common;
using Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Persistence.Repositories
{
    public class RepositoryBase<T> : IRepository<T> where T : BaseEntity
    {
        protected readonly AssetTrackingDbContext Context;
        protected readonly DbSet<T> DbSet;

        public RepositoryBase(AssetTrackingDbContext context)
        {
            Context = context;
            DbSet = context.Set<T>();
        }

        public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default, params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = DbSet;
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
            return await query.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        }

        public virtual async Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await DbSet.Where(x => !x.IsDeleted).AsNoTracking().ToListAsync(cancellationToken);
        }

        public virtual async Task<List<T>> GetFilteredAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default, params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = DbSet.Where(x => !x.IsDeleted);
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
            return await query.Where(predicate).AsNoTracking().ToListAsync(cancellationToken);
        }

        public virtual async Task AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            entity.CreatedOn = DateTime.UtcNow;
            await DbSet.AddAsync(entity, cancellationToken);
        }

        public virtual void Update(T entity)
        {
            entity.UpdatedOn = DateTime.UtcNow;
            DbSet.Update(entity);
        }

        public virtual void Delete(T entity)
        {
            DbSet.Remove(entity);
        }

        public virtual async Task<(List<T> Items, int TotalCount)> GetPagedAsync(
            int pageNumber,
            int pageSize,
            string? searchTerm,
            Expression<Func<T, bool>>? filterExpression,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy,
            CancellationToken cancellationToken = default,
            params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = DbSet.Where(x => !x.IsDeleted);

            foreach (var include in includes)
            {
                query = query.Include(include);
            }

            if (filterExpression != null)
            {
                query = query.Where(filterExpression);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.Trim().ToLower();
                var parameter = Expression.Parameter(typeof(T), "x");
                Expression? searchExpression = null;

                var properties = typeof(T).GetProperties()
                    .Where(p => p.PropertyType == typeof(string) && 
                                (p.Name == "Name" || p.Name == "Description" || p.Name == "Code" || p.Name == "AssetNumber" || p.Name == "EpcCode" || p.Name == "Username" || p.Name == "Email"));

                foreach (var prop in properties)
                {
                    var propertyAccess = Expression.Property(parameter, prop);
                    
                    // Handle potential null values for optional string properties
                    var isNotNull = Expression.NotEqual(propertyAccess, Expression.Constant(null, typeof(string)));
                    
                    var toLowerCall = Expression.Call(propertyAccess, typeof(string).GetMethod("ToLower", Type.EmptyTypes)!);
                    var containsCall = Expression.Call(toLowerCall, typeof(string).GetMethod("Contains", new[] { typeof(string) })!, Expression.Constant(searchTerm));
                    
                    var conditionalExpression = Expression.AndAlso(isNotNull, containsCall);

                    searchExpression = searchExpression == null 
                        ? conditionalExpression 
                        : Expression.OrElse(searchExpression, conditionalExpression);
                }

                if (searchExpression != null)
                {
                    var lambda = Expression.Lambda<Func<T, bool>>(searchExpression, parameter);
                    query = query.Where(lambda);
                }
            }

            var totalCount = await query.CountAsync(cancellationToken);

            if (orderBy != null)
            {
                query = orderBy(query);
            }
            else
            {
                query = query.OrderByDescending(x => x.CreatedOn);
            }

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }
    }
}
