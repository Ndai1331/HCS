using HC.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.UserSignatures;
using Volo.Abp.Content;

namespace HC.Controllers.UserSignatures;

[RemoteService]
[Area("app")]
[ControllerName("UserSignature")]
[Route("api/app/user-signatures")]
public abstract class UserSignatureControllerBase : AbpController
{
    protected IUserSignaturesAppService _userSignaturesAppService;

    public UserSignatureControllerBase(IUserSignaturesAppService userSignaturesAppService)
    {
        _userSignaturesAppService = userSignaturesAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<UserSignatureWithNavigationPropertiesDto>> GetListAsync(GetUserSignaturesInput input)
    {
        return _userSignaturesAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<UserSignatureWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _userSignaturesAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<UserSignatureDto> GetAsync(Guid id)
    {
        return _userSignaturesAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("identity-user-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        return _userSignaturesAppService.GetIdentityUserLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<UserSignatureDto> CreateAsync(UserSignatureCreateDto input)
    {
        return _userSignaturesAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<UserSignatureDto> UpdateAsync(Guid id, UserSignatureUpdateDto input)
    {
        return _userSignaturesAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _userSignaturesAppService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("as-excel-file")]
    public virtual Task<IRemoteStreamContent> GetListAsExcelFileAsync(UserSignatureExcelDownloadDto input)
    {
        return _userSignaturesAppService.GetListAsExcelFileAsync(input);
    }

    [HttpGet]
    [Route("download-token")]
    public virtual Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        return _userSignaturesAppService.GetDownloadTokenAsync();
    }

    [HttpDelete]
    [Route("")]
    public virtual Task DeleteByIdsAsync(List<Guid> usersignatureIds)
    {
        return _userSignaturesAppService.DeleteByIdsAsync(usersignatureIds);
    }

    [HttpDelete]
    [Route("all")]
    public virtual Task DeleteAllAsync(GetUserSignaturesInput input)
    {
        return _userSignaturesAppService.DeleteAllAsync(input);
    }
}