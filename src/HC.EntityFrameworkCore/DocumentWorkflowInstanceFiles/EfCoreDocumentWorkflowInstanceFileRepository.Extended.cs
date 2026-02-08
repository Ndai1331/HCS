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

namespace HC.DocumentWorkflowInstanceFiles;

public class EfCoreDocumentWorkflowInstanceFileRepository : EfCoreDocumentWorkflowInstanceFileRepositoryBase, IDocumentWorkflowInstanceFileRepository
{
    public EfCoreDocumentWorkflowInstanceFileRepository(IDbContextProvider<HCDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }
}