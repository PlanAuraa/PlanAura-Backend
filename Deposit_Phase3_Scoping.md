# Deposit System — Phase 3 Scoping Notes

Prerequisites investigation for Phase 3 (notifications + grace period + cancellation/refund-review).
Phases 1 & 2 are committed (`b9214a2`, `c3defa6`) and their migrations applied. This note is the
read-only findings snapshot; the proposed section breakdown lives below the findings.

## Findings — what exists vs. what's new

### 1. Email — infra EXISTS, but NOT wired to notifications
- `IEmailService` / `EmailService` (MailKit SMTP via `MailSettings`, Gmail creds in appsettings), registered in DI.
- Used ONLY in `AuthService` (password-reset codes). NOT called by `NotificationService` or any booking/deposit flow — deposit events today are in-app only, no email.
- Phase 3 = integrate `IEmailService` into the notification path (not new infra, but real integration).
- Caveats: `SendEmail` is synchronous/blocking (connect+send inline, no queue/retry), plain-text only (no HTML template), no validation. Wants best-effort/async + templates for transactional volume.

### 2. In-app notifications — FULLY EXISTS, ready
- `Notification` entity (UserId, Type, Title, Body, DataJson, IsRead, CreatedAt).
- `NotificationService.NotifyUserAsync` / `NotifyRoleAsync` create rows; `GetMyNotificationsAsync` / `MarkAsReadAsync` / `MarkAllAsReadAsync` feed the bell icon. Types are const strings in `NotificationTypes`.
- Deposit flows already use it. Phase 3 = add new `NotificationTypes` + calls. Zero new infra.

### 3. Grace period — config only, everything else NEW
- `GracePeriodDays = 2` in `BookingOptions`, used nowhere (single reference).
- No "grace active" state; `RemainderFailed` just rests, job never retries it.
- New work: a grace clock (timestamp when RemainderFailed began — not recorded today); a client on-session "pay remainder now" path (the deferred SCA flow); a grace-expiry job pass → escalate to cancellation.
- Ends by: (a) client pays on-session → FullyPaid; or (b) grace expires → auto-cancellation.

### 4. Admin refund-review — EXISTS in THIS codebase; extend, don't rebuild
- Client `RequestCancellationAsync` → CancellationRequested + RefundStatus.PendingReview + estimate; notifies admins.
- Admin queue `IAdminBookingService.GetCancellationRequestsAsync()` (+ GetOpenDisputesAsync).
- Admin decision `ApproveCancellationAsync` (real Stripe refund via `AdminPaymentService.RefundPaymentAsync`) / `RejectCancellationAsync`. Exposed via `AdminBookingController`. `RefundStatus` enum models the lifecycle.
- **Gap A:** refund path only matches `PaymentStatus.Completed` (RefundPaymentAsync guard + CompletedPaymentByBookingRequestSpecification). Deposit payments are FullyPaid / DepositPaid_RemainderDue / RemainderFailed — never Completed → can't be refunded as-is. Needs widening.
- **Gap B:** a fully-paid deposit booking was charged via TWO PaymentIntents (deposit `GatewayReference` + `RemainderGatewayReference`). RefundPaymentAsync refunds only one PI. A full refund must refund both — biggest refund integration.
- **Gap C:** refund % is computed off `booking.AgreedPrice` (total); for a booking where only the deposit was collected (RemainderFailed), refunding a % of total over-refunds. Basis must become "amount actually captured".

## Scope summary
| Area | Status |
|---|---|
| In-app notifications | EXISTS, ready — add types + calls |
| Email service | EXISTS but unwired (+ sync/no-template caveats) |
| Admin refund-review queue | EXISTS in-codebase — extend |
| Grace period | Config only — clock, on-session pay, expiry job all new |
| Client on-session "pay remainder" | New (deferred SCA flow) |
| Deposit-aware refund (Gaps A/B/C) | New integration on existing refund path |

Riskiest new pieces: client on-session remainder payment (SCA), two-PaymentIntent refund (Gap B),
grace-expiry auto-cancel job.

---

## PROPOSED Phase 3 section breakdown (NOT yet approved — for review)

Legend: [LOW] wire into existing · [RISK] new/risky · [ADMIN] touches admin refund-review (cross-team).

- **A — Notification plumbing** [LOW]. Add deposit `NotificationTypes` + a "notify = in-app (+ best-effort email)" helper wiring `IEmailService` into `NotificationService`. Touches: NotificationService, NotificationTypes, EmailService. Deps: none. Foundation for C/D/E.
- **B — Grace clock** [LOW]. Additive column (e.g. `Payment.RemainderFailedAt` / `GraceEndsAt`); set it in the existing `RecordRemainderFailedAsync`. Touches: Payment entity + config, RemainderChargeJob, migration. Deps: none. Foundation for D/E.
- **C — Remainder outcome notifications** [LOW]. Notify client/vendor on remainder charged (FullyPaid) and remainder failed (with a "pay now" prompt for Phase-3 grace). Touches: RemainderChargeJob. Deps: A, B.
- **D — Client on-session "pay remainder now"** [RISK]. New client endpoint + on-session Stripe PaymentIntent against the saved card, reconcile → FullyPaid; ends grace. RISK: SCA/on-session confirmation + PCI + must share the Phase-2 idempotency/state gate so it can't race the job. Touches: BookingService/PaymentService, new controller action, gateway. Deps: B.
- **F — Deposit-aware refund extension** [RISK][ADMIN]. Fix Gaps A/B/C: widen refund to accept FullyPaid; refund BOTH PIs for a fully-paid deposit; correct refund basis to amount actually captured. RISK: two-PI refund correctness + partial states. Touches: AdminPaymentService, cancellation flow, specs. Deps: none structural; prerequisite for E's refund.
- **E — Grace-expiry auto-cancel job** [RISK][ADMIN-adjacent]. New recurring job: RemainderFailed past `GracePeriodDays` with no client payment → cancel, release slot, and route the deposit refund into the existing admin review queue (PendingReview). RISK: must not race D (client paying at expiry) — state-gated like Phase 2; auto-cancel side effects. Touches: new job + runner, cancellation/review path. Deps: B, D, and the review queue (existing); F for correct refund execution.

### Recommended order (smallest/least-risky first, risky pieces sequenced)
1. A (notifications) → 2. B (grace clock) → 3. C (remainder notifications) → 4. D (on-session pay, RISK) → 5. F (deposit-aware refund, RISK/ADMIN) → 6. E (grace-expiry auto-cancel, RISK).

Rationale: A/B/C are low-risk foundations that unlock the rest. D before E so the client pay path exists and both share state guards. F before E so auto-cancel's refund is deposit-correct end-to-end (E can be decoupled to route-to-review-only if admin coordination on F is delayed). F and E are the cross-team/admin-touching sections.
