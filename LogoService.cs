using ICD.Base.BusinessServiceContract;
using ICD.Base.RepositoryContract;
using ICD.Framework.AppMapper.Extensions;
using ICD.Framework.Data.UnitOfWork;
using ICD.Framework.DataAnnotation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICD.Base.BusinessService
{
    [Dependency(typeof(ILogoService))]
    public class LogoService : ILogoService
    {
        private readonly IUnitOfWork _db;
        private readonly ILogoRepository _logoRepository;

        public LogoService(IUnitOfWork db)
        {
            _db = db;
            _logoRepository = _db.GetRepository<ILogoRepository>();
        }

        public async Task<GetLogoResult> GetLogoAsync(GetLogoQuery query)
        {
            var result = new GetLogoResult();

            var logoResult = await _logoRepository.FirstOrDefaultAsync(x => x.Alias == query.Alias);

            if (logoResult != null)
            {
                result.Entity = logoResult.MapTo<GetLogoModel>();

                return result;
            }

            return result;
        }
    }
}
