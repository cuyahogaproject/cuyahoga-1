using System.Globalization;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service.SiteStructure;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Web.Core.PublicSite
{
	/// <summary>
	/// The old profile pages (Profile.aspx/register, /reset, /edit, /view/{id}), which the login box of the User module
	/// links to when it isn't connected to a UserProfile section: they go to the same page of the UserProfile section of
	/// the site.
	/// </summary>
	public class ProfileRedirectController : Controller
	{
		private readonly IPageLookup _pageLookup;
		private readonly IModuleTypeService _moduleTypeService;
		private readonly ISectionService _sectionService;

		public ProfileRedirectController(IPageLookup pageLookup, IModuleTypeService moduleTypeService,
			ISectionService sectionService)
		{
			this._pageLookup = pageLookup;
			this._moduleTypeService = moduleTypeService;
			this._sectionService = sectionService;
		}

		[HttpGet]
		[HttpHead]
		[Route("Profile.aspx/{command}/{userId:int?}")]
		public IActionResult Index(string command, int? userId)
		{
			string pathInfo = command.ToLowerInvariant() switch
			{
				"register" => "/Register",
				"reset" => "/ResetPassword",
				"edit" => "/EditProfile",
				"view" when userId != null => "/ViewProfile/" + userId.Value.ToString(CultureInfo.InvariantCulture),
				_ => null
			};
			Section section = pathInfo != null ? GetProfileSection() : null;
			return section != null
				? Redirect(PublicUrls.GetUrlFromSection(Request, section) + pathInfo)
				: NotFound();
		}

		/// <summary>
		/// The first UserProfile section on a page of the site of the request, or null.
		/// </summary>
		private Section GetProfileSection()
		{
			string siteUrl = Request.Scheme + "://" + Request.Host + Request.PathBase.Value?.ToLowerInvariant();
			Site site = this._pageLookup.GetSiteAliasByUrl(siteUrl)?.Site ?? this._pageLookup.GetSiteBySiteUrl(siteUrl);
			ModuleType profileModule = this._moduleTypeService.GetAllModuleTypes().Cast<ModuleType>()
				.FirstOrDefault(moduleType => moduleType.Name == "UserProfile");
			if (site == null || profileModule == null)
			{
				return null;
			}
			return this._sectionService.GetSectionsByModuleTypes(new List<ModuleType> { profileModule }).Cast<Section>()
				.FirstOrDefault(section => section.Node != null && section.Node.Site.Id == site.Id);
		}
	}
}
