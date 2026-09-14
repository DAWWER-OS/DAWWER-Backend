using DawwerOS.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DawwerOS.DAL.Context;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<VerificationCode> VerificationCodes => Set<VerificationCode>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<RevokedToken> RevokedTokens => Set<RevokedToken>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<StoreDocument> StoreDocuments => Set<StoreDocument>();
    public DbSet<StoreStaff> StoreStaffMembers => Set<StoreStaff>();
    public DbSet<StoreRole> StoreRoles => Set<StoreRole>();
    public DbSet<StorePermission> StorePermissions => Set<StorePermission>();
    public DbSet<StoreRolePermission> StoreRolePermissions => Set<StoreRolePermission>();
    public DbSet<StoreStaffPermission> StoreStaffPermissions => Set<StoreStaffPermission>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();


    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Enforce UTC DateTime storage and retrieval across all entities for PostgreSQL compatibility
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcNullableDateTimeConverter>();

        // Standardize decimal precision for money/financial fields to numeric(18, 2)
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        configurationBuilder.Properties<decimal?>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Enable PostgreSQL UUID extension
        modelBuilder.HasPostgresExtension("uuid-ossp");

        // Model-wide conventions
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Configure Guid primary keys to default to gen_random_uuid() in PostgreSQL
            var primaryKey = entityType.FindPrimaryKey();
            if (primaryKey != null)
            {
                foreach (var property in primaryKey.Properties)
                {
                    if (property.ClrType == typeof(Guid))
                    {
                        property.SetDefaultValueSql("gen_random_uuid()");
                    }
                }
            }

            // Foreign key convention: Restrict delete behavior to prevent accidental cascade deletions
            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
            }
        }

        // Automatically discover and apply all IEntityTypeConfiguration classes in this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        SeedInitialStaffPermissionsAndRoles(modelBuilder);
        SeedMasterCategories(modelBuilder);
    }

    private static void SeedMasterCategories(ModelBuilder modelBuilder)
    {
        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var electronicsId = Guid.Parse("00000000-0000-0000-0004-000000000001");
        var groceriesId = Guid.Parse("00000000-0000-0000-0004-000000000002");
        var fashionId = Guid.Parse("00000000-0000-0000-0004-000000000003");
        var homeId = Guid.Parse("00000000-0000-0000-0004-000000000004");
        var healthId = Guid.Parse("00000000-0000-0000-0004-000000000005");
        var sportsId = Guid.Parse("00000000-0000-0000-0004-000000000006");

        var categories = new List<Category>
        {
            new()
            {
                Id = electronicsId,
                Name = "Electronics",
                Description = "Computers, smartphones, accessories, and electronic appliances.",
                IconUrl = "/icons/categories/electronics.svg",
                IsActive = true,
                DisplayOrder = 1,
                ParentCategoryId = null,
                CreatedAt = seedDate
            },
            new()
            {
                Id = groceriesId,
                Name = "Groceries & Food",
                Description = "Everyday food items, beverages, fresh produce, and pantry staples.",
                IconUrl = "/icons/categories/groceries.svg",
                IsActive = true,
                DisplayOrder = 2,
                ParentCategoryId = null,
                CreatedAt = seedDate
            },
            new()
            {
                Id = fashionId,
                Name = "Fashion & Apparel",
                Description = "Clothing, shoes, watches, and fashion accessories.",
                IconUrl = "/icons/categories/fashion.svg",
                IsActive = true,
                DisplayOrder = 3,
                ParentCategoryId = null,
                CreatedAt = seedDate
            },
            new()
            {
                Id = homeId,
                Name = "Home & Kitchen",
                Description = "Cookware, home decor, furniture, and kitchen essentials.",
                IconUrl = "/icons/categories/home.svg",
                IsActive = true,
                DisplayOrder = 4,
                ParentCategoryId = null,
                CreatedAt = seedDate
            },
            new()
            {
                Id = healthId,
                Name = "Health & Beauty",
                Description = "Personal care, cosmetics, vitamins, and wellness products.",
                IconUrl = "/icons/categories/health.svg",
                IsActive = true,
                DisplayOrder = 5,
                ParentCategoryId = null,
                CreatedAt = seedDate
            },
            new()
            {
                Id = sportsId,
                Name = "Sports & Outdoors",
                Description = "Sporting equipment, fitness gear, and outdoor activity items.",
                IconUrl = "/icons/categories/sports.svg",
                IsActive = true,
                DisplayOrder = 6,
                ParentCategoryId = null,
                CreatedAt = seedDate
            },

            // Subcategories under Electronics
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0004-000000000011"),
                Name = "Smartphones & Tablets",
                Description = "Mobile phones, tablets, and wearable accessories.",
                IsActive = true,
                DisplayOrder = 1,
                ParentCategoryId = electronicsId,
                CreatedAt = seedDate
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0004-000000000012"),
                Name = "Laptops & Computers",
                Description = "Personal computers, laptops, monitors, and peripherals.",
                IsActive = true,
                DisplayOrder = 2,
                ParentCategoryId = electronicsId,
                CreatedAt = seedDate
            },

            // Subcategories under Groceries
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0004-000000000021"),
                Name = "Fresh Produce",
                Description = "Fruits, vegetables, and fresh herbs.",
                IsActive = true,
                DisplayOrder = 1,
                ParentCategoryId = groceriesId,
                CreatedAt = seedDate
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0004-000000000022"),
                Name = "Beverages & Drinks",
                Description = "Water, juices, soft drinks, tea, and coffee.",
                IsActive = true,
                DisplayOrder = 2,
                ParentCategoryId = groceriesId,
                CreatedAt = seedDate
            }
        };

        modelBuilder.Entity<Category>().HasData(categories);
    }

    private static void SeedInitialStaffPermissionsAndRoles(ModelBuilder modelBuilder)
    {
        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // 1. Seed the 8 Initial Permissions
        var permViewProducts = new StorePermission
        {
            Id = Guid.Parse("00000000-0000-0000-0001-000000000001"),
            Code = "Products.View",
            Name = "View Products",
            Category = "Products",
            Description = "View catalog products and product details.",
            CreatedAt = seedDate
        };

        var permManageProducts = new StorePermission
        {
            Id = Guid.Parse("00000000-0000-0000-0001-000000000002"),
            Code = "Products.Manage",
            Name = "Manage Products",
            Category = "Products",
            Description = "Create, edit, and deactivate store products and categories.",
            CreatedAt = seedDate
        };

        var permViewInventory = new StorePermission
        {
            Id = Guid.Parse("00000000-0000-0000-0001-000000000003"),
            Code = "Inventory.View",
            Name = "View Inventory",
            Category = "Inventory",
            Description = "View store stock levels and inventory availability.",
            CreatedAt = seedDate
        };

        var permManageInventory = new StorePermission
        {
            Id = Guid.Parse("00000000-0000-0000-0001-000000000004"),
            Code = "Inventory.Manage",
            Name = "Manage Inventory",
            Category = "Inventory",
            Description = "Adjust stock levels, restock items, and set availability thresholds.",
            CreatedAt = seedDate
        };

        var permViewOrders = new StorePermission
        {
            Id = Guid.Parse("00000000-0000-0000-0001-000000000005"),
            Code = "Orders.View",
            Name = "View Orders",
            Category = "Orders",
            Description = "View customer pickup reservations and order history.",
            CreatedAt = seedDate
        };

        var permManageOrders = new StorePermission
        {
            Id = Guid.Parse("00000000-0000-0000-0001-000000000006"),
            Code = "Orders.Manage",
            Name = "Manage Orders",
            Category = "Orders",
            Description = "Process orders, accept/reject reservations, and update fulfillment states.",
            CreatedAt = seedDate
        };

        var permManageStaff = new StorePermission
        {
            Id = Guid.Parse("00000000-0000-0000-0001-000000000007"),
            Code = "Staff.Manage",
            Name = "Manage Staff",
            Category = "Staff",
            Description = "Add, update, suspend, and remove store staff accounts and assign roles.",
            CreatedAt = seedDate
        };

        var permManageStore = new StorePermission
        {
            Id = Guid.Parse("00000000-0000-0000-0001-000000000008"),
            Code = "Store.Manage",
            Name = "Manage Store",
            Category = "Store",
            Description = "Manage store settings, operating hours, and general configuration.",
            CreatedAt = seedDate
        };

        modelBuilder.Entity<StorePermission>().HasData(
            permViewProducts,
            permManageProducts,
            permViewInventory,
            permManageInventory,
            permViewOrders,
            permManageOrders,
            permManageStaff,
            permManageStore
        );

        // 2. Seed System Preset Roles
        var roleManager = new StoreRole
        {
            Id = Guid.Parse("00000000-0000-0000-0002-000000000001"),
            StoreId = null,
            Name = "Store Manager",
            Description = "Full operational and administrative store management access.",
            IsSystemRole = true,
            CreatedAt = seedDate
        };

        var roleSupervisor = new StoreRole
        {
            Id = Guid.Parse("00000000-0000-0000-0002-000000000002"),
            StoreId = null,
            Name = "Store Supervisor",
            Description = "Manages catalog, inventory, and customer reservations.",
            IsSystemRole = true,
            CreatedAt = seedDate
        };

        var roleInventoryClerk = new StoreRole
        {
            Id = Guid.Parse("00000000-0000-0000-0002-000000000003"),
            StoreId = null,
            Name = "Inventory Clerk",
            Description = "Maintains store stock and catalog entries.",
            IsSystemRole = true,
            CreatedAt = seedDate
        };

        var roleCashier = new StoreRole
        {
            Id = Guid.Parse("00000000-0000-0000-0002-000000000004"),
            StoreId = null,
            Name = "Cashier",
            Description = "Handles pickup orders, customer checkouts, and inventory views.",
            IsSystemRole = true,
            CreatedAt = seedDate
        };

        modelBuilder.Entity<StoreRole>().HasData(
            roleManager,
            roleSupervisor,
            roleInventoryClerk,
            roleCashier
        );

        // 3. Seed Role Permissions
        var rolePermissions = new List<StoreRolePermission>
        {
            // Store Manager: All 8 permissions
            new() { Id = Guid.Parse("00000000-0000-0003-0001-000000000001"), StoreRoleId = roleManager.Id, StorePermissionId = permViewProducts.Id, CreatedAt = seedDate },
            new() { Id = Guid.Parse("00000000-0000-0003-0001-000000000002"), StoreRoleId = roleManager.Id, StorePermissionId = permManageProducts.Id, CreatedAt = seedDate },
            new() { Id = Guid.Parse("00000000-0000-0003-0001-000000000003"), StoreRoleId = roleManager.Id, StorePermissionId = permViewInventory.Id, CreatedAt = seedDate },
            new() { Id = Guid.Parse("00000000-0000-0003-0001-000000000004"), StoreRoleId = roleManager.Id, StorePermissionId = permManageInventory.Id, CreatedAt = seedDate },
            new() { Id = Guid.Parse("00000000-0000-0003-0001-000000000005"), StoreRoleId = roleManager.Id, StorePermissionId = permViewOrders.Id, CreatedAt = seedDate },
            new() { Id = Guid.Parse("00000000-0000-0003-0001-000000000006"), StoreRoleId = roleManager.Id, StorePermissionId = permManageOrders.Id, CreatedAt = seedDate },
            new() { Id = Guid.Parse("00000000-0000-0003-0001-000000000007"), StoreRoleId = roleManager.Id, StorePermissionId = permManageStaff.Id, CreatedAt = seedDate },
            new() { Id = Guid.Parse("00000000-0000-0003-0001-000000000008"), StoreRoleId = roleManager.Id, StorePermissionId = permManageStore.Id, CreatedAt = seedDate },

            // Store Supervisor: Products & Inventory & Orders (6 permissions)
            new() { Id = Guid.Parse("00000000-0000-0003-0002-000000000001"), StoreRoleId = roleSupervisor.Id, StorePermissionId = permViewProducts.Id, CreatedAt = seedDate },
            new() { Id = Guid.Parse("00000000-0000-0003-0002-000000000002"), StoreRoleId = roleSupervisor.Id, StorePermissionId = permManageProducts.Id, CreatedAt = seedDate },
            new() { Id = Guid.Parse("00000000-0000-0003-0002-000000000003"), StoreRoleId = roleSupervisor.Id, StorePermissionId = permViewInventory.Id, CreatedAt = seedDate },
            new() { Id = Guid.Parse("00000000-0000-0003-0002-000000000004"), StoreRoleId = roleSupervisor.Id, StorePermissionId = permManageInventory.Id, CreatedAt = seedDate },
            new() { Id = Guid.Parse("00000000-0000-0003-0002-000000000005"), StoreRoleId = roleSupervisor.Id, StorePermissionId = permViewOrders.Id, CreatedAt = seedDate },
            new() { Id = Guid.Parse("00000000-0000-0003-0002-000000000006"), StoreRoleId = roleSupervisor.Id, StorePermissionId = permManageOrders.Id, CreatedAt = seedDate },

            // Inventory Clerk: Products & Inventory (4 permissions)
            new() { Id = Guid.Parse("00000000-0000-0003-0003-000000000001"), StoreRoleId = roleInventoryClerk.Id, StorePermissionId = permViewProducts.Id, CreatedAt = seedDate },
            new() { Id = Guid.Parse("00000000-0000-0003-0003-000000000002"), StoreRoleId = roleInventoryClerk.Id, StorePermissionId = permManageProducts.Id, CreatedAt = seedDate },
            new() { Id = Guid.Parse("00000000-0000-0003-0003-000000000003"), StoreRoleId = roleInventoryClerk.Id, StorePermissionId = permViewInventory.Id, CreatedAt = seedDate },
            new() { Id = Guid.Parse("00000000-0000-0003-0003-000000000004"), StoreRoleId = roleInventoryClerk.Id, StorePermissionId = permManageInventory.Id, CreatedAt = seedDate },

            // Cashier: Products.View, Inventory.View, Orders.View, Orders.Manage (4 permissions)
            new() { Id = Guid.Parse("00000000-0000-0003-0004-000000000001"), StoreRoleId = roleCashier.Id, StorePermissionId = permViewProducts.Id, CreatedAt = seedDate },
            new() { Id = Guid.Parse("00000000-0000-0003-0004-000000000002"), StoreRoleId = roleCashier.Id, StorePermissionId = permViewInventory.Id, CreatedAt = seedDate },
            new() { Id = Guid.Parse("00000000-0000-0003-0004-000000000003"), StoreRoleId = roleCashier.Id, StorePermissionId = permViewOrders.Id, CreatedAt = seedDate },
            new() { Id = Guid.Parse("00000000-0000-0003-0004-000000000004"), StoreRoleId = roleCashier.Id, StorePermissionId = permManageOrders.Id, CreatedAt = seedDate }
        };

        modelBuilder.Entity<StoreRolePermission>().HasData(rolePermissions);
    }

    public override int SaveChanges()
    {
        UpdateAuditFields();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAuditFields();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateAuditFields()
    {
        // Enforce immutability of AuditLog entries: Audit records cannot be modified or deleted
        var immutableViolations = ChangeTracker.Entries<AuditLog>()
            .Where(e => e.State == EntityState.Modified || e.State == EntityState.Deleted);

        if (immutableViolations.Any())
        {
            throw new InvalidOperationException("Audit log entries are immutable and cannot be modified or deleted.");
        }

        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
            {
                // Ensure CreatedAt is set to UTC now
                var createdAtProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == nameof(BaseEntity.CreatedAt));
                if (createdAtProp != null && (createdAtProp.CurrentValue is not DateTime dt || dt == default))
                {
                    createdAtProp.CurrentValue = now;
                }

                // Ensure Guid ID is generated if empty
                var idProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == nameof(BaseEntity.Id));
                if (idProp != null && idProp.Metadata.ClrType == typeof(Guid) && (idProp.CurrentValue == null || (Guid)idProp.CurrentValue == Guid.Empty))
                {
                    idProp.CurrentValue = Guid.NewGuid();
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                // Set UpdatedAt on update
                var updatedAtProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == nameof(BaseEntity.UpdatedAt));
                if (updatedAtProp != null)
                {
                    updatedAtProp.CurrentValue = now;
                }
            }
        }
    }
}

public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter() : base(
        v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }
}

public class UtcNullableDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public UtcNullableDateTimeConverter() : base(
        v => !v.HasValue ? v : (v.Value.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)),
        v => !v.HasValue ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc))
    {
    }
}

