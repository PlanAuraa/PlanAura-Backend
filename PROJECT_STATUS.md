# Planura Backend Progress Report

_Generated from a direct review of the current codebase (solution `Planura.sln`) on 2026-07-12. Every statement below is based on files that exist in the repository at the time of review. Anything not found in the code is explicitly marked "Not Implemented."_

---

## 1. Project Overview

**Architecture**: Clean Architecture, split into the following class library/API projects (see `Planura.sln`):

- `Planura.Core.Domain` — entities, enums, string constants, repository interfaces (`IUnitOfWork`, `IGenericRepository`). No dependencies on other layers.
- `Planura.Core.Application` — DTOs (`Models/`), service interfaces + implementations (`Services/`), specifications (`Specifications/`), AutoMapper mappings (`Mappings/`), cross-cutting constants (`Common/AuthorizationPolicies.cs`, `Common/CustomClaimTypes.cs`).
- `Planura.Core.Application.Abstraction` — thin abstraction project holding `IAttachmentService` (kept separate from `Planura.Core.Application` so Infrastructure can implement it without a circular reference).
- `Planura.Infrastructure` — implementations of application interfaces that need external concerns: `TokenService` (JWT), `CurrentUserService`, `AttachmentService` (local file storage), `ApprovedVendorHandler`/`ApprovedVendorRequirement` (authorization), and DI wiring (`InfrastructureServiceCollectionExtensions`).
- `Planura.Infrastructure.Persistence` — `PlanuraDbContext`, EF Core `IEntityTypeConfiguration<T>` classes (`Configurations/`), migrations (`Migrations/`), generic repository + `UnitOfWork` implementation (`Repositories/`), identity/role seeding (`Seed/IdentityDataSeeder.cs`).
- `Planura.Apis.Controller` — all MVC controllers (`Controllers/`) and their supporting models.
- `Planura.Apis` — the actual ASP.NET Core host (`Program.cs`), Swagger setup, middleware wiring.
- `Planura.Shared` — cross-cutting error types (`Errors/Models`, custom exceptions such as `BadRequestExeption`, `NotFoundExeption`, `UnAuthorizedExeption`) and the global error response shape (`Errors/Response`).

There is no dedicated test project in the solution — **Not Implemented**.

**Current authentication flow**: ASP.NET Core Identity (`ApplicationUser : IdentityUser<long>`) + JWT bearer tokens issued by `TokenService`. Registration endpoints exist for both clients (`AuthController.RegisterClient`) and vendors (`AuthController.RegisterVendor`), plus a shared `Login` endpoint and a `GET api/auth/me` endpoint. Tokens carry `sub`/`NameIdentifier` (user id), `Jti`, `Email`, `Name`, one `ClaimTypes.Role` claim per assigned role, and — only for vendors — a custom `vendor_id` claim (`CustomClaimTypes.VendorId`). `JwtBearerEvents.OnTokenValidated` re-checks the user's `IsActive` flag against the database on every request, so a suspended account is rejected immediately even with a still-valid token.

**Current authorization policies** (`Planura.Core.Application.Common.AuthorizationPolicies`, registered in `InfrastructureServiceCollectionExtensions.AddInfrastructure`):

- `ClientOnly` — `RequireRole("client")`.
- `VendorOnly` — `RequireRole("vendor")`.
- `AdminOnly` — `RequireRole("admin")`.
- `ApprovedVendor` — requires the `vendor` role **and** a custom `ApprovedVendorRequirement`/`ApprovedVendorHandler` that re-queries the vendor's row on every request and succeeds only if `VerificationStatus.IsApproved(...)` is true (status is `verified` or `trusted`). This means an admin approval takes effect immediately, with no new login required.

Only two controllers currently apply any of these policies: `AdminAccountsController` and `AdminVendorVerificationController` (both `AdminOnly`). `AuthController` uses `[AllowAnonymous]`/`[Authorize]` as appropriate. `VendorAvailabilityController`, `VendorPackagesController`, and `ServiceCategoriesController` have **no `[Authorize]` attributes at all** — see section 8.

---

## 2. Vendor Module Status

- **Register Vendor endpoint**: Implemented. `POST api/auth/register/vendor` (`AuthController.RegisterVendor`), `[AllowAnonymous]`, binds `[FromForm] RegisterVendorDto` (multipart/form-data, required for the file uploads). Delegates to `IAuthService.RegisterVendorAsync`.
- **DTOs**: `RegisterVendorDto` (account fields + business fields + `VendorType` + required `IFormFile`s for `NationalIdFront`, `NationalIdBack`, `SelfieWithId`, `PortfolioImages`, and conditional `CommercialRegistration`/`TaxCard`). Admin review DTOs `ApproveVendorDto` (`VendorId`) and `RejectVendorDto` (`VendorId` + required, max-500-char `RejectionReason`) also exist under `Models/VendorVerification/`.
- **Entities**: `Vendor`, `VendorVerification`, `VendorVerificationDocument`, `VendorVerificationHistory`, `PortfolioMedia`, `PortfolioLink`, `VendorPackage`, `VendorAvailability` all exist in `Planura.Core.Domain/Entities/`, each with an EF Core configuration class.
- **Migrations**: three migrations touch the vendor schema — `20260710142924_initial-Set` (creates all vendor-related tables including `vendor_verification_history`), `20260710165037_VendorAvailability_Concurrency_Enum` (adds a `RowVersion` concurrency token and enum-backed status to `VendorAvailability`), and `20260712141443_AddVendorVerificationDocuments` (drops the old single-URL columns `commercial_doc_url`/`national_id_doc_url` from `vendor_verifications`, adds `vendor_type` to `vendors`, adds `is_current` to `vendor_verifications`, and creates the `vendor_verification_documents` table). The model snapshot is in sync with all three.
- **`VendorType`**: `enum VendorType { Individual = 1, Business = 2 }` (`Planura.Core.Domain.Enums`). Drives conditional document requirements in registration.
- **`VerificationStatus`**: **not an enum** — it is a `static class` of string constants (`Unverified`, `Pending`, `Verified`, `Trusted`, `Rejected`) plus a static `IsApproved(string?)` helper, in `Planura.Core.Domain.Constants`. Stored as a plain string column on both `Vendor.VerificationStatus` and `VendorVerification.Status`.
- **`VerificationDocumentType`**: `enum VerificationDocumentType { NationalIdFront, NationalIdBack, SelfieWithId, NationalId, CommercialRegistration, TaxCard }`. Note the unused `NationalId` member — see section 8.
- **`VendorVerification`**: implemented entity representing one verification attempt per vendor (`Status`, `IsCurrent`, `SubmittedAt`, `ReviewedAt`, `ReviewedByAdminId`, `RejectionReason`, `TrustedSince`), with a `Documents` and `History` collection navigation.
- **`VendorVerificationDocument`**: implemented — one row per uploaded document (`DocumentType`, `FileUrl`, `OriginalFileName`, `ContentType`, `FileSizeBytes`), FK to `VendorVerification`, cascade delete.
- **`PortfolioMedia`**: implemented — one row per uploaded portfolio image during registration (`MediaType`, `FileUrl`, `Title`, `FileSizeKb`, `DisplayOrder`). `PortfolioLink` also exists (for external links such as Instagram/website) but nothing in the codebase currently writes to it — it is defined and configured, but unused by any service.
- **Attachment upload**: `IAttachmentService`/`AttachmentService` (Infrastructure) saves files to `wwwroot/images/{folder}/{guid}.{ext}` on local disk. Hardcoded to allow only `.png`, `.jpg`, `.jpeg` and a 2 MB max size for every file type, including legal/ID documents — no PDF support. Used by vendor registration (`vendor-verification-documents` and `vendor-portfolio` folders) and by `ServiceCategoryService` for category icons.
- **Transaction handling**: `RegisterVendorAsync` and `RegisterClientAsync` both wrap their work in `IUnitOfWork.BeginTransactionAsync()` / `CommitTransactionAsync()` with a `catch { RollbackTransactionAsync(); throw; }` pattern. `VendorVerificationService.ApproveVendorAsync`/`RejectVendorAsync` follow the same begin/commit/rollback pattern (see section 8 for a redundant-`SaveChangesAsync` note).
- **JWT generation**: `TokenService.CreateToken(user, roles, vendorId)` — shared by client registration, vendor registration, and login. Vendor registration passes the newly created vendor's `Id` so the token immediately carries the `vendor_id` claim.
- **Role assignment**: `UserManager.AddToRoleAsync(user, Roles.Vendor)` during registration. Roles (`admin`, `vendor`, `client`) are seeded idempotently at startup by `IdentityDataSeeder`.
- **Pending verification flow**: Implemented at the data/service level — every new vendor is created with `VerificationStatus = "pending"` and a matching `VendorVerification` row (`Status = "pending"`, `IsCurrent = true`). **Admin review is also implemented**: `AdminVendorVerificationController` (`api/admin/vendor-verifications`, `AdminOnly`) exposes `POST approve` and `POST reject`, both backed by `VendorVerificationService`, which updates `Vendor.VerificationStatus`, updates the current `VendorVerification` row, and writes a `VendorVerificationHistory` row recording the transition. There is, however, no endpoint to **list** pending verifications for an admin queue — see section 5.

---

## 3. Files Modified

Files directly implementing or configuring the Vendor workflow (registration, verification, packages, availability, and their shared dependencies):

**Domain**
- `Planura.Core.Domain/Entities/Vendor.cs`
- `Planura.Core.Domain/Entities/VendorVerification.cs`
- `Planura.Core.Domain/Entities/VendorVerificationDocument.cs`
- `Planura.Core.Domain/Entities/VendorVerificationHistory.cs`
- `Planura.Core.Domain/Entities/PortfolioMedia.cs`
- `Planura.Core.Domain/Entities/PortfolioLink.cs`
- `Planura.Core.Domain/Entities/VendorPackage.cs`
- `Planura.Core.Domain/Entities/VendorAvailability.cs`
- `Planura.Core.Domain/Entities/ApplicationUser.cs` (vendor-related navigation properties)
- `Planura.Core.Domain/Entities/ServiceCategory.cs` (`Vendors` navigation)
- `Planura.Core.Domain/Enums/VendorType.cs`
- `Planura.Core.Domain/Enums/VerificationDocumentType.cs`
- `Planura.Core.Domain/Enums/AvailabilityStatus.cs`
- `Planura.Core.Domain/Constants/VerificationStatus.cs`
- `Planura.Core.Domain/Constants/Roles.cs`
- `Planura.Core.Domain/Repositories/IUnitOfWork.cs`, `IGenericRepository.cs`

**Application**
- `Planura.Core.Application/Models/Auth/RegisterVendorDto.cs`
- `Planura.Core.Application/Models/Auth/AuthResponseDto.cs`
- `Planura.Core.Application/Models/Auth/JwtTokenResult.cs`
- `Planura.Core.Application/Models/VendorVerification/ApproveVendorDto.cs`
- `Planura.Core.Application/Models/VendorVerification/RejectVendorDto.cs`
- `Planura.Core.Application/Models/VendorPackage/*.cs` (Create/Update/Search/Dto)
- `Planura.Core.Application/Models/VendorAvailability/*.cs` (Create/Update/Check/BookSlot/Dto)
- `Planura.Core.Application/Services/IAuthService.cs`, `AuthService.cs`
- `Planura.Core.Application/Services/IVendorVerificationService.cs`, `VendorVerificationService.cs`
- `Planura.Core.Application/Services/IVendorPackageService.cs`, `VendorPackageService.cs`
- `Planura.Core.Application/Services/IVendorAvailabilityService.cs`, `VendorAvailabilityService.cs`
- `Planura.Core.Application/Specifications/Vendor/VendorByUserIdSpecification.cs`
- `Planura.Core.Application/Specifications/VendorVerification/CurrentVendorVerificationSpecification.cs`
- `Planura.Core.Application/Specifications/VendorVerification/PendingVendorVerificationsSpecification.cs`
- `Planura.Core.Application/Specifications/VendorAvailability/*.cs` (4 specs)
- `Planura.Core.Application/Specifications/VendorPackage/*.cs` (3 specs)
- `Planura.Core.Application/Common/AuthorizationPolicies.cs`, `CustomClaimTypes.cs`
- `Planura.Core.Application/Extensions/ApplicationServiceCollectionExtensions.cs`
- `Planura.Core.Application.Abstraction/AttachementService/IAttachmentService.cs`

**Infrastructure**
- `Planura.Infrastructure/AttachementService/AttachmentService.cs`
- `Planura.Infrastructure/Services/TokenService.cs`
- `Planura.Infrastructure/Services/CurrentUserService.cs`
- `Planura.Infrastructure/Authorization/ApprovedVendorRequirement.cs`, `ApprovedVendorHandler.cs`
- `Planura.Infrastructure/Extensions/InfrastructureServiceCollectionExtensions.cs`

**Infrastructure.Persistence**
- `Planura.Infrastructure.Persistence/Configurations/VendorConfiguration.cs`
- `Planura.Infrastructure.Persistence/Configurations/VerificationConfiguration.cs` (`VendorVerificationConfiguration` + `VendorVerificationHistoryConfiguration`)
- `Planura.Infrastructure.Persistence/Configurations/VendorVerificationDocumentConfiguration.cs`
- `Planura.Infrastructure.Persistence/Configurations/PortfolioConfiguration.cs` (`PortfolioMediaConfiguration` + `PortfolioLinkConfiguration`)
- `Planura.Infrastructure.Persistence/Configurations/LookupAndVendorOfferingConfiguration.cs` (`ServiceCategoryConfiguration` + `VendorPackageConfiguration`, and vendor availability config)
- `Planura.Infrastructure.Persistence/Migrations/20260710142924_initial-Set.cs` (+ `.Designer.cs`)
- `Planura.Infrastructure.Persistence/Migrations/20260710165037_VendorAvailability_Concurrency_Enum.cs` (+ `.Designer.cs`)
- `Planura.Infrastructure.Persistence/Migrations/20260712141443_AddVendorVerificationDocuments.cs` (+ `.Designer.cs`)
- `Planura.Infrastructure.Persistence/Migrations/PlanuraDbContextModelSnapshot.cs`
- `Planura.Infrastructure.Persistence/Repositories/UnitOfWork.cs`
- `Planura.Infrastructure.Persistence/Seed/IdentityDataSeeder.cs`

**API layer**
- `Planura.Apis.Controller/Controllers/AuthController.cs` (`RegisterVendor` action)
- `Planura.Apis.Controller/Controllers/AdminVendorVerificationController.cs`
- `Planura.Apis.Controller/Controllers/VendorAvailabilityController.cs`
- `Planura.Apis.Controller/Controllers/VendorPackagesController.cs`
- `Planura.Apis.Controller/Controllers/ServiceCategoriesController.cs`

---

## 4. Current Registration Workflow

Step-by-step as implemented in `AuthService.RegisterVendorAsync`, called from `AuthController.RegisterVendor`:

1. Vendor submits `POST api/auth/register/vendor` as `multipart/form-data`, populating `RegisterVendorDto` (account fields, business fields, `VendorType`, and the required document/portfolio files).
2. `ValidateVendorRegistrationAsync` runs (outside the transaction): confirms `VendorType` is a defined enum value; if `CategoryId` is supplied, confirms the `ServiceCategory` exists; if `VendorType == Business`, requires non-empty `CommercialRegistration` and `TaxCard` files; always requires non-empty `NationalIdFront`, `NationalIdBack`, `SelfieWithId`, and at least one `PortfolioImages` entry. Any failure throws `BadRequestExeption`/`NotFoundExeption`.
3. A database transaction begins (`IUnitOfWork.BeginTransactionAsync`).
4. An `ApplicationUser` is created via `UserManager.CreateAsync` (Identity handles password hashing/validation).
5. The `vendor` role is assigned via `UserManager.AddToRoleAsync`.
6. A `Vendor` row is created (`BusinessName`, `BusinessDescription`, `CategoryId`, `City`, `Address`, `VendorType`, `VerificationStatus = "pending"`).
7. A `VendorVerification` row is created for that vendor (`Status = "pending"`, `SubmittedAt = UtcNow`, `IsCurrent = true`), linked via navigation property.
8. Each required document (`NationalIdFront`, `NationalIdBack`, `SelfieWithId`, and — for business vendors — `CommercialRegistration`, `TaxCard`) is uploaded through `IAttachmentService.UploadAsynce` into `wwwroot/images/vendor-verification-documents/...` and persisted as a `VendorVerificationDocument` row.
9. Each portfolio image is uploaded through `IAttachmentService` into `wwwroot/images/vendor-portfolio/...` and persisted as a `PortfolioMedia` row, preserving upload order via `DisplayOrder`.
10. The transaction commits (`CommitTransactionAsync`, which itself calls `SaveChangesAsync` before committing). Any exception in steps 4–9 triggers `RollbackTransactionAsync` in a `catch` block and rethrows.
11. The user's roles are re-read, and `BuildAuthResponse` issues a JWT via `TokenService.CreateToken`, embedding the new vendor's `Id` as the `vendor_id` claim.
12. `AuthResponseDto` (access token, expiry, user id, full name, email, roles) is returned to the caller. The vendor is authenticated immediately but cannot pass the `ApprovedVendor` authorization policy until an admin approves the verification (`VerificationStatus.IsApproved` only accepts `verified`/`trusted`).

---

## 5. Remaining Work

- [ ] Pending Vendors API (admin queue/list endpoint) — `PendingVendorVerificationsSpecification` already exists but is not referenced by any service or controller.
- [ ] Vendor Details API (admin/public single-vendor view, including documents and verification status).
- [ ] Vendor Profile API (`GET/PUT` for a vendor's own profile — business info, logo/cover image).
- [ ] Resubmit Verification flow (allow a rejected vendor to submit new documents and create a new `VendorVerification`, flipping the previous row's `IsCurrent` to `false`).
- [ ] Verification History API (expose `VendorVerificationHistory` — the table and entity exist and are already written to by approve/reject, but nothing reads it back out).
- [ ] Portfolio management endpoints (add/remove/reorder `PortfolioMedia`; `PortfolioLink` entity exists but has no service or endpoint at all).
- [ ] Notifications on submit/approve/reject (a `Notification` entity exists in the domain, but nothing in the Vendor workflow creates one).
- [ ] Broader document-type support in `IAttachmentService` (currently `.png`/`.jpg`/`.jpeg` only, 2 MB cap, applied uniformly to ID photos and legal documents).
- [ ] Authorization on `VendorAvailabilityController` / `VendorPackagesController` / `ServiceCategoriesController` (none of the three currently has any `[Authorize]` attribute).
- [ ] Integration/unit tests (no test project exists in the solution).
- [ ] Swagger/XML documentation on controllers and DTOs (Swagger is wired up in `Program.cs` and lists endpoints, but no `[ProducesResponseType]`/XML doc comments are present on the Vendor controllers).

Already implemented and therefore **not** listed as remaining: Admin Approve Vendor, Admin Reject Vendor, Save Rejection Reason, and `VendorVerificationHistory` writing (all present in `AdminVendorVerificationController`/`VendorVerificationService`).

---

## 6. Existing APIs

| Endpoint | Status | Description |
|---|---|---|
| `POST api/auth/register/vendor` | Implemented | Registers a new vendor account with documents and portfolio images; returns JWT. `[AllowAnonymous]`. |
| `POST api/auth/register/client` | Implemented (not vendor-specific) | Registers a client account. Listed for context only. |
| `POST api/auth/login` | Implemented (shared) | Logs in any user; embeds `vendor_id` claim if the user is a vendor. |
| `GET api/auth/me` | Implemented (shared) | Returns current user's id/name/email/phone/roles. No vendor-specific fields. |
| `POST api/admin/vendor-verifications/approve` | Implemented | Approves a vendor's current pending verification. `AdminOnly`. |
| `POST api/admin/vendor-verifications/reject` | Implemented | Rejects a vendor's current pending verification with a required reason. `AdminOnly`. |
| `GET api/admin/vendor-verifications` (list/pending) | Not Implemented | No controller action exists; only the underlying specification does. |
| `GET api/vendors/{id}` / `GET api/vendors/me` | Not Implemented | No vendor profile controller exists. |
| `GET/POST api/vendorpackages` and sub-routes | Implemented (no auth) | Full CRUD + search for vendor packages (`VendorPackagesController`). No `[Authorize]` attribute present. |
| `GET/POST api/vendoravailability` and sub-routes | Implemented (no auth) | Full CRUD + booking/cancel/check for vendor availability (`VendorAvailabilityController`). No `[Authorize]` attribute present. |
| `GET/POST api/servicecategories` and sub-routes | Implemented (no auth) | CRUD for the shared service-category lookup used by vendor registration. No `[Authorize]` attribute present. |
| `POST/GET api/admin/users/{id}/suspend`, `/reactivate` | Implemented (not vendor-specific) | Account suspension, unrelated to verification status. Listed for context only. |

---

## 7. Existing Database Tables

All tables below are confirmed in the EF Core migrations/model snapshot.

- **`vendors`** — one row per vendor business, 1:1 with `users` (unique index on `user_id`), optional FK to `service_categories`. Holds `verification_status` (string, indexed) and `vendor_type` (int, added in the third migration).
- **`vendor_verifications`** — many rows per vendor (one per verification attempt). FK to `vendors`; optional FK to `users` via `reviewed_by_admin_id`. Holds `status`, `is_current`, `submitted_at`, `reviewed_at`, `rejection_reason`, `trusted_since`.
- **`vendor_verification_documents`** — many rows per verification (one per uploaded document). FK to `vendor_verifications`, cascade delete. Holds `document_type`, `file_url`, `original_file_name`, `content_type`, `file_size_bytes`.
- **`vendor_verification_history`** — many rows per verification (one per status transition). FK to `vendor_verifications` (cascade delete) and optional FK to `users` via `changed_by_admin_id`. Holds `previous_status`, `new_status`, `notes`, `changed_at`.
- **`portfolio_media`** — many rows per vendor. FK to `vendors`, cascade delete. Holds `media_type`, `file_url`, `thumbnail_url`, `title`, `file_size_kb`, `display_order`.
- **`portfolio_links`** — many rows per vendor. FK to `vendors`, cascade delete. Holds `platform`, `url`, `title`. Table exists but is currently never written to by any service.
- **`vendor_packages`** — many rows per vendor. FK to `vendors`, cascade delete. Holds pricing/package info consumed by `VendorPackagesController`.
- **`vendor_availability`** — many rows per vendor. FK to `vendors`; optional FK to `booking_requests`. Has a `RowVersion` concurrency token (added in the second migration) and an enum-backed `status` column.
- **`service_categories`** — shared lookup table, one row per category, referenced by `vendors.category_id`.
- **`users`, `roles`, `user_roles`, etc.** — ASP.NET Identity tables (snake_case), shared across all roles including vendor.

**Relations summary**: `users (1) — (1) vendors`, `vendors (1) — (many) vendor_verifications`, `vendor_verifications (1) — (many) vendor_verification_documents`, `vendor_verifications (1) — (many) vendor_verification_history`, `vendors (1) — (many) portfolio_media`, `vendors (1) — (many) portfolio_links`, `vendors (1) — (many) vendor_packages`, `vendors (1) — (many) vendor_availability`, `service_categories (1) — (many) vendors`.

---

## 8. Possible Problems

- **`PendingVendorVerificationsSpecification` is dead code.** It is defined (`Specifications/VendorVerification/PendingVendorVerificationsSpecification.cs`) but never referenced by any service, repository call, or controller in the codebase. There is currently no way to retrieve the list of pending vendors it was clearly built for.
- **No authorization on three vendor-adjacent controllers.** `VendorAvailabilityController`, `VendorPackagesController`, and `ServiceCategoriesController` have no `[Authorize]` attributes at class or action level (verified by inspecting each file directly), unlike `AdminAccountsController` and `AdminVendorVerificationController`, which are both `[Authorize(Policy = AuthorizationPolicies.AdminOnly)]`. Any authenticated or anonymous caller can currently create/update/delete packages, availability slots, and service categories.
- **`PortfolioLink` entity and table exist but are unused.** It's fully modeled and configured (`PortfolioConfiguration.cs`) but no service or controller ever creates, reads, or deletes a `PortfolioLink` row.
- **`VerificationDocumentType.NationalId` is an unused enum member.** Registration only ever uses `NationalIdFront`, `NationalIdBack`, `SelfieWithId`, `CommercialRegistration`, `TaxCard` — the plain `NationalId` value is never referenced by `AuthService` or any other code.
- **Inconsistent transaction-commit pattern between services.** `AuthService.RegisterVendorAsync`/`RegisterClientAsync` call `CommitTransactionAsync()` directly (which internally calls `SaveChangesAsync` before committing). `VendorVerificationService.ApproveVendorAsync`/`RejectVendorAsync` call `SaveChangesAsync()` explicitly and then also call `CommitTransactionAsync()` (which saves again). This isn't a functional bug — the second save is a no-op — but it's an inconsistency between the two services' style.
- **`AttachmentService` file-type/size rules are uniform and hardcoded.** `.png`/`.jpg`/`.jpeg` only, 2 MB max, applied identically to selfies, national ID photos, portfolio images, and legal documents (commercial registration, tax card). There is no per-document-type override.
- **`AuthResponseDto` does not expose the vendor's id.** `RegisterVendorAsync` computes `vendor.Id` and embeds it in the JWT's `vendor_id` claim, but `AuthResponseDto` itself has no `VendorId` field, so a client must decode the JWT to learn the new vendor's id rather than reading it directly from the registration response body.
- **No test project in the solution** — correctness of the transaction/rollback logic, validation branches, and upload persistence is unverified by automated tests.

---

## 9. Suggested Development Order

1. **Pending Vendors API** — lowest effort, highest unblock value: the specification already exists, so this is mainly a new service method + controller action on `AdminVendorVerificationController`. Without it, admins have no way to discover which vendors are waiting for review except by querying the database directly.
2. **Vendor Details API** — needed by the same admin queue (to view submitted documents before approving/rejecting) and reuses the same `VendorVerification`/`VendorVerificationDocument` data already being fetched for the list.
3. **Vendor Profile API** — vendors need to read/update their own business info after registration; this is independent of verification review and can proceed in parallel once the admin-facing endpoints are stable.
4. **Verification History API** — the data (`VendorVerificationHistory`) is already being written by approve/reject, so exposing it is a read-only endpoint with no new write logic, best done once profile/detail views establish the response-shaping conventions.
5. **Resubmit Verification** — depends on the review flow (steps 1–2) being complete, since a vendor can only resubmit after a rejection is visible and understood.
6. **Portfolio management endpoints** — lower priority since portfolio images are already captured at registration; this only matters once vendors need to edit their profile post-onboarding.
7. **Tests** — write once the remaining endpoints stabilize, to avoid re-writing tests against changing method signatures.
8. **Swagger/XML documentation** — a final polish pass once the API surface for the Vendor module is complete.

This order front-loads the two endpoints that unblock admins (who currently cannot discover or inspect pending vendors through the API at all), then vendor-facing self-service endpoints, then the lower-urgency review-history/portfolio/testing/documentation work.

---

## 10. Progress Percentage

```
Vendor Registration ............ 100%
Verification Submission ........ 100%
Admin Review (Approve/Reject) .. 100%
Pending Vendors / Admin Queue ... 0%
Vendor Profile .................. 0%
Verification History API ........ 0%
Resubmit Verification ........... 0%
Portfolio Management ............ 0%
Testing .......................... 0%
Overall Vendor Module .......... ~45%
```

Basis: registration (data model, validation, document upload, transaction, JWT) and the admin approve/reject review flow (including history logging) are fully implemented end to end. Everything that lets a vendor or admin interact with that data afterward — viewing pending vendors, vendor profile, verification history, resubmission, and portfolio editing — has no service or endpoint yet. No automated tests exist for any of it.

---

## Summary

**Completed**: Vendor self-registration (`POST api/auth/register/vendor`) end to end — validation, document/portfolio upload, transactional persistence of `Vendor`/`VendorVerification`/`VendorVerificationDocument`/`PortfolioMedia`, role assignment, and JWT issuance. Admin verification review (`POST .../approve`, `POST .../reject`) is also fully implemented, including `VendorVerificationHistory` logging and the `ApprovedVendor` policy that unlocks vendor-only actions the moment a vendor is approved.

**Next to implement**: an admin-facing pending-vendors list/detail API (the specification for it already exists and is unused), a vendor self-profile API, a verification-history read endpoint, and a resubmission flow for rejected vendors — in that order, since the first two unblock admins and vendors from seeing data that's already being persisted.

**Blocking the workflow right now**: nothing prevents a vendor from registering or an admin from approving/rejecting via direct API calls, but there is no way for an admin to *discover* pending vendors through the API (no list endpoint), which in practice makes the review step unusable without direct database access.
