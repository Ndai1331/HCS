using System.Collections.Generic;
using System.Linq;
using HC.Blazor.Pages;

namespace HC.Blazor.Components.DepartmentTreeSelect;

public static class DepartmentTreeSelectHelper
{
    public static void ExpandAllNodes(IEnumerable<DepartmentTreeView>? departments)
    {
        if (departments == null)
        {
            return;
        }

        foreach (var dept in departments)
        {
            dept.Collapsed = false;
            if (dept.Children != null && dept.Children.Any())
            {
                ExpandAllNodes(dept.Children);
            }
        }
    }
}
