using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Blazorise;
using Blazorise.DataGrid;
using Volo.Abp.BlazoriseUI.Components;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Components.Web.Theming.PageToolbars;
using HC.Departments;
using HC.Permissions;
using HC.Shared;
using System.Threading;

namespace HC.Blazor.Pages;

public partial class Departments
{
    private List<DepartmentTreeView> DepartmentTreeViews { get; set; }
    private IReadOnlyList<DepartmentDto> DepartmentList { get; set; }
    private List<DepartmentTreeView> AllDepartmentsFlat { get; set; }
    private List<DepartmentTreeView> AllDepartmentsForSelect2 { get; set; }
    
    private DepartmentTreeView SelectedDepartment { get; set; } = new();
    
    // Properties for binding (fields are disabled, so setters are no-op)
    private string ParentDepartmentName
    {
        get => GetParentDepartmentName();
        set { } // No-op setter for binding (field is disabled)
    }
    
    private string SelectedDepartmentLeaderUser
    {
        get => GetLeaderUserName();
        set { } // No-op setter for binding (field is disabled)
    }
    private string SelectedDepartmentCode
    {
        get => SelectedDepartment?.Code ?? "";
        set { } // No-op setter for binding (field is disabled)
    }
    
    private string SelectedDepartmentName
    {
        get => SelectedDepartment?.Name ?? "";
        set { } // No-op setter for binding (field is disabled)
    }
    
    private int SelectedDepartmentLevel
    {
        get => SelectedDepartment?.Level ?? 0;
        set { } // No-op setter for binding (field is disabled)
    }
    
    private int SelectedDepartmentSortOrder
    {
        get => SelectedDepartment?.SortOrder ?? 0;
        set { } // No-op setter for binding (field is disabled)
    }
    private Modal CreateDepartmentModal { get; set; } = new();
    private DepartmentCreateDto NewDepartment { get; set; }
    private Modal EditDepartmentModal { get; set; } = new();
    private DepartmentUpdateDto EditingDepartment { get; set; }
        
    private DepartmentTreeView? NewParentDepartment { get; set; }
    private DepartmentTreeView? MovingDepartment { get; set; }
    private List<DepartmentTreeView>? MovableDepartmentTree { get; set; }
    private Modal MoveDepartmentModal { get; set; } = new();
    private Validations NewDepartmentValidations { get; set; } = new();
    private Validations EditingDepartmentValidations { get; set; } = new();
    private Guid EditingDepartmentId { get; set; }

    private IReadOnlyList<LookupDto<Guid>> IdentityUsersCollection { get; set; } = new List<LookupDto<Guid>>();
    private List<LookupDto<Guid>> SelectedLeaderUser { get; set; } = new();

    private bool CanCreateDepartment { get; set; }
    private bool CanEditDepartment { get; set; }
    private bool CanDeleteDepartment { get; set; }
    
    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems;
    protected PageToolbar Toolbar { get; } = new PageToolbar();

    public Departments()
    {
        DepartmentList = new List<DepartmentDto>();
        NewDepartment = new DepartmentCreateDto();
        EditingDepartment = new DepartmentUpdateDto();
        SelectedDepartment = new DepartmentTreeView();
        DepartmentTreeViews = new List<DepartmentTreeView>();
        AllDepartmentsFlat = new List<DepartmentTreeView>();
        AllDepartmentsForSelect2 = new List<DepartmentTreeView>();
        BreadcrumbItems = new List<Volo.Abp.BlazoriseUI.BreadcrumbItem>();
    }

    protected override async Task OnInitializedAsync()
    {
        await SetPermissionsAsync();
        await GetDepartmentsAsync();
        await GetIdentityUserCollectionLookupAsync();

        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["Departments"]));
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await SetBreadcrumbItemsAsync();
            await SetToolbarItemsAsync();
            await InvokeAsync(StateHasChanged);
        }
    }

    protected virtual ValueTask SetBreadcrumbItemsAsync()
    {
        return ValueTask.CompletedTask;
    }

    protected virtual ValueTask SetToolbarItemsAsync()
    {
        Toolbar.AddButton(L["NewDepartment"], async () => {
            await OpenCreateDepartmentModal();
        }, IconName.Add, requiredPolicyName: HCPermissions.Departments.Create);
        return ValueTask.CompletedTask;
    }

    private async Task SetPermissionsAsync()
    {
        CanCreateDepartment = await AuthorizationService
            .IsGrantedAsync(HCPermissions.Departments.Create);
            
        CanEditDepartment = await AuthorizationService
            .IsGrantedAsync(HCPermissions.Departments.Edit);
            
        CanDeleteDepartment = await AuthorizationService
            .IsGrantedAsync(HCPermissions.Departments.Delete);
    }

    private async Task GetDepartmentsAsync()
    {
        var result = await DepartmentsAppService.GetListAsync(new GetDepartmentsInput
        {
            MaxResultCount = LimitedResultRequestDto.MaxMaxResultCount
        });

        DepartmentList = result.Items.Select(x => x.Department).ToList();

        // Map each DepartmentDto to DepartmentTreeView manually
        var departments = DepartmentList.Select(d => ObjectMapper.Map<DepartmentDto, DepartmentTreeView>(d)).ToList();

        var departmentsDictionary = new Dictionary<string, List<DepartmentTreeView>>();

        // Build dictionary: key = ParentId, value = list of children
        foreach (var department in departments)
        {
            var parentId = department.ParentId ?? string.Empty;

            if (!departmentsDictionary.ContainsKey(parentId))
            {
                departmentsDictionary.Add(parentId, new List<DepartmentTreeView>());
            }

            departmentsDictionary[parentId].Add(department);
        }

        // Set Children for each department: Children = entities where ParentId = this.Id
        foreach (var department in departments)
        {
            var departmentId = department.Id.ToString();
            if (departmentsDictionary.ContainsKey(departmentId))
            {
                department.Children = departmentsDictionary[departmentId];
            }
            else
            {
                department.Children = new List<DepartmentTreeView>();
            }
        }

        if (departmentsDictionary.Any())
        {
            DepartmentTreeViews = departmentsDictionary.ContainsKey(string.Empty) 
                ? departmentsDictionary[string.Empty] 
                : new List<DepartmentTreeView>();
        }
        else
        {
            DepartmentTreeViews = new List<DepartmentTreeView>();
        }

        // Build flat list for dropdown (flatten tree structure)
        AllDepartmentsFlat = FlattenDepartments(DepartmentTreeViews);
        
        // Expand all nodes by default
        ExpandAllNodes(DepartmentTreeViews);
        
        // Create list for Select2 (include root option)
        AllDepartmentsForSelect2 = new List<DepartmentTreeView>();
        // Add root option
        AllDepartmentsForSelect2.Add(new DepartmentTreeView 
        { 
            Id = Guid.Empty, 
            Name = L["Root"].Value,
            TreeLevel = -1 // Special level for root
        });
        // Add all departments
        AllDepartmentsForSelect2.AddRange(AllDepartmentsFlat);
    }
    
    // Helper method to expand all nodes recursively
    private void ExpandAllNodes(List<DepartmentTreeView> departments)
    {
        if (departments == null) return;
        
        foreach (var dept in departments)
        {
            dept.Collapsed = false; // Expand this node
            if (dept.Children != null && dept.Children.Any())
            {
                ExpandAllNodes(dept.Children); // Recursively expand children
            }
        }
    }
    
    // Helper method to flatten tree for dropdown with level tracking
    private List<DepartmentTreeView> FlattenDepartments(List<DepartmentTreeView> departments, int treeLevel = 0)
    {
        var result = new List<DepartmentTreeView>();
        foreach (var dept in departments)
        {
            // Set tree level for display (don't modify the actual Level property)
            dept.TreeLevel = treeLevel;
            result.Add(dept);
            if (dept.Children != null && dept.Children.Any())
            {
                result.AddRange(FlattenDepartments(dept.Children, treeLevel + 1));
            }
        }
        return result;
    }
    
    // Format department name with dashes based on tree level
    private string GetDepartmentDisplayName(DepartmentTreeView department)
    {
        if (department == null || string.IsNullOrEmpty(department.Name))
            return "";
        
        // Special handling for root option
        if (department.Id == Guid.Empty)
            return department.Name;
            
        // TreeLevel 0 = root level, no dash
        // TreeLevel 1 = one dash, TreeLevel 2 = two dashes, etc.
        var dashes = new string('-', department.TreeLevel);
        return string.IsNullOrEmpty(dashes) ? department.Name : $"{dashes} {department.Name}";
    }
    
    // Get department by ID for Select2
    private Task<DepartmentTreeView> GetDepartmentByIdAsync(List<DepartmentTreeView> items, string id, CancellationToken token)
    {
        if (items == null || items.Count == 0)
        {
            return Task.FromResult(new DepartmentTreeView 
            { 
                Id = Guid.Empty, 
                Name = L["Root"].Value,
                TreeLevel = -1
            });
        }
        
        if (string.IsNullOrEmpty(id) || id == "null")
        {
            var root = items.FirstOrDefault(x => x.Id == Guid.Empty);
            return Task.FromResult(root ?? new DepartmentTreeView 
            { 
                Id = Guid.Empty, 
                Name = L["Root"].Value,
                TreeLevel = -1
            });
        }
            
        if (Guid.TryParse(id, out var guidId))
        {
            var department = items.FirstOrDefault(x => x.Id == guidId);
            if (department != null)
            {
                return Task.FromResult(department);
            }
        }
        
        // Return root option as fallback
        return Task.FromResult(new DepartmentTreeView 
        { 
            Id = Guid.Empty, 
            Name = L["Root"].Value,
            TreeLevel = -1
        });
    }

    private string GetParentDepartmentName()
    {
        if (SelectedDepartment == null || string.IsNullOrEmpty(SelectedDepartment.ParentId))
        {
            return L["Root"].Value;
        }

        if (Guid.TryParse(SelectedDepartment.ParentId, out var parentId))
        {
            // Try to find in AllDepartmentsFlat first (flattened list)
            var parent = AllDepartmentsFlat?.FirstOrDefault(d => d.Id == parentId);
            if (parent != null)
            {
                return parent.Name ?? "";
            }
            
            // If not found, try to find in DepartmentList
            var parentDto = DepartmentList?.FirstOrDefault(d => d.Id == parentId);
            if (parentDto != null)
            {
                return parentDto.Name ?? "";
            }
        }

        return "";
    }

    private string GetLeaderUserName()
    {
        if (SelectedDepartment == null || SelectedDepartment.LeaderUserId == null)
        {
            return "";
        }

        if (Guid.TryParse(SelectedDepartment.LeaderUserId.ToString(), out var leaderUserId))
        {
            // Try to find in IdentityUsersCollection first (flattened list)
            var user = IdentityUsersCollection?.FirstOrDefault(u => u.Id == leaderUserId);
            if (user != null)
            {
                return user.DisplayName ?? "";
            }
        }

        return "";
    }

    private async Task OnSelectedDepartmentNodeChangedAsync(DepartmentTreeView node)
    {
        if (node == null)
        {
            return;
        }

        // Compare by Id instead of reference
        if (SelectedDepartment?.Id == node.Id)
        {
            return;
        }

        // Create a new instance using mapper to ensure Blazor detects the change
        SelectedDepartment = ObjectMapper.Map<DepartmentTreeView, DepartmentTreeView>(node);
        
        await InvokeAsync(StateHasChanged);
    }

    private async Task OpenCreateDepartmentModal(DepartmentTreeView node = null)
    {
        NewDepartmentValidations?.ClearAll();

        // If node is provided, set it as default parent
        if (node != null)
        {
            // Create a new instance using mapper to ensure Blazor detects the change
            SelectedDepartment = ObjectMapper.Map<DepartmentTreeView, DepartmentTreeView>(node);
            
            // Set ParentId to the selected node's Id (for adding sub-department)
            NewDepartment = new DepartmentCreateDto 
            { 
                ParentId = node.Id.ToString(),
                LeaderUserId = null
            };
        }
        else
        {
            // If node is null, it means adding root department
            SelectedDepartment = new DepartmentTreeView { Id = Guid.Empty, Name = L["Root"].Value };
            NewDepartment = new DepartmentCreateDto 
            { 
                ParentId = null,
                LeaderUserId = null
            };
        }

        // Reset leader user selection
        SelectedLeaderUser = new List<LookupDto<Guid>>();

        // Force state update before showing modal
        await InvokeAsync(StateHasChanged);
        
        CreateDepartmentModal.Show();
        
        // Force another update after modal is shown to ensure form fields are updated
        await Task.Delay(10);
        await InvokeAsync(StateHasChanged);
    }

    private async Task CreateDepartmentAsync()
    {
        try
        {
            if (await NewDepartmentValidations.ValidateAll() == false)
            {
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);            
            var createdDepartment = await DepartmentsAppService.CreateAsync(NewDepartment);

            var createdDepartmentTreeView =
                ObjectMapper.Map<DepartmentDto, DepartmentTreeView>(createdDepartment);

            if (string.IsNullOrEmpty(NewDepartment.ParentId))
            {
                DepartmentTreeViews.Add(createdDepartmentTreeView);
            }
            else
            {
                SelectedDepartment.Children ??= new List<DepartmentTreeView>();
                SelectedDepartment.Children.Add(createdDepartmentTreeView);
                SelectedDepartment.Collapsed = false;
            }

            await CloseCreateDepartmentModalAsync();
            await GetDepartmentsAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            await BlockUiService.UnBlock();
        }
    }

    private Task CloseCreateDepartmentModalAsync()
    {
        CreateDepartmentModal.Hide();
        return Task.CompletedTask;
    }

    private async Task OpenEditDepartmentModal(DepartmentTreeView node)
    {
        EditingDepartmentValidations?.ClearAll();
        await OnSelectedDepartmentNodeChangedAsync(node);

        EditingDepartmentId = node.Id;
        // Map directly from DepartmentTreeView to DepartmentUpdateDto
        EditingDepartment = ObjectMapper.Map<DepartmentTreeView, DepartmentUpdateDto>(node);

        if (string.IsNullOrEmpty(EditingDepartment.ParentId))
        {
            SelectedDepartment = new DepartmentTreeView { Id = node.Id, Name = L["Root"].Value };
        }
        else
        {
            SelectedDepartment = node;
        }

        EditDepartmentModal.Show();
    }

    private async Task UpdateDepartmentAsync()
    {
        try
        {
            if (await EditingDepartmentValidations.ValidateAll() == false)
            {
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);            
        EditingDepartment.ParentId = SelectedDepartment.ParentId;

        await DepartmentsAppService.UpdateAsync(EditingDepartmentId, EditingDepartment);

        await GetDepartmentsAsync();

        await CloseEditDepartmentModalAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            await BlockUiService.UnBlock();
        }
    }

    private Task CloseEditDepartmentModalAsync()
    {
        EditDepartmentModal.Hide();
        return Task.CompletedTask;
    }

    private async Task OpenDeleteDepartmentModalAsync(DepartmentTreeView node)
    {
        SelectedDepartment = node;

        if (SelectedDepartment is null)
        {
            return;
        }

        if (SelectedDepartment.HasChildren)
        {
            await UiMessageService.Error(L["DeleteSubDepartments"]);
            return;
        }

        if (!await UiMessageService.Confirm(L["DepartmentDeleteWarningMessage", SelectedDepartment.Name]))
        {
            return;
        }
            
        await DeleteDepartmentAsync();
    }

    private async Task DeleteDepartmentAsync()
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);            
            await DepartmentsAppService.DeleteAsync(SelectedDepartment.Id);

            await GetDepartmentsAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            await BlockUiService.UnBlock();
        }
    }
        
    private async Task OpenMoveDepartmentModal(DepartmentTreeView node)
    {
        MovingDepartment = node;
            
        var departmentsDto = await DepartmentsAppService.GetListAsync(new GetDepartmentsInput
        {
            MaxResultCount = LimitedResultRequestDto.MaxMaxResultCount
        });

        // Map each DepartmentWithNavigationPropertiesDto to DepartmentTreeView manually
        var departments = departmentsDto.Items
            .Select(x => ObjectMapper.Map<DepartmentDto, DepartmentTreeView>(x.Department))
            .ToList();

        var departmentsDictionary = new Dictionary<string, List<DepartmentTreeView>>();
            
        foreach (var department in departments)
        {
            if (department.Name.StartsWith(MovingDepartment.Name))
            {
                continue;
            }
                
            var parentId = department.ParentId ?? string.Empty;

            if (!departmentsDictionary.ContainsKey(parentId))
            {
                departmentsDictionary.Add(parentId, new List<DepartmentTreeView>());
            }
                
            departmentsDictionary[parentId].Add(department);
        }

        foreach (var department in departments)
        {
            var departmentId = department.Id.ToString();
            if (departmentsDictionary.ContainsKey(departmentId))
            {
                department.Children = departmentsDictionary[departmentId];
            }
            else
            {
                department.Children = new List<DepartmentTreeView>();
            }
        }

        var rootNode = new DepartmentTreeView
        {
            Id = Guid.Empty,
            Children = departmentsDictionary.ContainsKey(string.Empty) ? departmentsDictionary[string.Empty] : new List<DepartmentTreeView>(),
            Collapsed = false,
            Name = L["Root"].Value
        };

        NewParentDepartment = rootNode;
            
        MovableDepartmentTree = new List<DepartmentTreeView> { rootNode };
            
        MoveDepartmentModal.Show();
    }
        
    private Task MovableDepartmentSelected(DepartmentTreeView node)
    {
        NewParentDepartment = node;
        return Task.CompletedTask;
    }
        
    private Task CloseMoveDepartmentModal()
    {
        MovableDepartmentTree = null;
        MovingDepartment = null;
        NewParentDepartment = null;
            
        MoveDepartmentModal.Hide();
        return Task.CompletedTask;
    }
        
    private async Task MoveDepartmentAsync()
    {
        var newParentId = NewParentDepartment.Id == Guid.Empty ? null : NewParentDepartment.Id.ToString();
            
        if (MovingDepartment.ParentId == newParentId)
        {
            return;
        }
            
        if (!await UiMessageService.Confirm(L["DepartmentMoveConfirmMessage", MovingDepartment.Name, NewParentDepartment.Name].Value))
        {
            return;
        }

        try
        {
            EditingDepartmentId = MovingDepartment.Id;
            EditingDepartment = ObjectMapper.Map<DepartmentTreeView, DepartmentUpdateDto>(MovingDepartment);
            EditingDepartment.ParentId = newParentId;
                        
            await DepartmentsAppService.UpdateAsync(EditingDepartmentId, EditingDepartment);
            
            await CloseMoveDepartmentModal();
            await GetDepartmentsAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    protected virtual void OnLeaderUserIdChanged()
    {
        NewDepartment.LeaderUserId = SelectedLeaderUser.FirstOrDefault()?.Id ?? Guid.Empty;
    }

    private async Task GetIdentityUserCollectionLookupAsync(string? newValue = null)
    {
        IdentityUsersCollection = (await DepartmentsAppService.GetIdentityUserLookupAsync(new LookupRequestDto { Filter = newValue })).Items;
    }

    private async Task<List<LookupDto<Guid>>> GetIdentityUserCollectionLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        IdentityUsersCollection = (await DepartmentsAppService.GetIdentityUserLookupAsync(new LookupRequestDto { Filter = filter })).Items;
        return IdentityUsersCollection.ToList();
    }
}

