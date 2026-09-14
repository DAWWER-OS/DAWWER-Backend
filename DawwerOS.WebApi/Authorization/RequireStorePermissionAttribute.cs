using Microsoft.AspNetCore.Mvc;

namespace DawwerOS.WebApi.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequireStorePermissionAttribute : TypeFilterAttribute
{
    public string? Permission { get; }

    public RequireStorePermissionAttribute(string? permission = null) : base(typeof(StorePermissionFilter))
    {
        Permission = permission;
        Arguments = new object[] { permission ?? string.Empty };
    }
}
