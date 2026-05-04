namespace Volo.Abp.Elsa;

public static class AbpElsaDbProperties
{
    public static string DbTablePrefix { get; set; } = "Elsa";

    public static string? DbSchema { get; set; } = null;

    public const string ConnectionStringName = "Elsa";
}
