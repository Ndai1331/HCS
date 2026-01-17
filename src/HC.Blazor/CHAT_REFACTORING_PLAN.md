# CHAT REFACTORING PLAN: Direct Chat → Conversation-Based

## 🎯 Objective
Convert all chat types (including User-to-User) to use Conversation logic instead of Direct chat.

## Current Architecture
```
Direct Chat (User-to-User)
├── No ConversationId
├── Uses TargetUserId only
└── Peer-to-peer messaging

Group/Project/Task Chat
├── Has ConversationId
├── Multiple members in ConversationMember table
└── Room-based messaging
```

## Target Architecture
```
ALL CHATS use Conversation:
├── User-to-User: Conversation with 2 members
├── Group: Conversation with multiple members
├── Project: Conversation with project members
└── Task: Conversation with task members
```

---

## 📋 Refactoring Tasks

### Phase 1: Domain Layer

#### 1.1 Update ConversationType enum
**File:** `src/HC.Domain.Shared/Chat/ConversationType.cs`

```csharp
public enum ConversationType
{
    // REMOVE: Direct = 1,  ← Delete this
    
    User = 1,      // ← Rename from Direct, still 2 members but uses Conversation
    Group = 2,
    Project = 3,
    Task = 4
}
```

#### 1.2 Add CreateUserConversation
**File:** `src/HC.Application.Contracts/Chat/Conversations/CreateUserConversationInput.cs` (NEW)

```csharp
public class CreateUserConversationInput
{
    public Guid TargetUserId { get; set; }
    public string? Name { get; set; } // Optional custom name
}
```

#### 1.3 Update IConversationAppService
**File:** `src/HC.Application.Contracts/Chat/Conversations/IConversationAppService.cs`

```csharp
Task<ConversationDto> CreateUserConversationAsync(CreateUserConversationInput input);
```

---

### Phase 2: Application Layer

#### 2.1 Implement CreateUserConversationAsync
**File:** `src/HC.Application/Chat/Conversations/ConversationAppService.cs`

```csharp
public async Task<ConversationDto> CreateUserConversationAsync(CreateUserConversationInput input)
{
    var currentUserId = CurrentUser.GetId();
    
    // Check if conversation already exists between these 2 users
    var existing = await _conversationRepository.FindUserConversationAsync(currentUserId, input.TargetUserId);
    if (existing != null)
    {
        return ObjectMapper.Map<Conversation, ConversationDto>(existing);
    }
    
    // Create new conversation
    var conversation = new Conversation(
        GuidGenerator.Create(),
        ConversationType.User, // Use User type instead of Direct
        input.Name ?? "Private Chat", // Default name
        CurrentTenant.Id
    );
    
    await _conversationRepository.InsertAsync(conversation);
    
    // Add 2 members
    await _conversationMemberRepository.InsertAsync(
        new ConversationMember(GuidGenerator.Create(), conversation.Id, currentUserId, "MEMBER", CurrentTenant.Id)
    );
    
    await _conversationMemberRepository.InsertAsync(
        new ConversationMember(GuidGenerator.Create(), conversation.Id, input.TargetUserId, "MEMBER", CurrentTenant.Id)
    );
    
    return ObjectMapper.Map<Conversation, ConversationDto>(conversation);
}
```

#### 2.2 Refactor SendMessageAsync
**File:** `src/HC.Application/Chat/Conversations/ConversationAppService.cs`

**BEFORE:**
```csharp
if (input.ConversationId.HasValue)
{
    // Group logic
}
else
{
    // Direct logic ← REMOVE THIS
}
```

**AFTER:**
```csharp
// ALL messages require ConversationId
if (!input.ConversationId.HasValue)
{
    throw new BusinessException("HC.Chat:ConversationIdRequired");
}

var conversation = await _conversationRepository.GetWithMembersAsync(input.ConversationId.Value);
// ... unified logic for all conversation types
```

#### 2.3 Update GetConversationAsync
Remove Direct chat logic, all use ConversationId.

---

### Phase 3: UI Layer (Blazor)

#### 3.1 Update Chat1.razor.cs
**File:** `src/HC.Blazor/Pages/Chat1/Chat1.razor.cs`

**Changes:**
- Remove all `CurrentChatContact.Type == ConversationType.Direct` checks
- All chats now have `ConversationId`
- Update `CreateDirectConversationAsync` → `CreateUserConversationAsync`

**Example:**
```csharp
// BEFORE
if (CurrentChatContact.Type == ConversationType.Direct)
{
    ChatConversationDto = await ConversationAppService.GetConversationAsync(
        new GetConversationInput { TargetUserId = CurrentChatContact.UserId }
    );
}

// AFTER
ChatConversationDto = await ConversationAppService.GetConversationAsync(
    new GetConversationInput { ConversationId = CurrentChatContact.ConversationId.Value }
);
```

#### 3.2 Update ProcessReceivedMessage
**File:** `src/HC.Blazor/Pages/Chat1/Chat1.razor.cs`

**BEFORE:**
```csharp
if (CurrentChatContact.Type == ConversationType.Direct)
{
    // Check sender/receiver logic
}
else if (CurrentChatContact.Type != ConversationType.Direct && CurrentConversationId.HasValue)
{
    // Check ConversationId
}
```

**AFTER:**
```csharp
// Unified logic: ALL use ConversationId
if (CurrentConversationId.HasValue && 
    message.ConversationId.HasValue &&
    message.ConversationId.Value == CurrentConversationId.Value)
{
    // Display message
}
```

---

### Phase 4: Data Migration

#### 4.1 Migration Strategy

**Option A: Fresh Start (No existing data)**
- Just deploy new code
- All new chats use Conversation

**Option B: Migrate existing Direct chats**
```sql
-- Find all Direct chat conversations (no ConversationId)
-- Create Conversation for each pair
-- Link existing messages to new Conversation
```

**Migration Script:** (If needed)
```csharp
// Create migration to convert existing Direct chats
// to User-type Conversations
```

---

## 🧪 Testing Checklist

- [ ] User can chat with another User (creates Conversation)
- [ ] Existing Groups still work
- [ ] Projects still work
- [ ] Tasks still work
- [ ] Multi-tab sync works for User chats
- [ ] Message sending works
- [ ] Message receiving works (real-time)
- [ ] Conversation list shows correctly
- [ ] No Direct chat references remain

---

## 🚨 Breaking Changes

### API Changes:
- `SendMessageInput` now requires `ConversationId` (no more TargetUserId-only)
- Remove `StartConversation` methods that used Direct logic

### UI Changes:
- Remove Direct chat UI paths
- All chats show Conversation name

### Database:
- May need migration for existing Direct chats

---

## 📝 Implementation Steps

1. ✅ Review current code
2. ⬜ Backup database
3. ⬜ Update ConversationType enum
4. ⬜ Implement CreateUserConversationAsync
5. ⬜ Refactor SendMessageAsync (remove Direct logic)
6. ⬜ Update UI to use Conversation for all
7. ⬜ Test thoroughly
8. ⬜ Migrate existing data (if any)
9. ⬜ Deploy

---

## 💡 Benefits

✅ **Simpler code**: One logic path for all chat types
✅ **Consistent**: All chats are Conversations
✅ **Scalable**: Easy to add features to all chat types
✅ **Better permissions**: Conversation-level permissions
✅ **Cross-tab sync**: Works the same for all types

---

## ⚠️ Risks

- Breaking existing Direct chats (need migration)
- API changes may break clients
- Need thorough testing

---

**Estimated Effort:** 4-6 hours for full refactoring + testing
