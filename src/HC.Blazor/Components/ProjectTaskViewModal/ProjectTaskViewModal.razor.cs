using Blazorise;
using HC.ProjectTasks;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HC.ProjectTaskAssignments;
using HC.ProjectTaskDocuments;
using HC.DocumentFiles;
using Volo.Abp.Application.Dtos;

namespace HC.Blazor.Components.ProjectTaskViewModal;

public partial class ProjectTaskViewModal
{
    // Parameters
    [Parameter] public ProjectTaskWithNavigationPropertiesDto? Task { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public EventCallback<ProjectTaskDocumentWithNavigationPropertiesDto> OnViewPdfDocument { get; set; }

    // Modal reference
    private Modal TaskDetailModal { get; set; } = default!;

    // Data
    private IReadOnlyList<ProjectTaskAssignmentWithNavigationPropertiesDto> SelectedTaskAssignments { get; set; } = new List<ProjectTaskAssignmentWithNavigationPropertiesDto>();
    private IReadOnlyList<ProjectTaskDocumentWithNavigationPropertiesDto> SelectedTaskDocuments { get; set; } = new List<ProjectTaskDocumentWithNavigationPropertiesDto>();
    private string SelectedTab { get; set; } = "general";

    // PDF viewer state
    private Dictionary<Guid, bool> DocumentHasPdfCache { get; set; } = new();

    /// <summary>
    /// Show the modal with the specified task
    /// </summary>
    public async Task ShowAsync(ProjectTaskWithNavigationPropertiesDto task)
    {
        Task = task;
        SelectedTab = "general";
        SelectedTaskAssignments = new List<ProjectTaskAssignmentWithNavigationPropertiesDto>();
        SelectedTaskDocuments = new List<ProjectTaskDocumentWithNavigationPropertiesDto>();
        DocumentHasPdfCache = new Dictionary<Guid, bool>();

        await LoadTaskDataAsync();
        await TaskDetailModal.Show();
    }

    /// <summary>
    /// Hide the modal
    /// </summary>
    public async Task HideAsync()
    {
        await TaskDetailModal.Hide();
    }

    /// <summary>
    /// Load task assignments and documents
    /// </summary>
    private async Task LoadTaskDataAsync()
    {
        if (Task == null) return;

        try
        {
            // Load assignments
            var assignmentsInput = new GetProjectTaskAssignmentsInput
            {
                ProjectTaskId = Task.ProjectTask.Id,
                MaxResultCount = 100,
                SkipCount = 0
            };
            var assignmentsResult = await ProjectTaskAssignmentsAppService.GetListAsync(assignmentsInput);
            SelectedTaskAssignments = assignmentsResult.Items;

            // Load documents
            var documentsInput = new GetProjectTaskDocumentsInput
            {
                ProjectTaskId = Task.ProjectTask.Id,
                MaxResultCount = 100,
                SkipCount = 0
            };
            var documentsResult = await ProjectTaskDocumentsAppService.GetListAsync(documentsInput);
            SelectedTaskDocuments = documentsResult.Items;

            // Cache PDF file info for documents
            await CacheDocumentPdfInfoAsync(SelectedTaskDocuments);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    /// <summary>
    /// Cache PDF file information for documents
    /// </summary>
    private async Task CacheDocumentPdfInfoAsync(IEnumerable<ProjectTaskDocumentWithNavigationPropertiesDto> documents)
    {
        foreach (var doc in documents)
        {
            if (doc.Document != null && !DocumentHasPdfCache.ContainsKey(doc.Document.Id))
            {
                var hasPdf = await CheckIfDocumentHasPdfFileAsync(doc.Document.Id);
                DocumentHasPdfCache[doc.Document.Id] = hasPdf;
            }
        }
    }

    /// <summary>
    /// Check if a document has a PDF file
    /// </summary>
    private async Task<bool> CheckIfDocumentHasPdfFileAsync(Guid documentId)
    {
        try
        {
            var documentFilesResult = await DocumentFilesAppService.GetListAsync(new GetDocumentFilesInput
            {
                DocumentId = documentId,
                MaxResultCount = 1,
                SkipCount = 0
            });

            if (!documentFilesResult.Items.Any())
            {
                return false;
            }

            var documentFile = documentFilesResult.Items.First();
            return IsPdfFileExtension(documentFile.DocumentFile.Name) && !string.IsNullOrEmpty(documentFile.DocumentFile.Path);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Check if file extension is PDF
    /// </summary>
    private bool IsPdfFileExtension(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
        return extension == ".pdf";
    }

    /// <summary>
    /// Check if document has PDF file (from cache)
    /// </summary>
    private bool DocumentHasPdfFile(Guid documentId)
    {
        return DocumentHasPdfCache.TryGetValue(documentId, out var hasPdf) && hasPdf;
    }

    /// <summary>
    /// Open PDF viewer modal for document
    /// </summary>
    private async Task OpenPdfViewerModalForDocumentAsync(ProjectTaskDocumentWithNavigationPropertiesDto projectTaskDocument)
    {
        await OnViewPdfDocument.InvokeAsync(projectTaskDocument);
    }

    /// <summary>
    /// Close the modal
    /// </summary>
    private async Task CloseModalAsync()
    {
        await HideAsync();
        if (OnClose.HasDelegate)
        {
            await OnClose.InvokeAsync();
        }
    }

    /// <summary>
    /// Handle tab selection change
    /// </summary>
    private void OnSelectedTabChanged(string name)
    {
        SelectedTab = name;
    }

    /// <summary>
    /// Get user display name
    /// </summary>
    private string GetUserDisplayName(Volo.Abp.Identity.IdentityUserDto user)
    {
        var fullName = $"{user.Name} {user.Surname}".Trim();
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        return user.UserName ?? string.Empty;
    }

    /// <summary>
    /// Get user initial (first letter of name or username)
    /// </summary>
    private string GetUserInitial(Volo.Abp.Identity.IdentityUserDto user)
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

    /// <summary>
    /// Get badge color for task status
    /// </summary>
    protected string GetStatusBadgeColor(string status)
    {
        return status switch
        {
            "TODO" => "secondary",
            "IN_PROGRESS" => "primary",
            "WAITING" => "warning",
            "DONE" => "success",
            "CANCELLED" => "danger",
            _ => "secondary",
        };
    }

    /// <summary>
    /// Get badge color for task priority
    /// </summary>
    protected string GetPriorityBadgeColor(string priority)
    {
        return priority switch
        {
            "LOW" => "secondary",
            "MEDIUM" => "info",
            "HIGH" => "warning",
            "URGENT" => "danger",
            _ => "secondary",
        };
    }
}
