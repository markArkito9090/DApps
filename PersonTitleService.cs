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
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ICD.Base.BusinessService
{
    [Dependency(typeof(IPersonTitleService))]
    public class PersonTitleService : IPersonTitleService
    {
        private readonly IUnitOfWork _db;
        private readonly IPersonTitleRepository _personTitleRepository;
        private readonly IPersonTitleLanguageRepository _personTitleLanguageRepository;
        private readonly IItemRowRepository _itemRowRepository;
        private readonly IEntityService _entityService;
        public PersonTitleService(IUnitOfWork db, IEntityService entityService)
        {
            _db = db;
            _personTitleRepository = _db.GetRepository<IPersonTitleRepository>();
            _personTitleLanguageRepository = _db.GetRepository<IPersonTitleLanguageRepository>();
            _itemRowRepository = _db.GetRepository<IItemRowRepository>();
            _entityService = entityService;
        }

        public async Task<BasePersonTitleResult> InsertPersonTitleAsync(InsertPersonTitleRequest request)
        {
            var result = new BasePersonTitleResult();

            int order;
            try
            {
                order = _personTitleRepository.Max(pt => pt.Order);
            }
            catch (Exception)
            {
                order = 0;
            }

            var itemRowKey = await _itemRowRepository.FirstOrDefaultAsync(ir => ir.Alias == request.ItemRowAlias);

            if (itemRowKey.IsNull())
                throw new ICDException("NoItemRowFound");

            var personTitleEntity = request.MapTo<PersonTitleEntity>();
            personTitleEntity.Order = order + 1;

            if (request.ItemRowAlias == "Legal")
                personTitleEntity.IsLegal = true;
            else if (request.ItemRowAlias == "Real")
                personTitleEntity.IsLegal = false;

            personTitleEntity.ItemRowRef_LegalType = itemRowKey.Key;

            personTitleEntity.PersonTitleLanguages = new List<PersonTitleLanguageEntity>
            {
                new PersonTitleLanguageEntity
                {
                    LanguageRef = request.LanguageRef,
                    _Name = request._Name,
                    _Description = request._Description
                }
            };

            await _personTitleRepository.AddAsync(personTitleEntity);

            try
            {
                await _db.SaveChangesAsync();

                await _entityService.InsertMultilingualEntityAsync<PersonTitleEntity, InsertPersonTitleRequest, int>(personTitleEntity, request);
            }
            catch (DbUpdateException e)
            {
                SqlErrorHandling.Handler(e, "DuplicateValue");
            }
            return result;
        }

        public async Task<DeleteTypeIntResult> DeletePersonTitleByIdAsync(DeleteTypeIntRequest request)
        {
            var result = new DeleteTypeIntResult();

            await _personTitleRepository.DeleteWithAsync(pt => pt.Key == request.Key);
            await _personTitleLanguageRepository.DeleteWithAsync(ptl => ptl.PersonTitleRef == request.Key);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException e)
            {

                SqlErrorHandling.Handler(e);
            }

            result.Success = true;
            return result;
        }

        public async Task<GetPersonTitlesResult> GetPersonTitlesByLanguageRefAsync(GetPersonTitlesQuery query)
        {
            var result = new GetPersonTitlesResult
            {
                Entities = new List<GetPersonTitlesModel>(),
                PageIndex = query.Page,
                PageSize = query.Page
            };

            var searchQuery = query.ToQueryDataSource<PersonTitleView>();

            if (query.IsActive.HasValue)
                searchQuery.AddFilter(new ExpressionFilterInfo<PersonTitleView>(ptv => ptv.IsActive == query.IsActive.Value));
            if (query.IsLegal.HasValue)
                searchQuery.AddFilter(new ExpressionFilterInfo<PersonTitleView>(ptv => ptv.IsLegal == query.IsLegal.Value));

            var personTitleResult = await _personTitleRepository.GetPersonTitlesByLanguageRef(searchQuery, query.LanguageRef);

            if (personTitleResult.Entities.Any())
            {
                result = personTitleResult.MapTo<GetPersonTitlesResult>();
            }

            return result;
        }

        public async Task<BasePersonTitleResult> UpdatePersonTitleByIdAsync(UpdatePersonTitleRequest request)
        {
            var result = new BasePersonTitleResult();

            var personTitleEntity = await _personTitleRepository.FindAsync(request.Key);

            if (personTitleEntity.IsNull())
                throw new ICDException("NotFound");

            if (personTitleEntity != null)
            {
                personTitleEntity = request.MapTo<PersonTitleEntity>();
                personTitleEntity.Key = request.Key;

                if (request.ItemRowAlias == "Legal")
                    personTitleEntity.IsLegal = true;
                else if (request.ItemRowAlias == "Real")
                    personTitleEntity.IsLegal = false;
            }

            var personTitleLanguageEntity = await _personTitleLanguageRepository.FirstOrDefaultAsync(ptl => ptl.LanguageRef == request.LanguageRef && ptl.PersonTitleRef == request.Key);
            if (personTitleLanguageEntity != null)
            {
                var key = personTitleLanguageEntity.Key;
                personTitleLanguageEntity = request.MapTo<PersonTitleLanguageEntity>();
                personTitleLanguageEntity.Key = key;
                personTitleLanguageEntity.PersonTitleRef = request.Key;
            }

            if (personTitleLanguageEntity != null && personTitleEntity != null)
            {
                _personTitleRepository.Update(personTitleEntity);
                _personTitleLanguageRepository.Update(personTitleLanguageEntity);
                await _db.SaveChangesAsync();

                result.Success = true;
            }
            else
                result.Success = false;
            return result;
        }

        //public object TestGetPersonProc()
        //{
        //    return _personTitleRepository.TestGetPersonProc(_configuration);
        //}

        public async Task<GetPersonTitleByKeyResult> GetPersonTitleByKeyAsync(GetPersonTitleByKeyQuery query)
        {
            var finalResult = new GetPersonTitleByKeyResult();

            var queryDataSource = query.ToQueryDataSource<PersonTitleView>();

            queryDataSource.AddFilter(new ExpressionFilterInfo<PersonTitleView>(x => x.Key == query.Key));

            var result = await _personTitleRepository.GetPersonTitlesByLanguageRef(queryDataSource, query.LanguageRef);

            if (!result.Entities.Any())
                return finalResult;

            finalResult.Entity = result.Entities.First().MapTo<GetPersonTitlesModel>();

            return finalResult;
        }
    }
}
