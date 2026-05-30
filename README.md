# 🐦 TweetWebApp — Production-Grade Twitter Clone REST API

> A fully functional, security-hardened social media REST API built with **.NET 10** and **Entity Framework Core**, featuring stateless JWT authentication, cryptographically secure refresh token rotation, a dynamic personalized feed engine, and a relational social graph — all designed with clean separation of concerns and a focus on real-world backend engineering principles.

---

## 📋 Table of Contents

- [Architecture & Design](#-architecture--design)
- [Tech Stack](#-tech-stack)
- [Data Model](#-data-model)
- [Key Engineering Challenges & Solutions](#-key-engineering-challenges--solutions)
- [API Documentation](#-api-documentation--openapi--scalar)
- [API Endpoints Reference](#-api-endpoints-reference)
- [How to Run](#-how-to-run-locally)

---

## 🏗️ Architecture & Design

This project follows a clear **N-Tier / MVC separation of concerns**, structured around three distinct, non-overlapping layers:

```
┌─────────────────────────────────────────┐
│         Presentation Layer              │
│   Controllers  ·  DTOs  ·  Middleware   │
├─────────────────────────────────────────┤
│           Business / Domain Layer        │
│     Models  ·  Auth Logic  ·  Helpers   │
├─────────────────────────────────────────┤
│           Data Access Layer             │
│    AppDbContext  ·  EF Core  ·  SQL     │
└─────────────────────────────────────────┘
```

**Controllers** are kept deliberately thin — they handle HTTP concerns only (request validation, response mapping, status codes). All domain logic (token generation, hashing, feed assembly, business rule enforcement) is encapsulated in private helper methods or dedicated services, ensuring each layer has a single, well-defined responsibility.

**DTOs (Data Transfer Objects)** act as a strict contract between the API surface and the outside world. Domain models are never serialized directly, which prevents accidental exposure of sensitive fields like `PasswordHash` and decouples the database schema from the API contract.

**EF Core Fluent API** is used exclusively for schema configuration — no `[DataAnnotation]` clutter in the models. Composite primary keys, unique indexes, `OnDelete` cascade rules, and max-length constraints are all expressed in `AppDbContext.OnModelCreating()`, keeping the domain models clean.

---

## 🛠️ Tech Stack

| Category | Technology |
|---|---|
| **Framework** | .NET 10 (ASP.NET Core Web API) |
| **ORM** | Entity Framework Core 10 |
| **Database** | Microsoft SQL Server |
| **Authentication** | JWT Bearer (Microsoft.IdentityModel.Tokens) |
| **Password Hashing** | BCrypt.Net-Next |
| **API Documentation** | OpenAPI 3 + Scalar UI |
| **Migration Tool** | EF Core Code-First Migrations |

---

## 🗃️ Data Model

The relational schema is designed around five core entities with carefully considered relationships and constraints:

```
Users ──────┐
 │          │
 │ 1:N      │ M:N (composite PK)
 ▼          ▼
Tweets     Follows (FollowerId, FollowingId)
 │
 │ M:N (composite PK)
 ▼
Likes (UserId, TweetId)

Users
 │ 1:N
 ▼
RefreshTokens
```

**Key schema decisions:**
- **`Likes` and `Follows`** use **composite primary keys** `(UserId, TweetId)` and `(FollowerId, FollowingId)` respectively — eliminating the need for a surrogate key and enforcing uniqueness at the database level rather than the application layer.
- **`User.Email` and `User.Username`** are enforced as unique via dedicated database indexes (`HasIndex(...).IsUnique()`), preventing race conditions during concurrent registrations.
- **`OnDelete` behaviors** are explicitly defined: tweets cascade-delete with their author, while likes use `Restrict` to prevent orphaned constraint violations in SQL Server's multi-path delete detection.

---

## 🔬 Key Engineering Challenges & Solutions

### Challenge 1 — Stateless Authentication with Short-Lived JWTs & Secure Refresh Token Rotation

**Situation:** Standard JWT-only authentication forces a painful trade-off: long-lived tokens (bad security — a leaked token is valid for hours or days) vs. short-lived tokens (bad UX — users get logged out constantly).

**Task:** Design an authentication flow where access tokens expire quickly to limit the blast radius of a token leak, while users remain seamlessly logged in across sessions — without compromising security.

**Action:** Implemented a **dual-token architecture** with automatic rotation:

- **Access tokens** are intentionally short-lived (15 minutes), signed with HMAC-SHA256, and carry only the minimal required claims (`NameIdentifier`, `Name`, `Email`).
- **Refresh tokens** are generated using `RandomNumberGenerator.GetBytes(64)` — cryptographically secure random bytes, not guessable pseudo-random values.
- **Critically, refresh tokens are never stored in plaintext.** Before persisting to the database, each token is hashed with **SHA-256** (`SHA256.HashData`). This means even a full database breach yields zero usable tokens — the attacker only obtains pre-image-resistant hashes.
- On every `/refresh` call, the old token is **immediately revoked** and a brand-new pair is issued. The old token's hash is stored in `ReplacedByToken` for a complete **rotation audit trail**.
- **Reuse detection:** If an already-revoked or expired refresh token is presented, the system treats this as a **potential token theft scenario** and immediately revokes *all* active refresh tokens for that user, forcing a full re-login.

```csharp
// Token reuse detected — could indicate a compromised token
if (!stored.IsActive)
{
    await RevokeAllUserTokensAsync(userId);  // Scorched-earth revocation
    return Unauthorized("Refresh token expired or revoked. Please login again.");
}

// Normal rotation: revoke old, issue new pair
stored.IsRevoked = true;
stored.ReplacedByToken = newHash;  // Audit trail preserved
_db.RefreshTokens.Add(new RefreshToken { Token = HashToken(newRefreshToken), ... });
```

**Result:** A production-grade auth flow that mirrors the OAuth 2.0 refresh token rotation spec. Users enjoy persistent sessions, short-lived tokens minimize exposure windows, and a full revocation chain makes token-theft attacks immediately detectable and neutralizable.

---

### Challenge 2 — Duplicate-Action Prevention for Likes & Follows with Database-Level Enforcement

**Situation:** In a social platform with many concurrent users, naive application-layer checks (`if (alreadyLiked) return BadRequest(...)`) are vulnerable to race conditions. Two simultaneous requests from the same user can both pass the "already liked?" check before either has committed — resulting in duplicate rows and corrupted like counts.

**Task:** Design a strategy that prevents duplicate likes and duplicate follows at the correct architectural layer, without requiring distributed locks or complex transaction management.

**Action:** Solved the problem at the **schema level rather than the application level**, using **composite primary keys** as the uniqueness constraint:

```csharp
// EF Core Fluent API — composite PK acts as a unique constraint
modelBuilder.Entity<Like>(e =>
{
    e.HasKey(l => new { l.UserId, l.TweetId }); // DB-enforced uniqueness
});

modelBuilder.Entity<Follow>(e =>
{
    e.HasKey(f => new { f.FollowerId, f.FollowingId }); // Self-documenting intent
});
```

The composite PK means SQL Server itself will reject any duplicate `(UserId, TweetId)` insert at the storage engine level, regardless of application-layer race conditions. The application-layer check (`AnyAsync(...)`) is retained for clean, human-readable error responses — but it is a UX convenience, not the actual safety net. The database is the last line of defense and is always consistent.

**Result:** Idempotency guaranteed by the database. No surrogate keys wasting storage. No possibility of corrupted counts from race conditions. The schema design itself communicates business intent — a user can only like a tweet once, by definition.

---

### Challenge 3 — Personalized Feed Engine with Efficient Social Graph Traversal

**Situation:** Generating a user's personalized feed is one of the most query-intensive operations in any social platform. A naive approach — fetching all tweets and filtering in memory — is catastrophically non-scalable. Even fetching all followed-user IDs in a loop (N+1 queries) degrades linearly with the number of follows.

**Task:** Build a feed query that traverses the social graph (the `Follows` table) and returns a correctly sorted, paginated, enriched feed in a **single round-trip** to the database.

**Action:** The query is designed as a single, pipeline-optimized LINQ chain that EF Core translates into one efficient SQL statement:

```csharp
// Step 1: Materialize the social graph for this user — one query
var followingIds = await _db.Follows
    .Where(f => f.FollowerId == myId)
    .Select(f => f.FollowingId)
    .ToListAsync();

followingIds.Add(myId); // Include own tweets in the feed

// Step 2: Single query — filter, join, sort, paginate, and project
var tweets = await _db.Tweets
    .Include(t => t.User)   // Eager load author (avoids N+1)
    .Include(t => t.Likes)  // Eager load likes (for count + IsLikedByMe)
    .Where(t => followingIds.Contains(t.UserId))
    .OrderByDescending(t => t.CreatedAt)
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync();
```

The response envelope also includes pagination metadata (`TotalCount`, `TotalPages`, `Page`, `PageSize`) so clients can implement infinite scroll or page navigation without additional API calls.

**Additional Design Consideration — `IsLikedByMe` Personalization:** The `TweetResponse` DTO includes an `IsLikedByMe` boolean computed server-side from the already-loaded `Likes` collection. This prevents the client from needing to maintain local like-state or make additional requests per tweet — the feed response is immediately renderable.

**Result:** A feed query that scales horizontally with the number of follows, executes in a bounded number of database round-trips (two: one for the social graph, one for the paginated feed), and returns a fully enriched, client-ready response.

---

### Challenge 4 — Resource Authorization: Preventing Tweet Deletion by Non-Owners

**Situation:** Standard `[Authorize]` middleware only verifies that a request is *authenticated* — it does not verify that the authenticated user has permission to operate on a *specific resource*. Without explicit resource-level authorization, any authenticated user could delete any tweet by simply knowing its ID.

**Task:** Enforce ownership-based authorization on mutable tweet operations without introducing a separate authorization framework.

**Action:** After confirming the tweet exists, the controller extracts the `userId` from the JWT claims and compares it against `tweet.UserId` before allowing the delete. No additional framework needed — the JWT itself is the source of truth.

```csharp
[HttpDelete("{id:int}")]
[Authorize]
public async Task<IActionResult> DeleteTweet(int id)
{
    var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    var tweet = await _db.Tweets.FindAsync(id);
    if (tweet == null) return NotFound();

    // Resource-level ownership check — not just authentication
    if (tweet.UserId != userId)
        return Forbid(); // 403, not 401 — the distinction matters

    _db.Tweets.Remove(tweet);
    await _db.SaveChangesAsync();
    return Ok();
}
```

Returning `403 Forbidden` (not `401 Unauthorized`) is an intentional, spec-correct choice: the user *is* authenticated, but they are not *authorized* for this specific resource. This distinction is important for client-side error handling.

**Result:** Zero chance of cross-user resource manipulation. The authorization check is co-located with the action it protects, making it immediately auditable. No hidden policy configuration required.

---

## 📖 API Documentation — OpenAPI & Scalar

The API is fully documented via **OpenAPI 3.0**, integrated directly into the ASP.NET Core pipeline via `AddOpenApi()`. In the development environment, interactive documentation is served at:

```
https://localhost:{port}/scalar/v1
```

The OpenAPI document is globally configured with **JWT Bearer authentication**, meaning the Scalar UI presents an "Authorize" mechanism out of the box. All secured endpoints are automatically flagged in the documentation — no manual annotation required per-endpoint.

> The OpenAPI spec is generated at runtime from the actual route definitions and controller signatures, ensuring the documentation is always in sync with the code.

---

## 📡 API Endpoints Reference

### Authentication — `/api/auth`

| Method | Endpoint | Auth Required | Description |
|---|---|---|---|
| `POST` | `/api/auth/register` | ❌ | Register a new user, receive JWT + refresh token |
| `POST` | `/api/auth/login` | ❌ | Login with email/password, receive JWT + refresh token |
| `POST` | `/api/auth/refresh` | ❌ | Exchange a valid refresh token for a new token pair |
| `POST` | `/api/auth/logout` | ❌ | Revoke a refresh token |

### Tweets — `/api/tweets`

| Method | Endpoint | Auth Required | Description |
|---|---|---|---|
| `POST` | `/api/tweets` | ✅ | Create a new tweet |
| `GET` | `/api/tweets/{id}` | ⚪ Optional | Get a single tweet by ID |
| `GET` | `/api/tweets/user/{userId}` | ⚪ Optional | Get all tweets by a user (paginated) |
| `DELETE` | `/api/tweets/{id}` | ✅ (owner only) | Delete own tweet |

### Likes — `/api/tweets/{tweetId}`

| Method | Endpoint | Auth Required | Description |
|---|---|---|---|
| `POST` | `/api/tweets/{tweetId}/like` | ✅ | Like a tweet |
| `DELETE` | `/api/tweets/{tweetId}/like` | ✅ | Unlike a tweet |

### Social Graph — `/api/users/{targetUserId}`

| Method | Endpoint | Auth Required | Description |
|---|---|---|---|
| `POST` | `/api/users/{targetUserId}/follow` | ✅ | Follow a user |
| `DELETE` | `/api/users/{targetUserId}/follow` | ✅ | Unfollow a user |

### Feed — `/api/feed`

| Method | Endpoint | Auth Required | Description |
|---|---|---|---|
| `GET` | `/api/feed?page=1&pageSize=20` | ✅ | Get personalized paginated feed |

---

## 🚀 How to Run Locally

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (LocalDB, Express, or Developer Edition)
- A SQL Server connection string

### Steps

**1. Clone the repository**

```bash
git clone https://github.com/your-username/TweetWebApp.git
cd TweetWebApp
```

**2. Configure `appsettings.json`**

Open `appsettings.json` and update the following sections with your own values:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=TweetDb;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "JwtSettings": {
    "SecretKey": "YOUR_SECRET_KEY_MINIMUM_32_CHARS_LONG",
    "Issuer": "TweetWebApp",
    "Audience": "TweetWebAppUsers",
    "RefreshTokenDays": "7"
  }
}
```

> ⚠️ **Security Note:** Never commit a real `SecretKey` to source control. Use `dotnet user-secrets` or environment variables in production.

**3. Apply EF Core Migrations**

This will create the database schema automatically from the migration history:

```bash
dotnet ef database update
```

**4. Run the application**

```bash
dotnet run
```

**5. Open the API documentation**

Navigate to the Scalar interactive docs in your browser:

```
https://localhost:{port}/scalar/v1
```

Use the "Authorize" button, paste your JWT from the `/api/auth/login` response, and start exploring the API.

---

## 🔐 Security Considerations

- **Passwords** are hashed with BCrypt (adaptive work factor) — never stored or logged in plaintext.
- **Refresh tokens** are stored as SHA-256 hashes — a database breach yields no usable tokens.
- **JWT secrets** are externalized via configuration — not hardcoded anywhere in the source.
- **Access tokens** are intentionally short-lived (15 min) to minimize the impact of token leakage.
- **Reuse detection** triggers full session revocation on suspicious refresh token activity.
- **Resource authorization** is enforced at the controller level — ownership is verified before any mutation.

---

<p align="center">
  Built with ❤️ using .NET 10 · EF Core · SQL Server · JWT
</p>
