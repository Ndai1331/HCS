using Volo.Abp.EntityFrameworkCore;
using HC.EntityFrameworkCore;

namespace HC.SurveyResults;

public class EfCoreSurveyResultRepository : EfCoreSurveyResultRepositoryBase, ISurveyResultRepository
{
    public EfCoreSurveyResultRepository(IDbContextProvider<HCDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }
}