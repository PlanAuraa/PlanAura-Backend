Vendor Onboarding & Verification — Architecture Review and Implementation Roadmap
=================================================================================

Reviewed solution: `Planura` (Clean Architecture, .NET 9). Scope of this document: everything needed
before you write a single line of vendor-onboarding code.

1. What already exists — Authentication & Authorization
---------------------------------------------------------

Authentication is fully implemented and should be treated as a closed module. Summary of what it does:

- **Identity**: `ApplicationUser : IdentityUser<long>` (custom fields: FullName, PreferredLanguage, IsActive,
  LastLoginAt, CreatedAt/UpdatedAt). Tables are mapped to snake_case (`users`, `roles`, `user_roles`, etc.)
  via `PlanuraDbContext.ConfigureIdentityTables`.
- **Roles**: three fixed roles in `Planura.Core.Domain.Constants.Roles` — `admin`, `vendor`, `client` —
  seeded on startup by `IdentityDataSeeder` (idempotent, also seeds one admin account from `Seed:Admin`
  config if present).
- **Registration**: `AuthController.RegisterClient` → `AuthService.RegisterClientAsync` creates the
  Identity user, assigns `Roles.Client`, creates a `Client` row, all inside one `IUnitOfWork` transaction,
  then returns a JWT. **There is no vendor-registration endpoint yet** — this is the first gap you'll fill.
- **Login**: `AuthController.Login` → `AuthService.LoginAsync` validates credentials, checks `IsActive`,
  and — importantly — if the user is in the `vendor` role, looks up their `Vendor` row via
  `VendorByUserIdSpecification` and embeds `vendor_id` as a custom claim in the JWT.
- **Me endpoint**: `GET api/auth/me` → `CurrentUserDto` (id, name, email, phone, roles). Vendor-specific
  profile data is deliberately not in here — that's your `GET api/vendors/me` to build.
- **JWT**: `TokenService.CreateToken` issues `sub`/`NameIdentifier` (userId), `Jti`, `Email`, `Name`, one
  `ClaimTypes.Role` claim per role, and an optional `vendor_id` custom claim
  (`Planura.Core.Application.Common.CustomClaimTypes.VendorId`) when a vendor id is supplied.
  `JwtBearerEvents.OnTokenValidated` re-checks `IsActive` against the DB on every request, so a suspended
  account is blocked immediately even with a valid token.
- **Policies** (`Planura.Core.Application.Common.AuthorizationPolicies`): `ClientOnly`, `VendorOnly`,
  `AdminOnly` (simple `RequireRole`), and **`ApprovedVendor`** — a custom, DB-driven policy
  (`ApprovedVendorRequirement` + `ApprovedVendorHandler`) that re-queries the `Vendor` row on every request
  and succeeds only if `VerificationStatus.IsApproved(vendor.VerificationStatus)` is true (i.e. status is
  `verified` or `trusted`). **This is the seam your verification module plugs into**: the moment you flip
  `Vendor.VerificationStatus` to `verified`, every `[Authorize(Policy = ApprovedVendor)]` endpoint opens up
  for that vendor immediately, with no new login required.
- **Admin account actions**: `AdminAccountsController` (`api/admin/users`) exposes suspend/reactivate,
  implemented in `AccountAdminService`. This only toggles `IsActive`; it is unrelated to verification status
  and won't need touching.

Auth-related files (for reference, do not modify unless explicitly asked):
`AuthController.cs`, `AdminAccountsController.cs`, `IAuthService.cs`/`AuthService.cs`,
`IAccountAdminService.cs`/`AccountAdminService.cs`, `AuthorizationPolicies.cs`, `CustomClaimTypes.cs`,
`Roles.cs`, `ApplicationUser.cs`, `Client.cs`, `TokenService.cs`, `CurrentUserService.cs`,
`ApprovedVendorRequirement.cs`/`ApprovedVendorHandler.cs`, `InfrastructureServiceCollectionExtensions.cs`,
`IdentityDataSeeder.cs`, `JwtOptions.cs`, `Models/Auth/*`.

2. What already exists — Vendor Onboarding & Verification
------------------------------------------------------------

This is the most important finding: **the data model is already built.** A previous migration
(`initial-Set`) already created the tables, and EF configurations already exist. Nothing you build should
require a new migration unless explicitly noted below.

- **`Vendor` entity**: 1:1 with `ApplicationUser` (unique index on `UserId`), optional `ServiceCategory`,
  `BusinessName` (required), description, city/address/geo, cover/logo image URLs, and — critically —
  its own denormalized `VerificationStatus` string (default `"unverified"`), plus rating/review rollups.
  Configured in `VendorConfiguration.cs`.
- **`VendorVerification` entity**: one vendor → many verification rows (`Vendor.Verifications`). Each row
  holds `Status`, `CommercialDocUrl`, `NationalIdDocUrl`, `SubmittedAt`, `ReviewedAt`, `ReviewedByAdminId`,
  `RejectionReason`, `TrustedSince`. Configured in `VendorVerificationConfiguration` (inside
  `VerificationConfiguration.cs`).
- **`VendorVerificationHistory` entity**: child of a single `VendorVerification` row — `PreviousStatus`,
  `NewStatus`, `ChangedByAdminId`, `Notes`, `ChangedAt`. Configured alongside it in the same file.
- **`VerificationStatus` constants** (`Planura.Core.Domain.Constants`): `Unverified`, `Pending`, `Verified`,
  `Trusted`, `Rejected`, plus the `IsApproved()` helper the policy handler relies on.
- **`VendorByUserIdSpecification`**: already exists, already used by both login and the policy handler —
  reuse it everywhere you need "the current user's vendor row."
- **`ApprovedVendorRequirement`/`Handler`**: already wired into DI and the policy table.
- **`IAttachmentService`/`AttachmentService`**: generic file upload abstraction (Infrastructure), used
  today by `ServiceCategoryService` for category icons. Currently whitelists only `.png/.jpg/.jpeg` and
  caps size at 2 MB — both hardcoded. Verification documents (national ID, commercial registration) will
  very likely need `.pdf` support at minimum, so this is a gap, not a redesign.

What's completely missing (no files exist for any of this today):
- Vendor self-registration endpoint/DTO/service method (only client registration exists).
- `Vendor` profile service/controller/DTOs (create, read own profile, read public profile, update).
- `VendorVerification` service/controller/DTOs for the entire submit → review → approve/reject →
  resubmit → history lifecycle.
- Any AutoMapper mappings for `Vendor`, `VendorVerification`, `VendorVerificationHistory`.
- Any specifications under `Specifications/VendorVerification/`.
- Admin-facing review queue / approve / reject endpoints.
- `.pdf` support in `AttachmentService`'s allowed-extensions list.

3. Existing entities/services to reuse (do not recreate)
-------------------------------------------------------------

- `Vendor`, `VendorVerification`, `VendorVerificationHistory` entities — already modeled, already migrated.
- `VerificationStatus` constants and `IsApproved()` — the single source of truth for "is this vendor
  allowed to act as an approved vendor."
- `VendorByUserIdSpecification` — "get my vendor row" everywhere (services, policy handlers, controllers).
- `IUnitOfWork` / `IGenericRepository<TEntity, TKey>` / `BaseSpecification<T>` / `SpecificationEvaluator` —
  the only data-access pattern in this codebase. No entity-specific repositories exist or should be added;
  new query shapes are new `BaseSpecification<T>` subclasses.
- `ICurrentUserService` — already exposes `UserId`, `VendorId` (from the JWT claim), `Roles`, `IsInRole`.
  Use `VendorId` for every "my own resource" check instead of trusting a route parameter.
- `IAttachmentService` — extend its allowed-extensions list rather than writing a second upload path.
- `AuthorizationPolicies` (`VendorOnly`, `AdminOnly`, `ApprovedVendor`) — sufficient for every endpoint in
  this module; no new policy constants should be needed.
- `ApiResponse` / `ApiValidationErrorResponse` / custom exceptions (`NotFoundExeption`, `BadRequestExeption`,
  `UnAuthorizedExeption`, `ValidationExeption`) + `ExeptionHandlerMiddleware` — throw, never try/catch in
  controllers or services.

4. Architectural decisions to stay consistent with
------------------------------------------------------

- **Layering**: Domain (entities, constants, repo abstractions) → Application.Abstraction (cross-cutting
  interfaces like `IAttachmentService`) → Application (services, DTOs under `Models/{Feature}/`,
  specifications, AutoMapper profile) → Infrastructure / Infrastructure.Persistence (concrete
  implementations, EF config, migrations) → Apis.Controller (controllers only) → Apis (composition root).
  Follow this exactly: new DTOs go in `Core.Application/Models/Vendor/` and `Models/VendorVerification/`;
  new specs go in `Core.Application/Specifications/Vendor/` and `Specifications/VendorVerification/`.
- **Service shape**: one `IXService`/`XService` pair per aggregate, constructor-injected with
  `IUnitOfWork` + `IMapper` (+ `IAttachmentService` when files are involved), registered in
  `ApplicationServiceCollectionExtensions.AddApplicationServices`. No business logic in controllers.
- **Controllers are thin**: bind DTO → call one service method → return `Ok`/`CreatedAtAction`/`NoContent`.
  Route pattern `api/[controller]` for resource controllers, explicit routes (`api/admin/...`) for
  admin-scoped ones, matching `AdminAccountsController`.
- **Validation**: DataAnnotations on DTOs for shape/format, explicit guard clauses throwing
  `BadRequestExeption` inside the service for business rules (see `ServiceCategoryService`,
  `VendorPackageService.ValidatePackage`) — no FluentValidation in this codebase.
- **AutoMapper**: every entity↔DTO pair gets one `CreateMap<>` line added to the single
  `MappingProfile.cs` — don't create per-feature profiles.
- **Snake_case DB naming** is automatic (`UseSnakeCaseNames()`); don't hand-name columns.
- **Authorization is two-layered** and both layers matter: role-based `[Authorize(Policy = ...)]` at the
  endpoint, plus an ownership check inside the service using `ICurrentUserService.VendorId` — never trust
  a `vendorId` route/body parameter for "my own" endpoints.
- **Critical consistency point discovered during review**: `ApprovedVendorHandler` reads
  `Vendor.VerificationStatus` (the denormalized field on the `Vendor` row), *not*
  `VendorVerification.Status`. Every place that changes a verification's status (approve, reject) **must
  also update the parent `Vendor.VerificationStatus`** in the same transaction, or the `ApprovedVendor`
  policy will silently keep using stale data.
- **Resubmission model (confirmed with you)**: each vendor resubmission after a rejection creates a
  **new** `VendorVerification` row rather than mutating the rejected one. "Current" verification = most
  recent row by `CreatedAt`. This keeps each row's `ReviewedByAdminId`/`RejectionReason` immutable and
  matches the existing one-to-many schema without needing new columns.

5. Implementation Roadmap
-----------------------------

No code yet — this is the task breakdown, in build order. Each task is intentionally small.

---

### Task 1 — Vendor Registration endpoint

**Goal**: let a new user self-register as a vendor: create the Identity user, assign the `vendor` role,
create the `Vendor` profile row, and return a JWT that already carries the `vendor_id` claim — mirroring
`RegisterClientAsync` exactly, in one `IUnitOfWork` transaction.

**New files**: `Planura.Core.Application/Models/Auth/RegisterVendorDto.cs` (FullName, Email, Phone,
Password, ConfirmPassword, BusinessName, optional CategoryId/City).

**Existing files to modify**: `IAuthService.cs` (+`RegisterVendorAsync`), `AuthService.cs` (implement:
transaction → create `ApplicationUser` → `AddToRoleAsync(Roles.Vendor)` → create `Vendor { UserId,
BusinessName, CategoryId, City, VerificationStatus = VerificationStatus.Unverified }` → commit → build JWT
*with* the new vendor's id, same as `LoginAsync` already does), `AuthController.cs` (+`POST
api/auth/register/vendor`, `[AllowAnonymous]`).

**Dependencies**: none — only touches the auth module, and the `Vendor` entity already exists.

**Why first**: every other task (profile, verification, RBAC) assumes a `Vendor` row and a `vendor` role
already exist for the logged-in user. Nothing downstream can be built or tested without this.

---

### Task 2 — Vendor Profile read/update ("Create Vendor Profile" + "Vendor Profile endpoint")

**Goal**: expose the vendor's own profile (`GET api/vendors/me`), a public profile view (`GET
api/vendors/{id}`), and self-service editing (`PUT api/vendors/me`) for the fields not touched by
verification (business name, description, city/address, category).

**New files**: `IVendorService.cs`/`VendorService.cs`, `VendorController.cs`,
`Models/Vendor/VendorDto.cs`, `Models/Vendor/UpdateVendorProfileDto.cs`,
`Specifications/Vendor/VendorProfileSpecification.cs` (includes `Category`).

**Existing files to modify**: `ApplicationServiceCollectionExtensions.cs` (register the new service),
`MappingProfile.cs` (`Vendor` ↔ `VendorDto`/`UpdateVendorProfileDto`).

**Dependencies**: Task 1 (needs vendor rows to read/update).

**Why before verification**: the verification screens (both vendor-facing and admin-facing) will want to
show business name/category alongside the verification state, so the read model should exist first even
though it's built out independently.

---

### Task 3 — Vendor Verification service skeleton + specifications

**Goal**: stand up the shared business-rule core (`IVendorVerificationService`) that every later
controller (vendor-facing and admin-facing) will call into — no HTTP surface yet, just the service,
specs, and DTOs.

**New files**: `IVendorVerificationService.cs`/`VendorVerificationService.cs`,
`Specifications/VendorVerification/VendorVerificationByIdSpecification.cs`,
`Specifications/VendorVerification/CurrentVendorVerificationSpecification.cs` (latest row for a vendor,
`Include(History)`, `Include(ReviewedByAdmin)`),
`Specifications/VendorVerification/PendingVendorVerificationsSpecification.cs` (admin queue),
`Models/VendorVerification/VendorVerificationDto.cs`,
`Models/VendorVerification/VendorVerificationHistoryDto.cs`.

**Existing files to modify**: `MappingProfile.cs`, `ApplicationServiceCollectionExtensions.cs`.

**Dependencies**: Task 1 (vendor + role must exist).

**Why before any controller**: submit/approve/reject/resubmit/history all share the same "get current
verification for this vendor," "record a history row," and "sync `Vendor.VerificationStatus`" logic — that
belongs in one place, built once, so tasks 4–10 are thin controller methods calling into it.

---

### Task 4 — Upload Verification Documents (vendor-facing)

**Goal**: vendor uploads their commercial-registration and/or national-ID documents against their current
`Unverified`/`Rejected` verification row.

**New files**: `Models/VendorVerification/UploadVerificationDocumentsDto.cs` (`IFormFile?
CommercialDoc`, `IFormFile? NationalIdDoc`), `VendorVerificationController.cs` (new — first endpoint on
it: `POST api/vendor-verifications/documents`, `[Authorize(Policy = VendorOnly)]`).

**Existing files to modify**: `AttachementService.cs` (add `.pdf` to the allowed-extensions list — flag
the size cap too; 2 MB may be tight for scanned documents), `VendorVerificationService.cs`
(`UploadDocumentsAsync`: get-or-create the current row via `ICurrentUserService.VendorId`, guard that it's
in an editable state, save files to a `verification-docs` folder, set the URL fields).

**Dependencies**: Task 3.

**Why before submit**: submission validates that both documents are present — that precondition can't be
tested until upload exists.

---

### Task 5 — Submit Verification (vendor-facing)

**Goal**: vendor explicitly moves their verification from draft (`Unverified`) to `Pending`: validates both
document URLs are set, stamps `SubmittedAt`, writes a `VendorVerificationHistory` row.

**Existing files to modify**: `VendorVerificationService.cs` (`SubmitAsync`),
`VendorVerificationController.cs` (`POST api/vendor-verifications/submit`).

**Dependencies**: Task 4.

**Why next**: this is the event that makes a verification visible to admins — the admin queue (Task 6) has
nothing to show until this exists.

---

### Task 6 — Admin Review Queue

**Goal**: admins list all `Pending` verifications with enough vendor context (business name, category,
document links, submitted date) to make a decision.

**New files**: `AdminVendorVerificationController.cs` (new controller, kept separate from
`AdminAccountsController` since it's a distinct aggregate), `Models/VendorVerification/
VendorVerificationQueueItemDto.cs`.

**Existing files to modify**: `VendorVerificationService.cs` (`GetPendingAsync`, using
`PendingVendorVerificationsSpecification`).

**Dependencies**: Tasks 3 and 5 (needs pending data to exist and be queryable).

**Why before approve/reject**: admins need to see what they're approving/rejecting before those actions
have anything meaningful to operate on in a real workflow/test.

---

### Task 7 — Admin Approve

**Goal**: admin approves a pending verification. Sets `VendorVerification.Status = Verified`,
`ReviewedAt`, `ReviewedByAdminId`; **also sets `Vendor.VerificationStatus = Verified`** (the field the
`ApprovedVendor` policy actually reads); inserts a `VendorVerificationHistory` row
(`Previous = Pending, New = Verified`).

**Existing files to modify**: `VendorVerificationService.cs` (`ApproveAsync`),
`AdminVendorVerificationController.cs` (`POST api/admin/vendor-verifications/{id}/approve`,
`[Authorize(Policy = AdminOnly)]`).

**Dependencies**: Task 6.

**Why before reject**: approve and reject share the same "must currently be Pending" guard and history-
write plumbing; building the simpler one (no reason payload) first gives you the pattern to copy for reject.

---

### Task 8 — Admin Reject with Reason

**Goal**: admin rejects with a mandatory reason. Sets `Status = Rejected`, `ReviewedAt`,
`ReviewedByAdminId`, `RejectionReason`; syncs `Vendor.VerificationStatus = Rejected`; inserts history row.

**New files**: `Models/VendorVerification/RejectVerificationDto.cs` (`Reason`, required, min length).

**Existing files to modify**: `VendorVerificationService.cs` (`RejectAsync`),
`AdminVendorVerificationController.cs` (`POST .../reject`).

**Dependencies**: Task 7 (shares the approve pattern).

**Why before resubmission**: resubmission is only reachable from a `Rejected` state, so reject must exist
first for that state to be reachable at all.

---

### Task 9 — Vendor Resubmission

**Goal**: vendor whose verification was rejected uploads new documents and resubmits — creating a **new**
`VendorVerification` row (per the confirmed decision), leaving the rejected row as permanent history.

**Existing files to modify**: `VendorVerificationService.cs` (`ResubmitAsync`: guard current status ==
`Rejected`, create new row referencing the vendor, reuse the upload+submit logic from Tasks 4/5 against the
new row), `VendorVerificationController.cs` (`POST api/vendor-verifications/resubmit`).

**Dependencies**: Task 8 (needs a reachable `Rejected` state), Tasks 4/5 (reuses their logic).

**Why here**: it's the direct continuation of rejection and closes the vendor-facing lifecycle loop before
you build reporting on top of it.

---

### Task 10 — Vendor Verification History

**Goal**: expose the full audit trail — every `VendorVerification` row for a vendor (each a submission
cycle) with its nested `VendorVerificationHistory` entries, chronologically ordered. Vendor sees their own
(`GET api/vendor-verifications/me/history`); admin sees any vendor's (`GET
api/admin/vendor-verifications/{vendorId}/history`).

**New files**: `Models/VendorVerification/VendorVerificationTimelineDto.cs`.

**Existing files to modify**: `VendorVerificationService.cs` (`GetHistoryAsync(vendorId)` — all rows for
the vendor, each with its `History` collection included), both controllers (new GET actions).

**Dependencies**: Tasks 3–9 (needs the full lifecycle already producing real rows/history to be
meaningful — building this earlier means revisiting it every time a new transition is added).

**Why last data task**: it's a pure read/reporting view over everything built above; building it earlier
would mean touching it repeatedly as new statuses and transitions get added.

---

### Task 11 — RBAC hardening pass

**Goal**: a dedicated review pass, not new features: confirm every new endpoint has the correct
`[Authorize(Policy = ...)]`, confirm every "my own resource" action uses
`ICurrentUserService.VendorId`/`UserId` rather than a client-supplied id, and regression-test that a
vendor's **existing** JWT (issued before approval) is still gated correctly by the DB-driven
`ApprovedVendor` policy after Task 7 flips their status — proving the sync in Task 7/8 actually works
end-to-end without requiring re-login.

**Existing files to review**: all controllers/services touched in Tasks 1–10, `AuthorizationPolicies.cs`
(confirm no new policy constants are actually needed — they shouldn't be).

**Dependencies**: all previous tasks.

**Why last**: a security review is only meaningful once the full surface area exists; reviewing endpoint-
by-endpoint as you go would miss cross-endpoint consistency issues (e.g. one controller checking ownership
and another forgetting to).

---

### Task 12 — End-to-end verification pass

**Goal**: walk both full lifecycles manually or via integration test: (a) register vendor → upload → submit
→ admin queue → approve → confirm `ApprovedVendor`-gated endpoint now accepts the existing token; (b)
register vendor → submit → reject → resubmit → approve. Confirm no migration is actually required (schema
already supports everything above) — the only exception would be if you decide during Task 4 that `.pdf`
support requires a size-limit change per upload type, which is a code constant, not a schema change.

**Dependencies**: everything.

---

Quick task-dependency chain: `1 → 2` (parallel-safe with 3) · `1 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10 → 11 → 12`.
Task 2 can be built in parallel with Task 3 onward since it doesn't touch verification state.
