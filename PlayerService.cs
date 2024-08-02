using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ICD.Base.BusinessServiceContract;
using ICD.Base.Domain.Entity;
using ICD.Base.Player.Commands;
using ICD.Base.RepositoryContract;
using ICD.Framework.AppMapper.Extensions;
using ICD.Framework.Data.UnitOfWork;
using ICD.Framework.DataAnnotation;
using ICD.Framework.Extensions;
using ICD.Framework.Model;
using ICD.Framework.QueryDataSource;
using ICD.Framework.QueryDataSource.Fitlter;
using ICD.Infrastructure.Exception;

namespace ICD.Base.BusinessService
{
    [Dependency(typeof(IPlayerService))]
    public class PlayerService : IPlayerService
    {
        private readonly IUnitOfWork _db;
        private readonly IPlayerRepository _playerRepository;

        public PlayerService(IUnitOfWork db)
        {
            _db = db;
            _playerRepository = _db.GetRepository<IPlayerRepository>();

        }
        public async Task<GetPlayersByTeamResult> GetplayersInTeamAsync(GetPlayersByTeamQuery query)
        {
            QueryDataSource<PlayerEntity> queryDataSource = query.ToQueryDataSource<PlayerEntity>();
            queryDataSource.AddFilter(new ExpressionFilterInfo<PlayerEntity>(x=>x.TeamId==query.TeamId));
            ListQueryResult<PlayerEntity> Result = await _playerRepository.GetAllPlayersAsync(queryDataSource);

            var finalResult = Result.MapTo<GetPlayersByTeamResult>();
            return finalResult; 

        }

        public async Task<InsertPlayerResult> InsertPlayerAsync(InsertPlayerRequest request)
        {
            var newplayer = request.MapTo<PlayerEntity>();

            await _playerRepository.AddAsync(newplayer);
            await _db.SaveChangesAsync();
            return new InsertPlayerResult { Success = true };

        }

        public async Task<RemovePlayerFromTeamResult> RemovePlayerFromTeamAsync(RemovePlayerFromTeamRequest request)
        {
            var player =
                await _playerRepository.FirstOrDefaultAsync(
                    x => x.TeamId == request.TeamId && x.Key == request.PlayerId);
            if (player.IsNull())
                throw new ICDException("NotFound");
            player.TeamId = null;
            _playerRepository.Update(player);
            await _db.SaveChangesAsync();
            return new RemovePlayerFromTeamResult { Success = true };

        }

        public async Task<UpdatePlayerResult> UpdatePlayerAsync(UpdatePlayerRequest request)
        {
            var player = await _playerRepository.FirstOrDefaultAsync(x => x.Key == request.Key);
            if (player.IsNull())
                throw new ICDException("NotFound");

            player = request.MapTo<PlayerEntity>();

            _playerRepository.Update(player);
            await _db.SaveChangesAsync();

            return new UpdatePlayerResult { Success = true };
        }
    }
}
