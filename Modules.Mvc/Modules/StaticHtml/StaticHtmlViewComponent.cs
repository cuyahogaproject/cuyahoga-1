using Cuyahoga.Modules.StaticHtml;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Modules.Mvc.StaticHtml
{
	/// <summary>
	/// Shows the html of a StaticHtml section.
	/// </summary>
	public class StaticHtmlViewComponent : ModuleViewComponent<StaticHtmlModule>
	{
		protected override Task<IViewComponentResult> RenderAsync()
		{
			StaticHtmlContent content = Module.GetContent();
			var html = new HtmlString(content?.Content ?? String.Empty);
			return Task.FromResult<IViewComponentResult>(View("~/Modules/StaticHtml/StaticHtml.cshtml", html));
		}
	}
}
