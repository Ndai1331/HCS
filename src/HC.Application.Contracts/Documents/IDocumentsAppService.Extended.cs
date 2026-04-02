using System;
using System.Threading.Tasks;
using HC.Shared;

namespace HC.Documents;

public partial interface IDocumentsAppService
{
    Task<LookupDto<Guid>?> GetUnitLookupByIdAsync(Guid id);
}