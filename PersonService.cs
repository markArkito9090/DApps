using ICD.Base.BusinessServiceContract;
using ICD.Base.Domain.Entity;
using ICD.Base.Domain.View;
using ICD.Base.RepositoryContract;
using ICD.Base.RepositoryContract.External_Repository_Contract.HRM;
using ICD.Framework.AppMapper.Extensions;
using ICD.Framework.Data.UnitOfWork;
using ICD.Framework.DataAnnotation;
using ICD.Framework.Extensions;
using ICD.Framework.QueryDataSource;
using ICD.Framework.QueryDataSource.Fitlter;
using ICD.Infrastructure.BusinessServiceContract;
using ICD.Infrastructure.Exception;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ICD.Base.BusinessService
{
    [Dependency(typeof(IPersonService))]
    public class PersonService : IPersonService
    {
        private readonly IUnitOfWork _db;
        private readonly IPersonRepository _personRepository;
        private readonly IPersonLanguageRepository _personLanguageRepository;
        private readonly ICompanyRepository _companyExternalRepository;
        private readonly ICompanyLanguageRepository _companyLanguageRepository;
        private readonly IPersonTitleRepository _personTitleRepository;
        private readonly IEntityService _entityService;

        public PersonService(IUnitOfWork db, IEntityService entityService)
        {
            _db = db;
            _personRepository = _db.GetRepository<IPersonRepository>();
            _personLanguageRepository = _db.GetRepository<IPersonLanguageRepository>();
            _companyExternalRepository = _db.GetRepository<ICompanyRepository>();
            _companyLanguageRepository = _db.GetRepository<ICompanyLanguageRepository>();
            _personTitleRepository = _db.GetRepository<IPersonTitleRepository>();
            _entityService = entityService;

        }

        public async Task<InsertPersonResult> InsertPersonAsync(InsertPersonRequest request)
        {
            var result = new InsertPersonResult
            {
                Entity = new InsertPersonModel()
            };


            if (request._LastName == null)
                request._LastName = "";
            if (request.EconomicId == null)
                request.EconomicId = "";

            var personEntity = request.MapTo<PersonEntity>();

            personEntity.PersonLanguages = new List<PersonLanguageEntity>
            {
                new PersonLanguageEntity
                {
                    LanguageRef = request.LanguageRef,
                    _FatherName = request._FatherName,
                    _Name = request._Name,
                    _LastName = request._LastName
                }
            };

            await _personRepository.AddAsync(personEntity);


            try
            {
                await _db.SaveChangesAsync();

                await _entityService.InsertMultilingualEntityAsync<PersonEntity, InsertPersonRequest, long>(personEntity, request);
            }
            catch (DbUpdateException e)
            {
                SqlErrorHandling.Handler(e, "DuplicateValue");
            }

            var searchQuery = new QueryDataSource<PersonView>();

            searchQuery.AddFilter(new ExpressionFilterInfo<PersonView>(pv => pv.Key == personEntity.Key));

            var personResult = await _personRepository.GetPeoplesByTitleRefAndLanguageRef(searchQuery, request.LanguageRef);

            result.Entity = personResult.Entities.FirstOrDefault().MapTo<InsertPersonModel>();

            return result;
        }

        public async Task<DeleteTypeLongResult> DeleteByIdAsync(DeleteTypeLongRequest request)
        {
            await _personRepository.DeleteWithAsync(p => p.Key == request.Key);
            await _personLanguageRepository.DeleteWithAsync(pl => pl.PersonRef == request.Key);

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

        public async Task<GetPeopleByspecificationResult> GetBySpecificationAsync(GetPeopleBySpecificationQuery query)
        {
            var result = new GetPeopleByspecificationResult()
            {
                Entities = new List<GetPeopleByspecificationModel>()
            };

            var searchQuery = query.ToQueryDataSource<PersonView>();

            if (query.IsLegal.HasValue)
                searchQuery.AddFilter(new ExpressionFilterInfo<PersonView>(pv => pv.IsLegal == query.IsLegal));
            if (query.PersonTitleRef.HasValue)
                searchQuery.AddFilter(new ExpressionFilterInfo<PersonView>(pv => pv.PersonTitleRef == query.PersonTitleRef));
            if (query._Name.IsNotNull())
                searchQuery.AddFilter(new ExpressionFilterInfo<PersonView>(pv => pv._Name == query._Name));
            if (query._LastName.IsNotNull())
                searchQuery.AddFilter(new ExpressionFilterInfo<PersonView>(pv => pv._LastName == query._LastName));
            if (query.NationalIdentity.IsNotNull())
                searchQuery.AddFilter(new ExpressionFilterInfo<PersonView>(pv => pv.NationalIdentity == query.NationalIdentity));
            if (query.BaseTypeAliases.IsNotNull() && query.BaseTypeAliases.Any())
                searchQuery.AddFilter(new ExpressionFilterInfo<PersonView>(x => query.BaseTypeAliases.Contains(x.BaseTypeAlias)));


            var personResult = await _personRepository.GetPeoplesByTitleRefAndLanguageRef(searchQuery, query.LanguageRef);

            if (personResult.Entities.Any())
            {
                result = personResult.MapTo<GetPeopleByspecificationResult>();
            }

            return result;
        }

        public async Task<BasePersonResult> UpdatePersonAsync(UpdatePersonRequest request)
        {
            var result = new BasePersonResult();

            var personEntity = await _personRepository.FindAsync(request.Key);

            if (personEntity.IsNull())
                throw new ICDException("NotFound");

            personEntity = request.MapTo<PersonEntity>();
            personEntity.Key = request.Key;


            var personLanguageEntity = await _personLanguageRepository.FirstOrDefaultAsync(pl => pl.PersonRef == request.Key && pl.LanguageRef == request.LanguageRef);

            var key = personLanguageEntity.Key;
            personLanguageEntity = request.MapTo<PersonLanguageEntity>();
            personLanguageEntity.PersonRef = request.Key;
            personLanguageEntity.FullName = request._Name + ' ' + request._LastName;
            personLanguageEntity.Key = key;


            var personTitleEntity = await _personTitleRepository.FirstOrDefaultAsync(x => x.Key == personEntity.PersonTitleRef);
            if (personTitleEntity.IsNull())
                throw new ICDException("NotFound");

            if (personTitleEntity.IsLegal)
            {

                var companyResult = await _companyExternalRepository.FirstOrDefaultAsync(c => c.PersonRef == request.Key);
                if (companyResult.IsNotNull())
                {

                    var companyLanguageEntity = await _companyLanguageRepository.FirstOrDefaultAsync(x => x.CompanyRef == companyResult.Key && x.LanguageRef == request.LanguageRef);
                    companyLanguageEntity._Title = personLanguageEntity.FullName;

                    _companyLanguageRepository.Update(companyLanguageEntity);
                }

            }



            if (personLanguageEntity != null && personEntity != null)
            {
                _personRepository.Update(personEntity);
                _personLanguageRepository.Update(personLanguageEntity);

                await _db.SaveChangesAsync();

                result.Success = true;
            }
            else
                result.Success = false;

            return result;

        }

        public async Task<GetPeopleByKeyResult> GetPeopleByKeyAsync(GetPeopleByKeyQuery query)
        {
            var result = new GetPeopleByKeyResult()
            {
                Entity = new GetPeopleByKeyModel()
            };

            var searchQuery = query.ToQueryDataSource<PersonView>();

            searchQuery.AddFilter(new ExpressionFilterInfo<PersonView>(x => query.Key == x.Key));

            var personResult = await _personRepository.GetPeoplesByTitleRefAndLanguageRef(searchQuery, query.LanguageRef);

            if (personResult.Entities.Any())
            {
                result.Entity = personResult.Entities.FirstOrDefault().MapTo<GetPeopleByKeyModel>();
            }

            return result;
        }
    }
}