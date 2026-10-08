namespace DawwerOS.DAL.Entities.Enums;

public enum OrderStatus
{
    Submitted = 1,
    Accepted = 2,
    Preparing = 3,
    Ready = 4,
    Completed = 5,
    Rejected = 6,
    Cancelled = 7,
    Expired = 8
}

public enum PaymentStatus
{
    AwaitingPayment = 1,
    UnderReview = 2,
    EvidenceRejected = 3,
    Paid = 4,
    DueOnPickup = 5,
    DueOnDelivery = 6,
    RefundPending = 7,
    PartiallyRefunded = 8,
    Refunded = 9
}

public enum FulfillmentType
{
    Pickup = 1,
    Delivery = 2
}

public enum ReservationStatus
{
    Active = 1,
    Consumed = 2,
    Released = 3,
    Expired = 4
}

public enum ReservationHoldType
{
    CartCheckout = 1,
    AwaitingPayment = 2
}

public enum ReceiptReviewDecision
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

public enum AmendmentStatus
{
    PendingCustomerApproval = 1,
    Approved = 2,
    Rejected = 3,
    Expired = 4
}

public enum RefundTaskStatus
{
    Pending = 1,
    RepaymentUploaded = 2,
    Verified = 3,
    Rejected = 4
}

public enum DiscrepancyType
{
    Underpayment = 1,
    Overpayment = 2
}

public enum DiscrepancyStatus
{
    Flagged = 1,
    Resolved = 2,
    WrittenOff = 3
}
