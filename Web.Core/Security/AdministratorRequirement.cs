using Cuyahoga.Core.Domain;
using Microsoft.AspNetCore.Authorization;

namespace Cuyahoga.Web.Core.Security
{
	/// <summary>
	/// The logged-in user has the Administrator permission (like AdminBasePage of the WebForms site checks).
	/// </summary>
	public class AdministratorRequirement : AuthorizationHandler<AdministratorRequirement>, IAuthorizationRequirement
	{
		public const string PolicyName = "Administrator";

		protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AdministratorRequirement requirement)
		{
			HttpContext httpContext = context.Resource as HttpContext;
			User user = httpContext != null ? LegacyAuthenticationHandler.GetUser(httpContext) : null;
			if (user != null && user.HasPermission(AccessLevel.Administrator))
			{
				context.Succeed(requirement);
			}
			return Task.CompletedTask;
		}
	}
}
