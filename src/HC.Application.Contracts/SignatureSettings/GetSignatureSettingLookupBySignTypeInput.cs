using HC.Shared;

namespace HC.SignatureSettings;

public class GetSignatureSettingLookupBySignTypeInput : LookupRequestDto
{
    public string? DefaultSignType { get; set; }
}
