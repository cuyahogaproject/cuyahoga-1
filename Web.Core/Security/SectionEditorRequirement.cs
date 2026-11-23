using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service.SiteStructure;
using Microsoft.AspNetCore.Authorization;
using NHibernate;

namespace Cuyahoga.Web.Core.Security
{
	/// <summary>
	/// The logged-in user may edit the section of the url (SectionId), like ModuleAdminBasePage of the WebForms site
	/// checks for the admin pages of modules.
	/// </summary>
	public class SectionEditorRequirement : IAuthorizationRequirement
	{
		public const string PolicyName = "SectionEditor";
	}

	public class SectionEditorHandler : AuthorizationHandler<SectionEditorRequirement>
	{
		private readonly ISectionService _sectionService;

		public SectionEditorHandler(ISectionService sectionService)
		{
			this._sectionService = sectionService;
		}

		protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, SectionEditorRequirement requirement)
		{
			HttpContext httpContext = context.Resource as HttpContext;
			User user = httpContext != null ? LegacyAuthenticationHandler.GetUser(httpContext) : null;
			if (user != null && Int32.TryParse(httpContext.Request.Query["SectionId"], out int sectionId))
			{
				Section section = this._sectionService.GetSectionById(sectionId);
				try
				{
					if (user.CanEdit(section))
					{
						context.Succeed(requirement);
					}
				}
				catch (ObjectNotFoundException)
				{
					// An unknown section: nobody may edit it.
				}
			}
			return Task.CompletedTask;
		}
	}
}
