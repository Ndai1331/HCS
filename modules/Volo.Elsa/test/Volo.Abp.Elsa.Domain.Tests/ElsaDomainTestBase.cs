using Volo.Abp.Modularity;

namespace Volo.Abp.Elsa;

/* Inherit from this class for your domain layer tests.
 * See SampleManager_Tests for example.
 */
public abstract class ElsaDomainTestBase<TStartupModule> : ElsaTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
