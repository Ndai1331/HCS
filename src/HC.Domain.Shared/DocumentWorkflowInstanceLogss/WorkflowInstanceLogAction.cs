namespace HC.DocumentWorkflowInstanceLogss;

/// <summary>
/// Enum for DocumentWorkflowInstanceLogs.Action column
/// </summary>
public enum WorkflowInstanceLogAction
{
    /// <summary>Tạo văn bản</summary>
    CREATE_DOCUMENT,

    /// <summary>Văn thư trình ký</summary>
    SUBMIT_WORKFLOW,

    /// <summary>Duyệt</summary>
    APPROVE,

    /// <summary>Ký</summary>
    SIGN,

    /// <summary>Từ chối</summary>
    REJECT,

    /// <summary>Trả về</summary>
    RETURN,

    /// <summary>Chuyển khoa</summary>
    TRANSFER_UNIT,

    /// <summary>Phân công nhân viên</summary>
    ASSIGN_USER,

    /// <summary>Bắt đầu xử lý</summary>
    START_PROCESS,

    /// <summary>Hoàn thành xử lý</summary>
    COMPLETE_PROCESS,

    /// <summary>Quy trình hoàn thành</summary>
    WORKFLOW_COMPLETED,

    /// <summary>Quy trình bị hủy</summary>
    WORKFLOW_CANCELLED,

    /// <summary>Đổi người ký bước chưa ký (người trình ký)</summary>
    UPDATE_SIGNER,

    /// <summary>Gia hạn thời gian trình ký</summary>
    EXTEND_WORKFLOW
}
