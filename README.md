# DawwerOS - Backend API

Production-ready, layered **ASP.NET Core 9 Web API** built with **Entity Framework Core 9**, **PostgreSQL** (`Npgsql`), and **Swagger UI** (`Swashbuckle.AspNetCore`).

---

## 🏗️ Architecture Overview

The solution follows a strict **3-project layered architecture**:

```text
DawwerOS.WebApi  (Controllers, Middleware, Swagger UI, Startup)
       │
       ▼
DawwerOS.Business (DTOs, Generic & Domain Services, Validation, Unified ApiResponse)
       │
       ▼
DawwerOS.DAL      (EF Core 9, PostgreSQL Npgsql, Entities, Generic Repository, Migrations)
```

### Architectural Principles & Rules
- **Decoupled Layers**:
  - `DawwerOS.DAL` has **no** reference to Business or WebApi.
  - `DawwerOS.Business` references **only** `DawwerOS.DAL`.
  - `DawwerOS.WebApi` references `DawwerOS.Business` (plus EF Core Design for migration tooling).
- **Strict Controller Boundaries**: Controllers **never** interact directly with `AppDbContext` or Repositories. All communication flows exclusively through Business services.
- **Repository Pattern**: Generic repository (`IGenericRepository<T>`) provides asynchronous CRUD, querying, existence checks, counting, and tracking separation.
- **Generic Service Pattern**: Highly extensible `GenericService<TEntity, TResponseDto, TCreateDto, TUpdateDto>` and `GenericService<TEntity, TDto>` handling full CRUD, predicate filtering, manual DTO mapping, and lifecycle validation hooks (`BeforeCreateAsync`, `BeforeUpdateAsync`, `BeforeDeleteAsync`, etc.).
- **Unified API Response**: All service operations return `ApiResponse<T>` providing consistent JSON structure across the entire API:
  ```json
  {
    "success": true,
    "message": "Operation completed successfully.",
    "data": { ... },
    "errors": null
  }
  ```
- **Global Exception Middleware**: Catches unhandled exceptions, logs detailed diagnostics via `ILogger`, and returns HTTP 500 with a safe `ApiResponse<object>` without leaking database internals or stack traces in production.

---

## 📁 Solution Structure

```text
DawwerOS/
├── DawwerOS.sln
│
├── DawwerOS.DAL/
│   ├── Configurations/              # IEntityTypeConfiguration<T> classes
│   ├── Context/
│   │   └── AppDbContext.cs          # EF Core DbContext with auto-discovery of configurations
│   ├── Entities/
│   │   └── BaseEntity.cs            # Base entity with Id, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy
│   ├── Repositories/
│   │   ├── Interfaces/
│   │   │   └── IGenericRepository.cs
│   │   └── Implementations/
│   │       └── GenericRepository.cs
│   ├── DependencyInjection.cs       # Extension method: AddDataAccess(configuration)
│   └── DawwerOS.DAL.csproj
│
├── DawwerOS.Business/
│   ├── Common/
│   │   └── ApiResponse.cs           # Unified API response envelope
│   ├── DTOs/                        # Request / Response DTOs
│   ├── Services/
│   │   ├── Interfaces/
│   │   │   └── IGenericService.cs   # Generic service contract (multi-DTO & single-DTO support)
│   │   └── Implementations/
│   │       └── GenericService.cs    # Abstract generic service with full CRUD & lifecycle hooks
│   ├── DependencyInjection.cs       # Extension method: AddBusiness()
│   └── DawwerOS.Business.csproj
│
└── DawwerOS.WebApi/
    ├── Controllers/                 # RESTful API Controllers
    ├── Middleware/
    │   └── ExceptionMiddleware.cs   # Global unhandled exception handler
    ├── Properties/
    │   └── launchSettings.json      # Launch profiles (HTTPS 7212, HTTP 5037)
    ├── appsettings.json
    ├── appsettings.Development.json # PostgreSQL ConnectionString configuration
    ├── Program.cs                   # Clean minimal startup pipeline
    └── DawwerOS.WebApi.csproj
```

---

## 🛠️ Technology Stack

| Technology | Description |
| :--- | :--- |
| **.NET 9** | Latest LTS target framework (`net9.0`) |
| **ASP.NET Core Web API** | REST API framework |
| **Entity Framework Core 9** | ORM for database modeling and migrations |
| **PostgreSQL / Npgsql** | High-performance open-source relational database provider |
| **Swashbuckle.AspNetCore** | Swagger UI and OpenAPI documentation generation |
| **Dependency Injection** | Built-in Microsoft DI extensions across all layers |

---

## 🚀 Getting Started

### 1. Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (version 9.0.300 or later)
- [PostgreSQL](https://www.postgresql.org/) database server running locally or via Docker
- `dotnet-ef` global CLI tool:
  ```powershell
  dotnet tool install --global dotnet-ef
  # or update if already installed:
  dotnet tool update --global dotnet-ef
  ```

### 2. Configure PostgreSQL Connection String
Update `DawwerOS.WebApi/appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=DawwerOSDb;Username=postgres;Password=your_password"
  }
}
```

### 3. Build the Solution
```powershell
dotnet restore
dotnet build DawwerOS.sln
```

### 4. Create and Apply Migrations
Whenever you add or modify database entities:
```powershell
# Add a new migration
dotnet ef migrations add <MigrationName> --project DawwerOS.DAL --startup-project DawwerOS.WebApi --output-dir Migrations

# Apply migrations to PostgreSQL
dotnet ef database update --project DawwerOS.DAL --startup-project DawwerOS.WebApi
```

### 5. Run the API
```powershell
dotnet run --project DawwerOS.WebApi
```

### 6. Access Swagger UI
- **HTTPS:** [https://localhost:7212/swagger](https://localhost:7212/swagger)
- **HTTP:** [http://localhost:5037/swagger](http://localhost:5037/swagger)

---

## 🧩 How to Add a New Entity (In 4 Easy Steps)

Adding a new entity to **DawwerOS** requires minimal boilerplate:

### Step 1: Create Entity & Configuration (`DawwerOS.DAL`)
```csharp
// DawwerOS.DAL/Entities/WasteItem.cs
public class WasteItem : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public decimal WeightKg { get; set; }
}

// DawwerOS.DAL/Configurations/WasteItemConfiguration.cs
public class WasteItemConfiguration : IEntityTypeConfiguration<WasteItem>
{
    public void Configure(EntityTypeBuilder<WasteItem> builder)
    {
        builder.ToTable("WasteItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.Property(x => x.WeightKg).HasPrecision(10, 2);
    }
}
```
Add the `DbSet` to `AppDbContext.cs`:
```csharp
public DbSet<WasteItem> WasteItems => Set<WasteItem>();
```

### Step 2: Create DTOs (`DawwerOS.Business`)
```csharp
// DawwerOS.Business/DTOs/WasteItems/WasteItemDto.cs
public class WasteItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal WeightKg { get; set; }
}

public class CreateWasteItemDto
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Range(0.01, 10000.0)]
    public decimal WeightKg { get; set; }
}

public class UpdateWasteItemDto : CreateWasteItemDto { }
```

### Step 3: Create Service (`DawwerOS.Business`)
Inherit from `GenericService` to get full CRUD and query operations automatically:
```csharp
public interface IWasteItemService : IGenericService<WasteItem, WasteItemDto, CreateWasteItemDto, UpdateWasteItemDto> { }

public class WasteItemService 
    : GenericService<WasteItem, WasteItemDto, CreateWasteItemDto, UpdateWasteItemDto>, IWasteItemService
{
    public WasteItemService(IGenericRepository<WasteItem> repository) : base(repository) { }

    protected override WasteItemDto MapToResponseDto(WasteItem entity) => new()
    {
        Id = entity.Id,
        Title = entity.Title,
        WeightKg = entity.WeightKg
    };

    protected override WasteItem MapToEntity(CreateWasteItemDto dto) => new()
    {
        Title = dto.Title,
        WeightKg = dto.WeightKg
    };

    protected override void UpdateEntity(WasteItem entity, UpdateWasteItemDto dto)
    {
        entity.Title = dto.Title;
        entity.WeightKg = dto.WeightKg;
    }
}
```
Register the service in `DawwerOS.Business/DependencyInjection.cs`:
```csharp
services.AddScoped<IWasteItemService, WasteItemService>();
```

### Step 4: Create Controller (`DawwerOS.WebApi`)
```csharp
[ApiController]
[Route("api/waste-items")]
[Produces("application/json")]
public class WasteItemsController : ControllerBase
{
    private readonly IWasteItemService _service;

    public WasteItemsController(IWasteItemService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct) 
        => Ok(await _service.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWasteItemDto dto, CancellationToken ct)
    {
        var result = await _service.CreateAsync(dto, ct);
        return result.Success ? CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result) : BadRequest(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWasteItemDto dto, CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, dto, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
```

---

## 🌿 Git Branches & Workflow

- **`main`**: Production-ready, stable codebase.
- **`development`**: Active development branch for ongoing features and changes.
