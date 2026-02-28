using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.SignatureSettings;
using Volo.Abp.Content;
using HC.Shared;

namespace HC.Controllers.SignatureSettings;

[RemoteService]
[Area("app")]
[ControllerName("SignatureSetting")]
[Route("api/app/signature-settings")]
public abstract class SignatureSettingControllerBase : AbpController
{
    protected ISignatureSettingsAppService _signatureSettingsAppService;

    public SignatureSettingControllerBase(ISignatureSettingsAppService signatureSettingsAppService)
    {
        _signatureSettingsAppService = signatureSettingsAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<SignatureSettingDto>> GetListAsync(GetSignatureSettingsInput input)
    {
        return _signatureSettingsAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<SignatureSettingDto> GetAsync(Guid id)
    {
        return _signatureSettingsAppService.GetAsync(id);
    }

    [HttpPost]
    public virtual Task<SignatureSettingDto> CreateAsync(SignatureSettingCreateDto input)
    {
        return _signatureSettingsAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<SignatureSettingDto> UpdateAsync(Guid id, SignatureSettingUpdateDto input)
    {
        return _signatureSettingsAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _signatureSettingsAppService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("as-excel-file")]
    public virtual Task<IRemoteStreamContent> GetListAsExcelFileAsync(SignatureSettingExcelDownloadDto input)
    {
        return _signatureSettingsAppService.GetListAsExcelFileAsync(input);
    }

    [HttpGet]
    [Route("download-token")]
    public virtual Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        return _signatureSettingsAppService.GetDownloadTokenAsync();
    }

    [HttpDelete]
    [Route("")]
    public virtual Task DeleteByIdsAsync(List<Guid> signaturesettingIds)
    {
        return _signatureSettingsAppService.DeleteByIdsAsync(signaturesettingIds);
    }

    [HttpDelete]
    [Route("all")]
    public virtual Task DeleteAllAsync(GetSignatureSettingsInput input)
    {
        return _signatureSettingsAppService.DeleteAllAsync(input);
    }

    [HttpGet]
    [Route("lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetSignatureSettingLookupAsync(LookupRequestDto input)
    {
        return _signatureSettingsAppService.GetSignatureSettingLookupAsync(input);
    }

    [HttpGet]
    [Route("lookup-by-sign-type")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetSignatureSettingLookupBySignTypeAsync(GetSignatureSettingLookupBySignTypeInput input)
    {
        return _signatureSettingsAppService.GetSignatureSettingLookupBySignTypeAsync(input);
    }
}