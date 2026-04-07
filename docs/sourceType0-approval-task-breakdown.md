# Task breakdown & implementation status - SourceType=0 leadership approval

## Scope agreed
- Keep **Blazorise PdfViewer** for quick document viewing.
- Use **pdf.js (pdfPick)** for the note-position picking flow.
- Add/extend enums/constants as needed.

## Tasks

- [x] Create separate feature branch: `feature/sourceType0-approval-flow`.
- [x] Add document status enum values for archive approval flow:
  - `CHO_PHE_DUYET`
  - `DA_PHE_DUYET`
- [x] Add new contract DTOs for approval APIs:
  - `SubmitDocumentForApprovalInput`
  - `RejectDocumentApprovalInput`
  - `ApproveDocumentWithNoteInput`
- [x] Extend `IDocumentsAppService` with APIs:
  - `SubmitForApprovalAsync`
  - `RejectApprovalAsync`
  - `ApproveWithNoteAsync`
- [x] Implement backend flow in `DocumentsAppService.Extended`:
  - submit for approval (assignment + status + history + notification)
  - reject approval (status + history + notification)
  - approve with note (pdf stamp note + new file + status + history + notification)
- [x] Extend PDF stamping service with coordinate note insertion API (`AddTextNote`).
- [x] Keep Blazorise viewer and add PDF.js picker in UI:
  - load `pdfInterop.js` in Blazor app shell
  - add approval modal in `DocumentDetail`
  - capture coordinates via `OnPdfClick` JS interop callback

## Pending
- [x] Replace temporary `LeaderUserId` GUID textbox with searchable user lookup (Select2).
- [x] Add dedicated permission keys for approval actions (if business wants separate control from `Documents.Send`).
- [ ] Add integration tests once .NET SDK/runtime is available in CI/runner.
