using ICD.Base.BusinessServiceContract;
using ICD.Base.Domain.Entity;
using ICD.Base.Domain.External_View;
using ICD.Base.Domain.View;
using ICD.Base.RepositoryContract;
using ICD.Base.RepositoryContract.External_Repository_Contract.INF;
using ICD.Framework.AppMapper.Extensions;
using ICD.Framework.Data.UnitOfWork;
using ICD.Framework.DataAnnotation;
using ICD.Framework.Extensions;
using ICD.Framework.QueryDataSource;
using ICD.Framework.QueryDataSource.Fitlter;
using ICD.Infrastructure.BusinessServiceContract;
using ICD.Infrastructure.Exception;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICD.Base.BusinessService
{
    [Dependency(typeof(ITaxService))]
    public class TaxService : ITaxService
    {
        private readonly IUnitOfWork _db;
        private readonly ITaxRepository _taxRepository;
        private readonly ITaxLanguageRepository _taxLanguageRepository;
        private readonly IApplicationRepository _applicationRepository;
        private readonly IEntityService _entityService;

        public TaxService(IUnitOfWork db, IEntityService entityService)
        {
            _db = db;
            _taxRepository = _db.GetRepository<ITaxRepository>();
            _taxLanguageRepository = _db.GetRepository<ITaxLanguageRepository>();
            _applicationRepository = _db.GetRepository<IApplicationRepository>();
            _entityService = entityService;
        }

        public async Task<DeleteTypeIntResult> DeleteTaxByIdAsync(DeleteTypeIntRequest request)
        {
            await _taxRepository.DeleteWithAsync(t => t.Key == request.Key);
            await _taxLanguageRepository.DeleteWithAsync(tl => tl.TaxRef == request.Key);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException e)
            {

                SqlErrorHandling.Handler(e);
            }

            return new DeleteTypeIntResult { Success = true };
        }

        public async Task<GetTaxResult> GetTaxAsync(GetTaxQuery query)
        {
            var result = new GetTaxResult
            {
                Entities = new List<GetTaxModel>()
            };

            var searchQuery = query.ToQueryDataSource<TaxView>();

            var taxResult = await _taxRepository.GetTaxAsync(searchQuery, query.LanguageRef);

            if (taxResult.Entities.Any())
            {
                result = taxResult.MapTo<GetTaxResult>();
            }

            return result;
        }

        public async Task<GetTaxByKeyResult> GetTaxByKeyAsync(GetTaxByKeyQuery query)
        {
            var result = new GetTaxByKeyResult
            {
                Entity = new GetTaxModel()
            };

            var searchQuery = query.ToQueryDataSource<TaxView>();
            searchQuery.AddFilter(new ExpressionFilterInfo<TaxView>(q => q.Key== query.Key));
            var taxResult = (await _taxRepository.GetTaxAsync(searchQuery, query.LanguageRef));

            if (taxResult.Entities.Any())
            {
                result.Entity= new GetTaxModel
                {
                    Alias=taxResult.Entities.FirstOrDefault().Alias,
                    IsActive=taxResult.Entities.FirstOrDefault().IsActive,
                    Key=query.Key,
                    LanguageRef=query.LanguageRef,
                    TaxPercent=taxResult.Entities.FirstOrDefault().TaxPercent,
                    TaxRef=taxResult.Entities.FirstOrDefault().TaxRef,
                    _Description = taxResult.Entities.FirstOrDefault()._Description,
                    _Title= taxResult.Entities.FirstOrDefault()._Title
                };
            }

            return result;
        }

        public async Task<BaseTaxResult> InsertTaxAsync(InsertTaxRequest request)
        {
            var taxEntity = request.MapTo<TaxEntity>();

            taxEntity.TaxLanguages = new List<TaxLanguageEntity>
            {
                new TaxLanguageEntity
                {
                    LanguageRef = request.LanguageRef,
                    _Title = request._Title,
                    _Description = request._Description
                }
            };

            await _taxRepository.AddAsync(taxEntity);

            try
            {
                await _db.SaveChangesAsync();

                await _entityService.InsertMultilingualEntityAsync<TaxEntity, InsertTaxRequest, int>(taxEntity, request);
            }
            catch (DbUpdateException e)
            {
                SqlErrorHandling.Handler(e, "DuplicateValue");
            }
            return new BaseTaxResult();
        }

        public async Task<InsertTaxByTableNameResult> InsertTaxByTableNameAsync(InsertTaxByTableNameRequest request)
        {
            var result = new InsertTaxByTableNameResult
            {
                Entity = new InsertTaxByTableNameModel()
            };

            var taxEntity = request.MapTo<TaxEntity>();

            taxEntity.TaxLanguages = new List<TaxLanguageEntity>
            {
                new TaxLanguageEntity
                {
                    LanguageRef = request.LanguageRef,
                    _Title = request._Title,
                    _Description = request._Description
                }
            };

            await _taxRepository.AddAsync(taxEntity);

            await _db.SaveChangesAsync();

            var searchQuery = new QueryDataSource<ApplicationKeyView>();

            searchQuery.AddFilter(new ExpressionFilterInfo<ApplicationKeyView>(a => a.Alias == "Base"));
            searchQuery.AddFilter(new ExpressionFilterInfo<ApplicationKeyView>(a => a.Name == request.TableName));

            var applicationResult = await _applicationRepository.GetApplicationKeyAsync(searchQuery);

            var application = applicationResult.Entities.FirstOrDefault();

            result.Entity.ApplicationRef = application.ApplicationKey;
            result.Entity.TableRef = application.ApplicationTableKey;
            result.Entity.EntityRef = (int)taxEntity.Key;

            return result;
        }

        public async Task<BaseTaxResult> UpdateTaxAsync(UpdateTaxRequest request)
        {
            var result = new BaseTaxResult();

            var taxEntity = await _taxRepository.FindAsync(request.Key);

            if (taxEntity.IsNull())
                throw new ICDException("NotFound");

            taxEntity = request.MapTo<TaxEntity>();
            taxEntity.Key = request.Key;

            var taxLanguageEntity = await _taxLanguageRepository.FirstOrDefaultAsync(tl => tl.TaxRef == request.Key && tl.LanguageRef == request.LanguageRef);
            if (taxLanguageEntity != null)
            {
                var key = taxLanguageEntity.Key;
                taxLanguageEntity = request.MapTo<TaxLanguageEntity>();
                taxLanguageEntity.TaxRef = request.Key;
                taxLanguageEntity.Key = key;
            }

            if (taxEntity != null && taxLanguageEntity != null)
            {
                _taxRepository.Update(taxEntity);
                _taxLanguageRepository.Update(taxLanguageEntity);
                await _db.SaveChangesAsync();

                result.Success = true;
            }
            else
                result.Success = false;

            return result;
        }
    }
}
