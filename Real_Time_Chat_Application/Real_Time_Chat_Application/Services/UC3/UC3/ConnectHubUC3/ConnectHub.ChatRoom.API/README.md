# ConnectHub — ChatRoom Service (UC-3)

**Real-Time Chat Application (.NET 8 / ASP.NET Core + SignalR + RabbitMQ)**

## Overview

The ChatRoom Service is the UC-3 microservice for the ConnectHub platform. It handles room creation, membership management, role-based access, room invites, and real-time presence events. Real-time events are powered by **ASP.NET Core SignalR** (RoomHub). Domain events are published to **RabbitMQ** after every DB operation and incoming events from UC1 and UC2 are consumed.

---

## Project Structure

```
ConnectHub.ChatRoom.API/
├── Controllers/
│   ├── ChatRoomController.cs          # REST endpoints for rooms, members, invites
│   └── AdminChatRoomController.cs     # Admin-only [Authorize(Roles="Admin")]
├── Data/
│   └── ChatRoomDbContext.cs           # EF Core DbContext with SQL Server
├── Hubs/
│   └── RoomHub.cs                     # SignalR Hub — room presence & membership
├── Middleware/
│   └── ExceptionHandlingMiddleware.cs # Global exception → consistent JSON errors
├── Migrations/
│   ├── 20260424000001_InitialCreate.cs
│   ├── 20260424000001_InitialCreate.Designer.cs
│   └── ChatRoomDbContextModelSnapshot.cs
├── Models/
│   ├── DTOs/
│   │   └── ChatRoomDtos.cs            # All request/response DTOs + ApiResponseDto<T>
│   ├── Entities/
│   │   ├── ChatRoom.cs                # Core EF Core entity
│   │   ├── RoomMember.cs              # Join table with Role (ADMIN/MODERATOR/MEMBER)
│   │   └── RoomInvite.cs             # Private room invite with expiry
│   └── Events/
│       └── ChatRoomEvents.cs          # Outbound + inbound RabbitMQ event contracts
├── Repositories/
│   ├── Interfaces/
│   │   ├── IChatRoomRepository.cs
│   │   └── IRabbitMqPublisher.cs
│   └── Implementations/
│       ├── ChatRoomRepository.cs
│       └── RabbitMqPublisher.cs
├── Services/
│   ├── Interfaces/
│   │   └── IChatRoomService.cs
│   └── Implementations/
│       ├── ChatRoomService.cs         # Business logic + RabbitMQ publishing
│       └── ChatRoomConsumer.cs        # BackgroundService for inbound events
├── appsettings.json                   # Includes RabbitMQ config section
├── appsettings.Development.json
├── Program.cs                         # JWT + SignalR + EF Core + Serilog + Swagger
├── Dockerfile
└── docker-compose.yml
```

---

## Features

### From Sprint PDF
| Feature | Implementation |
|---|---|
| Create Room / Group Chat | `POST /api/rooms` — creates ChatRoom entity; creator added as RoomMember with Role = ADMIN |
| Join / Leave Room | `POST /api/rooms/{id}/join` + `POST /api/rooms/{id}/leave` + `RoomHub.JoinRoom/LeaveRoom` |
| Send Room Message | Handled by UC2 ChatHub — UC3 consumes `connecthub.room.message.sent` to update last message |
| View Room Members | `GET /api/rooms/{id}/members` — IList&lt;RoomMember&gt; with roles and join dates |
| Manage Room (Update/Delete) | `PUT/DELETE /api/rooms/{id}` — room admin only |
| Browse Public Rooms | `GET /api/rooms/public` — where RoomType=PUBLIC and IsActive=true |
| Admin: View All Rooms | `GET /api/admin/rooms` — Admin-scoped |
| Admin: Delete Room | `DELETE /api/admin/rooms/{id}` |

### Extra Features (beyond PDF)
| Feature | Implementation |
|---|---|
| Room Search | `GET /api/rooms/search?q=` — EF Core LIKE on Name + Description |
| My Rooms | `GET /api/rooms/my` — rooms the authenticated user belongs to |
| Role Management | `PUT /api/rooms/{id}/members/{userId}/role` — ADMIN/MODERATOR/MEMBER |
| Remove Member | `DELETE /api/rooms/{id}/members/{userId}` — Admin or Moderator |
| Private Room Invites | `POST /api/rooms/{id}/invite` + `PUT /api/rooms/invites/{id}/respond` |
| Pending Invites | `GET /api/rooms/invites/pending` |
| Room Avatar | `AvatarUrl` field on ChatRoom entity |
| Last Message Preview | Updated via RabbitMQ consumer from UC2 — sidebar display without querying Message-Service |

---

## RabbitMQ Integration

### Outbound Events (published AFTER DB save)
| Event | Queue | Consumers |
|---|---|---|
| `RoomCreatedEvent` | `connecthub.room.created` | Notification-Service |
| `RoomDeletedEvent` | `connecthub.room.deleted` | **UC2 Message-Service** (soft-deletes all room messages) |
| `RoomMemberJoinedEvent` | `connecthub.room.member.joined` | Notification-Service |
| `RoomMemberLeftEvent` | `connecthub.room.member.left` | Notification-Service |
| `RoomUpdatedEvent` | `connecthub.room.updated` | Notification-Service |

### Inbound Events (consumed by BackgroundService)
| Event | Queue | Source | Action |
|---|---|---|---|
| `RoomMessageSentEvent` | `connecthub.room.message.sent` | **UC2 Message-Service** | Updates `LastMessageContent` + `LastMessageAt` on ChatRoom |
| `UserDeactivatedEvent` | `connecthub.user.deactivated` | **UC1 Auth-Service** | Removes user from all active room memberships |

---

## SignalR Events (RoomHub at `/hubs/rooms`)

### Client → Hub
| Method | Parameters | Description |
|---|---|---|
| `JoinRoom` | roomId | Join SignalR group + DB membership if new |
| `LeaveRoom` | roomId | Leave SignalR group + DB soft-leave |
| `TypingInRoom` | roomId, isTyping | Typing indicator to others in room |
| `NotifyRoomUpdated` | roomId, name, description | Broadcast room update to group |
| `NotifyMemberRoleChanged` | roomId, userId, newRole | Broadcast role change |
| `NotifyMemberRemoved` | roomId, removedUserId | Broadcast member removal |

### Hub → Client
| Event | Triggered By |
|---|---|
| `UserJoinedRoom` | User joins room |
| `UserLeftRoom` | User leaves room |
| `RoomUpdated` | Room details changed |
| `MemberRoleChanged` | Admin changes a member's role |
| `MemberRemoved` | Admin/Moderator removes a member |
| `RoomTypingIndicator` | User typing in room |
| `UserConnected` | User connects to hub |
| `UserDisconnected` | User disconnects from hub |

---

## REST Endpoints

### ChatRoomController (`/api/rooms`)
| Method | Route | Role | Description |
|---|---|---|---|
| POST | `/` | User | Create room (creator becomes ADMIN) |
| GET | `/{id}` | User/Member | Get room details |
| PUT | `/{id}` | Room ADMIN | Update name/description/type |
| DELETE | `/{id}` | Room ADMIN | Soft-delete room |
| GET | `/public` | User | Browse public rooms (paginated) |
| GET | `/search?q=` | User | Search rooms by name/description |
| GET | `/my` | User | Get user's rooms |
| POST | `/{id}/join` | User | Join public room |
| POST | `/{id}/leave` | Member | Leave room |
| GET | `/{id}/members` | Member | List members with roles + dates |
| PUT | `/{id}/members/{userId}/role` | Room ADMIN | Change member role |
| DELETE | `/{id}/members/{userId}` | ADMIN/MODERATOR | Remove member |
| POST | `/{id}/invite` | ADMIN/MODERATOR | Invite user to private room |
| PUT | `/invites/{id}/respond` | Invited User | Accept or decline invite |
| GET | `/invites/pending` | User | Get pending invites |

### AdminChatRoomController (`/api/admin/rooms`) — Admin role only
| Method | Route | Description |
|---|---|---|
| GET | `/` | All rooms platform-wide |
| DELETE | `/{id}` | Admin delete any room |
| GET | `/{id}/members` | Members of any room |

---

## Setup

### Prerequisites
- .NET 8 SDK
- SQL Server / SQL Server Express
- RabbitMQ (default: localhost:5672)

### Connection String
```json
"ConnectionStrings": {
  "ChatRoomDb": "Server=localhost\\SQLEXPRESS;Database=ConnectHub_ChatRoom;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

### JWT — Must match UC1 and UC2
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
Swagger UI: http://localhost:5002/swagger

---

## EF Core Indexes
- `IX_ChatRooms_RoomType_IsActive` — browse public rooms
- `IX_ChatRooms_CreatedByUserId` — user's created rooms
- `IX_RoomMembers_RoomId_UserId` (unique) — membership lookup
- `IX_RoomMembers_RoomId_Role` — admin/moderator checks
- `IX_RoomMembers_UserId_IsActive` — user's active memberships
- `IX_RoomInvites_RoomId_UserId_Status` — pending invite check
- `IX_RoomInvites_InvitedUserId_Status` — user's pending invites

---

*Confidential | ConnectHub Platform | Internal Use Only*
