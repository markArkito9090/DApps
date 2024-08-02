using ICD.Base.BusinessServiceContract;
using ICD.Base.Domain.Entity;
using ICD.Base.Domain.View;
using ICD.Base.RepositoryContract;
using ICD.Framework.AppMapper.Extensions;
using ICD.Framework.Data.UnitOfWork;
using ICD.Framework.DataAnnotation;
using ICD.Framework.Extensions;
using ICD.Framework.QueryDataSource.Fitlter;
using ICD.Infrastructure.BusinessServiceContract;
using ICD.Infrastructure.Exception;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ICD.Base.BusinessService
{
    [Dependency(typeof(IExpenseCenterService))]
    public class ExpenseCenterService : IExpenseCenterService
    {
        private readonly IUnitOfWork _db;
        private readonly IExpenseCenterRepository _expenseCenterRepository;
        private readonly IExpenseCenterLanguageRepository _expenseCenterLanguageRepository;
        private readonly IEntityService _entityService;

        public ExpenseCenterService(IUnitOfWork db, IEntityService entityService)
        {
            _db = db;
            _expenseCenterRepository = _db.GetRepository<IExpenseCenterRepository>();
            _expenseCenterLanguageRepository = _db.GetRepository<IExpenseCenterLanguageRepository>();
            _entityService = entityService;
        }

        public async Task<DeleteTypeLongResult> DeleteExpenseCenterAsync(DeleteTypeLongRequest request)
        {
            await _expenseCenterRepository.DeleteWithAsync(x => x.Key == request.Key);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException e)
            {

                SqlErrorHandling.Handler(e);
            }

            return new DeleteTypeLongResult { Success = true };
        }

        public async Task<GetExpenseCenterByKeyResult> GetExpenseCenterByKeyAsync(GetExpenseCenterByKeyQuery query)
        {
            var result = new GetExpenseCenterByKeyResult
            {
                Entity = new GetExpenseCenterByKeyModel()
            };

            var queryDataSource = query.ToQueryDataSource<ExpenseCenterView>();
            
            queryDataSource.AddFilter(new ExpressionFilterInfo<ExpenseCenterView>(x => x.Key == query.Key));

            var expenceResult = await _expenseCenterRepository.GetExpenseCentersAsync(queryDataSource, query.LanguageRef);

            if (expenceResult.Entities.Any())
                result.Entity = expenceResult.Entities.FirstOrDefault().MapTo<GetExpenseCenterByKeyModel>();

            return result;
        }

        public async Task<GetExpenseCentersResult> GetExpenseCentersAsync(GetExpenseCentersQuery query)
        {
            var result = new GetExpenseCentersResult
            {
                Entities = new List<GetExpenseCenterModel>()
            };

            var queryDataSource = query.ToQueryDataSource<ExpenseCenterView>();

            if (query.IsActive.HasValue)
                queryDataSource.AddFilter(new ExpressionFilterInfo<ExpenseCenterView>(x => x.IsActive == query.IsActive));

            if (query.CompanyRef.HasValue)
                queryDataSource.AddFilter(new ExpressionFilterInfo<ExpenseCenterView>(x => x.CompanyRef == query.CompanyRef));

            var expenceResult = await _expenseCenterRepository.GetExpenseCentersAsync(queryDataSource, query.LanguageRef);

            if (expenceResult.Entities.Any())
                result = expenceResult.MapTo<GetExpenseCentersResult>();

            return result;
        }

        public async Task<BaseExpenseCenterResult> InsertExpenseCenterAsync(InsertExpenseCenterRequest request)
        {
            var expenseCenterEntity = request.MapTo<ExpenseCenterEntity>();

            expenseCenterEntity.ExpenseCenterLanguages = new List<ExpenseCenterLanguageEntity>
            {
                new ExpenseCenterLanguageEntity
                {
                        LanguageRef = request.LanguageRef,
                        _Title = request._Title
                }
            };

            await _expenseCenterRepository.AddAsync(expenseCenterEntity);

            try
            {
                await _db.SaveChangesAsync();

                await _entityService.InsertMultilingualEntityAsync<ExpenseCenterEntity, InsertExpenseCenterRequest, long>(expenseCenterEntity, request);
            }
            catch (DbUpdateException e)
            {
                SqlErrorHandling.Handler(e, "DuplicateValue");
            }

            return new BaseExpenseCenterResult();
        }

        public async Task<BaseExpenseCenterResult> UpdateExpenseCenterAsync(UpdateExpenseCenterRequest request)
        {
            var expensCenterEntity = await _expenseCenterRepository.FirstOrDefaultAsync(x => x.Key == request.Key);

            if (expensCenterEntity.IsNull())
                throw new ICDException("NotFound");

            expensCenterEntity = request.MapTo<ExpenseCenterEntity>();

            _expenseCenterRepository.Update(expensCenterEntity);

            var eclEntity = await _expenseCenterLanguageRepository.FirstOrDefaultAsync(x => x.ExpenseCenterRef == request.Key && x.LanguageRef == request.LanguageRef);

            eclEntity._Title = request._Title;

            _expenseCenterLanguageRepository.Update(eclEntity);

            await _db.SaveChangesAsync();

            return new BaseExpenseCenterResult();
        }
    }
}
