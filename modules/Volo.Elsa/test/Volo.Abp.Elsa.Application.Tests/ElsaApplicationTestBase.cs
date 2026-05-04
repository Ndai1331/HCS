using Volo.Abp.Modularity;

namespace Volo.Abp.Elsa;

/* Inherit from this class for your application layer tests.
 * See SampleAppService_Tests for example.
 */
public abstract class ElsaApplicationTestBase<TStartupModule> : ElsaTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
