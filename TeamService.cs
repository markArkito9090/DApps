using ICD.Base.BusinessServiceContract;
using ICD.Base.Domain.Entity;
using ICD.Base.RepositoryContract;
using ICD.Framework.AppMapper.Extensions;
using ICD.Framework.Data.UnitOfWork;
using ICD.Framework.DataAnnotation;
using ICD.Framework.Extensions;
using ICD.Framework.Model;
using ICD.Framework.QueryDataSource;
using ICD.Framework.QueryDataSource.Fitlter;
using ICD.Infrastructure.Exception;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICD.Base.BusinessService
{
    [Dependency(typeof(ITeamService))]
    public class TeamService : ITeamService
    {
        private readonly IUnitOfWork _db;
        private readonly ITeamRepository _teamRepository;
        private readonly IChampionshipRepository _championshipRepository;
        public TeamService(IUnitOfWork db)
        {
            _db = db;
            _teamRepository = _db.GetRepository<ITeamRepository>();
            _championshipRepository=_db.GetRepository<IChampionshipRepository>();

        }

        public async Task<GetTeamsBySpecificChampionshipResult> GetTeamsBySpecificChampionshipAsync(GetTeamsBySpecificChampionshipQuery query)
        {
            QueryDataSource<TeamEntity> queryDataSource = query.ToQueryDataSource<TeamEntity>();
            queryDataSource.AddFilter(new ExpressionFilterInfo<TeamEntity>(x=>x.ChampionshipId==query.ChampionshipId));
            ListQueryResult<TeamEntity> Result = await _teamRepository.GetAllTeamsAsync(queryDataSource);

            var finalResult = Result.MapTo<GetTeamsBySpecificChampionshipResult>();
            return finalResult;
        }

        public async Task<InsertTeamResult> InsertTeamAsync(InsertTeamRequest request)
        {
            TeamEntity teamEntity = request.MapTo<TeamEntity>();
            await _teamRepository.AddAsync(teamEntity);
            await _db.SaveChangesAsync();

            return new InsertTeamResult { Success = true };


        }

        public async Task<RemoveTeamFromChampionshipResult> RemoveTeamFromChampionshipAsync(RemoveTeamFromChampionshipRequest request)
        {
            var team = await _teamRepository.FirstOrDefaultAsync(x =>
                x.Key == request.TeamId && x.ChampionshipId == request.ChampionShipId);
            if (team.IsNull())
            {
                throw new ICDException("Not Found");
            }
            team.ChampionshipId = null;
            _teamRepository.Update(team);
            await _db.SaveChangesAsync();

            return new RemoveTeamFromChampionshipResult { Success = true };

        }
    }
}
