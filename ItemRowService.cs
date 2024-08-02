using ICD.Base.BusinessServiceContract;
using ICD.Base.Domain.Entity;
using ICD.Base.Domain.View;
using ICD.Base.RepositoryContract;
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
    [Dependency(typeof(IItemRowService))]
    public class ItemRowService : IItemRowService
    {
        private readonly IUnitOfWork _db;
        private readonly IItemRowRepository _itemRowRepository;
        private readonly IItemRowLanguageRepository _itemRowLanguageRepository;
        private readonly IEntityService _entityService;

        public ItemRowService(IUnitOfWork db, IEntityService entityService)
        {
            _db = db;
            _itemRowRepository = _db.GetRepository<IItemRowRepository>();
            _itemRowLanguageRepository = _db.GetRepository<IItemRowLanguageRepository>();
            _entityService = entityService;

        }

        public async Task<InsertItemRowResult> InsertItemRowAsync(InsertItemRowRequest request)
        {
            var queryDataSource = new QueryDataSource<ItemRowByKeyView>();
            queryDataSource.AddFilter(new ExpressionFilterInfo<ItemRowByKeyView>(x => x.ItemGroupRef == request.ItemGroupRef));

            var itemRowResult = await _itemRowRepository.GetItemRowByKeyAsync(queryDataSource, request.LanguageRef);

            var newEntity = new ItemRowEntity();
            var itemRowEntityList = new List<ItemRowEntity>();
            if (!itemRowResult.Entities.Any())
            {
                if (!request.Details.Any())
                    throw new ICDException("NoDetailsSelected");

                foreach (var itm in request.Details)
                {
                    newEntity = new ItemRowEntity
                    {
                        Alias = itm.Alias,
                        IsActive = itm.IsActive,
                        ItemGroupRef = request.ItemGroupRef,
                        Value = itm.Value
                    };


                    newEntity.ItemRowLanguages = new List<ItemRowLanguageEntity> { new ItemRowLanguageEntity
                    {
                        LanguageRef = request.LanguageRef,
                        _Title = itm._Title
                    }};

                    await _itemRowRepository.AddAsync(newEntity);

                    itemRowEntityList.Add(newEntity);
                }

                try
                {
                    await _db.SaveChangesAsync();

                    foreach (var item in itemRowEntityList)
                    {
                        await _entityService.InsertMultilingualEntityAsync<ItemRowEntity, object , int>(item, item.ItemRowLanguages.First(), request.LanguageRef);
                    }
                }
                catch (DbUpdateException e)
                {
                    SqlErrorHandling.Handler(e, "DuplicateValue");
                }

                return new InsertItemRowResult();
            }

            var exsKeys = itemRowResult.Entities.Select(x => x.Key);
            var newKey = request.Details.Select(x => x.Key);
            var deletedKeys = itemRowResult.Entities.ToList().FindAll(x => !newKey.Contains(x.Key));

            foreach (var detail in request.Details)
            {
                var itemRow = itemRowResult.Entities.FirstOrDefault(x => x.Key == detail.Key);

                if (itemRow.IsNotNull())
                {
                    var itemRowEntity = new ItemRowEntity
                    {
                        Key = itemRow.Key,
                        Alias = detail.Alias,
                        IsActive = detail.IsActive,
                        ItemGroupRef = request.ItemGroupRef,
                        Value = detail.Value,
                    };

                    _itemRowRepository.Update(itemRowEntity);


                    var itemRowLanguageEntity = new ItemRowLanguageEntity
                    {
                        Key = itemRow.ItemRowLanguageKey,
                        LanguageRef = request.LanguageRef,
                        _Title = detail._Title,
                        ItemRowRef = itemRow.Key
                    };

                    _itemRowLanguageRepository.Update(itemRowLanguageEntity);
                }

                if (itemRow.IsNull())
                {
                    newEntity = new ItemRowEntity
                    {
                        Alias = detail.Alias,
                        IsActive = detail.IsActive,
                        ItemGroupRef = request.ItemGroupRef,
                        Value = detail.Value
                    };

                    newEntity.ItemRowLanguages = new List<ItemRowLanguageEntity> { new ItemRowLanguageEntity
                    {
                        LanguageRef = request.LanguageRef,
                        _Title = detail._Title
                    }};

                    await _itemRowRepository.AddAsync(newEntity);

                    itemRowEntityList.Add(newEntity);
                }
            }

            if (deletedKeys.Any())
            {
                var keys = deletedKeys.Select(x => x.Key);

                try
                {
                    _itemRowRepository.DeleteRangeByIds(keys);
                }
                catch (System.Exception e)
                {

                    throw;
                }
            }

            try
            {
                await _db.SaveChangesAsync();
                foreach (var item in itemRowEntityList)
                {
                    await _entityService.InsertMultilingualEntityAsync<ItemRowEntity, object, int>(item, item.ItemRowLanguages.First(), request.LanguageRef);
                }
            }
            catch (DbUpdateException e)
            {
                SqlErrorHandling.Handler(e, "DuplicateValue");
            }

            return new InsertItemRowResult();
        }

        public async Task<DeleteItemRowResult> DeleteItemRowByIdAsync(DeleteItemRowRequest request)
        {
            _itemRowRepository.DeleteRangeByIds(request.Keys);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException e)
            {

                SqlErrorHandling.Handler(e);
            }

            return new DeleteItemRowResult();
        }

        public async Task<GetItemRowsResult> GetItemRowsAsync(GetItemRowsQuery query)
        {
            var result = new GetItemRowsResult
            {
                Entities = new List<GetItemRowsModel>()
            };

            var searchQuery = query.ToQueryDataSource<ItemRowView>();

            if (query.ItemGroupRef.HasValue)
                searchQuery.AddFilter(new ExpressionFilterInfo<ItemRowView>(ir => ir.ItemGroupRef == query.ItemGroupRef));

            if (query.ItemGroupAlias.IsNotNull())
                searchQuery.AddFilter(new ExpressionFilterInfo<ItemRowView>(ir => ir.ItemGroupAlias == query.ItemGroupAlias && ir.IsActive == true));

            var itemRowResult = await _itemRowRepository.GetItemRowsByItemGroupRef(searchQuery, query.LanguageRef);

            if (itemRowResult.Entities.Any())
            {
                result = itemRowResult.MapTo<GetItemRowsResult>();
                result.Success = true;
            }
            return result;
        }

    }
}
