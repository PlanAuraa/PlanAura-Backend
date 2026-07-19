# PlanAura Admin Dashboard — Implementation Review

**Reviewer:** Senior .NET Backend Engineer / Technical Reviewer (audit only — no features implemented)
**Reviewed against:** `AdminDashboardPlan.md` (dated 2026-07-17)
**Review date:** 2026-07-18
**Branch:** `admin-dashboard` (local working tree, with uncommitted changes — see §4 and §7)
**Method:** Full source read of every controller/service/specification/DTO/migration touching the admin surface, a live `dotnet` run of the API against the real local SQL Server database (via Visual Studio, since the review sandbox cannot reach `DESKTOP-UH4OOGH\SQLEXPRESS` or build .NET 9), and direct HTTP testing of every admin endpoint through the running Swagger instance (JWT login, then `fetch()` calls executed in-browser against `https://localhost:7123`).

**Scope note:** This review covers the **backend only** (`PlanAura-Backend`), per the access granted. The plan document also specifies an Angular frontend; frontend status referenced below is taken from the plan's own claims and was not independently re-verified.

---

## 1. Executive Summary

The plan document, although dated one day before this review, is already out of date: a meaningful slice of backend work — an admin dashboard statistics endpoint and a full dispute-resolution backend (list/detail/resolve) — has been built since the plan's analysis. None of it is committed to git. It exists only in the local working tree (`git status` shows `AdminBookingController.cs`, `AdminDashboardController.cs`, and their services/specs/models as **untracked**, alongside a service-folder reorganization touching Auth, Booking, Category, AI Chat, and the Hangfire job as **modified-but-uncommitted**). This is the single highest-priority operational finding in this review: real, working progress is one `git clean -fd` or lost machine away from disappearing, and it isn't on `origin/admin-dashboard` for anyone else to see or build on.

Functionally, the admin backend today covers four areas end-to-end and verified live: vendor-verification review (list pending, approve, reject, view detail/history), account suspension/reactivation (with genuinely well-built immediate session invalidation — confirmed live, a still-valid JWT is rejected the instant an admin suspends the account), a flat dashboard-statistics endpoint, and a dispute list/detail/resolve flow. Everything else the plan calls for — vendor/client/booking management beyond disputes, payments and refunds, review moderation (the base review feature doesn't exist for any role), analytics/reports (all 26 chart endpoints), notification broadcast, admin-account management, and audit logging — is still entirely unbuilt, matching the plan's own gap list.

One live-confirmed security bug carries over unfixed from the plan: `ServiceCategoriesController`'s mutating endpoints (`POST`/`PUT`/`DELETE`) are reachable by any authenticated client or vendor, not just admins. I proved this by creating and deleting a category with a freshly registered client account — no admin role required. The plan flagged this as **High** priority a day ago; it is still open. A second, newly-found issue in the uncommitted dispute code: `AdminDisputeDetailsDto` never surfaces the actual dispute reason (only the booking's original client message and vendor response, which predate the dispute) — the developer even left a `// there is an a bug` comment on the surrounding controller action, which lines up with this gap. There's also a hardcoded seed-admin password (`Admin@12345`) committed in plaintext to `appsettings.json`.

Code quality on what exists is generally reasonable — clean separation of controller/service/specification/repository, correct use of `IUnitOfWork`, typed exceptions, DTOs that don't leak entities — but is undercut by pervasive, unprofessional rough edges that made it into shared, production-facing code: every custom exception class is misspelled (`NotFoundExeption`, `BadRequestExeption`, `UnAuthorizedExeption`), the validation-error DTO's array field is literally named `Erroes`, and the default API error messages are Star Wars/Yoda-speak jokes ("Authorized , you are not", "Errors are the path to the dark side...") that are returned verbatim to real API callers, not just used in tests.

**Overall completion estimate (backend, weighted across the plan's 15 in-scope pages, Settings excluded as explicitly deferred): ~30%.**

**Is it production-ready?** No. Beyond the missing 70% of scope, the one fully "done" admin surface that's public-facing in effect (`ServiceCategoriesController`) has a live, exploitable authorization hole, and there is no audit trail for any admin action taken today (suspensions, rejections, dispute resolutions all happen with no unified log). Shipping this as-is would mean any logged-in client or vendor can mutate platform category data, and no admin action is currently traceable to who did it after the fact outside the narrow vendor-verification history.

---

## 2. Features Status

| Feature (plan §2.x) | Status | Notes |
|---|---|---|
| 2.1 Dashboard Overview | 🟡 Partial | `GET /api/admin/dashboard/statistics` exists and is accurate (live-verified), but only returns 7 flat counts. No open-disputes count, no revenue-by-month, no recent-activity feed, no charts (0 of the 26 metrics in plan §3 beyond the totals piggybacked here). Not built or committed at the time the plan was written. |
| 2.2 Pending Vendor Approvals | ✅ Complete | `GET .../pending`, `POST .../approve`, `POST .../reject` all live-tested and correct. Only gap: no server-side paging (returns full unpaged array), matching the plan's own "nice-to-have." |
| 2.3 Vendor Details | 🟡 Partial | `GET .../{vendorId}` and `.../history` work correctly (live-tested, including a full reject cycle). `VendorDetailsDto` still lacks `UserId`, `AvgRating`, `TotalReviews`, `TotalCompletedBookings` — confirmed via `git diff`, the file was touched but only for an unrelated nullability fix. No "promote to Trusted" endpoint exists anywhere (confirmed by full-repo grep). |
| 2.4 Vendor Management (All Vendors) | ❌ Not Started | No `GET /api/admin/vendors` list endpoint. `AllVendorsSpecification` exists but is only used internally for the dashboard's `TotalVendors` count, not exposed as a list. |
| 2.5 Client Management | ❌ Not Started | No `GET /api/admin/clients` or detail endpoint. `AllClientsSpecification` exists, same situation as above — count-only, not a browsable list. |
| 2.6 Booking Management | ❌ Not Started | No general `GET /api/admin/bookings` or `GET /api/admin/bookings/{id}`. Only the dispute-scoped subset (2.7) exists. |
| 2.7 Dispute Resolution | 🟡 Partial / 🚨 Buggy | Backend now exists (`AdminBookingController` + `AdminBookingService`, uncommitted) — list, detail, resolve all live-tested and functionally work. But: `AdminDisputeDetailsDto` never surfaces the actual dispute reason (see §7, bug #2); `GetOpenDisputesAsync` is hardcoded to `Open` only (plan's `status=open|resolved` filter doesn't exist, so resolved disputes are invisible after the fact); `ResolveDisputeDto` has no `RefundClient` option, so the plan's "resolve + optional refund" flow is unreachable (refund infra doesn't exist at all yet, consistent with §5's payment gap). |
| 2.8 Categories | 🚨 Buggy | CRUD itself works correctly (live-tested create/delete), but the controller is still `[Authorize]` only, not `AdminOnly` — **live-confirmed**: a plain client account successfully created and deleted a category. This is the plan's flagged High-priority gap, still open a day later. |
| 2.9 Payments & Transactions | ❌ Not Started | No admin payment list, summary, or refund endpoint. `IPaymentGatewayService` still has no `RefundPaymentIntentAsync` prerequisite method. |
| 2.10 Reviews & Moderation | ❌ Not Started | Confirmed via migration: `reviews`/`review_responses` tables and FKs exist in the very first migration, but zero DTO/service/controller for any role — clients can't submit reviews, vendors can't respond, admins have nothing to moderate. This is a full feature build, not an admin-layer addition. |
| 2.11 Reports | ❌ Not Started | None of the ~12-26 analytics endpoints in plan §3 exist. |
| 2.12 Notifications | 🟡 Partial | Inbox (`GET/POST /api/notifications`, mark-read) works and is reused correctly for any role including admin. `NotifyRoleAsync` exists in the service layer but has no controller — no broadcast endpoint exists. |
| 2.13 Admin Accounts | 🟡 Partial | Suspend/reactivate work and are live-tested end-to-end, including confirming immediate mid-session lockout. No list/create-admin endpoints. No "last admin" or self-suspend guard exists in `AccountAdminService` — confirmed by reading the full class, there is no such check. |
| 2.14 Audit Logs | ❌ Not Started | No `AdminAuditLog` entity, no migration, no endpoint. The only trails that exist are `VendorVerificationHistory` (scoped to verification) and `BookingStatusHistory` (scoped to bookings, and re-used to store dispute-flag notes as free text). |
| 2.15 Settings | ❌ Not Started (deferred by design) | Correctly out of scope per the plan — no `PlatformSettings` data model exists to back it. Not counted against the completion score. |
| 2.16 Admin Profile | 🟡 Partial | Read works "for free" via `GET /api/auth/me`. No profile-update or password-change endpoint exists for any role, admin included. |

---

## 3. API Review

Every row below was exercised live against the running instance (`https://localhost:7123`) using an admin JWT obtained via `POST /api/auth/login` with the seeded admin account, except where noted.

| Route | Method | Authorization | Request | Response | Tested? | Working? | Problems found |
|---|---|---|---|---|---|---|---|
| `api/admin/users/{userId}/suspend` | POST | AdminOnly | — | `AccountStatusDto` | Yes (live, on a throwaway client account) | Yes | None. Confirmed a still-valid JWT is rejected (`401`) immediately after suspension, mid-session — this is a strong, correctly-implemented control. |
| `api/admin/users/{userId}/reactivate` | POST | AdminOnly | — | `AccountStatusDto` | Yes (live) | Yes | None functionally. No guard preventing an admin from suspending the *last* admin or themselves (see §6). |
| `api/admin/vendor-verifications/approve` | POST | AdminOnly | `ApproveVendorDto` | 200 | Read/traced, not executed (would have consumed the only other pending test vendor) | Presumed working — code path is symmetric with `reject`, which was fully tested | None found in code. |
| `api/admin/vendor-verifications/reject` | POST | AdminOnly | `RejectVendorDto` | 200 | Yes (live, full cycle) | Yes | None. Verified: status transitions `pending → rejected`, `VendorVerificationHistory` row written with correct previous/new status, reviewer name, notes, timestamp; dashboard counts updated in the same request cycle. |
| `api/admin/vendor-verifications/pending` | GET | AdminOnly | — | `PendingVendorDto[]` | Yes (live) | Yes | Unpaged (returns full array) — matches plan's known gap. `categoryName` was `null` for both test vendors (data-dependent, not confirmed as a bug). |
| `api/admin/vendor-verifications/{vendorId}` | GET | AdminOnly | — | `VendorDetailsDto` | Yes (live) | Yes | Missing `UserId`/performance fields per plan §2.3 (DTO gap, not a functional bug). |
| `api/admin/vendor-verifications/{vendorId}/history` | GET | AdminOnly | — | `VendorVerificationHistoryDto[]` | Yes (live, before and after a reject) | Yes | Empty before any transition (expected — nothing to log yet), correctly populated after reject. |
| `api/admin/dashboard/statistics` | GET | AdminOnly | — | `DashboardStatisticsDto` | Yes (live, twice, before/after a reject) | Yes | `TotalVendors` (12) does not reconcile with `PendingVendors + ApprovedVendors + RejectedVendors` (10) — there's no "Unverified" bucket, so the four numbers won't visually add up on a KPI card without a 5th field. Not in the plan's DTO shape at all (plan asked for `summary`, not `statistics`, with disputes/revenue-trend/recent-activity — none of that is here). |
| `api/admin/bookings/disputes` | GET | AdminOnly | — | `AdminDisputeListItemDto[]` | Yes (live) | Yes, but incomplete | Hardcoded to `DisputeStatus.Open` only — no way to list resolved disputes (plan wanted `?status=open\|resolved`). |
| `api/admin/bookings/disputes/{bookingId}` | GET | AdminOnly | — | `AdminDisputeDetailsDto` | Yes (live, both valid and 999999) | Yes for the happy/404 path | **Never surfaces the actual dispute reason** — see Bug #2, §7. Correctly returns `404` for a nonexistent booking. |
| `api/admin/bookings/disputes/{bookingId}/resolve` | POST | AdminOnly | `ResolveDisputeDto` | 204 | Yes (live — nonexistent booking, non-open booking, missing required field) | Validation paths all correct (`404`, `400` x2) | Full "resolve an actually-open dispute" happy path not exercised (no open disputes existed in the seeded data, and manufacturing one requires a full client booking→accept→dispute lifecycle out of scope for a read-mostly audit). No `RefundClient` option in the DTO — the plan's resolve-with-refund flow is structurally impossible right now. |
| `api/ServiceCategories` (GET/POST/PUT/DELETE/by-slug) | mixed | **`[Authorize]` only — not AdminOnly** | `CreateServiceCategoryDto` / `UpdateServiceCategoryDto` (multipart) | `ServiceCategoryDto` | Yes (live) | Functionally yes, **but this is the security bug** | Live-exploited: registered a plain client, used its token to `POST` a new category (`201 Created`) and `DELETE` it (`204`) — no admin role required. Also, because the whole controller requires `[Authorize]` with no `[AllowAnonymous]` on the `GET`s, anonymous visitors can't browse categories at all, unlike the public vendor-browse endpoints. |
| `api/auth/login`, `api/auth/register/client`, `api/auth/me` | — | mixed | — | — | Yes (used as test infrastructure) | Yes | `RegisterClientDto` requires `ConfirmPassword`, which isn't obvious from a first bad request — the resulting `400` at least names the missing field correctly. |

**Not admin-facing at all yet (confirmed absent from the live Swagger schema):** `GET /api/admin/vendors`, `GET /api/admin/clients`, `GET /api/admin/bookings` (general), `GET/POST /api/admin/payments*`, any `/api/admin/reviews*`, any `/api/admin/reports/*`, `POST /api/admin/notifications/broadcast`, `GET/POST /api/admin/admins`, `GET /api/admin/audit-logs`, `PUT /api/auth/me`, any change-password endpoint, `POST /api/admin/vendors/{id}/trust`.

---

## 4. Code Review

**Controllers.** Thin and consistent — each admin controller delegates immediately to a single injected service interface, uses `[Authorize(Policy = AuthorizationPolicies.AdminOnly)]` correctly (except `ServiceCategoriesController`), and returns appropriate `ActionResult<T>` types. `AdminBookingController` has one real code smell: a bare, unexplained comment `// there is an a bug` sitting directly above `GetDisputeDetailsAsync` — left in the code, not resolved, not tracked as a ticket anywhere I could find. It also has an inconsistent brace/spacing style (`public async Task<IActionResult>ResolveDisputeAsync(...)` — missing space before the method name) that the rest of the codebase doesn't share, suggesting it was hand-typed quickly rather than through the same formatting pass as older controllers.

**Services.** `AccountAdminService` is a good small example — a two-line public surface (`SuspendAsync`/`ReactivateAsync`) both routed through one private `SetActiveAsync`, with an idempotency check (skips the `UpdateAsync` call if the state is already correct) and a clear XML doc comment explaining *why* suspension is enforced immediately (ties it to the JWT `OnTokenValidated` handler). `AdminDashboardService` and `AdminBookingService` are similarly small and readable. The main service-layer inconsistency: some services build response DTOs via AutoMapper (`AdminBookingService` maps `BookingRequest → AdminDisputeListItemDto`), while others (`VendorVerificationService.GetVendorDetailsAsync`) hand-construct the DTO with `new VendorDetailsDto { ... }` inline. Neither approach is wrong, but the mix means a future engineer can't predict which pattern a given service uses without opening the file.

**Specifications.** Clean, single-purpose, and named for exactly what they filter (`PendingVendorCountSpecification`, `OpenDisputesSpecification`, etc.). One genuine smell: `AllVendorsSpecification`, `AllClientsSpecification`, and `AllBookingRequestsSpecification` all exist solely to satisfy `IGenericRepository<T>.GetCountAsync(ISpecification<T> spec)` with a no-op `base(x => true)` filter — three files whose entire purpose is working around the repository contract not having a plain `CountAllAsync()` overload. This is a missing abstraction, not a bug.

**Repositories.** `GenericRepository<TEntity, TKey>` is a textbook generic repository — spec-based querying, tracking toggles, count/sum helpers — and applies specifications consistently through one private `ApplySpecifications` method. No issues found.

**DTOs.** Generally tight (no entity leakage), but two DTOs carry fields that don't get populated meaningfully today: `AdminDisputeDetailsDto.ClientMessage`/`VendorResponse` map by AutoMapper convention straight from `BookingRequest`, but those fields hold the *original booking request's* message/vendor-response text, not anything dispute-specific — see Bug #2. `VendorDetailsDto` is missing the fields the plan explicitly asked for (§2.3) despite the file showing as modified in git.

**AutoMapper.** `MappingProfile` is small and readable, but notably *doesn't* cover the DTOs that are hand-built in services (`VendorDetailsDto`, `DashboardStatisticsDto`, `PendingVendorDto`), so "which DTOs go through AutoMapper" isn't a rule you can state simply — see the services note above.

**Validation.** Data-annotation based (`[Required]`, `[MaxLength(500)]` on `ResolveDisputeDto`, `RejectVendorDto`) and enforced correctly — live-tested (`ResolveDisputeDto` without `ResolutionNotes` correctly returns `400` naming the field). `ApiValidationErrorResponse` (the shape every validation failure returns) has a real, permanent typo: the array property is named **`Erroes`**, not `Errors` — this isn't a serialization quirk, it's the literal C# property name, so it's baked into the public API contract every frontend has to consume.

**Dependency Injection.** `ApplicationServiceCollectionExtensions.AddApplicationServices` correctly registers every service referenced by the controllers I found, including the three new/uncommitted admin services (`IAccountAdminService`, `IAdminDashboardService`, `IAdminBookingService`) — the app would not start with a DI resolution failure; verified this directly by starting the app in the debugger. No dead registrations or missing registrations found.

**Cross-cutting naming issue.** Every custom exception type in `Planura.Shared.Errors.Models` is misspelled: `NotFoundExeption`, `BadRequestExeption`, `UnAuthorizedExeption` (missing the "c" in "Exception"). This is used everywhere — dozens of call sites across every service — so it's not a typo to casually rename, but it is a real, pervasive quality issue in a shared library referenced by the entire application.

**Unprofessional default messages (found live, not hypothetical).** `ApiResponse.GetDefaultMessageForStatusCode` returns, verbatim, to real API responses:
- `400 → "A Bad Request ,you have made"`
- `401 → "Authorized , you are not"`
- `500 → "Errors are the path to the dark side , Errors lead to anger .Anger lead to hate . Hate lead to career change "`

These are Yoda/Star-Wars jokes left in a code path that every unauthenticated or malformed request actually hits — confirmed by triggering a real `401` and a real `400` against the live server during testing. Harmless functionally, but not appropriate for anything beyond a personal side project, and worth a deliberate decision (keep as an inside joke, or replace) rather than leaving it by default.

---

## 5. Database Review

Schema was verified by reading every EF Core migration (`initial-Set`, `VendorAvailability_Concurrency_Enum`, `AddVendorVerificationDocuments`, `Initial1`, `AddBookingPaymentAndSlotHolds`, `AddPaymentReminderSentAt`, `AddHoldExpiresAt`, `AddPaymentAuthorizationFields`, `AddAiChatConversations`) and cross-checked against live behavior through the running API (row counts changing correctly after a reject action, dashboard aggregates updating in the same request cycle).

**Tables present (confirmed via migrations):** `users`, `roles`, `user_roles`, `user_claims`, `user_logins`, `user_tokens`, `role_claims` (ASP.NET Identity), `clients`, `vendors`, `service_categories`, `vendor_verifications`, `vendor_verification_history`, `vendor_verification_documents`, `vendor_packages`, `vendor_availability`, `portfolio_media`, `portfolio_links`, `event_plans`, `event_plan_items`, `booking_requests`, `booking_status_history`, `payments`, `reviews`, `review_responses`, `notifications`, `ai_chat_conversations`, `ai_chat_messages`, `ai_event_visualizations`, `ai_invitations`.

**Relationships/FKs:** All FKs the plan describes exist and are correctly declared, including the ones relevant to admin work: `vendor_verifications.reviewed_by_admin_id → users`, `vendor_verification_history.changed_by_admin_id → users`, `booking_status_history.changed_by_user_id → users`, `reviews`/`review_responses` FKs to `booking_requests`/`clients`/`vendors`. The dispute columns (`dispute_status`, `disputed_at`, `disputed_by_user_id`, `resolution_notes`, `resolved_at`, `resolved_by_admin_id`) were added in `AddBookingPaymentAndSlotHolds` directly on `booking_requests`, matching the plan's description of a "fully-modeled but never-written" dispute block — it's now written to, via the uncommitted `AdminBookingService`.

**Constraints:** Indexes exist on the columns admin queries actually filter by (`ix_vendors_verification_status`, `ix_booking_requests_status`, `ix_vendors_city`), which is good practice ahead of the "All Vendors"/"All Bookings" list endpoints the plan still wants built. `VendorAvailability` has an explicit `RowVersion`/concurrency setup (own migration, `VendorAvailability_Concurrency_Enum`) plus a named unique-ish constraint `idx_vendor_availability_no_double_hold` guarding against double-booking a slot — a real, deliberate integrity control.

**Seed data:** `IdentityDataSeeder` seeds exactly one admin (`admin@planura.local`) idempotently from `Seed:Admin` config — confirmed by using these exact credentials to log in live. This account's password (`Admin@12345`) is stored in plaintext in `appsettings.json`, which is tracked in git (confirmed via `git show HEAD:...`) — see §6.

**Missing:** No `AdminAuditLog` table/migration anywhere (matches plan). No `PlatformSettings` table (matches plan, correctly deferred).

**Live-verified data integrity behavior:** rejecting a vendor correctly moved it from `pending` to `rejected` in one transaction, wrote a matching `vendor_verification_history` row with accurate previous/new status and reviewer identity, and the dashboard's aggregate counts reflected the change on the very next request — no caching/staleness issue observed.

---

## 6. Security Review

**Authentication.** JWT bearer auth, `OnTokenValidated` re-checks `IsActive` against the DB on every request — confirmed live: suspending an account instantly invalidates its still-unexpired token (`401` on the next call with the same JWT). This is a genuinely strong control most systems get wrong (many only check at login time).

**Authorization / Admin policies.** `AuthorizationPolicies.AdminOnly` (`RequireRole("admin")`) is applied correctly on every admin controller I tested **except** `ServiceCategoriesController`, which is `[Authorize]` only. **Live-exploited during this review:** a freshly registered client account (`role: client`, no elevated privileges) successfully called `POST /api/ServiceCategories` (`201 Created`) and `DELETE /api/ServiceCategories/{id}` (`204 No Content`). Any authenticated client or vendor can create, edit, or delete platform-wide service categories today. **Severity: High — live-confirmed, not theoretical.**

**Role checks / ownership validation.** Everywhere else I tested (dashboard statistics, dispute endpoints, vendor-verification endpoints), a non-admin token correctly received `403`. No ownership-bypass issues found in the admin surface itself (these endpoints are intentionally global/admin-scoped, so "ownership" doesn't apply the way it would to, say, a vendor editing another vendor's package).

**Missing guardrails.** `AdminAccountsController`'s suspend/reactivate endpoints accept any `userId`, including another admin's or the caller's own — there is no "can't suspend the last admin" or "can't suspend yourself" check anywhere in `AccountAdminService`. With only one admin seeded today this is low-likelihood, but it's a real gap the plan itself calls out (§2.13) as needing to be added before an "Admin Accounts" page ships.

**Input validation.** Data-annotation validation is applied and enforced correctly on the DTOs I tested (`ResolveDisputeDto`, `RejectVendorDto`, registration DTOs) — confirmed via live `400` responses naming the exact missing/invalid field.

**Secrets.** `Seed:Admin:Password` (`Admin@12345`) is stored in plaintext in `Planura.Apis/appsettings.json`, which is committed to git (present in `HEAD`, not just the working tree). This is a real, low-effort-to-fix issue: move it to user-secrets/environment variables/a vault, and rotate the seeded password since it's already been exposed in history.

**CORS.** `AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()` — confirmed directly in `Program.cs`. Since auth is bearer-token (not cookies), this doesn't create a classic CSRF hole, but it's a maximally permissive baseline for a system that's about to gain its most sensitive surface (an admin dashboard) and is worth tightening to a known origin list before that ships.

**Swagger exposure.** Correctly gated behind `app.Environment.IsDevelopment()` — confirmed by reading `Program.cs`; not reachable in a non-Development environment as configured.

**Dangerous endpoints.** No destructive admin endpoint I found (suspend, reject, resolve-dispute) skips confirmation-worthy validation, but none of them write to any audit trail either except vendor-verification actions (which get `VendorVerificationHistory`) — a suspend, reactivate, or dispute-resolution action today leaves no record of *which admin* did it beyond what's in `ResolvedByAdminId` on the row itself (no timestamped, human-readable log an admin team could review later). This compounds the "no audit log" gap already known from the plan.

---

## 7. Bugs Found

| Severity | Location | Description | Suggested Fix |
|---|---|---|---|
| High | `ServiceCategoriesController` (class-level `[Authorize]`) | Any authenticated client or vendor can create/edit/delete service categories — live-exploited during this review. | Change class attribute to `[Authorize(Policy = AuthorizationPolicies.AdminOnly)]`; add `[AllowAnonymous]` to the `GET` actions if public browsing is intended (currently even reads require login, unlike other public browse endpoints). |
| Medium | `AdminBookingService.GetDisputeDetailsAsync` / `AdminDisputeDetailsDto` | The dispute-details DTO exposes `ClientMessage`/`VendorResponse`, which AutoMapper convention-maps straight from `BookingRequest` — these are the *original booking request's* message/response, not the dispute reason. The actual dispute reason (captured as free text in `BookingStatusHistory.Notes`, e.g. `"Dispute raised: {reason}"`) is never surfaced anywhere in the admin dispute view. An admin reviewing a dispute currently cannot see why it was raised without querying the DB directly. Matches the developer's own `// there is an a bug` comment left on the surrounding controller action. | Add a `DisputeReason` field to `AdminDisputeDetailsDto`, sourced either from a new dedicated column on `BookingRequest` (cleaner) or by joining the relevant `BookingStatusHistory` row and parsing/storing the reason text explicitly. |
| Medium | `ResolveDisputeDto` / `ResolveDisputeAsync` | No `RefundClient` option exists, so the plan's "resolve + optionally refund" workflow can't be built without first adding the refund gateway method (`IPaymentGatewayService.RefundPaymentIntentAsync`, still missing) and threading it through here. | Track as a dependency: build the refund gateway method first, then extend the resolve DTO/flow. |
| Low | `AdminBookingService.GetOpenDisputesAsync` | Hardcoded to `DisputeStatus.Open`; the plan's `?status=open|resolved` filter doesn't exist, so once a dispute is resolved it disappears from any admin list view. | Add a status query parameter and a corresponding specification variant. |
| Low | `AdminDashboardService` / `DashboardStatisticsDto` | `PendingVendors + ApprovedVendors + RejectedVendors` does not equal `TotalVendors` (no "Unverified" bucket) — confirmed live (12 total vs. 10 across the three buckets). | Add an `UnverifiedVendors` count, or clearly document that `TotalVendors` isn't meant to reconcile with the status buckets. |
| Low | `ApiValidationErrorResponse.Erroes` | Public API field name is misspelled ("Erroes" instead of "Errors") — a permanent part of the JSON contract every client must consume. | Rename to `Errors` (breaking change — coordinate with frontend before fixing). |
| Low | `Planura.Shared.Errors.Models.*` | All three custom exception classes are misspelled: `NotFoundExeption`, `BadRequestExeption`, `UnAuthorizedExeption`. Message format is also grammatically broken (`"{name} with : ({key} is not Found)"` → e.g. `"Dispute with : (999999 is not Found)"`). | Low priority given the blast radius (dozens of call sites), but worth a deliberate rename pass with find/replace plus a message-format cleanup. |
| Low | `ApiResponse.GetDefaultMessageForStatusCode` | Default error messages are Yoda/Star-Wars jokes returned verbatim on real `400`/`401`/`500` responses (confirmed live: `"Authorized , you are not"`, `"A Bad Request ,you have made"`). | Replace with plain, professional default messages before any external consumer (or the real admin frontend) relies on displaying them. |
| Low | `Planura.Apis/appsettings.json` | Seeded admin password (`Admin@12345`) committed in plaintext to source control. | Move to user-secrets/environment config, rotate the password. |
| Low | `AdminAccountsController` / `AccountAdminService` | No guard against suspending the last remaining admin or self-suspending. | Add a check in `SetActiveAsync` (or the controller) before deactivating an admin account. |
| Info | Working tree (`git status`) | All new admin work (`AdminBookingController`/`Service`, `AdminDashboardController`/`Service`, related specs/models, plus a broad service-namespace reorg touching Auth/Booking/Category/AiChat/BookingHoldExpiryJob) is **uncommitted** — untracked or modified-but-not-staged. Nothing here is on `origin/admin-dashboard`. | Commit and push immediately; this is real, working, tested progress that is currently unprotected against loss. |

---

## 8. Missing Compared to the Plan

Everything below is confirmed absent from the live Swagger schema and/or the source tree, and matches (or narrows) the plan's own §5 gap table:

- `GET /api/admin/vendors` and `GET /api/admin/vendors/{vendorId}` (all-vendors admin list/detail) — not implemented; only the pending-only queue exists.
- `GET /api/admin/clients` and `GET /api/admin/clients/{clientId}` — not implemented at all.
- `GET /api/admin/bookings` and `GET /api/admin/bookings/{id}` (general cross-platform booking oversight) — not implemented; only the dispute-scoped subset exists now.
- `GET/POST /api/admin/payments*`, `POST /api/admin/payments/{id}/refund`, and the prerequisite `IPaymentGatewayService.RefundPaymentIntentAsync` — none exist.
- The entire Reviews & Moderation feature — no baseline `POST /api/reviews`, `GET /api/vendors/{id}/reviews`, or `POST /api/reviews/{id}/response` for any role, and no admin moderation layer. Tables exist in the DB; nothing above the data layer does.
- All Section-3 analytics/report endpoints (registrations trend, vendor funnel, revenue per month, top categories/vendors, rating distribution, system health, etc.) — zero exist.
- `POST /api/admin/notifications/broadcast` — the underlying `NotifyRoleAsync` service method exists but has no controller.
- `GET/POST /api/admin/admins` (list/create additional admin accounts) — not implemented; still exactly one seed-time admin.
- `AdminAuditLog` entity, migration, and `GET /api/admin/audit-logs` — none exist.
- `PUT /api/auth/me` and any password-change endpoint — not implemented for any role.
- `POST /api/admin/vendors/{vendorId}/trust` (promote Verified → Trusted) — not implemented; confirmed via full-repo grep, nothing ever sets `VerificationStatus.Trusted` or `TrustedSince`.
- `VendorDetailsDto` enhancements (`UserId`, `AvgRating`, `TotalReviews`, `TotalCompletedBookings`) and `ServiceCategoryDto.vendorCount` — neither applied, despite `VendorDetailsDto` showing as a modified file in git (the actual change was an unrelated nullability fix).
- Server-side paging on the pending-vendors list — still absent.
- `GET /api/admin/reports/export` (CSV export) — not implemented.

**Partially closed since the plan was written (uncommitted):** dispute list/detail/resolve (`AdminBookingController`), and a flat dashboard-statistics endpoint (`AdminDashboardController`) — both real, working, and live-tested, but neither is on the plan's radar because neither is committed to git.

---

## 9. Unexpected Features

Features present in the codebase that the plan does not credit as built, or doesn't cover at all:

- **`AdminBookingController` + `AdminBookingService`** (dispute list/detail/resolve) — built after the plan's analysis and not committed; the plan lists this entire area as "Not implemented — backend gap." It's now a working, if imperfect, backend.
- **`AdminDashboardController` + `AdminDashboardService`** (`GET /api/admin/dashboard/statistics`) — same situation; the plan lists Dashboard Overview backend as fully "Not implemented."
- **A real, substantial `Planura.Tests` project** — unit tests for `AuthService`, `BookingService`, `EventPlanService`, `PaymentService`, `VendorService`, `VendorVerificationService`, `BookingHoldExpiryJob`, plus controller tests for `BookingRequestsController`, `EventPlansController`, `PaymentsController`, with real test helpers (mocked `IUnitOfWork`, identity mocks, form-file factories). **This directly contradicts the plan's §1.1 claim that "No test project exercises any of this logic."** None of the new admin services (`AdminBookingService`, `AdminDashboardService`, `AccountAdminService`) have any test coverage yet, however.
- **AI Chat feature** (`AiChatController`, `AiChatService`, `ai_chat_conversations`/`ai_chat_messages` tables, `OpenAiOptions` config) — a full client-facing AI planning-assistant feature integrated with an external OpenAI-compatible API. The plan explicitly and correctly scopes this out of admin-dashboard v1, so it isn't a "gap," but it's worth flagging that there is currently **zero admin visibility or moderation** over an external-API-calling, potentially cost-incurring feature — worth a future admin consideration even if not v1.

---

## 10. Recommended Development Order

1. **Commit and push the uncommitted admin work immediately** (`AdminBookingController`/`Service`, `AdminDashboardController`/`Service`, and the related service-namespace reorg). This isn't new development — it's protecting development that already happened and currently only exists on one machine.
2. **Fix `ServiceCategoriesController`'s authorization** (`AdminOnly` on mutations, `[AllowAnonymous]` on reads if public browsing is desired). Trivial change, live-confirmed security hole, matches the plan's own #1 priority from a day ago.
3. **Fix the dispute-reason gap** in `AdminDisputeDetailsDto`/`AdminBookingService` — small, and directly unblocks the dispute-resolution feature from actually being usable (an admin can't make an informed resolve/reject decision without knowing why the dispute was raised).
4. **Rotate and relocate the seeded admin password** out of `appsettings.json` — a five-minute fix with real exposure since it's already in git history.
5. **Extend `DashboardStatisticsDto` into a true summary**: add the missing "Unverified" bucket, an open-disputes count, and a recent-activity feed — the plan's original Phase 1 items #3/#4, now half-done.
6. **Vendor Management (All Vendors) and Client Management** (plan Phase 2) — both currently zero, and the underlying specs (`AllVendorsSpecification`, `AllClientsSpecification`) already exist as a starting point (they're just count-only today).
7. **General Booking Management** (`GET /api/admin/bookings` + detail) — extends naturally from the dispute-scoped work already done in `AdminBookingController`.
8. **Payments & refund infrastructure** — start with `IPaymentGatewayService.RefundPaymentIntentAsync` (the hard prerequisite), then the admin payment list/summary/refund endpoints, then go back and wire `RefundClient` into dispute resolution.
9. **Reviews & Moderation** — biggest remaining feature; needs the baseline review feature (client submit, vendor respond) before any admin moderation layer makes sense.
10. **Reports/analytics, notification broadcast, admin-account CRUD** — lower urgency, can build incrementally per-page as each owning feature needs a chart.
11. **Audit Logs** — per the plan's own advice, instrument this *as* each of the above ships (suspend, resolve-dispute, refund, category CRUD, broadcast), not as a retrofit after the fact — retrofitting is real, avoidable extra work.
12. **Test coverage for the new admin services** — `AdminBookingService`, `AdminDashboardService`, and `AccountAdminService` currently have zero tests despite a real, working test project existing in the repo with the right mocking infrastructure already built (`UnitOfWorkMockExtensions`, `IdentityMockFactory`).

---

## 11. Completion Percentage

Estimated **backend** completion per module (frontend not independently verified in this review):

```
Dashboard Overview ............... 22%   (flat statistics endpoint only; no charts, no recent-activity, no disputes count)
Pending Vendor Approvals ......... 92%   (fully working; only pagination missing)
Vendor Details .................... 70%   (detail + history work; DTO enhancements + trust endpoint missing)
Vendor Management (All Vendors) ...  0%
Client Management .................  0%
Booking Management (general) ......  0%
Dispute Resolution ................ 65%   (list/detail/resolve work; reason-surfacing bug, no refund chaining, no resolved-status filter)
Categories ......................... 80%   (CRUD fully functional, but not admin-locked — security gap caps this)
Payments & Transactions ...........  0%
Reviews & Moderation ...............  0%   (no baseline feature exists for any role)
Reports .............................  0%
Notifications ...................... 50%   (inbox complete; broadcast missing)
Admin Accounts ...................... 35%   (suspend/reactivate work; no list/create, no last-admin guard)
Audit Logs ...........................  0%
Admin Profile ......................... 30%   (read-only via /auth/me; no update, no password change)
Settings ................................ N/A  (explicitly deferred, not counted)

Overall Admin Dashboard (backend): ~30%
```

This is a real, measurable step up from where the plan's own analysis left off the day before (roughly 15-20%, by the plan's own accounting of "only vendor verification is complete") — but the increase exists entirely outside version control right now, and the one feature that's functionally complete and reachable by the public (`Categories`) is not actually admin-gated, so it isn't safe to call "done" in the way its CRUD completeness would otherwise suggest.

---

## Verification Notes

- Live testing was performed against the real local SQL Server database via a `dotnet` debug session started in Visual Studio (Debug ▸ Start, `https` launch profile), confirmed by successful login, real seeded data (12 vendors, 2 pending at the start of the session, 1 client, 2 bookings), and state changes (a test category create/delete, a test client register/suspend/reactivate, a vendor reject) that persisted and were reflected correctly in subsequent reads.
- One environment quirk encountered and resolved during testing: Visual Studio's "break on user-unhandled exceptions" setting caused the debugger to pause the entire process (not just log) every time a custom exception type was thrown for the first time in the session — this looked like hung/frozen requests until diagnosed and disabled per-exception-type in Debug ▸ Windows ▸ Exception Settings. Not an application bug; noted here only so the cause of any apparent "hang" during this review is on record.
- Test artifacts created during this review (one throwaway client account `audit-test-client@planura.local`, one vendor rejected with reason `"AUDIT TEST: ..."`, one category created and then deleted) were left in place except the category, which was cleaned up via `DELETE`. The rejected test vendor and throwaway client were left as-is since reversing them would itself be a write action beyond the scope of "audit only" — flagging here so they're not mistaken for real user data.
