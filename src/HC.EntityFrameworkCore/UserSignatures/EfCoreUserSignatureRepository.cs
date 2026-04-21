using Volo.Abp.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using HC.EntityFrameworkCore;

namespace HC.UserSignatures;

public abstract class EfCoreUserSignatureRepositoryBase : EfCoreRepository<HCDbContext, UserSignature, Guid>
{
    public EfCoreUserSignatureRepositoryBase(IDbContextProvider<HCDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task DeleteAllAsync(string? filterText = null, string? signType = null, string? providerCode = null, string? tokenRef = null, string? signatureImage = null, DateTime? validFromMin = null, DateTime? validFromMax = null, DateTime? validToMin = null, DateTime? validToMax = null, bool? isActive = null, Guid? identityUserId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, signType, providerCode, tokenRef, signatureImage, validFromMin, validFromMax, validToMin, validToMax, isActive, identityUserId);
        var ids = query.Select(x => x.UserSignature.Id);
        await DeleteManyAsync(ids, cancellationToken: GetCancellationToken(cancellationToken));
    }

    public virtual async Task<UserSignatureWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // M2: reuse the LEFT JOIN query used by list-mode instead of issuing a correlated
        // `FirstOrDefault` subquery for IdentityUser inside `Select`. Also switches the
        // call path to async.
        var query = await GetQueryForNavigationPropertiesAsync();
        return await query
            .Where(x => x.UserSignature.Id == id)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<UserSignatureWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? signType = null, string? providerCode = null, string? tokenRef = null, string? signatureImage = null, DateTime? validFromMin = null, DateTime? validFromMax = null, DateTime? validToMin = null, DateTime? validToMax = null, bool? isActive = null, Guid? identityUserId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, signType, providerCode, tokenRef, signatureImage, validFromMin, validFromMax, validToMin, validToMax, isActive, identityUserId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? UserSignatureConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<UserSignatureWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return from userSignature in (await GetDbSetAsync())
               join identityUser in (await GetDbContextAsync()).Set<IdentityUser>() on userSignature.IdentityUserId equals identityUser.Id into identityUsers
               from identityUser in identityUsers.DefaultIfEmpty()
               select new UserSignatureWithNavigationProperties
               {
                   UserSignature = userSignature,
                   IdentityUser = identityUser
               };
    }

    protected virtual IQueryable<UserSignatureWithNavigationProperties> ApplyFilter(IQueryable<UserSignatureWithNavigationProperties> query, string? filterText, string? signType = null, string? providerCode = null, string? tokenRef = null, string? signatureImage = null, DateTime? validFromMin = null, DateTime? validFromMax = null, DateTime? validToMin = null, DateTime? validToMax = null, bool? isActive = null, Guid? identityUserId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.UserSignature.ProviderCode!.Contains(filterText!) || e.UserSignature.TokenRef!.Contains(filterText!) || e.UserSignature.SignatureImage!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(signType), e => e.UserSignature.SignType.Contains(signType)).WhereIf(!string.IsNullOrWhiteSpace(providerCode), e => e.UserSignature.ProviderCode.Contains(providerCode)).WhereIf(!string.IsNullOrWhiteSpace(tokenRef), e => e.UserSignature.TokenRef.Contains(tokenRef)).WhereIf(!string.IsNullOrWhiteSpace(signatureImage), e => e.UserSignature.SignatureImage.Contains(signatureImage)).WhereIf(validFromMin.HasValue, e => e.UserSignature.ValidFrom >= validFromMin!.Value).WhereIf(validFromMax.HasValue, e => e.UserSignature.ValidFrom <= validFromMax!.Value).WhereIf(validToMin.HasValue, e => e.UserSignature.ValidTo >= validToMin!.Value).WhereIf(validToMax.HasValue, e => e.UserSignature.ValidTo <= validToMax!.Value).WhereIf(isActive.HasValue, e => e.UserSignature.IsActive == isActive).WhereIf(identityUserId != null && identityUserId != Guid.Empty, e => e.IdentityUser != null && e.IdentityUser.Id == identityUserId);
    }

    public virtual async Task<List<UserSignature>> GetListAsync(string? filterText = null, string? signType = null, string? providerCode = null, string? tokenRef = null, string? signatureImage = null, DateTime? validFromMin = null, DateTime? validFromMax = null, DateTime? validToMin = null, DateTime? validToMax = null, bool? isActive = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, signType, providerCode, tokenRef, signatureImage, validFromMin, validFromMax, validToMin, validToMax, isActive);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? UserSignatureConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, string? signType = null, string? providerCode = null, string? tokenRef = null, string? signatureImage = null, DateTime? validFromMin = null, DateTime? validFromMax = null, DateTime? validToMin = null, DateTime? validToMax = null, bool? isActive = null, Guid? identityUserId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, signType, providerCode, tokenRef, signatureImage, validFromMin, validFromMax, validToMin, validToMax, isActive, identityUserId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<UserSignature> ApplyFilter(IQueryable<UserSignature> query, string? filterText = null, string? signType = null, string? providerCode = null, string? tokenRef = null, string? signatureImage = null, DateTime? validFromMin = null, DateTime? validFromMax = null, DateTime? validToMin = null, DateTime? validToMax = null, bool? isActive = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.ProviderCode!.Contains(filterText!) || e.TokenRef!.Contains(filterText!) || e.SignatureImage!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(signType), e => e.SignType.Contains(signType)).WhereIf(!string.IsNullOrWhiteSpace(providerCode), e => e.ProviderCode.Contains(providerCode)).WhereIf(!string.IsNullOrWhiteSpace(tokenRef), e => e.TokenRef.Contains(tokenRef)).WhereIf(!string.IsNullOrWhiteSpace(signatureImage), e => e.SignatureImage.Contains(signatureImage)).WhereIf(validFromMin.HasValue, e => e.ValidFrom >= validFromMin!.Value).WhereIf(validFromMax.HasValue, e => e.ValidFrom <= validFromMax!.Value).WhereIf(validToMin.HasValue, e => e.ValidTo >= validToMin!.Value).WhereIf(validToMax.HasValue, e => e.ValidTo <= validToMax!.Value).WhereIf(isActive.HasValue, e => e.IsActive == isActive);
    }
}