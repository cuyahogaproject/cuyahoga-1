using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Web.Core.PublicSite
{
	/// <summary>
	/// Renders a public page that <see cref="PublicPageTransformer"/> has resolved, when the module of the active
	/// section (if any) has no controller of its own.
	/// </summary>
	public class PublicPageController : Controller
	{
		public IActionResult Index()
		{
			return HttpContext.GetPublicPage() != null ? new PublicPageResult() : NotFound();
		}
	}
}
