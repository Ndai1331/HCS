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
using Microsoft.Extensions.Logging;

namespace HC.ProjectTasks;

public class EfCoreProjectTaskRepository : EfCoreProjectTaskRepositoryBase, IProjectTaskRepository
{
    private readonly ILogger<EfCoreProjectTaskRepositoryBase> _logger;
    public EfCoreProjectTaskRepository(IDbContextProvider<HCDbContext> dbContextProvider, ILogger<EfCoreProjectTaskRepositoryBase> logger) : base(dbContextProvider, logger)
    {
        _logger = logger;
    }
}