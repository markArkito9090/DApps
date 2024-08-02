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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ICD.Base.BusinessService
{
    [Dependency(typeof(IItemGroupService))]
    public class ItemGroupService : IItemGroupService
    {
        private readonly IUnitOfWork _db;
        private readonly IItemGroupRepository _itemGroupRepository;
        private readonly IItemGroupLanguageRepository _itemGroupLanguageRepository;
        private readonly IItemRowRepository _itemRowRepository;
        private readonly IEntityService _entityService;

        public ItemGroupService(IUnitOfWork db, IEntityService entityService)
        {
            _db = db;
            _itemGroupRepository = _db.GetRepository<IItemGroupRepository>();
            _itemGroupLanguageRepository = _db.GetRepository<IItemGroupLanguageRepository>();
            _itemRowRepository = _db.GetRepository<IItemRowRepository>();
            _entityService = entityService;

        }

        public async Task<InsertItemGroupResult> InsertItemGroupAsync(InsertItemGroupRequest request)
        {
            var result = new InsertItemGroupResult
            {
                Entity = new InsertItemGroupModel()
            };

            var itemGroupEntity = request.MapTo<ItemGroupEntity>();

            itemGroupEntity.ItemGroupLanguages = new List<ItemGroupLanguageEntity>
            {
                new ItemGroupLanguageEntity
                {
                    LanguageRef = request.LanguageRef,
                    _Title = request._Title
                }
            };

            await _itemGroupRepository.AddAsync(itemGroupEntity);

            try
            {
                await _db.SaveChangesAsync();

                await _entityService.InsertMultilingualEntityAsync<ItemGroupEntity, InsertItemGroupRequest, int>(itemGroupEntity, request);
            }
            catch (DbUpdateException e)
            {
                SqlErrorHandling.Handler(e, "DuplicateValue");
            }

            result.Entity = new InsertItemGroupModel
            {
                Key = itemGroupEntity.Key,
                Alias = request.Alias,
                ApplicationRef = request.ApplicationRef,
                IsActive = request.IsActive,
                _Title = request._Title
            };

            return result;
        }

        public async Task<DeleteTypeIntResult> DeleteItemGroupByIdAsync(DeleteTypeIntRequest request)
        {
            await _itemRowRepository.DeleteWithAsync(x => x.ItemGroupRef == request.Key);

            await _itemGroupRepository.DeleteWithAsync(ig => ig.Key == request.Key);
            await _itemGroupLanguageRepository.DeleteWithAsync(igl => igl.ItemGroupRef == request.Key);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException e)
            {

                SqlErrorHandling.Handler(e);
            }

            return new DeleteTypeIntResult();
        }

        public async Task<GetItemGroupsResult> GetItemGroupsByApplicationRefAsync(GetItemGroupsQuery query)
        {
            var result = new GetItemGroupsResult
            {
                Entities = new List<GetItemGroupsModel>()
            };

            var searchQuery = query.ToQueryDataSource<ItemGroupView>();

            if (query.ApplicationRef.HasValue)
                searchQuery.AddFilter(new ExpressionFilterInfo<ItemGroupView>(igv => igv.ApplicationRef == query.ApplicationRef));
            if (query.IsActive.HasValue)
                searchQuery.AddFilter(new ExpressionFilterInfo<ItemGroupView>(igv => igv.IsActive == query.IsActive));

            var itemGroupResult = await _itemGroupRepository.GetItemGroupsByApplicationRef(searchQuery, query.LanguageRef);

            if (itemGroupResult.Entities.Any())
            {
                result = itemGroupResult.MapTo<GetItemGroupsResult>();
            }

            return result;
        }

        public async Task<BaseItemGroupResult> UpdateItemGroupByid(UpdateItemGroupRequest request)
        {
            var result = new BaseItemGroupResult();

            var itemGroupEntity = await _itemGroupRepository.FindAsync(request.Key);

            if (itemGroupEntity.IsNull())
                throw new ICDException("NotFound");

            itemGroupEntity = request.MapTo<ItemGroupEntity>();

            itemGroupEntity.Key = request.Key;


            var itemGroupLanguageEntity = await _itemGroupLanguageRepository.FirstOrDefaultAsync(igl => igl.LanguageRef == request.LanguageRef && igl.ItemGroupRef == request.Key);
            if (itemGroupLanguageEntity != null)
            {
                var key = itemGroupLanguageEntity.Key;
                itemGroupLanguageEntity = request.MapTo<ItemGroupLanguageEntity>();
                itemGroupLanguageEntity.Key = key;
                itemGroupLanguageEntity.ItemGroupRef = request.Key;
            }

            if (itemGroupEntity != null && itemGroupLanguageEntity != null)
            {
                _itemGroupRepository.Update(itemGroupEntity);
                _itemGroupLanguageRepository.Update(itemGroupLanguageEntity);
                await _db.SaveChangesAsync();

                result.Success = true;
            }
            else
                result.Success = false;
            return result;
        }

        public async Task<GetItemGroupsByKeyResult> GetItemGroupsByKeyAsync(GetItemGroupsByKeyQuery query)
        {
            var result = new GetItemGroupsByKeyResult
            {
                Entity = new GetItemGroupsByKeyModel()
            };

            var searchQuery = query.ToQueryDataSource<ItemGroupView>();

            searchQuery.AddFilter(new ExpressionFilterInfo<ItemGroupView>(igv => igv.Key == query.Key));

            var itemGroupResult = await _itemGroupRepository.GetItemGroupsByApplicationRef(searchQuery, query.LanguageRef);

            if (itemGroupResult.Entities.Any())
            {
                result.Entity = itemGroupResult.Entities.FirstOrDefault().MapTo<GetItemGroupsByKeyModel>();
            }

            return result;
        }
    }
}
