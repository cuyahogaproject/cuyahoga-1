using Cuyahoga.Web.Core.PublicSite;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Cuyahoga.Web.Core.Services
{
	/// <summary>
	/// Activates the modules before a page of this site runs (not for requests that are forwarded to the WebForms
	/// site). It runs before authentication, which already opens an NHibernate session.
	/// </summary>
	public class ModuleActivationMiddleware
	{
		private readonly RequestDelegate _next;

		public ModuleActivationMiddleware(RequestDelegate next)
		{
			this._next = next;
		}

		public async Task InvokeAsync(HttpContext context, ModuleActivation moduleActivation,
			InstallationState installationState)
		{
			// Not while the database isn't there yet or is being upgraded (the installer pages).
			if (context.GetEndpoint()?.Metadata.GetMetadata<CompiledPageActionDescriptor>() != null
				&& installationState.IsInstalled() && !installationState.NeedsUpgrade())
			{
				moduleActivation.ActivateModules();
			}
			await this._next(context);
		}
	}
}
