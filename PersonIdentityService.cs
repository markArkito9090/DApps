using ICD.Base.BusinessServiceContract;
using ICD.Base.Domain.Entity;
using ICD.Base.RepositoryContract;
using ICD.Framework.AppMapper.Extensions;
using ICD.Framework.Data.UnitOfWork;
using ICD.Framework.DataAnnotation;
using ICD.Framework.Extensions;
using ICD.Infrastructure.Exception;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace ICD.Base.BusinessService
{
    [Dependency(typeof(IPersonIdentityService))]
    public class PersonIdentityService : IPersonIdentityService
    {
        private readonly IUnitOfWork _db;
        private readonly IPersonIdentityRepository _personIdentityRepository;

        public PersonIdentityService(IUnitOfWork db)
        {
            _db = db;
            _personIdentityRepository = _db.GetRepository<IPersonIdentityRepository>();
        }

        public async Task<BasePersonIdentityResult> InsertPersonIdentityAsync(InsertPersonIdentityRequest request)
        {
            var personIdentityEntity = request.MapTo<PersonIdentityEntity>();
            await _personIdentityRepository.AddAsync(personIdentityEntity);

            await _db.SaveChangesAsync();
            return new BasePersonIdentityResult();
        }

        public async Task<GetPersonIdentityResult> GetPersonIdentityByPersonRefAsync(GetPersonIdentityQuery query)
        {
            var result = new GetPersonIdentityResult();

            var personIdentity = await _personIdentityRepository.FirstOrDefaultAsync(pi => pi.PersonRef == query.PersonRef);

            result.Entity = personIdentity.MapTo<GetPersonIdentityModel>();
            result.Success = true;
            return result;
        }

        public async Task<BasePersonIdentityResult> UpdatePersonIdentityByIdAsync(UpdatePersonIdentityRequest request)
        {
            var personIdentityEntity = await _personIdentityRepository.FindAsync(request.Key);

            if (personIdentityEntity.IsNull())
                throw new ICDException("NotFound");

            personIdentityEntity = request.MapTo<PersonIdentityEntity>();
            personIdentityEntity.Key = request.Key;

            _personIdentityRepository.Update(personIdentityEntity);

            await _db.SaveChangesAsync();

            return new BasePersonIdentityResult { Success = true };
        }

        public async Task<DeleteTypeIntResult> DeletePersonIdentityByIdAsync(DeleteTypeIntRequest request)
        {
            await _personIdentityRepository.DeleteWithAsync(pi => pi.Key == request.Key);

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
    }
}
