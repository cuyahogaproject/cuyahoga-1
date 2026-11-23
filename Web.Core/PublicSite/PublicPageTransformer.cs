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
			// Other requests than GET, HEAD and POST go to the WebForms site.
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
				this._logger.LogDebug("{Url} goes to the WebForms site: {Resolution}.", request.Path, resolution.Kind);
				return null;
			}
			string missing = GetMissingParts(resolution.Page);
			if (missing != null)
			{
				this._logger.LogDebug("{Url} goes to the WebForms site: {Missing} not moved yet.", request.Path, missing);
				return null;
			}

			// The module of the section in the url handles the request when it has a controller for it (a file
			// download, a posted form).
			string controller = resolution.Page.ActiveSection != null
				? this._moduleControllers.GetControllerName(resolution.Page.ActiveSection.ModuleType, request.Method)
				: null;
			if (isPost && controller == null)
			{
				// A postback of a WebForms page.
				this._logger.LogDebug("{Url} goes to the WebForms site: no module controller for the post.", request.Path);
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
