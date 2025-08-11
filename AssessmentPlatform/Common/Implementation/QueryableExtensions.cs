using AssessmentPlatform.Dtos.CommonDto;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
namespace AssessmentPlatform.Common.Implementation
{
    public static class QueryableExtensions
    {
        public static async Task<PaginationResponse<T>> ApplyPaginationAsync<T>(this IQueryable<T> query,PaginationRequest request, Expression<Func<T, bool>> searchPredicate = null)
        {
            // Apply search filter
            if (!string.IsNullOrWhiteSpace(request.SearchText) && searchPredicate != null)
            {
                query = query.Where(searchPredicate);
            }

            // Apply sorting
            if (!string.IsNullOrEmpty(request.SortBy))
            {
                query = ApplyOrderBy(query, request.SortBy, request.SortDirection);
            }

            var totalRecords = await query.CountAsync();

            var data = await query
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            return new PaginationResponse<T>
            {
                Data = data,
                TotalRecords = totalRecords,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize
            };
        }

        private static IQueryable<T> ApplyOrderBy<T>(IQueryable<T> source, string propertyName, string direction)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var property = Expression.PropertyOrField(parameter, propertyName);
            var lambda = Expression.Lambda(property, parameter);

            string methodName = direction?.ToLower() == "desc" ? "OrderByDescending" : "OrderBy";

            var result = typeof(Queryable).GetMethods()
                .Where(m => m.Name == methodName && m.GetParameters().Length == 2)
                .Single()
                .MakeGenericMethod(typeof(T), property.Type)
                .Invoke(null, new object[] { source, lambda }) ?? new();

            return (IQueryable<T>)result;
        }

    }
}