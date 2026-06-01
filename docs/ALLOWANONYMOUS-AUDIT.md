# AllowAnonymous Endpoint Audit

Last updated: 2026-05-31

This document lists all `[AllowAnonymous]` endpoints in `HC.Application` and their intended purpose.

## Intentionally public (survey kiosk)

| Service | Method | Purpose |
|---------|--------|---------|
| `SurveyResultsAppService` | `CreatePublicSurveyResultAsync` | Public survey rating submission |
| `SurveyResultsAppService` | `CreatePublicSurveyResultsAsync` | Batch public survey rating submission |
| `SurveySessionsAppService` | `CreatePublicSurveySessionAsync` | Public survey session creation |
| `SurveyLocationsAppService.Extended` | Public lookup methods | Survey kiosk location resolution |
| `SurveyCriteriasAppService.Extended` | Public lookup methods | Survey kiosk criteria resolution |
| `SurveyFilesAppService.Extended` | Public file access | Survey kiosk attachments |

## One-time export token (30s TTL, validated via `ExcelDownloadAnonymousTokenHelper`)

All `GetListAsExcelFileAsync` methods use `[AllowAnonymous]` because the browser download link cannot send JWT headers. Access is gated by a one-time token issued only to authenticated users via `GetDownloadTokenAsync`.

Affected entities: Documents, DocumentFiles, DocumentAssignments, DocumentHistories, DocumentWorkflowInstances, Departments, Units, Positions, MasterDatas, Workflows, WorkflowDefinitions, WorkflowTemplates, WorkflowStepTemplates, WorkflowStepAssignments, Projects, ProjectTasks, ProjectMembers, ProjectTaskAssignments, ProjectTaskDocuments, CalendarEvents, CalendarEventParticipants, Notifications, NotificationReceivers, UserDepartments, UserSignatures, SignatureSettings, SurveyResults, SurveySessions, SurveyCriterias, SurveyFiles, SurveyLocations, Reports.

## Workflow signing export

| Service | Method | Purpose |
|---------|--------|---------|
| `DocumentWorkflowInstancesAppService.SigningExport` | Excel export with one-time token | Same pattern as above |

## Fixed security issues (2026-05-31)

| Issue | Fix |
|-------|-----|
| `SurveyResultsAppService` class-level `[AllowAnonymous]` bypassed all CRUD auth | Removed class-level attribute; kept only on public create methods |
| `ReportsAppService.GetListForNavigationAsync` exposed report list anonymously | Removed `[AllowAnonymous]`; inherits `[Authorize(HCPermissions.MasterDatas.ReportsDefault)]` from base |

## Review checklist for new endpoints

1. Never apply `[AllowAnonymous]` at class level on services that inherit CRUD base classes.
2. Public APIs must validate tenant context explicitly (see survey public methods).
3. Export endpoints must use `ExcelDownloadAnonymousTokenHelper.ValidateAndConsumeOneTimeExportTokenAsync`.
4. Do not expose internal exception messages to anonymous callers.
