using HC.Documents;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using JetBrains.Annotations;
using Volo.Abp;

namespace HC.DocumentFiles;

public abstract class DocumentFileBase : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    [NotNull]
    public virtual string Name { get; set; }

    [CanBeNull]
    public virtual string? Path { get; set; }

    [CanBeNull]
    public virtual string? Hash { get; set; }

    public virtual bool IsSigned { get; set; }

    public virtual DateTime UploadedAt { get; set; }

    public Guid? DocumentId { get; set; }

    protected DocumentFileBase()
    {
    }

    public DocumentFileBase(Guid id, Guid? documentId, string name, bool isSigned, DateTime uploadedAt, string? path = null, string? hash = null)
    {
        Id = id;
        Check.NotNull(name, nameof(name));
        Name = name;
        IsSigned = isSigned;
        UploadedAt = uploadedAt;
        Path = path;
        Hash = hash;
        DocumentId = documentId;
    }
}