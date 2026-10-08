ذ# DAWER — Sprint 3

## Sprint 3 — Online Orders and Payment Receipts

**Start Date:** Monday, 28 September 2026  
**End Date:** Tuesday, 6 October 2026

**Sprint Goal:** Implement single-store checkout with transactional stock holds, order confirmation workflows, manual payment receipt verification, and cancellation/refund accounting.

---

## Feature 3.1: Shopping Lists, Cart, and Stock Holds

**Target Completion:** 30 September 2026  
**Depends On:** Sprint 2 published catalog

### Backend (.NET Core)
- Implement cart management.
- Implement order creation.
- Store price and item snapshots at order creation.
- Implement atomic stock holds.
- Implement Hangfire recurring jobs to release expired stock holds automatically.

### Frontend
- Build merchant order intake queues.
- Build stock allocation monitors.

### Mobile
- Build store-scoped shopping lists.
- Build cart item quantity steppers.
- Show price breakdowns.
- Build order submission flows.

### Acceptance Criteria
- Each order is restricted to a single store.
- Expired stock holds automatically return reserved inventory.
- Concurrent order submissions cannot oversell stock.

---

## Feature 3.2: Pickup and Delivery Options

**Target Completion:** 2 October 2026  
**Depends On:** Feature 3.1 and merchant delivery rules

### Backend (.NET Core)
- Validate delivery addresses against merchant delivery zones.
- Calculate delivery surcharges.
- Freeze fulfillment information snapshots on the order.

### Frontend
- Build merchant delivery zone configuration.
- Build delivery fee rules.
- Build pickup window settings.

### Mobile
- Build checkout screens with:
  - Pickup option.
  - Delivery option.
  - Address entry.
  - Delivery notes.
  - Finalized cost breakdown.

### Acceptance Criteria
- Orders outside supported delivery areas are blocked.
- Delivery fees are locked into the order snapshot at submission.

---

## Feature 3.3: Merchant Review and Order History

**Target Completion:** 3 October 2026  
**Depends On:** Features 3.1 and 3.2

### Order Statuses
- Submitted
- Accepted
- Preparing
- Ready
- Completed
- Rejected
- Cancelled
- Expired

### Backend (.NET Core)
- Enforce order state-machine transitions.
- Manage customer consent records for revised totals.
- Record timestamps for status transitions.

### Frontend
- Build merchant order management boards.
- Build substitution selectors.
- Build revised total submission tools.

### Mobile
- Build order tracking timelines.
- Implement status notification handlers.
- Build approval prompts for amended totals.

### Acceptance Criteria
- Any price or item amendment requires explicit customer approval before the order advances.
- Rejected or expired orders release stock holds immediately.

---

## Feature 3.4: Payment Receipt Upload and Review

**Target Completion:** 5 October 2026  
**Depends On:** Feature 3.3

### Payment Statuses
- Awaiting Payment
- Under Review
- Evidence Rejected
- Paid
- Due on Pickup
- Due on Delivery
- Refund Pending
- Partially Refunded
- Refunded

### Backend (.NET Core)
- Implement private receipt storage access.
- Log payment proof submissions.
- Implement payment reviewer permissions.
- Enforce payment state transitions.

### Frontend
- Build payment review interfaces.
- Build receipt inspection viewers.
- Build approval/rejection dialogs.
- Build cash logging controls.

### Mobile
- Build receipt image/file upload.
- Show payment status indicators.
- Show rejection reasons.
- Support payment proof resubmission.

### Acceptance Criteria
- Uploading a payment receipt does not automatically mark the order as Paid.
- Manual reviewer approval is required before payment is confirmed.
- Duplicate receipt hashes are flagged to reduce replay fraud.

---

## Feature 3.5: Cancellation, Refunds, and Discrepancies

**Target Completion:** 6 October 2026  
**Depends On:** Feature 3.4

### Backend (.NET Core)
- Implement cancellation cutoff rules.
- Implement refund ledger adjustments.
- Track overpayments and underpayments.
- Flag balance discrepancies.

### Frontend
- Build refund review interfaces.
- Build repayment evidence uploaders.
- Build store discrepancy logs.

### Mobile
- Display cancellation actions only within permitted windows.
- Display refund status history.

### Acceptance Criteria
- Cancelling a paid order creates a traceable refund task.
- Paid-order cancellation does not directly create an unverified balance change.
- Unresolved financial discrepancies are excluded from settlements.

---

## Sprint 3 Deliverables

By the end of Sprint 3, the platform should support:

- Single-store shopping carts.
- Transactional stock reservations.
- Automatic release of expired stock holds.
- Pickup and delivery checkout options.
- Delivery zone and fee validation.
- Merchant order review and status management.
- Customer approval of amended totals.
- Payment receipt upload.
- Manual payment verification.
- Cancellation workflows.
- Refund tracking.
- Overpayment and underpayment handling.
- Financial discrepancy tracking.

---

## Sprint 3 Core Flow

```text
Published Product
      ↓
Add to Cart
      ↓
Reserve Stock
      ↓
Choose Pickup / Delivery
      ↓
Submit Order
      ↓
Merchant Review
      ↓
Customer Approval if Total Changes
      ↓
Upload Payment Receipt
      ↓
Payment Review
      ↓
Paid / Rejected / Resubmit
      ↓
Cancellation or Refund Handling if Needed
```
