namespace HC.Notifications;

public enum RelatedType
{
    DOCUMENT,
    /// <summary>
    /// Leader approval inbox deep link (manage-documents SentToMe + approval modal).
    /// </summary>
    APPROVAL_DOCUMENT,
    WORKFLOW,
    PROJECT,
    TASK,
    CHAT_ROOM,
    CALENDAR_EVENT
}