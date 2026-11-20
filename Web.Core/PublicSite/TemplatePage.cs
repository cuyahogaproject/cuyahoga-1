using Microsoft.AspNetCore.Mvc.Razor;

namespace Cuyahoga.Web.Core.PublicSite
{
	/// <summary>
	/// Base class of the templates (Templates/_ViewImports.cshtml). A template is the layout of the public page, but
	/// the content comes from the placeholders, not from the body of the page, so a template doesn't call RenderBody().
	/// </summary>
	public abstract class TemplatePage : RazorPage<PublicPageModel>
	{
		public override void EnsureRenderedBodyOrSections()
		{
			IgnoreBody();
			base.EnsureRenderedBodyOrSections();
		}
	}
}
