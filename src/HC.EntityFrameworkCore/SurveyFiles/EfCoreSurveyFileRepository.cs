using HC.SurveySessions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using HC.EntityFrameworkCore;

namespace HC.SurveyFiles;

public abstract class EfCoreSurveyFileRepositoryBase : EfCoreRepository<HCDbContext, SurveyFile, Guid>
{
    public EfCoreSurveyFileRepositoryBase(IDbContextProvider<HCDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task DeleteAllAsync(string? filterText = null, string? uploaderType = null, string? fileName = null, string? filePath = null, int? fileSizeMin = null, int? fileSizeMax = null, string? mimeType = null, string? fileType = null, Guid? surveySessionId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, uploaderType, fileName, filePath, fileSizeMin, fileSizeMax, mimeType, fileType, surveySessionId);
        var ids = query.Select(x => x.SurveyFile.Id);
        await DeleteManyAsync(ids, cancellationToken: GetCancellationToken(cancellationToken));
    }

    public virtual async Task<SurveyFileWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        return await query
            .Where(x => x.SurveyFile.Id == id)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<SurveyFileWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? uploaderType = null, string? fileName = null, string? filePath = null, int? fileSizeMin = null, int? fileSizeMax = null, string? mimeType = null, string? fileType = null, Guid? surveySessionId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, uploaderType, fileName, filePath, fileSizeMin, fileSizeMax, mimeType, fileType, surveySessionId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? SurveyFileConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<SurveyFileWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return from surveyFile in (await GetDbSetAsync())
               join surveySession in (await GetDbContextAsync()).Set<SurveySession>() on surveyFile.SurveySessionId equals surveySession.Id into surveySessions
               from surveySession in surveySessions.DefaultIfEmpty()
               select new SurveyFileWithNavigationProperties
               {
                   SurveyFile = surveyFile,
                   SurveySession = surveySession
               };
    }

    protected virtual IQueryable<SurveyFileWithNavigationProperties> ApplyFilter(IQueryable<SurveyFileWithNavigationProperties> query, string? filterText, string? uploaderType = null, string? fileName = null, string? filePath = null, int? fileSizeMin = null, int? fileSizeMax = null, string? mimeType = null, string? fileType = null, Guid? surveySessionId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.SurveyFile.UploaderType!.Contains(filterText!) || e.SurveyFile.FileName!.Contains(filterText!) || e.SurveyFile.FilePath!.Contains(filterText!) || e.SurveyFile.MimeType!.Contains(filterText!) || e.SurveyFile.FileType!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(uploaderType), e => e.SurveyFile.UploaderType.Contains(uploaderType)).WhereIf(!string.IsNullOrWhiteSpace(fileName), e => e.SurveyFile.FileName.Contains(fileName)).WhereIf(!string.IsNullOrWhiteSpace(filePath), e => e.SurveyFile.FilePath.Contains(filePath)).WhereIf(fileSizeMin.HasValue, e => e.SurveyFile.FileSize >= fileSizeMin!.Value).WhereIf(fileSizeMax.HasValue, e => e.SurveyFile.FileSize <= fileSizeMax!.Value).WhereIf(!string.IsNullOrWhiteSpace(mimeType), e => e.SurveyFile.MimeType.Contains(mimeType)).WhereIf(!string.IsNullOrWhiteSpace(fileType), e => e.SurveyFile.FileType.Contains(fileType)).WhereIf(surveySessionId != null && surveySessionId != Guid.Empty, e => e.SurveySession != null && e.SurveySession.Id == surveySessionId);
    }

    public virtual async Task<List<SurveyFile>> GetListAsync(string? filterText = null, string? uploaderType = null, string? fileName = null, string? filePath = null, int? fileSizeMin = null, int? fileSizeMax = null, string? mimeType = null, string? fileType = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, uploaderType, fileName, filePath, fileSizeMin, fileSizeMax, mimeType, fileType);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? SurveyFileConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, string? uploaderType = null, string? fileName = null, string? filePath = null, int? fileSizeMin = null, int? fileSizeMax = null, string? mimeType = null, string? fileType = null, Guid? surveySessionId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, uploaderType, fileName, filePath, fileSizeMin, fileSizeMax, mimeType, fileType, surveySessionId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<SurveyFile> ApplyFilter(IQueryable<SurveyFile> query, string? filterText = null, string? uploaderType = null, string? fileName = null, string? filePath = null, int? fileSizeMin = null, int? fileSizeMax = null, string? mimeType = null, string? fileType = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.UploaderType!.Contains(filterText!) || e.FileName!.Contains(filterText!) || e.FilePath!.Contains(filterText!) || e.MimeType!.Contains(filterText!) || e.FileType!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(uploaderType), e => e.UploaderType.Contains(uploaderType)).WhereIf(!string.IsNullOrWhiteSpace(fileName), e => e.FileName.Contains(fileName)).WhereIf(!string.IsNullOrWhiteSpace(filePath), e => e.FilePath.Contains(filePath)).WhereIf(fileSizeMin.HasValue, e => e.FileSize >= fileSizeMin!.Value).WhereIf(fileSizeMax.HasValue, e => e.FileSize <= fileSizeMax!.Value).WhereIf(!string.IsNullOrWhiteSpace(mimeType), e => e.MimeType.Contains(mimeType)).WhereIf(!string.IsNullOrWhiteSpace(fileType), e => e.FileType.Contains(fileType));
    }
}