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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ICD.Base.BusinessService
{
    [Dependency(typeof(IGLNService))]
    public class GLNService : IGLNService
    {
        private readonly IUnitOfWork _db;
        private readonly IGLNRepository _glnRepository;
        private readonly IGLNLanguageRepository _glnLanguageRepository;
        private readonly IEntityService _entityService;

        public GLNService(IUnitOfWork db, IEntityService entityService)
        {
            _db = db;
            _glnRepository = _db.GetRepository<IGLNRepository>();
            _glnLanguageRepository = _db.GetRepository<IGLNLanguageRepository>();
            _entityService = entityService;
        }

        public async Task<DeleteTypeLongResult> DeleteGLNByIdAsync(DeleteTypeLongRequest request)
        {
            await _glnRepository.DeleteWithAsync(g => g.Key == request.Key);
            await _glnLanguageRepository.DeleteWithAsync(gl => gl.GLNRef == request.Key);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException e)
            {

                SqlErrorHandling.Handler(e);
            }

            return new DeleteTypeLongResult { Success = true };
        }

        public async Task<GetGLNResult> GetGLNAsync(GetGLNQuery query)
        {
            var result = new GetGLNResult
            {
                Entities = new List<GetGLNModel>()
            };

            var searchQuery = query.ToQueryDataSource<GLNView>();

            if (query.PersonRef.HasValue)
                searchQuery.AddFilter(new ExpressionFilterInfo<GLNView>(x => x.PersonRef == query.PersonRef));

            var glnResult = await _glnRepository.GetGLNAsync(searchQuery, query.LanguageRef);

            if (glnResult.Entities.Any())
            {
                result = glnResult.MapTo<GetGLNResult>();
            }

            return result;
        }

        public async Task<GetGLNByCompanyResult> GetGLNByCompanyAsync(GetGLNByCompanyQuery query)
        {
            var result = new GetGLNByCompanyResult
            {
                Entities = new List<GetGLNByCompanyModel>()
            };

            var searchQuery = query.ToQueryDataSource<GLNCoView>();

            searchQuery.AddFilter(new ExpressionFilterInfo<GLNCoView>(g => g.CompanyKey == query.CompanyRef));

            var glnResult = await _glnRepository.GetGLNsOfCompanyAsync(searchQuery, query.LanguageRef);

            if (glnResult.Entities.Any())
            {
                result = glnResult.MapTo<GetGLNByCompanyResult>();
            }

            return result;
        }

        public async Task<GetGLNByKeyResult> GetGLNByKeyAsync(GetGLNByKeyQuery query)
        {
            var result = new GetGLNByKeyResult
            {
                Entity = new GetGLNByKeyModel()
            };

            var searchQuery = query.ToQueryDataSource<GLNView>();

            searchQuery.AddFilter(new ExpressionFilterInfo<GLNView>(x => x.Key == query.Key));

            var glnResult = await _glnRepository.GetGLNAsync(searchQuery, query.LanguageRef);

            if (glnResult.Entities.Any())
            {
                result.Entity = glnResult.Entities.FirstOrDefault().MapTo<GetGLNByKeyModel>();
            }

            return result;
        }

        public async Task<InsertGLNResult> InsertGLNAsync(InsertGLNRequest request)
        {
            var result = new InsertGLNResult
            {
                Entity = new InsertGLNModel()
            };

            var glnEntity = request.MapTo<GLNEntity>();

            glnEntity.GLNLanguages = new List<GLNLanguageEntity>
            {
                new GLNLanguageEntity
                {
                    LanguageRef = request.LanguageRef,
                    _Address = request._Address,
                    _Title = request._Title
                }
            };

            await _glnRepository.AddAsync(glnEntity);

            try
            {
                await _db.SaveChangesAsync();

                await _entityService.InsertMultilingualEntityAsync<GLNEntity, InsertGLNRequest, long>(glnEntity, request);
            }
            catch (DbUpdateException e)
            {
                SqlErrorHandling.Handler(e, "DuplicateValue");
            }

            var searchQuery = new QueryDataSource<GLNView>();

            searchQuery.AddFilter(new ExpressionFilterInfo<GLNView>(x => x.Key == glnEntity.Key));

            var glnResult = await _glnRepository.GetGLNAsync(searchQuery, request.LanguageRef);

            result.Entity = glnResult.Entities.FirstOrDefault().MapTo<InsertGLNModel>();

            return result;
        }

        public async Task<InsertListGlnResult> InsertListGLNAsync(InsertListGlnRequest request)
        {
            if (!request.Details.Any())
            {
                await _glnRepository.DeleteWithAsync(x => x.PersonRef == request.PersonRef);

                await _db.SaveChangesAsync();

                return new InsertListGlnResult { Success = true };
            }

            var queryDataSource = new QueryDataSource<GLNView>();
            queryDataSource.AddFilter(new ExpressionFilterInfo<GLNView>(x => x.PersonRef == request.PersonRef));

            queryDataSource.DisablePaging = true;
            var existGlnRes = await _glnRepository.GetGLNAsync(queryDataSource, request.LanguageRef);

            List<GLNEntity> addedGlns = new List<GLNEntity>();

            if (!existGlnRes.Entities.Any())
            {
                foreach (var item in request.Details)
                {
                    var glnEntity = new GLNEntity
                    {
                        PersonRef = request.PersonRef,
                        GLN = item.GLN
                    };

                    glnEntity.GLNLanguages = new List<GLNLanguageEntity>
                    {
                        new GLNLanguageEntity
                        {
                            LanguageRef = request.LanguageRef,
                            _Title = item._Title,
                            _Address =  string.IsNullOrEmpty( item._Address) ? "" : item._Address
                        }
                    };

                    await _glnRepository.AddAsync(glnEntity);

                    addedGlns.Add(glnEntity);
                }

                try
                {
                    await _db.SaveChangesAsync();

                    foreach (var newgln in addedGlns)
                    {

                        await _entityService.InsertMultilingualEntityAsync<GLNEntity, object, long>(newgln, newgln.GLNLanguages.First(), request.LanguageRef);
                    }
                }
                catch (Exception e)
                {

                    SqlErrorHandling.Handler(e);
                }

                return new InsertListGlnResult { Success = true };
            }

            var newGlns = request.Details.Select(x => x.GLN).Distinct().ToList();
            var deleteGlns = existGlnRes.Entities.Where(x => !newGlns.Contains(x.GLN));

            foreach (var item in request.Details)
            {
                var existGln = existGlnRes.Entities.FirstOrDefault(x => x.GLN == item.GLN);
                if (existGln.IsNull())
                {
                    var glnEntity = new GLNEntity
                    {
                        PersonRef = request.PersonRef,
                        GLN = item.GLN
                    };

                    glnEntity.GLNLanguages = new List<GLNLanguageEntity>
                    {
                        new GLNLanguageEntity
                        {
                            LanguageRef = request.LanguageRef,
                            _Title = item._Title,
                            _Address = string.IsNullOrEmpty( item._Address) ? "" : item._Address
                        }
                    };

                    await _glnRepository.AddAsync(glnEntity);

                    addedGlns.Add(glnEntity);
                }
            }

            if (deleteGlns.Any())
            {
                var glnKeys = deleteGlns.Select(x => x.Key).ToList();

                _glnRepository.DeleteRangeByIds(glnKeys);
            }

            try
            {
                await _db.SaveChangesAsync();

                if (addedGlns.IsNotNull() && addedGlns.Any())
                    foreach (var newgln in addedGlns)
                    {
                        await _entityService.InsertMultilingualEntityAsync<GLNEntity, object, long>(newgln, newgln.GLNLanguages.First(), request.LanguageRef);
                    }
            }
            catch (Exception e)
            {

                SqlErrorHandling.Handler(e);
            }

            return new InsertListGlnResult { Success = true };

        }

        public async Task<InsertListLegalGLNResult> InsertListLegalGLNAsync(InsertListLegalGLNRequest request)
        {
            if (request.InsertListLegalGLNs.IsNull() || !request.InsertListLegalGLNs.Any())
                throw new ICDException("EmptyDetail");

            var personRefs = request.InsertListLegalGLNs.Select(x => x.PersonRef).Distinct().ToList();

            var queryDataSource = new QueryDataSource<GLNView>();
            queryDataSource.AddFilter(new ExpressionFilterInfo<GLNView>(x => personRefs.Contains(x.PersonRef)));

            queryDataSource.DisablePaging = true;
            var existGlnRes = await _glnRepository.GetGLNAsync(queryDataSource, request.LanguageRef);

            List<GLNEntity> addedGlns = new List<GLNEntity>();

            foreach (var req in request.InsertListLegalGLNs)
            {
                if (!req.Details.Any())
                {
                    await _glnRepository.DeleteWithAsync(x => x.PersonRef == req.PersonRef);

                    continue;
                }

                var exGlns = existGlnRes.Entities.Where(x => x.PersonRef == req.PersonRef);

                if (!exGlns.Any())
                {
                    foreach (var det in req.Details)
                    {
                        var glnEntity = new GLNEntity
                        {
                            PersonRef = req.PersonRef,
                            GLN = det.GLN
                        };

                        glnEntity.GLNLanguages = new List<GLNLanguageEntity>
                        {
                            new GLNLanguageEntity
                            {
                            LanguageRef = request.LanguageRef,
                            _Title = det._Title,
                            _Address =  string.IsNullOrEmpty( det._Address) ? "" : det._Address
                            }
                        };

                        await _glnRepository.AddAsync(glnEntity);

                        addedGlns.Add(glnEntity);
                    }
                }
                else
                {
                    var newGlns = req.Details.Select(x => x.GLN).Distinct().ToList();
                    var deleteGlns = exGlns.Where(x => !newGlns.Contains(x.GLN));

                    foreach (var det in req.Details)
                    {
                        var exgln = exGlns.FirstOrDefault(x => x.GLN == det.GLN);
                        if (exgln.IsNull())
                        {
                            var glnEntity = new GLNEntity
                            {
                                PersonRef = req.PersonRef,
                                GLN = det.GLN
                            };

                            glnEntity.GLNLanguages = new List<GLNLanguageEntity>
                            {
                                new GLNLanguageEntity
                                {
                                LanguageRef = request.LanguageRef,
                                _Title = det._Title,
                                _Address =  string.IsNullOrEmpty( det._Address) ? "" : det._Address
                                }
                            };

                            await _glnRepository.AddAsync(glnEntity);

                            addedGlns.Add(glnEntity);
                        }
                    }

                    if (deleteGlns.Any())
                    {
                        var glnKeys = deleteGlns.Select(x => x.Key);

                        _glnRepository.DeleteRangeByIds(glnKeys);
                    }
                }
            }

            try
            {
                await _db.SaveChangesAsync();

                foreach (var newgln in addedGlns)
                {

                    await _entityService.InsertMultilingualEntityAsync<GLNEntity, object, long>(newgln, newgln.GLNLanguages.First(), request.LanguageRef);
                }
            }
            catch (Exception e)
            {

                SqlErrorHandling.Handler(e);
            }

            return new InsertListLegalGLNResult { Success = true };
        }

        public async Task<BaseGLNResult> UpdateGLNAsync(UpdateGLNRequest request)
        {
            var result = new BaseGLNResult();

            var glnEntity = await _glnRepository.FindAsync(request.Key);

            if (glnEntity.IsNull())
                throw new ICDException("NotFound");

            if (glnEntity != null)
            {
                glnEntity = request.MapTo<GLNEntity>();
                glnEntity.Key = request.Key;
            }

            var glnLanguageEntity = await _glnLanguageRepository.FirstOrDefaultAsync(gl => gl.GLNRef == request.Key && gl.LanguageRef == request.LanguageRef);
            if (glnLanguageEntity != null)
            {
                var key = glnLanguageEntity.Key;
                glnLanguageEntity = request.MapTo<GLNLanguageEntity>();
                glnLanguageEntity.GLNRef = request.Key;
                glnLanguageEntity.Key = key;
            }

            if (glnEntity != null && glnLanguageEntity != null)
            {
                _glnRepository.Update(glnEntity);
                _glnLanguageRepository.Update(glnLanguageEntity);
                await _db.SaveChangesAsync();

                result.Success = true;
            }
            else
                result.Success = false;

            return result;
        }
    }
}
