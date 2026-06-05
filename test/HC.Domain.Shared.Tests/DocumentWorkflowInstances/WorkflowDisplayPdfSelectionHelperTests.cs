using System;
using System.Collections.Generic;
using HC.DocumentAssignments;
using HC.DocumentFiles;
using HC.WorkflowStepTemplates;
using Xunit;

namespace HC.DocumentWorkflowInstances;

public class WorkflowDisplayPdfSelectionHelperTests
{
    private static readonly Guid DocumentId = Guid.NewGuid();

    [Fact]
    public void SelectBestPdf_PrefersLatestSignedPdfOverUnsignedSubmitCopy()
    {
        var submitFile = CreatePdf(Guid.NewGuid(), "submit.pdf", isSigned: false, uploadedAt: new DateTime(2026, 1, 1));
        var signedFile = CreatePdf(Guid.NewGuid(), "signed-final.pdf", isSigned: true, uploadedAt: new DateTime(2026, 3, 1));

        var result = WorkflowDisplayPdfSelectionHelper.SelectBestPdf(
            new List<DocumentFile> { submitFile, signedFile },
            Array.Empty<DocumentAssignment>(),
            new Dictionary<Guid, string>());

        Assert.Equal(signedFile.Id, result!.Id);
    }

    [Fact]
    public void SelectBestPdf_WhenNoSignedFile_UsesLatestDoneBlockingAssignmentPdf()
    {
        var stepId = Guid.NewGuid();
        var submitFile = CreatePdf(Guid.NewGuid(), "submit.pdf", isSigned: false, uploadedAt: new DateTime(2026, 1, 1));
        var stepResultFile = CreatePdf(Guid.NewGuid(), "step1.pdf", isSigned: false, uploadedAt: new DateTime(2026, 2, 1));

        var assignment = CreateDoneAssignment(stepId, stepResultFile.Id, processedAt: new DateTime(2026, 2, 15));
        var stepTypes = new Dictionary<Guid, string> { [stepId] = nameof(WorkflowStepType.SIGN) };

        var result = WorkflowDisplayPdfSelectionHelper.SelectBestPdf(
            new List<DocumentFile> { submitFile, stepResultFile },
            new List<DocumentAssignment> { assignment },
            stepTypes);

        Assert.Equal(stepResultFile.Id, result!.Id);
    }

    [Fact]
    public void SelectBestPdf_IgnoresViewStepDoneAssignments()
    {
        var viewStepId = Guid.NewGuid();
        var submitFile = CreatePdf(Guid.NewGuid(), "submit.pdf", isSigned: false, uploadedAt: new DateTime(2026, 3, 1));
        var viewFile = CreatePdf(Guid.NewGuid(), "view-only.pdf", isSigned: false, uploadedAt: new DateTime(2026, 2, 1));

        var viewAssignment = CreateDoneAssignment(viewStepId, viewFile.Id, processedAt: new DateTime(2026, 3, 2));
        var stepTypes = new Dictionary<Guid, string> { [viewStepId] = nameof(WorkflowStepType.VIEW) };

        var result = WorkflowDisplayPdfSelectionHelper.SelectBestPdf(
            new List<DocumentFile> { submitFile, viewFile },
            new List<DocumentAssignment> { viewAssignment },
            stepTypes);

        Assert.Equal(submitFile.Id, result!.Id);
    }

    [Fact]
    public void SelectBestPdf_FallbackToLatestAnyPdf()
    {
        var older = CreatePdf(Guid.NewGuid(), "old.pdf", isSigned: false, uploadedAt: new DateTime(2026, 1, 1));
        var newer = CreatePdf(Guid.NewGuid(), "new.pdf", isSigned: false, uploadedAt: new DateTime(2026, 2, 1));

        var result = WorkflowDisplayPdfSelectionHelper.SelectBestPdf(
            new List<DocumentFile> { older, newer },
            Array.Empty<DocumentAssignment>(),
            new Dictionary<Guid, string>());

        Assert.Equal(newer.Id, result!.Id);
    }

    [Fact]
    public void SelectBestPdf_PrefersSignedResultFileWithoutDocumentId_OverSubmitCopy()
    {
        var stepId = Guid.NewGuid();
        var submitFile = CreatePdf(Guid.NewGuid(), "submit.pdf", isSigned: false, uploadedAt: new DateTime(2026, 1, 1));
        var signedResultFile = CreatePdf(
            Guid.NewGuid(),
            "step2-signed.pdf",
            isSigned: true,
            uploadedAt: new DateTime(2026, 3, 1),
            documentId: null);

        var assignment = CreateDoneAssignment(stepId, signedResultFile.Id, processedAt: new DateTime(2026, 3, 2));
        var stepTypes = new Dictionary<Guid, string> { [stepId] = nameof(WorkflowStepType.SIGN) };

        // Resolver merges assignment result files even when DocumentId is null.
        var result = WorkflowDisplayPdfSelectionHelper.SelectBestPdf(
            new List<DocumentFile> { submitFile, signedResultFile },
            new List<DocumentAssignment> { assignment },
            stepTypes);

        Assert.Equal(signedResultFile.Id, result!.Id);
    }

    [Fact]
    public void SelectBestPdf_SignedFileWinsOverNewerDoneAssignmentUnsignedResult()
    {
        var stepId = Guid.NewGuid();
        var signedFile = CreatePdf(Guid.NewGuid(), "fully-signed.pdf", isSigned: true, uploadedAt: new DateTime(2026, 2, 1));
        var assignmentFile = CreatePdf(Guid.NewGuid(), "partial.pdf", isSigned: false, uploadedAt: new DateTime(2026, 3, 1));
        var assignment = CreateDoneAssignment(stepId, assignmentFile.Id, processedAt: new DateTime(2026, 3, 15));
        var stepTypes = new Dictionary<Guid, string> { [stepId] = nameof(WorkflowStepType.SIGN) };

        var result = WorkflowDisplayPdfSelectionHelper.SelectBestPdf(
            new List<DocumentFile> { signedFile, assignmentFile },
            new List<DocumentAssignment> { assignment },
            stepTypes);

        Assert.Equal(signedFile.Id, result!.Id);
    }

    private static DocumentFile CreatePdf(
        Guid id,
        string name,
        bool isSigned,
        DateTime uploadedAt,
        Guid? documentId = null)
    {
        return new DocumentFile(id, documentId ?? DocumentId, name, isSigned, uploadedAt, path: $"/blob/{name}");
    }

    private static DocumentAssignment CreateDoneAssignment(Guid stepId, Guid fileResultId, DateTime processedAt)
    {
        var assignment = new DocumentAssignment(
            Guid.NewGuid(),
            DocumentId,
            stepId,
            Guid.NewGuid(),
            stepOrder: 1,
            nameof(WorkflowStepType.SIGN),
            nameof(DocumentAssignmentStatus.DONE),
            assignedAt: processedAt.AddDays(-1),
            processedAt: processedAt,
            isCurrent: false);
        assignment.DocumentFileResultId = fileResultId;
        return assignment;
    }
}
