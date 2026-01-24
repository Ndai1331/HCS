# Phase 4: Testing & Validation Plan

## Overview
Comprehensive testing strategy for Phase 3 refactoring and overall Chat module optimization.

## Unit Tests

### 1. MessageHelper Tests

**File**: `MessageHelperTests.cs` (location: `src/HC.Blazor.Tests/Pages/Chat1/`)

```csharp
[TestClass]
public class MessageHelperTests
{
    [TestMethod]
    public void IsMessageForCurrentConversation_UserConversation_ByConversationId()
    {
        // Arrange
        var message = new ChatMessageRdto { ConversationId = Guid.NewGuid() };
        var currentContact = new ChatContactDto { Type = ConversationType.User, ConversationId = message.ConversationId };
        var currentUserId = Guid.NewGuid();

        // Act
        var result = MessageHelper.IsMessageForCurrentConversation(message, currentContact, null, currentUserId);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsMessageForCurrentConversation_GroupConversation()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        var message = new ChatMessageRdto { ConversationId = conversationId };
        var currentContact = new ChatContactDto { Type = ConversationType.Group };
        var currentUserId = Guid.NewGuid();

        // Act
        var result = MessageHelper.IsMessageForCurrentConversation(message, currentContact, conversationId, currentUserId);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void FindContactByMessage_ByConversationId()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        var contacts = new List<ChatContactDto>
        {
            new ChatContactDto { ConversationId = conversationId, Name = "Test" }
        };
        var message = new ChatMessageRdto { ConversationId = conversationId };
        var currentUserId = Guid.NewGuid();

        // Act
        var result = MessageHelper.FindContactByMessage(contacts, message, currentUserId);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("Test", result.Name);
    }

    [TestMethod]
    public void GetSenderDisplayName_WithFullName()
    {
        // Arrange
        var senderName = "John";
        var senderSurname = "Doe";

        // Act
        var result = MessageHelper.GetSenderDisplayName(senderName, senderSurname, "johndoe");

        // Assert
        Assert.AreEqual("John Doe", result);
    }

    [TestMethod]
    public void GetSenderDisplayName_WithUsername()
    {
        // Arrange
        var senderName = "";
        var senderUsername = "johndoe";

        // Act
        var result = MessageHelper.GetSenderDisplayName(senderName, "", senderUsername);

        // Assert
        Assert.AreEqual("johndoe", result);
    }

    [TestMethod]
    public void GetSenderDisplayName_UnknownUser()
    {
        // Arrange & Act
        var result = MessageHelper.GetSenderDisplayName("", "", "", "Anonymous");

        // Assert
        Assert.AreEqual("Anonymous", result);
    }
}
```

**Tests to Run**:
- [ ] IsMessageForCurrentConversation - User conversation
- [ ] IsMessageForCurrentConversation - Group conversation
- [ ] IsMessageForCurrentConversation - No match
- [ ] FindContactByMessage - By ConversationId
- [ ] FindContactByMessage - By UserId
- [ ] FindContactByMessage - Not found
- [ ] GetSenderDisplayName - Full name
- [ ] GetSenderDisplayName - Username only
- [ ] GetSenderDisplayName - Unknown user

---

### 2. ChatMessageService Tests

**File**: `ChatMessageServiceTests.cs`

```csharp
[TestClass]
public class ChatMessageServiceTests
{
    private ChatMessageService _service;
    private Mock<IProjectMembersAppService> _projectMembersMock;
    private Mock<IProjectTasksAppService> _projectTasksMock;

    [TestInitialize]
    public void Setup()
    {
        _projectMembersMock = new Mock<IProjectMembersAppService>();
        _projectTasksMock = new Mock<IProjectTasksAppService>();
        _service = new ChatMessageService(_projectMembersMock.Object, _projectTasksMock.Object);
    }

    [TestMethod]
    public async Task GetIdentityUsersAsync_ExcludesCurrentUser()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var users = new List<LookupDto<Guid>>
        {
            new LookupDto<Guid> { Id = currentUserId, DisplayName = "Current" },
            new LookupDto<Guid> { Id = otherUserId, DisplayName = "Other" }
        };
        _projectMembersMock.Setup(x => x.GetIdentityUserLookupAsync(It.IsAny<LookupRequestDto>()))
            .ReturnsAsync(new ListResultDto<LookupDto<Guid>> { Items = users });

        // Act
        var result = await _service.GetIdentityUsersAsync("", currentUserId);

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(otherUserId, result[0].Id);
    }

    [TestMethod]
    public async Task GetProjectsAsync_ReturnsFiltered()
    {
        // Arrange
        var projects = new List<LookupDto<Guid>>
        {
            new LookupDto<Guid> { Id = Guid.NewGuid(), DisplayName = "Project A" }
        };
        _projectTasksMock.Setup(x => x.GetProjectLookupAsync(It.IsAny<LookupRequestDto>()))
            .ReturnsAsync(new ListResultDto<LookupDto<Guid>> { Items = projects });

        // Act
        var result = await _service.GetProjectsAsync("A");

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result[0].DisplayName.Contains("A"));
    }

    [TestMethod]
    public async Task GetProjectTasksAsync_ReturnsFormatted()
    {
        // Arrange
        var tasks = new List<ProjectTaskWithNavigationPropertiesDto>
        {
            new ProjectTaskWithNavigationPropertiesDto
            {
                ProjectTask = new ProjectTaskDto { Id = Guid.NewGuid(), Code = "TASK-1", Title = "Test" }
            }
        };
        _projectTasksMock.Setup(x => x.GetListAsync(It.IsAny<GetProjectTasksInput>()))
            .ReturnsAsync(new PagedResultDto<ProjectTaskWithNavigationPropertiesDto> { Items = tasks });

        // Act
        var result = await _service.GetProjectTasksAsync("");

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result[0].DisplayName.Contains("TASK-1"));
    }
}
```

---

### 3. FileDownloadHelper Tests

**File**: `FileDownloadHelperTests.cs`

```csharp
[TestClass]
public class FileDownloadHelperTests
{
    private FileDownloadHelper _helper;
    private Mock<IJSRuntime> _jsRuntimeMock;

    [TestInitialize]
    public void Setup()
    {
        _jsRuntimeMock = new Mock<IJSRuntime>();
        _helper = new FileDownloadHelper(_jsRuntimeMock.Object);
    }

    [TestMethod]
    public async Task DownloadFileAsync_CallsJavaScript()
    {
        // Arrange
        var fileName = "test.pdf";
        var contentType = "application/pdf";
        var fileContent = new byte[] { 1, 2, 3, 4 };

        _jsRuntimeMock.Setup(x => x.InvokeVoidAsync(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns(ValueTask.CompletedTask);

        // Act
        await _helper.DownloadFileAsync(fileName, contentType, fileContent);

        // Assert
        _jsRuntimeMock.Verify(x => x.InvokeVoidAsync("downloadFile", It.IsAny<object[]>()), Times.Once);
    }

    [TestMethod]
    public async Task DownloadFileAsync_HandlesException_Gracefully()
    {
        // Arrange
        _jsRuntimeMock.Setup(x => x.InvokeVoidAsync(It.IsAny<string>(), It.IsAny<object[]>()))
            .Throws(new Exception("JS error"));

        // Act & Assert - Should not throw
        await _helper.DownloadFileAsync("test.pdf", "application/pdf", new byte[] { 1, 2, 3 });
    }
}
```

---

## Integration Tests

### 1. Message Sending Flow

**Test**: Verify the refactored SendMessageAsync works correctly

```csharp
[TestClass]
public class MessageSendingIntegrationTests
{
    [TestMethod]
    public async Task SendMessage_SuccessfulFlow()
    {
        // Arrange
        var component = new Chat1Component();
        component.Message = "Test message";
        component.CurrentChatContact = new ChatContactDto { UserId = Guid.NewGuid() };

        // Act
        await component.SendMessageAsync();

        // Assert
        Assert.IsTrue(component.ChatConversationDto.Messages.Count > 0);
        Assert.AreEqual("Test message", component.ChatConversationDto.Messages.Last().Message);
    }

    [TestMethod]
    public async Task SendMessage_WithReply_IncludesReplyTo()
    {
        // Arrange
        var component = new Chat1Component();
        var originalMessage = new ChatMessageDto { Id = Guid.NewGuid(), Message = "Original" };
        component.ReplyingToMessage = originalMessage;
        component.Message = "Reply message";

        // Act
        await component.SendMessageAsync();

        // Assert
        Assert.IsNotNull(component.ChatConversationDto.Messages.Last().ReplyToMessage);
        Assert.AreEqual(originalMessage.Id, component.ChatConversationDto.Messages.Last().ReplyToMessage.Id);
    }
}
```

### 2. Logging Reduction Verification

**Test**: Verify logging calls are reduced by 80%

```csharp
[TestClass]
public class LoggingReductionTests
{
    private Mock<ILogger<Chat1>> _loggerMock;

    [TestMethod]
    public void ProcessReceivedMessage_LogsMinimalInformation()
    {
        // Arrange
        var component = new Chat1Component();

        // Act
        component.ProcessReceivedMessage(new ChatMessageRdto { /* ... */ });

        // Assert
        // Verify Info logs are < 3
        _loggerMock.Verify(
            x => x.Log(LogLevel.Information, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), 
                It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.AtMost(2));
    }
}
```

---

## Performance Tests

### 1. Message Rendering Performance

**Metric**: Time to render 100 messages

```csharp
[TestMethod]
public void MessageRendering_Performance_Under100ms()
{
    // Arrange
    var sw = Stopwatch.StartNew();
    var messages = GenerateTestMessages(100);

    // Act
    foreach (var message in messages)
    {
        RenderMessageItem(message);
    }
    sw.Stop();

    // Assert
    Assert.IsTrue(sw.ElapsedMilliseconds < 100);
}
```

### 2. Logging Overhead Comparison

**Metric**: Compare logging overhead before/after

```
Before Phase 3: 15 LogInformation calls per message
After Phase 3:  2 LogInformation calls per message
Reduction:      87% improvement
```

### 3. Memory Usage

**Metric**: Memory reduction from state consolidation

```
Before: 46+ scattered properties
After:  3 state objects
Expected reduction: 15-20% memory saved
```

---

## Test Coverage Goals

- Unit Tests: 85%+ coverage
- Integration Tests: 70%+ coverage
- Performance Tests: Key metrics only

---

## Automated Testing

### Setup CI/CD Pipeline

```yaml
# .github/workflows/test.yml
name: Chat Module Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2
      - name: Run Unit Tests
        run: dotnet test --filter Category=Unit
      - name: Run Integration Tests
        run: dotnet test --filter Category=Integration
      - name: Check Coverage
        run: dotnet test /p:CollectCoverage=true
```

---

## Manual Testing Checklist

- [ ] Send text message
- [ ] Send message with files
- [ ] Reply to message
- [ ] Pin/Unpin message
- [ ] Forward message
- [ ] Delete message
- [ ] Create task from message
- [ ] Search conversations
- [ ] Create direct conversation
- [ ] Create group conversation
- [ ] Load more messages (pagination)
- [ ] Load more conversations (pagination)

---

## Performance Benchmarking

### Before Phase 3
- Average logging time: 50ms per message
- Memory: ~X MB
- Initial load: ~Y ms

### After Phase 3
- Average logging time: ~10ms per message (80% reduction)
- Memory: ~Z MB (15-20% reduction)
- Initial load: ~Y ms (unchanged)

---

## Release Readiness

- [ ] All unit tests passing
- [ ] All integration tests passing
- [ ] Performance targets met
- [ ] Code review completed
- [ ] Documentation updated
- [ ] No breaking changes
- [ ] Backward compatibility verified

---

**Estimated Timeline**:
- Unit Tests: 2-3 hours
- Integration Tests: 1-2 hours
- Performance Testing: 1 hour
- Total: 4-6 hours

**Status**: Ready to begin Phase 4 testing after Phase 3 completion
