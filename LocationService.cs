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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ICD.Base.BusinessService
{
    [Dependency(typeof(ILocationService))]
    public class LocationService : ILocationService
    {
        private readonly IUnitOfWork _db;
        private readonly ILocationRepository _locationRepository;
        private readonly ILocationLanguageRepository _locationLanguageRepository;
        private readonly IEntityService _entityService;

        public LocationService(IUnitOfWork db, IEntityService entityService)
        {
            _db = db;
            _locationRepository = _db.GetRepository<ILocationRepository>();
            _locationLanguageRepository = _db.GetRepository<ILocationLanguageRepository>();
            _entityService = entityService;
        }

        public async Task<BaseLocationResult> InsertLocationAsync(InsertLocationRequest request)
        {
            var locationEntity = request.MapTo<LocationEntity>();
            if (locationEntity.ParentRef == null)
                locationEntity.LevelNo = 0;
            else
                locationEntity.LevelNo = Convert.ToInt32(locationEntity.ParentRef + 1);

            locationEntity.LocationLanguages = new List<LocationLanguageEntity>
            {
                new LocationLanguageEntity
                {
                    LanguageRef = request.LanguageRef,
                    _Name = request._Name
                }
            };

            await _locationRepository.AddAsync(locationEntity);

            try
            {
                await _db.SaveChangesAsync();

                await _entityService.InsertMultilingualEntityAsync<LocationEntity, InsertLocationRequest, int>(locationEntity, request);
            }
            catch (DbUpdateException e)
            {
                SqlErrorHandling.Handler(e, "DuplicateValue");
            }

            return new BaseLocationResult();
        }

        public async Task<DeleteTypeIntResult> DeleteLocationByIdAsync(DeleteTypeIntRequest request)
        {
            await _locationRepository.DeleteWithAsync(l => l.Key == request.Key);
            await _locationLanguageRepository.DeleteWithAsync(ll => ll.LocationRef == request.Key);

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

        public async Task<GetLocationResult> GetLocationAsync(GetLocationQuery query)
        {
            var result = new GetLocationResult
            {
                Entities = new List<GetLocationModel>()
            };

            var searchQuery = query.ToQueryDataSource<LocationView>();

            if (query.ParentRef.HasValue)
                searchQuery.AddFilter(new ExpressionFilterInfo<LocationView>(lv => lv.ParentRef == query.ParentRef));
            if (query.Alias.IsNotNull())
                searchQuery.AddFilter(new ExpressionFilterInfo<LocationView>(lv => lv.Alias == query.Alias));

            var locationResult = await _locationRepository.GetLocation(searchQuery, query.LanguageRef);

            if (locationResult.Entities.Any())
            {
                result = locationResult.MapTo<GetLocationResult>();
            }

            return result;
        }

        public async Task<BaseLocationResult> UpdateLocationAsync(UpdateLocationRequest request)
        {
            var result = new BaseLocationResult();

            var locationEntity = await _locationRepository.FindAsync(request.Key);

            if (locationEntity.IsNull())
                throw new ICDException("NotFound");

            locationEntity = request.MapTo<LocationEntity>();
            locationEntity.Key = request.Key;

            var locationLanguageEntity = await _locationLanguageRepository.FirstOrDefaultAsync(ll => ll.LocationRef == request.Key && ll.LanguageRef == request.LanguageRef);
            if (locationLanguageEntity != null)
            {
                var key = locationLanguageEntity.Key;
                locationLanguageEntity = request.MapTo<LocationLanguageEntity>();
                locationLanguageEntity.LocationRef = request.Key;
                locationLanguageEntity.Key = key;
            }

            if (locationEntity != null && locationLanguageEntity != null)
            {
                _locationRepository.Update(locationEntity);
                _locationLanguageRepository.Update(locationLanguageEntity);
                await _db.SaveChangesAsync();

                result.Success = true;
            }
            else
                result.Success = false;

            return result;
        }
    }
}
