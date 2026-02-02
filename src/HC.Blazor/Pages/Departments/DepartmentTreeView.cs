using System.Collections.Generic;
using System.Linq;
using HC.Departments;

namespace HC.Blazor.Pages;

public class DepartmentTreeView : DepartmentDto
{
    public bool Checked { get; set; } = false;
    // HasChildren: true when there are entities with ParentId = this.Id
    public bool HasChildren => Children?.Any() ?? false;
        
    // Children: list of entities where ParentId = this.Id
    public List<DepartmentTreeView> Children { get; set; }

    public bool Collapsed { get; set; } = false; // Default expanded

    public string Icon => Collapsed ? "fa-angle-right" : "fa-angle-down";
    
    // Tree level for display (0 = root, 1 = first child, etc.)
    public int TreeLevel { get; set; } = 0;

    public DepartmentTreeView()
    {
        Children = new List<DepartmentTreeView>();
    }
}

