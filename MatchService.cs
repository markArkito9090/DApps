using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ICD.Base.BusinessServiceContract;
using ICD.Base.Domain.Entity;
using ICD.Base.Domain.View;
using ICD.Base.Match.Queries;
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
    [Dependency(typeof(IMatchService))]
    public class MatchService : IMatchService
    {
        private readonly IUnitOfWork _db;
        private readonly IMatchRepository _matchRepository;
        private readonly IChampionshipRepository _championshipRepository;
        private readonly ITeamRepository _teamRepository;

        public MatchService(IUnitOfWork db)
        {
            _db = db;
            _matchRepository = _db.GetRepository<IMatchRepository>();
            _championshipRepository = _db.GetRepository<IChampionshipRepository>();
            _teamRepository = _db.GetRepository<ITeamRepository>();

        }


        public async Task<GetUpComingMatchinChampionshipResult> GetUpcomingMatchesinChampionship(GetUpComingMatchinChampionshipQuery query)
        {

            var finalResult = new GetUpComingMatchinChampionshipResult
            {
                Entities = new List<GetUpComingMatchinChampionshipModel>(),
            };

            QueryDataSource<MatchView> queryDataSource = query.ToQueryDataSource<MatchView>();
            queryDataSource.AddFilter(new ExpressionFilterInfo<MatchView>(x => x.Key == query.Key));
            queryDataSource.AddFilter(new ExpressionFilterInfo<MatchView>(x => x.Date >= DateTime.Now));
            ListQueryResult<MatchView> Result = await _matchRepository.GetTeamNameInMatchAsync(queryDataSource);


            foreach (var item in Result.Entities)
            {


                var newResult = item.MapTo<GetUpComingMatchinChampionshipModel>();

                finalResult.Entities.Add(newResult);


            }

            return finalResult;

        }


        public async Task<InsertMatchResult> InsertMatchAsync(InsertMatchRequest request)
        {
            var match = request.MapTo<MatchEntity>();

            await _matchRepository.AddAsync(match);
            await _db.SaveChangesAsync();

            return new InsertMatchResult { Success = true };

        }

        public async Task<UpdateMatchResult> UpdateMatchAsync(UpdateMatchRequest request)
        {
            var match = await _matchRepository.FirstOrDefaultAsync(x => x.Key == request.Key);
            if (match.IsNull())
                throw new ICDException("NotFound");

            //match = request.MapTo<MatchEntity>();
            match.AwayTeamScore = request.AwayTeamScore;
            match.HomeTeamScore = request.HomeTeamScore;

            _matchRepository.Update(match);
            await _db.SaveChangesAsync();
            return new UpdateMatchResult { Success = true };
        }
    }
}
