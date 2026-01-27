using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace HC.Blazor.Shared;

public static class HasRoleHelper
{
    public static async Task<bool> HasRoleAsync(this IAuthorizationService AuthorizationService, string role)
    {
        return await AuthorizationService.IsGrantedAsync(role);
    }
}