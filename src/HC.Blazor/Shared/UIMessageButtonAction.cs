using Volo.Abp.AspNetCore.Components.Messages;
using Microsoft.Extensions.Localization;
namespace HC.Blazor.Shared;

public static class UiMessageServiceExtension
{
    public static UiMessageOptions OkButtonAction(this IStringLocalizer L) => new UiMessageOptions { OkButtonText = L["Ok"] };
    public static UiMessageOptions CancelButtonAction(this IStringLocalizer L) => new UiMessageOptions { CancelButtonText = L["Cancel"] };
    public static UiMessageOptions ConfirmButtonAction(this IStringLocalizer L) => new UiMessageOptions { ConfirmButtonText = L["Confirm"] };
}