using System.Linq;
using System.Threading.Tasks;
using HC.Blazor.Components.Pages;
using Serilog;
using Volo.Abp.LeptonX.Shared;
using Volo.Abp.AspNetCore.Components.Web.Theming.Toolbars;

namespace HC.Blazor.Toolbars;

public class HCToolbarContributor : IToolbarContributor
{
    public Task ConfigureToolbarAsync(IToolbarConfigurationContext context)
    {
        // NOTE: This runs on the server. Logs appear in server output, not browser console.
        Log.Information("[HCToolbarContributor] ConfigureToolbarAsync called. ToolbarName={ToolbarName} ItemsCount={ItemsCount}",
            context.Toolbar.Name,
            context.Toolbar.Items.Count);

        var isMainToolbar =
            context.Toolbar.Name == "Main";

        // Remove ABP Chat toolbar item to avoid startup 500 when Chat hub endpoint
        // is not mapped in the current deployment topology.
        var chatToolbarItems = context.Toolbar.Items
            .Where(x => x.ComponentType?.FullName == "Volo.Chat.Blazor.Components.MessagesToolbarItem")
            .ToList();
        foreach (var chatToolbarItem in chatToolbarItems)
        {
            context.Toolbar.Items.Remove(chatToolbarItem);
        }
        var removedChatToolbarCount = chatToolbarItems.Count;

        // Add bell once, only on LeptonX main toolbars (desktop/mobile).
        var inserted = false;
        var insertIndex = -1;
        if (isMainToolbar && !context.Toolbar.Items.Any(x => x.ComponentType == typeof(Notification)))
        {
            // Place the bell right after the first built-in item (Appearance) in LeptonX TopMenu.
            // If theme order changes, we still keep it near the start.
            insertIndex = context.Toolbar.Items.Count == 0 ? 0 : 1;
            context.Toolbar.Items.Insert(insertIndex, new ToolbarItem(typeof(Notification)));
            inserted = true;
        }

        // Helpful debug: list item component types to verify ordering.
        Log.Information("[HCToolbarContributor] ToolbarName={ToolbarName} IsMain={IsMain} RemovedChatToolbarCount={RemovedChatToolbarCount} InsertedNotification={Inserted} InsertIndex={InsertIndex} ItemsCountAfter={ItemsCountAfter} Items={Items}",
            context.Toolbar.Name,
            isMainToolbar,
            removedChatToolbarCount,
            inserted,
            insertIndex,
            context.Toolbar.Items.Count,
            string.Join(" | ", context.Toolbar.Items.Select((x, i) => $"{i}:{x.ComponentType?.Name}")));

        return Task.CompletedTask;
    }
}