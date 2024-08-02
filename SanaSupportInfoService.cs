using ICD.Base.BusinessServiceContract;
using ICD.Base.Domain.Entity;
using ICD.Base.Domain.View;
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
    [Dependency(typeof(ISanaSupportInfoService))]
    public class SanaSupportInfoService : ISanaSupportInfoService
    {
        private readonly IUnitOfWork _db;
        private readonly ISanaSupportInfoRepository _sanaSupportInfoRepository;

        public SanaSupportInfoService(IUnitOfWork db)
        {
            _db = db;
            _sanaSupportInfoRepository = _db.GetRepository<ISanaSupportInfoRepository>();
        }

        public async Task<DeleteTypeIntResult> DeleteSanaSupportInfoAsync(DeleteTypeIntRequest request)
        {
            await _sanaSupportInfoRepository.DeleteWithAsync(x => x.Key == request.Key);

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

        public async Task<GetSanaSupportInfoResult> GetSanaSupportInfoAsync(GetSanaSupportInfoQuery query)
        {
            var queryDataSource = query.ToQueryDataSource<SanaSupportInfoView>();

            var result = await _sanaSupportInfoRepository.GetSanaSupportInfoAsync(queryDataSource, query.LanguageRef);

            return result.MapTo<GetSanaSupportInfoResult>();
        }

        public async Task<BaseSanaSupportInfoResult> InsertSanaSupportInfoAsync(InsertSanaSupportInfoRequest request)
        {
            var ssiEntity = request.MapTo<SanaSupportInfoEntity>();

            await _sanaSupportInfoRepository.AddAsync(ssiEntity);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException e)
            {

                SqlErrorHandling.Handler(e);
            }

            return new BaseSanaSupportInfoResult { Success = true };
        }

        public async Task<BaseSanaSupportInfoResult> UpdateSanaSupportInfoAsync(UpdateSanaSupportInfoRequest request)
        {
            var ssiEntity = await _sanaSupportInfoRepository.FirstOrDefaultAsync(x => x.Key == request.Key);

            if (ssiEntity.IsNull())
                throw new ICDException("NotFound");

            ssiEntity = request.MapTo<SanaSupportInfoEntity>();

            _sanaSupportInfoRepository.Update(ssiEntity);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException e)
            {

                SqlErrorHandling.Handler(e);
            }

            return new BaseSanaSupportInfoResult { Success = true };
        }
    }
}
