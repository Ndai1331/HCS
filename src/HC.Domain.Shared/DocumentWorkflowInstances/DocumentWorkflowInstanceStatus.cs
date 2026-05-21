namespace HC.DocumentWorkflowInstances;

/// <summary>
/// Enum for DocumentWorkflowInstances.Status column
/// </summary>
public enum DocumentWorkflowInstanceStatus
{
    /// <summary>Nháp</summary>
    DRAFT,

    /// <summary>Đang xử lý</summary>
    IN_PROGRESS,

    /// <summary>Quá hạn — còn ân hạn 1 ngày làm việc trước khi hủy</summary>
    OVERDUE,

    /// <summary>Đã hoàn thành</summary>
    COMPLETED,

    /// <summary>Từ chối</summary>
    REJECTED,

    /// <summary>Trả về</summary>
    RETURNED,

    /// <summary>Đã hủy</summary>
    CANCELLED
}
