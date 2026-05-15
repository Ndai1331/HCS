using HC.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.UserDepartments;
using Volo.Abp.Content;

namespace HC.Controllers.UserDepartments;

[RemoteService]
[Area("app")]
[ControllerName("UserDepartment")]
[Route("api/app/user-departments")]
public abstract class UserDepartmentControllerBase : AbpController
{
    protected IUserDepartmentsAppService _userDepartmentsAppService;

    public UserDepartmentControllerBase(IUserDepartmentsAppService userDepartmentsAppService)
    {
        _userDepartmentsAppService = userDepartmentsAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<UserDepartmentWithNavigationPropertiesDto>> GetListAsync(GetUserDepartmentsInput input)
    {
        return _userDepartmentsAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<UserDepartmentWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _userDepartmentsAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<UserDepartmentDto> GetAsync(Guid id)
    {
        return _userDepartmentsAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("department-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetDepartmentLookupAsync(LookupRequestDto input)
    {
        return _userDepartmentsAppService.GetDepartmentLookupAsync(input);
    }

    [HttpGet]
    [Route("identity-user-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        return _userDepartmentsAppService.GetIdentityUserLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<UserDepartmentDto> CreateAsync(UserDepartmentCreateDto input)
    {
        return _userDepartmentsAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<UserDepartmentDto> UpdateAsync(Guid id, UserDepartmentUpdateDto input)
    {
        return _userDepartmentsAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _userDepartmentsAppService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("as-excel-file")]
    public virtual Task<IRemoteStreamContent> GetListAsExcelFileAsync(UserDepartmentExcelDownloadDto input)
    {
        return _userDepartmentsAppService.GetListAsExcelFileAsync(input);
    }

    [HttpGet]
    [Route("download-token")]
    public virtual Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        return _userDepartmentsAppService.GetDownloadTokenAsync();
    }

    [HttpDelete]
    [Route("")]
    public virtual Task DeleteByIdsAsync(List<Guid> userdepartmentIds)
    {
        return _userDepartmentsAppService.DeleteByIdsAsync(userdepartmentIds);
    }

    [HttpDelete]
    [Route("all")]
    public virtual Task DeleteAllAsync(GetUserDepartmentsInput input)
    {
        return _userDepartmentsAppService.DeleteAllAsync(input);
    }
}