using Cuyahoga.Core.Domain;
using Cuyahoga.Web.Core.Security;
using Cuyahoga.Web.Core.Services;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Cuyahoga.Web.Core.PublicSite
{
	/// <summary>
	/// Decides per request whether this site renders a public page: only when the url is a public page, the page
	/// exists, the user may view it, and its template and all its modules have been moved to this site. Otherwise
	/// (null) the request goes on to the WebForms site, which then handles it like before: other pages, error pages,
	/// redirects of external links, the installer.
	/// </summary>
	public class PublicPageTransformer : DynamicRouteValueTransformer
	{
		private readonly InstallationState _installationState;
		private readonly ModuleActivation _moduleActivation;
		private readonly PageResolver _pageResolver;
		private readonly TemplateLayouts _templateLayouts;
		private readonly ModuleViewRegistry _moduleViews;

		public PublicPageTransformer(InstallationState installationState, ModuleActivation moduleActivation,
			PageResolver pageResolver, TemplateLayouts templateLayouts, ModuleViewRegistry moduleViews)
		{
			this._installationState = installationState;
			this._moduleActivation = moduleActivation;
			this._pageResolver = pageResolver;
			this._templateLayouts = templateLayouts;
			this._moduleViews = moduleViews;
		}

		public override async ValueTask<RouteValueDictionary> TransformAsync(HttpContext httpContext, RouteValueDictionary values)
		{
			HttpRequest request = httpContext.Request;
			// Postbacks go to the WebForms site.
			if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
			{
				return null;
			}
			PageRequest pageRequest = PublicUrlParser.Parse(request.Path.Value, request.QueryString.Value ?? String.Empty,
				request.Query);
			if (pageRequest == null || !this._installationState.IsInstalled())
			{
				return null;
			}

			this._moduleActivation.ActivateModules();
			await httpContext.AuthenticateAsync(LegacyAuthenticationHandler.SchemeName);
			User user = LegacyAuthenticationHandler.GetUser(httpContext);
			string siteUrl = request.Scheme + "://" + request.Host + request.PathBase.Value?.ToLowerInvariant();
			PageResolution resolution = this._pageResolver.Resolve(pageRequest, siteUrl, request.Cookies["CuyahogaCulture"], user);
			if (resolution.Kind != PageResolutionKind.Page || !CanRender(resolution.Page))
			{
				return null;
			}

			httpContext.SetPublicPage(resolution.Page);
			return new RouteValueDictionary { ["controller"] = "PublicPage", ["action"] = "Index" };
		}

		private bool CanRender(PublicPage page)
		{
			return page.Template != null && this._templateLayouts.HasLayout(page.Template)
				&& page.Sections.All(section => this._moduleViews.GetViewComponentType(section.Section.ModuleType) != null);
		}
	}
}
