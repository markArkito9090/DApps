using ICD.Base.BusinessServiceContract;
using ICD.Base.Domain.Entity;
using ICD.Base.Domain.View;
using ICD.Base.RepositoryContract;
using ICD.Framework.AppMapper.Extensions;
using ICD.Framework.Data.UnitOfWork;
using ICD.Framework.DataAnnotation;
using ICD.Framework.Extensions;
using ICD.Framework.QueryDataSource.Fitlter;
using ICD.Infrastructure.Exception;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICD.Base.BusinessService
{
    [Dependency(typeof(IPersonContactService))]
    public class PersonContactService : IPersonContactService
    {
        private readonly IUnitOfWork _db;
        private readonly IPersonContactRepository _personContactRepository;
        private readonly IContactTypeLanguageRepository _contactTypeLanguageRepository;

        public PersonContactService(IUnitOfWork db)
        {
            _db = db;
            _personContactRepository = _db.GetRepository<IPersonContactRepository>();
            _contactTypeLanguageRepository = _db.GetRepository<IContactTypeLanguageRepository>();
        }

        public async Task<GetPersonContactResult> GetPersonContactAsync(GetPersonContactQuery query)
        {
            var result = new GetPersonContactResult
            {
                Entities = new List<GetPersonContactModel>()
            };

            var searchQuery = query.ToQueryDataSource<PersonContactView>();

            searchQuery.AddFilter(new ExpressionFilterInfo<PersonContactView>(x => x.PersonRef == query.PersonRef));

            var personContactResult = await _personContactRepository.GetPersonContactAsync(searchQuery, query.LanguageRef);

            if (personContactResult.Entities.Any())
            {
                result = personContactResult.MapTo<GetPersonContactResult>();
            }

            return result;
        }

        public async Task<BasePersonContactResult> InsertPersonContactAsync(InsertPersonContactRequest request)
        {
            await _personContactRepository.DeleteWithAsync(x => x.PersonRef == request.PersonRef);

            var contactTypeRefs = request.Contacts.Select(x => x.ContactTypeRef).Distinct().ToList();

            var ctlResult = await _contactTypeLanguageRepository.FindAsync(x => contactTypeRefs.Contains(x.ContactTypeRef) && x.LanguageRef == request.LanguageRef);

            foreach (var c in request.Contacts)
            {
                var cRes = request.Contacts.Where(x => x.ContactTypeRef == c.ContactTypeRef);
                if (cRes.Count() > 1)
                {
                    if (!cRes.Where(x => x.IsMain == true).Any())
                    {
                        var title = ctlResult.First(x => x.ContactTypeRef == c.ContactTypeRef)._Title;
                        throw new ICDException("ContactTypeIsMain", title);
                    }
                    if (cRes.Where(x => x.IsMain == true).Count() > 1)
                    {
                        var title = ctlResult.First(x => x.ContactTypeRef == c.ContactTypeRef)._Title;
                        throw new ICDException("ContactTypeIsMainWrong",title);
                    }
                }
                else
                {
                    if (c.IsMain != true)
                    {
                        var title = ctlResult.First(x => x.ContactTypeRef == c.ContactTypeRef)._Title;
                        throw new ICDException("ContactTypeIsMain");
                    }
                }


                var personContactEntity = new PersonContactEntity
                {
                    PersonRef = request.PersonRef,
                    ContactInfo = c.ContactInfo,
                    ContactTypeRef = c.ContactTypeRef,
                    IsMain = c.IsMain
                };

                await _personContactRepository.AddAsync(personContactEntity);
            }


            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException e)
            {

                SqlErrorHandling.Handler(e);
            }

            return new BasePersonContactResult();
        }

    }
}
