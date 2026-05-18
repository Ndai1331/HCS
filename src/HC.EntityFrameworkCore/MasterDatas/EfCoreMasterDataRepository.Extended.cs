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

namespace HC.MasterDatas;

public class EfCoreMasterDataRepository : EfCoreMasterDataRepositoryBase, IMasterDataRepository
{
    public EfCoreMasterDataRepository(IDbContextProvider<HCDbContext> dbContextProvider) 
    : base(dbContextProvider)
    {
    }
}