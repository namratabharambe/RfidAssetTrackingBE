using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Linq;
using System.Security.Claims;

namespace API.Extensions
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public class PermissionAuthorizeAttribute : TypeFilterAttribute
    {
        public PermissionAuthorizeAttribute(string permission) : base(typeof(PermissionFilter))
        {
            Arguments = new object[] { permission };
        }
    }

    public class PermissionFilter : IAuthorizationFilter
    {
        private readonly string _permission;

        public PermissionFilter(string permission)
        {
            _permission = permission;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;

            if (!user.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            if (user.IsInRole("System Administrator"))
            {
                return;
            }

            var hasPermission = user.Claims
                .Any(c => c.Type == "permission" && c.Value == _permission);

            if (!hasPermission)
            {
                context.Result = new ForbidResult();
            }
        }
    }
}
