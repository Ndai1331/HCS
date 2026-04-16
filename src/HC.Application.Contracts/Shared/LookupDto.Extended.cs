namespace HC.Shared;

public class LookupDto<TKey> : LookupDtoBase<TKey>
{
    /// <summary>Optional fields for rich UI (e.g. user picker with avatar).</summary>
    public string? UserName { get; set; }

    public string? Surname { get; set; }

    public string? Name { get; set; }

    public string? PhoneNumber { get; set; }
}