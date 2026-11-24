using Cuyahoga.Core.Util;
using Cuyahoga.Web.Core.Security;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Web.Core.PublicSite
{
	/// <summary>
	/// Renders a public page that <see cref="PublicPageTransformer"/> has resolved, when the module of the active
	/// section (if any) has no controller of its own, and answers urls that don't give a page.
	/// </summary>
	public class PublicPageController : Controller
	{
		private readonly CuyahogaAppSettings _settings;

		public PublicPageController(CuyahogaAppSettings settings)
		{
			this._settings = settings;
		}

		public IActionResult Index()
		{
			return HttpContext.GetPublicPage() != null ? new PublicPageResult() : NotFound();
		}

		/// <summary>
		/// No page for the url (the answers of Error.aspx of the WebForms site).
		/// </summary>
		public IActionResult Unavailable()
		{
			PageResolution resolution = HttpContext.Features.Get<PageResolution>();
			switch (resolution?.Kind)
			{
				case PageResolutionKind.ExternalLink:
					return Redirect(resolution.RedirectUrl);
				case PageResolutionKind.Forbidden:
					// Visitors log in first (RedirectToLoginWhenAccessDenied).
					if (CuyahogaAuthentication.GetUser(HttpContext) == null && this._settings.RedirectToLoginWhenAccessDenied)
					{
						return Challenge(CuyahogaAuthentication.SchemeName);
					}
					return ErrorController.ErrorPage(StatusCodes.Status403Forbidden, "403 Access forbidden",
						"Access to the requested resource is forbidden.");
				case PageResolutionKind.SiteNotFound:
					return ErrorController.ErrorPage(StatusCodes.Status503ServiceUnavailable,
						"No site found at " + Request.Scheme + "://" + Request.Host + Request.PathBase,
						"The url you entered is invalid or the site is down for maintenance.");
				default:
					return ErrorController.ErrorPage(StatusCodes.Status404NotFound, "404 Page not found",
						"The requested page could not be found.");
			}
		}
	}
}
