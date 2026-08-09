# Deposit System — End-to-End Lifecycle

Reference for the frontend + admin teams. Covers Phases 1–3 (booking → deposit → remainder →
success/failure → grace → client-pay / auto-cancel / manual-cancel → refund).

## Happy path (deposit booking)
1. **Book** (event **> `FullPaymentThresholdDays`**, default 7 days out): server authorizes **`DepositPercentage`** (20%) of the price and saves the card (Stripe Customer + PaymentMethod). Payment `DepositAuthorized`, booking `Pending`/`Unpaid`. *(Event ≤ 7 days → full-payment path, no deposit.)*
2. **Vendor accepts** → deposit captured. Payment `DepositPaid_RemainderDue`, booking `DepositPaid`/`Accepted`.
3. **~`RemainderChargeLeadDays`** (4) before the event, the **hourly job** charges the remainder (`Total − Deposit`) off-session. Success → Payment `FullyPaid`, booking `Paid`; client + vendor notified (in-app + email).

## Failure → grace → resolution
4. **Remainder charge fails** (any reason incl. SCA): Payment `RemainderFailed`, booking `RemainderFailed`, grace clock starts (`RemainderFailedAt`). Client notified with a "pay now" prompt. Job does NOT retry.
5. **Client pays on-session** (during or before grace): `POST /api/booking-requests/{id}/pay-remainder` (ClientOnly, ownership-checked). Eligible from `DepositPaid_RemainderDue` or `RemainderFailed`. Charges the saved card on-session (SCA-capable). Success → `FullyPaid`/`Paid`, grace cleared.
   - Response `PayRemainderResultDto { Status, PaymentIntentId, ClientSecret, RequiresAction }`. If `RequiresAction=true`, complete 3-D Secure with `ClientSecret` (Stripe.js `confirmCardPayment`); success finalized via webhook.
6. **Grace expires unpaid** → the **grace-expiry job** (hourly) routes the booking to the EXISTING admin cancellation-review queue: `CancellationRequested` + `RefundStatus=PendingReview` + **0 refund (deposit forfeited)**. Admin approves (cancel + release slot, no refund) or rejects (back to `Accepted`).

## Cancellation
- **Pre-accept (Pending):** immediate cancel + slot released + authorization voided (works for `DepositAuthorized`).
- **Manual cancel, deposit-only** (`DepositPaid_RemainderDue` / `RemainderFailed`, remainder unpaid): **immediate** cancel + slot released + **deposit forfeited** (non-refundable), NO admin review. Cancellation quote shows 0 refund. In-flight charge (`RemainderCharging`) → rejected ("payment being processed").
- **Manual cancel, fully-paid deposit** (`FullyPaid`): routes to admin review; on approve, refund is deposit-aware — **both** PaymentIntents (deposit PI + remainder PI) refunded (full or admin-adjusted partial).
- **Manual cancel, full-path** (`Completed`): admin review, single-PI refund.

## Admin refund-review
- Deposit auto-cancellations and fully-paid deposit manual cancels appear in the same "Cancellation Requests" queue (`Status=CancellationRequested`).
- **Approve** = cancel + release slot (+ refund if amount > 0); **Reject** = back to `Accepted`.
- Refunds are deposit-aware: fully-paid deposit → two-PI refund. Deposit-only (remainder unpaid) → **non-refundable** by policy (never reaches admin — forfeited immediately).

## No double-charge guarantee
The background job and client-pay share an **atomic claim** (`DepositPaid_RemainderDue | RemainderFailed → RemainderCharging`): only one actor ever charges. An abandoned SCA stuck in `RemainderCharging` past `RemainderChargingTimeoutMinutes` (60) is auto-reclaimed to `RemainderFailed`.

## States & config
- **Payment:** `DepositAuthorized → DepositPaid_RemainderDue → (RemainderCharging) → FullyPaid | RemainderFailed`; refund → `Refunded`.
- **Booking payment-status:** `Unpaid → DepositPaid → Paid | RemainderFailed → Refunded`.
- **Config (`appsettings → Booking`):** `DepositPercentage=20`, `FullPaymentThresholdDays=7`, `RemainderChargeLeadDays=4`, `GracePeriodDays=2`, `RemainderChargingTimeoutMinutes=60`.
- **Jobs (Hangfire, hourly, single-instance):** `deposit-remainder-charge` (`0 * * * *`), `deposit-remainder-grace-expiry` (`30 * * * *`).
