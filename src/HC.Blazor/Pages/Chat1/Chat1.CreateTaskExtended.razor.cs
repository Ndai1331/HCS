using System;
using System.Threading.Tasks;
using HC.Blazor.Components.ProjectTaskCreateModal;
using HC.Chat.Conversations;
using HC.Chat.Messages;
using HC.ProjectTasks;
using HC.Shared;

namespace HC.Blazor.Pages.Chat1;

public partial class Chat1
{
    private ProjectTaskCreateModal? CreateTaskFromMessageModalRef { get; set; }

    protected async Task CreateTaskFromMessageAsync(ChatMessageDto message)
    {
        if (message == null || CreateTaskFromMessageModalRef == null)
        {
            return;
        }

        var options = new ProjectTaskCreateModal.CreateProjectTaskModalOptions
        {
            ProjectId = CurrentChatContact?.Type == ConversationType.Project ? CurrentChatContact.ProjectId : null,
            Title = (message.Message ?? string.Empty).TruncateWithPostfix(100, "..."),
            Description = BuildTaskDescriptionFromMessage(message),
            Priority = ProjectTaskPriority.LOW,
            Status = ProjectTaskStatus.TODO,
            StartDate = DateTime.Now,
            DueDate = DateTime.Now.AddDays(7),
            RequireAtLeastOneAssignee = false,
        };

        await CreateTaskFromMessageModalRef.OpenCreateProjectTaskModalAsync(options);
    }

    private static string BuildTaskDescriptionFromMessage(ChatMessageDto message)
    {
        var senderFullName = $"{message.SenderSurname} {message.SenderName}".Trim();
        if (string.IsNullOrWhiteSpace(senderFullName))
        {
            senderFullName = message.SenderUsername ?? string.Empty;
        }

        return $"[Công việc được tạo từ tin nhắn của {senderFullName} - Ngày gửi {message.MessageDate:dd/MM/yyyy HH:mm}] Nội dung tin nhắn: \n\n{message.Message}";
    }

    private Task OnTaskCreatedFromMessageAsync()
    {
        return InvokeAsync(StateHasChanged);
    }
}
