using Cuyahoga.Core.Domain;
using Cuyahoga.Web.Core.Security;
using Cuyahoga.Web.Core.Services;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Cuyahoga.Web.Core.PublicSite
{
	/// <summary>
	/// Turns a public url into the controller that answers it: the page (PublicPageController, or the controller of the
	/// module of the section in the url), or why there is no page (PublicPageController.Unavailable: not found,
	/// forbidden, an unknown site, an external link). Urls that aren't public pages (null) get the 404 of the site.
	/// </summary>
	public class PublicPageTransformer : DynamicRouteValueTransformer
	{
		private readonly InstallationState _installationState;
		private readonly ModuleActivation _moduleActivation;
		private readonly PageResolver _pageResolver;
		private readonly TemplateLayouts _templateLayouts;
		private readonly ModuleViewRegistry _moduleViews;
		private readonly ModuleControllerRegistry _moduleControllers;
		private readonly ILogger<PublicPageTransformer> _logger;

		public PublicPageTransformer(InstallationState installationState, ModuleActivation moduleActivation,
			PageResolver pageResolver, TemplateLayouts templateLayouts, ModuleViewRegistry moduleViews,
			ModuleControllerRegistry moduleControllers, ILogger<PublicPageTransformer> logger)
		{
			this._logger = logger;
			this._installationState = installationState;
			this._moduleActivation = moduleActivation;
			this._pageResolver = pageResolver;
			this._templateLayouts = templateLayouts;
			this._moduleViews = moduleViews;
			this._moduleControllers = moduleControllers;
		}

		public override async ValueTask<RouteValueDictionary> TransformAsync(HttpContext httpContext, RouteValueDictionary values)
		{
			HttpRequest request = httpContext.Request;
			// Other requests than GET, HEAD and POST aren't pages (the site answers 404).
			bool isPost = HttpMethods.IsPost(request.Method);
			if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method) && !isPost)
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
			await httpContext.AuthenticateAsync(CuyahogaAuthentication.SchemeName);
			User user = CuyahogaAuthentication.GetUser(httpContext);
			string siteUrl = request.Scheme + "://" + request.Host + request.PathBase.Value?.ToLowerInvariant();
			PageResolution resolution = this._pageResolver.Resolve(pageRequest, siteUrl, request.Cookies["CuyahogaCulture"], user);
			if (resolution.Kind != PageResolutionKind.Page)
			{
				// Not found, forbidden, an unknown site or an external link (PublicPageController.Unavailable).
				this._logger.LogDebug("{Url} is no page: {Resolution}.", request.Path, resolution.Kind);
				httpContext.Features.Set(resolution);
				return new RouteValueDictionary { ["controller"] = "PublicPage", ["action"] = "Unavailable" };
			}
			string missing = GetMissingParts(resolution.Page);
			if (missing != null)
			{
				throw new InvalidOperationException($"The page {request.Path} can't be shown: the {missing} has no view.");
			}

			// The module of the section in the url handles the request when it has a controller for it (a file
			// download, a posted form).
			string controller = resolution.Page.ActiveSection != null
				? this._moduleControllers.GetControllerName(resolution.Page.ActiveSection.ModuleType, request.Method)
				: null;
			if (isPost && controller == null)
			{
				// Nothing on the page takes a post (the site answers 404).
				this._logger.LogDebug("{Url}: no module controller for the post.", request.Path);
				return null;
			}

			httpContext.SetPublicPage(resolution.Page);
			return new RouteValueDictionary { ["controller"] = controller ?? "PublicPage", ["action"] = "Index" };
		}

		/// <summary>
		/// What this site lacks to render the page (the template or a module), or null.
		/// </summary>
		private string GetMissingParts(PublicPage page)
		{
			if (page.Template == null || !this._templateLayouts.HasLayout(page.Template))
			{
				return "template " + page.Template?.Name;
			}
			IEnumerable<string> modules = page.Sections
				.Select(section => section.Section.ModuleType)
				.Where(moduleType => this._moduleViews.GetViewComponentType(moduleType) == null)
				.Select(moduleType => moduleType.Name)
				.Distinct()
				.ToList();
			return modules.Any() ? "module " + String.Join(", ", modules) : null;
		}
	}
}
