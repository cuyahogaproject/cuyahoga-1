using Cuyahoga.Core.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Cuyahoga.Web.Mvc
{
	/// <summary>
	/// Base class for the controller of a module: it handles the requests for the section of the module in the url
	/// ({id}/section.aspx/...), so the module decides what the response is, for example a file download. The site
	/// calls the Index action; <see cref="PublicPageView"/> renders the page as usual. A module without a controller
	/// gets the page.
	/// </summary>
	/// <remarks>
	/// The site sends a request to the controller only when it has an Index action for the http method, so a form of
	/// the module can post to the section url with an [HttpPost] Index action. Other posts go to the WebForms site.
	///
	/// The site finds the controller of a module type by the ModuleController base class. Name it after the module
	/// ({ModuleName}Controller): controller names must be unique in the site.
	/// </remarks>
	/// <typeparam name="TModule">The module type.</typeparam>
	public abstract class ModuleController<TModule> : Controller where TModule : ModuleBase
	{
		/// <summary>
		/// The page of the request. Its active section is the section of the module.
		/// </summary>
		protected PublicPage PublicPage => HttpContext.GetPublicPage();

		/// <summary>
		/// The module of the active section, with the path info of the url. The view of the section gets the same
		/// instance.
		/// </summary>
		protected TModule Module =>
			(TModule)HttpContext.RequestServices.GetRequiredService<IPublicPageModules>().GetModule(PublicPage.ActiveSection);

		/// <summary>
		/// Removes the cached output of the section of the module, so a change shows immediately.
		/// </summary>
		protected void InvalidateCache()
		{
			HttpContext.RequestServices.GetRequiredService<ISectionOutputCache>().Remove(Module);
		}

		/// <summary>
		/// Renders the public page with all its sections.
		/// </summary>
		protected PublicPageResult PublicPageView()
		{
			return new PublicPageResult();
		}
	}
}
