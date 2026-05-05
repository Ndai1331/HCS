using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Data;

namespace HC.Reports;

public abstract class ReportManagerBase : DomainService
{
    protected IReportRepository _reportRepository;

    public ReportManagerBase(IReportRepository reportRepository)
    {
        _reportRepository = reportRepository;
    }

    public virtual async Task<Report> CreateAsync(string name, string url, int sortOrder, string? image = null)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.Length(name, nameof(name), ReportConsts.NameMaxLength);
        Check.NotNullOrWhiteSpace(url, nameof(url));
        Check.Length(url, nameof(url), ReportConsts.UrlMaxLength);
        Check.Length(image, nameof(image), ReportConsts.ImageMaxLength);
        var report = new Report(GuidGenerator.Create(), name, url, sortOrder, image);
        return await _reportRepository.InsertAsync(report);
    }

    public virtual async Task<Report> UpdateAsync(Guid id, string name, string url, int sortOrder, string? image = null, [CanBeNull] string? concurrencyStamp = null)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.Length(name, nameof(name), ReportConsts.NameMaxLength);
        Check.NotNullOrWhiteSpace(url, nameof(url));
        Check.Length(url, nameof(url), ReportConsts.UrlMaxLength);
        Check.Length(image, nameof(image), ReportConsts.ImageMaxLength);
        var report = await _reportRepository.GetAsync(id);
        report.Name = name;
        report.Url = url;
        report.SortOrder = sortOrder;
        report.Image = image;
        report.SetConcurrencyStampIfNotNull(concurrencyStamp);
        return await _reportRepository.UpdateAsync(report);
    }
}