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

namespace HC.DocumentWorkflowInstanceLogss;

public class EfCoreDocumentWorkflowInstanceLogsRepository : EfCoreDocumentWorkflowInstanceLogsRepositoryBase, IDocumentWorkflowInstanceLogsRepository
{
    public EfCoreDocumentWorkflowInstanceLogsRepository(IDbContextProvider<HCDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }
}