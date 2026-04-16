using HRMSAPI.Data;
using Microsoft.EntityFrameworkCore;
using Roomsy.DTOS.GenericsResponses;
using System.Linq.Expressions;
using System.Net;

namespace HRMSAPI.Implementation
{
    public class BaseService
    {
        protected readonly HRMSContext _context;
        public BaseService(HRMSContext context)
        {
            _context = context;
        }
        protected FetchAndResponse BuildFetchErrorResponse(string message, HttpStatusCode code) => new()
        {
            Status = false,
            Message = message,
            Code = code
        };

        protected FetchAndResponse BuildFetchSuccessResponse(string message, object data) => new()
        {
            Status = true,
            Message = message,
            Code = HttpStatusCode.OK,
            Data = data
        };

        protected ExecuteAndReponse BuildExecuteSuccessResponse(string message) => new()
        {
            Status = true,
            Message = message,
            Code = HttpStatusCode.OK,
        };
        protected ExecuteAndReponse BuildExecuteErrorResponse(string message, HttpStatusCode code) => new()
        {
            Status = false,
            Message = message,
            Code = code
        };

        public async Task<string> HashPassword(string plainText) => BCrypt.Net.BCrypt.HashPassword(plainText);

        public async Task<bool> VerifyPassword(string plainText, string hashedText) => BCrypt.Net.BCrypt.Verify(plainText, hashedText);
        /******* Get Functions*******/
        public async Task<T?> FindOneWithNoTrackingAsync<T>(
    Expression<Func<T, bool>> predicate
) where T : class
        {
            return await _context.Set<T>()
                                 .AsNoTracking()
                                 .FirstOrDefaultAsync(predicate);
        }
        //to get
        public async Task<T?> GetOneRecordWithTrackingAsync<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            return await _context.Set<T>().FirstOrDefaultAsync(predicate);
        }
        //to get multiple
        public async Task<List<T>?> GetMultipleRecordAsync<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            return await _context.Set<T>().AsNoTracking().Where(predicate).ToListAsync<T>();
        }

        //to get all
        public async Task<List<T>?> GetALLRecordAsync<T>() where T : class
        {
            return await _context.Set<T>().AsNoTracking().ToListAsync<T>();
        }

        /******* Save Functions  *******/
        public async Task<bool> SaveOneAsync<T>(T enity) where T : class
        {
            await _context.Set<T>().AddAsync(enity);
            int ra = await _context.SaveChangesAsync();
            return ra > 0;
        }
        public async Task SaveOneWithoutImmediateSaveAsync<T>(T enity) where T : class
        {
            await _context.Set<T>().AddAsync(enity);
        }
        public async Task<long> SaveOneAndGetIdAsync<T>(T entity, string idName) where T : class
        {
            await _context.Set<T>().AddAsync(entity);
            int result = await _context.SaveChangesAsync();

            if (result > 0)
            {
                var property = typeof(T).GetProperty(idName);
                if (property == null)
                    throw new ArgumentException($"Property '{idName}' not found on type '{typeof(T).Name}'");

                var value = property.GetValue(entity);

                if (value == null)
                    return 0;

                return Convert.ToInt64(value);
            }

            return 0;
        }
        //to add multiple
        public async Task<bool> SaveMultipleAsync<T>(List<T> entity) where T : class
        {
            await _context.Set<T>().AddRangeAsync(entity);
            int ra = await _context.SaveChangesAsync();
            return ra > 0;
        }


        public async Task<bool> AnyOne<T>(
    Expression<Func<T, bool>> predicate
) where T : class
        {
            return await _context.Set<T>()
                                 .AsNoTracking()
                                 .AnyAsync(predicate);
        }




        /******* Update Functions  *******/
        //to update
        public async Task<bool> UpdateOneAsync<T>(T entity) where T : class
        {
            var entry = _context.Entry(entity);
            if (entry.State == EntityState.Detached)
            {
                _context.Set<T>().Attach(entity);
                entry.State = EntityState.Modified;
            }

            int affectedRows = await _context.SaveChangesAsync();
            return affectedRows > 0;
        }
        public async Task UpdateOneWIthoutImmediateSaveAsync<T>(T entity) where T : class
        {
            var entry = _context.Entry(entity);
            if (entry.State == EntityState.Detached)
            {
                _context.Set<T>().Attach(entity);
                entry.State = EntityState.Modified;
            }
        }

        public async Task<bool> UpdateAllAsync<T>(List<T> entities) where T : class
        {
            if (entities == null || !entities.Any())
                return false;

            foreach (var entity in entities)
            {
                var entry = _context.Entry(entity);
                if (entry.State == EntityState.Detached)
                {
                    _context.Set<T>().Attach(entity);
                    entry.State = EntityState.Modified;
                }
            }

            int affectedRows = await _context.SaveChangesAsync();
            return affectedRows > 0;
        }
    }
}
