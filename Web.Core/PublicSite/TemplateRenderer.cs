using Cuyahoga.Core.Domain;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

namespace Cuyahoga.Web.Core.PublicSite
{
	/// <summary>
	/// Renders the layout of a template without a page (no sections, no navigation): for the placeholders of the
	/// template, and for its preview in the site administration (TemplatePreview.aspx).
	/// </summary>
	public class TemplateRenderer
	{
		private readonly IRazorViewEngine _viewEngine;
		private readonly IModelMetadataProvider _metadataProvider;
		private readonly ITempDataProvider _tempDataProvider;
		private readonly IHttpContextAccessor _httpContextAccessor;

		public TemplateRenderer(IRazorViewEngine viewEngine, IModelMetadataProvider metadataProvider,
			ITempDataProvider tempDataProvider, IHttpContextAccessor httpContextAccessor)
		{
			this._viewEngine = viewEngine;
			this._metadataProvider = metadataProvider;
			this._tempDataProvider = tempDataProvider;
			this._httpContextAccessor = httpContextAccessor;
		}

		/// <summary>
		/// The html of the template and its placeholders (in the order of the template), or null when the template has
		/// no layout.
		/// </summary>
		/// <param name="template">The template.</param>
		/// <param name="placeholderPickerControl">For the preview: the select element of the page that opened it, which
		/// the buttons in the placeholders set. Null for no buttons.</param>
		public async Task<(string Html, IReadOnlyList<string> Placeholders)?> RenderAsync(Template template,
			string placeholderPickerControl = null)
		{
			string layoutPath = TemplateLayouts.GetLayoutPath(template);
			ViewEngineResult viewResult = this._viewEngine.GetView(executingFilePath: null, viewPath: layoutPath, isMainPage: true);
			if (!viewResult.Success)
			{
				return null;
			}
			HttpContext httpContext = this._httpContextAccessor.HttpContext
				?? throw new InvalidOperationException("A template is only rendered in a request.");

			// A page without a site structure: the title is the name of the template.
			var site = new Site { Name = "Template preview" };
			var node = new Node { Title = template.Name, Site = site };
			var page = new PublicPage(site, node, null, String.Empty, null, template, false, new List<PageSection>(),
				new List<CustomMenu>());
			page.Assets.RegisterStylesheet("maincss",
				PublicUrls.GetApplicationPath(httpContext.Request) + template.BasePath + "/Css/" + template.Css);
			var model = new PublicPageModel(page, layoutPath) { PlaceholderPickerControl = placeholderPickerControl };

			var actionContext = new ActionContext(httpContext, httpContext.GetRouteData() ?? new RouteData(), new ActionDescriptor());
			using var writer = new StringWriter();
			var viewContext = new ViewContext(actionContext, viewResult.View,
				new ViewDataDictionary<PublicPageModel>(this._metadataProvider, new ModelStateDictionary()) { Model = model },
				new TempDataDictionary(httpContext, this._tempDataProvider), writer, new HtmlHelperOptions());
			await viewResult.View.RenderAsync(viewContext);
			return (writer.ToString(), model.RenderedPlaceholders);
		}
	}
}
