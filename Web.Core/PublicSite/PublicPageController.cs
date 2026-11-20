using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Web.Core.PublicSite
{
	/// <summary>
	/// Renders a public page that <see cref="PublicPageTransformer"/> has resolved: the sections first
	/// (Views/PublicPage/Index.cshtml), then the template as the layout.
	/// </summary>
	public class PublicPageController : Controller
	{
		public IActionResult Index()
		{
			PublicPage page = HttpContext.GetPublicPage();
			if (page == null)
			{
				return NotFound();
			}
			page.Assets.RegisterStylesheet("maincss",
				PublicUrls.GetApplicationPath(Request) + page.Template.BasePath + "/Css/" + page.Template.Css);
			return View(new PublicPageModel(page, TemplateLayouts.GetLayoutPath(page.Template)));
		}
	}
}
