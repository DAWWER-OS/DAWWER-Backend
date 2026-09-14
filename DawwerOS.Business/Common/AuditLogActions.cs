namespace DawwerOS.Business.Common;

public static class AuditLogActions
{
    // Merchant & Store Onboarding Actions
    public const string MerchantApproved = "MERCHANT_APPROVED";
    public const string MerchantRejected = "MERCHANT_REJECTED";
    public const string MerchantInfoRequested = "MERCHANT_INFO_REQUESTED";

    // Store Operations Actions
    public const string StoreSuspended = "STORE_SUSPENDED";
    public const string StoreActivated = "STORE_ACTIVATED";

    // Account Governance Actions
    public const string AccountSuspended = "ACCOUNT_SUSPENDED";
    public const string AccountActivated = "ACCOUNT_ACTIVATED";

    // Staff & Access Actions
    public const string StaffCreated = "STAFF_CREATED";
    public const string StaffRemoved = "STAFF_REMOVED";
    public const string RoleAssigned = "ROLE_ASSIGNED";
    public const string PermissionsUpdated = "PERMISSIONS_UPDATED";
    public const string RoleCreated = "ROLE_CREATED";
    public const string RoleUpdated = "ROLE_UPDATED";
    public const string RoleDeleted = "ROLE_DELETED";

    // Category Taxonomy Actions
    public const string CategoryCreated = "CATEGORY_CREATED";
    public const string CategoryUpdated = "CATEGORY_UPDATED";
    public const string CategoryActivated = "CATEGORY_ACTIVATED";
    public const string CategoryDeactivated = "CATEGORY_DEACTIVATED";
}
