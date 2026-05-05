using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using JetBrains.Annotations;
using Volo.Abp;

namespace HC.Reports;

public abstract class ReportBase : FullAuditedAggregateRoot<Guid>
{
    [NotNull]
    public virtual string Name { get; set; }

    [NotNull]
    public virtual string Url { get; set; }

    public virtual int SortOrder { get; set; }

    [CanBeNull]
    public virtual string? Image { get; set; }

    protected ReportBase()
    {
    }

    public ReportBase(Guid id, string name, string url, int sortOrder, string? image = null)
    {
        Id = id;
        Check.NotNull(name, nameof(name));
        Check.Length(name, nameof(name), ReportConsts.NameMaxLength, 0);
        Check.NotNull(url, nameof(url));
        Check.Length(url, nameof(url), ReportConsts.UrlMaxLength, 0);
        Check.Length(image, nameof(image), ReportConsts.ImageMaxLength, 0);
        Name = name;
        Url = url;
        SortOrder = sortOrder;
        Image = image;
    }
}