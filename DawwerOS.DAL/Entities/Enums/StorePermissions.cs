namespace DawwerOS.DAL.Entities.Enums;

public static class StorePermissions
{
    public const string ViewProducts = "Products.View";
    public const string ManageProducts = "Products.Manage";
    public const string ViewInventory = "Inventory.View";
    public const string ManageInventory = "Inventory.Manage";
    public const string ViewOrders = "Orders.View";
    public const string ManageOrders = "Orders.Manage";
    public const string ManageStaff = "Staff.Manage";
    public const string ManageStore = "Store.Manage";

    public static readonly IReadOnlyList<string> All = new[]
    {
        ViewProducts,
        ManageProducts,
        ViewInventory,
        ManageInventory,
        ViewOrders,
        ManageOrders,
        ManageStaff,
        ManageStore
    };
}
