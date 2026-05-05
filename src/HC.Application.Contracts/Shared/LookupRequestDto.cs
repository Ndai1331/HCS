using Volo.Abp.Application.Dtos;

namespace HC.Shared;

public abstract class LookupRequestDtoBase : PagedResultRequestDto
{
    public string? Filter { get; set; }

    public LookupRequestDtoBase()
    {
        MaxResultCount = MaxMaxResultCount;
    }
}