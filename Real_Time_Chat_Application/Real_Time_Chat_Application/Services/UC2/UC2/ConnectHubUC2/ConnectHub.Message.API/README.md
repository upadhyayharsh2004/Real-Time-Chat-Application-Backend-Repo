# ConnectHub — Message Service (UC-2)

**Real-Time Chat Application (.NET 8 / ASP.NET Core + SignalR)**

## Overview

The Message Service is the UC-2 microservice for the ConnectHub platform. It handles all message persistence, retrieval, real-time delivery, and management. Real-time bidirectional communication is powered by **ASP.NET Core SignalR** which maintains persistent WebSocket connections via the `ChatHub`.

---

## Project Structure

```
ConnectHub.Message.API/
├── Controllers/
│   ├── MessageController.cs          # REST endpoints (history, edit, delete, search, reactions, pins)
│   └── AdminMessageController.cs     # Admin-only endpoints
├── Data/
│   └── MessageDbContext.cs           # EF Core DbContext with SQL Server
├── Hubs/
│   └── ChatHub.cs                    # SignalR Hub — real-time messaging
├── Middleware/
│   └── ExceptionHandlingMiddleware.cs # Global exception handler → consistent JSON errors
├── Migrations/
│   ├── 20260424000001_InitialCreate.cs
│   └── MessageDbContextModelSnapshot.cs
├── Models/
│   ├── DTOs/
│   │   └── MessageDtos.cs            # All request/response DTOs + ApiResponseDto<T>
│   └── Entities/
│       ├── Message.cs                # Core EF Core entity (class diagram fields)
│       ├── MessageReaction.cs        # Emoji reactions (extra feature)
│       └── ConversationPin.cs        # Pinned messages (extra feature)
├── Repositories/
│   ├── Interfaces/
│   │   └── IMessageRepository.cs     # Interface — all Task<T> methods from class diagram
│   └── Implementations/
│       └── MessageRepository.cs      # EF Core implementation
├── Services/
│   ├── Interfaces/
│   │   └── IMessageService.cs        # Interface — business logic contract
│   └── Implementations/
│       └── MessageService.cs         # Business logic implementation
├── appsettings.json
├── appsettings.Development.json
├── Program.cs                        # Startup — JWT, SignalR, EF Core, Serilog, Swagger
└── ConnectHub.Message.API.csproj
```

---

## Features (from Case Study + Extensions)

### From PDF / Images

| Feature | Implementation |
|---|---|
| Send Direct Message | `ChatHub.SendDirectMessage(receiverId, content)` — persists Message entity, pushes via `Clients.User(receiverId).SendAsync("ReceiveMessage")` |
| Receive Real-Time Message | Hub calls `Clients.User(receiverId).SendAsync("ReceiveMessage")` instantly on message send |
| View Message History | `GET /api/messages/direct/{userId}` — paginated list of Message entities sorted by SentAt |
| Edit Message | `PUT /api/messages/{id}` — updates Content, sets `IsEdited = true`, `EditedAt = DateTime.UtcNow` |
| Delete Message | `DELETE /api/messages/{id}` — soft delete: `IsDeleted = true`, content replaced with `[Message deleted]` |
| Search Messages | `GET /api/messages/search?q=keyword` — EF Core LIKE query on `Message.Content` |
| Send Room Message | `ChatHub.SendRoomMessage(roomId, content)` — broadcasts via `Clients.Group(roomId.ToString())` |
| Typing Indicator | `ChatHub.TypingIndicator(recipientId, isTyping)` — pushes real-time typing event to recipient's connection |
| Mark Message Read | `ChatHub.MarkMessageRead(messageId)` — sets `IsRead = true`, `ReadAt = DateTime.UtcNow`, pushes read receipt to sender |
| View Unread Message Count | `GET /api/messages/unread-count/{userId}` — COUNT query on Message where `IsRead = false` |
| Admin Delete Message | `DELETE /api/admin/messages/{id}` — Admin role |
| Admin View All Messages | `GET /api/admin/messages/direct?senderId=&receiverId=` — Admin scoped |

### Extra Features (beyond spec)

| Feature | Implementation |
|---|---|
| Emoji Reactions | `POST /api/messages/{id}/reactions` — add/remove emoji; broadcasts via SignalR `ReactionAdded` |
| Pinned Messages | `POST /api/messages/{id}/pin` — pin important messages; `GET /api/messages/pins` |
| Reply Threading | `ReplyToMessageId` on Message entity — nested reply support |
| Recent Chats Sidebar | `GET /api/messages/recent` — most recent message per conversation partner |
| Paginated History | `?page=1&pageSize=20` on all list endpoints |
| Unread from Sender | `GET /api/messages/unread-count/{userId}/from/{senderId}` |
| Mark All Read | `PUT /api/messages/read-all/{senderId}` |
| Room Typing Indicator | `ChatHub.TypingIndicatorRoom(roomId, isTyping)` |
| Media Messages | `MessageType: IMAGE/FILE/AUDIO` with `MediaUrl` field |

---

## Message Entity (from Class Diagram Figure 3)

```csharp
public class Message
{
    int MessageId
    int SenderId
    int? ReceiverId      // null for room messages
    int? RoomId          // null for direct messages
    string Content
    string MessageType   // TEXT | IMAGE | FILE | AUDIO
    bool IsRead
    bool IsDeleted       // soft delete
    bool IsEdited
    DateTime SentAt
    DateTime? ReadAt
    DateTime? EditedAt
    string? MediaUrl
    int? ReplyToMessageId
}
```

---

## SignalR Events

### Client → Hub (invoke)
| Method | Parameters |
|---|---|
| `SendDirectMessage` | receiverId, content, messageType?, mediaUrl?, replyToMessageId? |
| `SendRoomMessage` | roomId, content, messageType?, mediaUrl?, replyToMessageId? |
| `TypingIndicator` | recipientId, isTyping |
| `TypingIndicatorRoom` | roomId, isTyping |
| `MarkMessageRead` | messageId |
| `JoinRoom` | roomId |
| `LeaveRoom` | roomId |
| `ReactToMessage` | messageId, emoji |

### Hub → Client (on)
| Event | Triggered By |
|---|---|
| `ReceiveMessage` | Direct message received |
| `MessageSent` | Confirmation to sender |
| `ReceiveRoomMessage` | Room message broadcast |
| `TypingIndicator` | Typing status from DM partner |
| `RoomTypingIndicator` | Typing status in room |
| `MessageRead` | Read receipt from receiver |
| `ReactionAdded` | Emoji reaction added |
| `UserConnected` | User came online |
| `UserDisconnected` | User went offline |
| `UserJoinedRoom` | User joined a room |
| `UserLeftRoom` | User left a room |

---

## REST Endpoints

### MessageController (`/api/messages`)

| Method | Route | Description |
|---|---|---|
| GET | `/direct/{userId}` | Paginated direct message history |
| GET | `/room/{roomId}` | Paginated room messages |
| GET | `/unread` | All unread messages |
| GET | `/unread-count/{userId}` | Unread count |
| GET | `/unread-count/{userId}/from/{senderId}` | Unread from specific sender |
| GET | `/recent` | Recent chats sidebar list |
| GET | `/search?q=` | Search messages |
| GET | `/{id}` | Get single message |
| PUT | `/{id}` | Edit message (own only) |
| PUT | `/{id}/read` | Mark single message as read |
| PUT | `/read-all/{senderId}` | Mark all from sender as read |
| DELETE | `/{id}` | Soft delete message (own / Admin) |
| POST | `/{id}/reactions` | Add emoji reaction |
| DELETE | `/{id}/reactions/{emoji}` | Remove emoji reaction |
| GET | `/{id}/reactions` | Get all reactions |
| POST | `/{id}/pin` | Pin a message |
| DELETE | `/pins/{pinId}` | Unpin a message |
| GET | `/pins` | Get pinned messages |

### AdminMessageController (`/api/admin/messages`) — Admin role only

| Method | Route | Description |
|---|---|---|
| GET | `/direct?senderId=&receiverId=` | All DMs between two users |
| GET | `/room/{roomId}` | All room messages |
| DELETE | `/{id}` | Admin delete any message |
| GET | `/{id}` | Get any message by ID |

---

## Setup

### Prerequisites
- .NET 8 SDK
- SQL Server / SQL Server Express

### Connection String
Update `appsettings.json`:
```json
"ConnectionStrings": {
  "MessageDb": "Server=localhost\\SQLEXPRESS;Database=ConnectHub_Message;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

### JWT Secret
Must match the Auth Service JWT secret:
```json
"Jwt": {
  "Secret": "ConnectHub_Super_Secret_JWT_Key_2026_Change_This_In_Production_MinLength32",
  "Issuer": "ConnectHub",
  "Audience": "ConnectHubUsers"
}
```

### Run
```bash
dotnet restore
dotnet run
```

Swagger UI: http://localhost:5000/swagger

### Migrations (if regenerating)
```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

---

## EF Core Indexes (Performance)

As specified in case study section 4.2:
- `IX_Messages_SenderId_ReceiverId` — direct message conversation queries
- `IX_Messages_RoomId_SentAt` — room message queries ordered by time
- `IX_Messages_ReceiverId_IsRead` — unread count queries
- `IX_Messages_IsDeleted` — filter soft-deleted messages

---

*Confidential | ConnectHub Platform | Internal Use Only*
