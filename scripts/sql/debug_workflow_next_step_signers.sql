-- Debug: why approve fails with "Vui lòng chọn người ký cho bước trình ký"
-- The error means the NEXT step (after current) has >1 role-resolved candidates and NextStepSignerUserId was null.
--
-- Replace filters below:
--   @document_title  : part of document title (e.g. N'%ổi trực%')
--   @submitter_name  : người trình (e.g. N'%Thưởng%')

-- 1) Find active workflow instance + current step
SELECT
    d."Id" AS document_id,
    d."Title",
    d."StorageNumber",
    dwi."Id" AS workflow_instance_id,
    dwi."Status" AS instance_status,
    dwi."CreatorId" AS submitter_user_id,
  (submitter."Surname" || ' ' || submitter."Name") AS submitter_name,
    dwi."CurrentStepId",
    cur."Order" AS current_step_order,
    cur."Name" AS current_step_name,
    w."Name" AS workflow_name
FROM "AppDocuments" d
JOIN "AppDocumentWorkflowInstances" dwi ON dwi."DocumentId" = d."Id"
JOIN "AppWorkflowStepTemplates" cur ON cur."Id" = dwi."CurrentStepId"
JOIN "AppWorkflows" w ON w."Id" = dwi."WorkflowId"
LEFT JOIN "AbpUsers" submitter ON submitter."Id" = dwi."CreatorId"
WHERE dwi."Status" IN ('IN_PROGRESS', 'OVERDUE')
  AND d."Title" ILIKE '%ổi trực%'   -- adjust
  AND d."IsDeleted" = false
ORDER BY dwi."StartedAt" DESC
LIMIT 5;

-- 2) All committed steps + assignments for an instance (set workflow_instance_id)
-- SELECT dwi."CommittedStepTemplateIdsJson" FROM "AppDocumentWorkflowInstances" dwi WHERE dwi."Id" = '...';

WITH instance AS (
    SELECT dwi.*
    FROM "AppDocumentWorkflowInstances" dwi
    WHERE dwi."Id" = 'WORKFLOW_INSTANCE_ID_HERE'  -- from query 1
)
SELECT
    st."Order",
    st."Name" AS step_name,
    st."Type",
    wsa."AssigneeType",
    wsa."IsPrimary",
    wsa."DefaultUserId",
    r."Name" AS role_name,
    wsa."RoleId"
FROM instance i
JOIN LATERAL (
    SELECT (jsonb_array_elements_text(i."CommittedStepTemplateIdsJson"::jsonb))::uuid AS step_id
) committed ON true
JOIN "AppWorkflowStepTemplates" st ON st."Id" = committed.step_id
LEFT JOIN "AppWorkflowStepAssignments" wsa ON wsa."StepId" = st."Id" AND wsa."IsActive" = true AND wsa."IsDeleted" = false
LEFT JOIN "AbpRoles" r ON r."Id" = wsa."RoleId"
ORDER BY st."Order", wsa."IsPrimary" DESC;

-- 3) Current pending assignments (who can sign now)
SELECT
    da."Id" AS assignment_id,
    da."WorkflowStepTemplateId",
    st."Order" AS step_order,
    st."Name" AS step_name,
    da."ReceiverUserId",
    (u."Surname" || ' ' || u."Name") AS receiver_name,
    da."Status",
    da."IsCurrent"
FROM "AppDocumentAssignments" da
JOIN "AppWorkflowStepTemplates" st ON st."Id" = da."WorkflowStepTemplateId"
JOIN "AbpUsers" u ON u."Id" = da."ReceiverUserId"
WHERE da."DocumentId" = 'DOCUMENT_ID_HERE'  -- from query 1
  AND da."IsDeleted" = false
  AND da."IsCurrent" = true
ORDER BY st."Order", da."CreationTime";

-- 4) NEXT step role candidates (same logic as backend: users with role in submitter OU chain)
-- Set submitter_user_id and role_id from step 2 (next step after current)
WITH RECURSIVE ou_chain AS (
    SELECT ou."Id", ou."ParentId", ou."DisplayName", 0 AS depth
    FROM "AbpUserOrganizationUnits" uou
    JOIN "AbpOrganizationUnits" ou ON ou."Id" = uou."OrganizationUnitId"
    WHERE uou."UserId" = 'SUBMITTER_USER_ID_HERE'
    ORDER BY uou."CreationTime"
    LIMIT 1

    UNION ALL

    SELECT parent."Id", parent."ParentId", parent."DisplayName", c.depth + 1
    FROM ou_chain c
    JOIN "AbpOrganizationUnits" parent ON parent."Id" = c."ParentId"
)
SELECT DISTINCT
    u."Id" AS user_id,
    u."UserName",
    (u."Surname" || ' ' || u."Name") AS full_name,
    ou."DisplayName" AS organization_unit,
    MIN(c.depth) AS ou_depth
FROM "AbpUsers" u
JOIN "AbpUserRoles" ur ON ur."UserId" = u."Id" AND ur."RoleId" = 'ROLE_ID_HERE'
JOIN "AbpUserOrganizationUnits" uou ON uou."UserId" = u."Id"
JOIN ou_chain c ON c."Id" = uou."OrganizationUnitId"
JOIN "AbpOrganizationUnits" ou ON ou."Id" = uou."OrganizationUnitId"
WHERE u."IsActive" = true
GROUP BY u."Id", u."UserName", u."Surname", u."Name", ou."DisplayName"
ORDER BY ou_depth, full_name;
-- If this returns >1 row, UI must let approver pick NextStepSignerUserId when advancing.
