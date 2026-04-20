namespace HC.Blazor.Pages;

public partial class ProjectTasks
{
    // Fallback to first character when there's no avatar.
    protected string GetUserInitial(Volo.Abp.Identity.IdentityUserDto user)
    {
        var name = (user.Name ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name.Substring(0, 1).ToUpperInvariant();
        }

        var userName = (user.UserName ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(userName))
        {
            return userName.Substring(0, 1).ToUpperInvariant();
        }

        return "?";
    }

    protected string GetUserDisplayName(Volo.Abp.Identity.IdentityUserDto user)
    {
        var fullName = $"{user.Surname} {user.Name}".Trim();
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        return user.UserName ?? string.Empty;
    }
}