using System.Globalization;
using System.Text.Encodings.Web;
using Cuyahoga.Core.Domain;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Web.Core.PublicSite
{
	/// <summary>
	/// Renders the sections and custom menus of a public page into the placeholders of its template, before the
	/// template itself, so modules can register stylesheets and scripts for the head. The section block (RSS icon,
	/// edit links, title) and the output cache are the ones of BaseModuleControl in the WebForms site.
	/// </summary>
	public class SectionRenderer
	{
		private readonly IPublicPageModules _modules;
		private readonly ModuleViewRegistry _moduleViews;
		private readonly SectionOutputCache _cache;
		private readonly HtmlEncoder _htmlEncoder;

		public SectionRenderer(IPublicPageModules modules, ModuleViewRegistry moduleViews, SectionOutputCache cache,
			HtmlEncoder htmlEncoder)
		{
			this._modules = modules;
			this._moduleViews = moduleViews;
			this._cache = cache;
			this._htmlEncoder = htmlEncoder;
		}

		/// <summary>
		/// Renders the sections and the menus of the page into <see cref="PublicPageModel.Placeholders"/>.
		/// </summary>
		/// <param name="model">The page.</param>
		/// <param name="viewComponentHelper">The view component helper of the view that renders the page.</param>
		/// <param name="request">The request.</param>
		public async Task RenderAsync(PublicPageModel model, IViewComponentHelper viewComponentHelper, HttpRequest request)
		{
			PublicPage page = model.Page;
			foreach (PageSection pageSection in page.Sections)
			{
				ModuleBase module = this._modules.GetModule(pageSection.Section);
				if (module == null)
				{
					// The module isn't activated.
					continue;
				}
				IHtmlContent html = await RenderSectionAsync(page, pageSection.Section, module, viewComponentHelper, request);
				model.GetPlaceholder(pageSection.Placeholder).AppendHtml(html);
			}
			foreach (CustomMenu menu in page.Menus)
			{
				if (menu.Nodes.Count > 0)
				{
					model.GetPlaceholder(menu.Placeholder).AppendHtml(RenderMenu(page, menu, request));
				}
			}
		}

		private async Task<IHtmlContent> RenderSectionAsync(PublicPage page, Section section, ModuleBase module,
			IViewComponentHelper viewComponentHelper, HttpRequest request)
		{
			var context = new ModuleViewContext(module, page);
			string content = await GetContentAsync(page, section, module, context, viewComponentHelper, request);

			string applicationPath = PublicUrls.GetApplicationPath(request);
			var html = new HtmlContentBuilder();
			if (context.DisplaySyndicationIcon)
			{
				html.AppendHtml("<div class=\"syndicate\"><a href=\"")
					.Append(PublicUrls.GetRssUrlFromSection(request, section) + module.ModulePathInfo)
					.AppendHtml("\"><img src=\"")
					.Append(applicationPath + "Images/feed-icon.png")
					.AppendHtml("\" alt=\"RSS-2.0\"/></a></div>");
			}
			User user = page.User;
			if (user != null && (user.CanEdit(section) || page.UserIsAdministrator))
			{
				html.AppendHtml("<div class=\"moduletools\">");
				if (request.Query["preview"] != "on")
				{
					AppendEditLinks(html, page, section, applicationPath);
				}
				html.AppendHtml("</div>");
			}
			html.AppendHtml("<div class=\"section\">");
			if (section.ShowTitle)
			{
				html.AppendHtml("<h3>").Append(module.DisplayTitle).AppendHtml("</h3>");
			}
			html.AppendHtml(content);
			html.AppendHtml("</div>");
			return html;
		}

		/// <summary>
		/// The content of the module view. It's cached for anonymous users when the section has a cache duration, but
		/// not when a form was posted (the view can show what the user entered) and not for urls with a query string
		/// (the cache key of the module doesn't have it, and the view can show it: the query of a search), and never for
		/// views with <see cref="NoSectionCacheAttribute"/>.
		/// </summary>
		private async Task<string> GetContentAsync(PublicPage page, Section section, ModuleBase module,
			ModuleViewContext context, IViewComponentHelper viewComponentHelper, HttpRequest request)
		{
			Type viewComponentType = this._moduleViews.GetViewComponentType(section.ModuleType)
				?? throw new InvalidOperationException($"The module {section.ModuleType.Name} has no view.");
			bool useCache = section.CacheDuration > 0 && module.CacheKey != null && page.User == null
				&& (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)) && !request.QueryString.HasValue
				&& !Attribute.IsDefined(viewComponentType, typeof(NoSectionCacheAttribute));
			CachedSection cached = useCache ? this._cache.Get(module) : null;
			if (cached != null)
			{
				module.DisplayTitle = cached.DisplayTitle;
				context.DisplaySyndicationIcon = cached.DisplaySyndicationIcon;
				foreach (KeyValuePair<string, string> stylesheet in cached.Stylesheets)
				{
					context.RegisterStylesheet(stylesheet.Key, stylesheet.Value);
				}
				foreach (KeyValuePair<string, string> javaScript in cached.JavaScripts)
				{
					context.RegisterJavaScript(javaScript.Key, javaScript.Value);
				}
				return cached.Html;
			}

			IHtmlContent content = await viewComponentHelper.InvokeAsync(viewComponentType, new { context });
			using var writer = new StringWriter();
			content.WriteTo(writer, this._htmlEncoder);
			string html = writer.ToString();
			if (useCache)
			{
				this._cache.Set(module,
					new CachedSection(html, module.DisplayTitle, context.DisplaySyndicationIcon, context.Stylesheets.ToList(),
						context.JavaScripts.ToList()),
					section.CacheDuration);
			}
			return html;
		}

		private static void AppendEditLinks(HtmlContentBuilder html, PublicPage page, Section section, string applicationPath)
		{
			User user = page.User;
			string sectionId = section.Id.ToString(CultureInfo.InvariantCulture);
			if (section.ModuleType.EditPath != null && user.CanEdit(section))
			{
				int nodeId = section.Node != null ? section.Node.Id : page.ActiveNode.Id;
				html.AppendHtml("&nbsp;<a href=\"")
					.Append($"{applicationPath}{section.ModuleType.EditPath}?NodeId={nodeId}&SectionId={sectionId}")
					.AppendHtml("\">Edit</a>");
			}
			if (page.UserIsAdministrator)
			{
				if (section.Node != null)
				{
					string nodeId = section.Node.Id.ToString(CultureInfo.InvariantCulture);
					html.AppendHtml("&nbsp;<a href=\"")
						.Append($"{applicationPath}Admin/SectionEdit.aspx?NodeId={nodeId}&SectionId={sectionId}")
						.AppendHtml("\">Section Properties</a>");
					html.AppendHtml("&nbsp;<a href=\"")
						.Append($"{applicationPath}Admin/NodeEdit.aspx?NodeId={nodeId}")
						.AppendHtml("\">Page Properties</a>");
				}
				else
				{
					html.AppendHtml("&nbsp;<a href=\"")
						.Append($"{applicationPath}Admin/SectionEdit.aspx?SectionId={sectionId}")
						.AppendHtml("\">Section Properties</a>");
				}
			}
		}

		/// <summary>
		/// A custom menu: a list of links to the nodes of the menu that the user may view.
		/// </summary>
		private static IHtmlContent RenderMenu(PublicPage page, CustomMenu menu, HttpRequest request)
		{
			var html = new HtmlContentBuilder();
			html.AppendHtml("<ul>");
			foreach (Node node in menu.Nodes)
			{
				if (node.ViewAllowed(page.User))
				{
					html.AppendHtml("<li>");
					AppendNodeLink(html, node, request, node.Id == page.ActiveNode.Id);
					html.AppendHtml("</li>");
				}
			}
			html.AppendHtml("</ul>");
			return html;
		}

		/// <summary>
		/// A link to a node, with the class 'selected' for the active node.
		/// </summary>
		public static void AppendNodeLink(HtmlContentBuilder html, Node node, HttpRequest request, bool isSelected)
		{
			html.AppendHtml("<a");
			if (isSelected)
			{
				html.AppendHtml(" class=\"selected\"");
			}
			html.AppendHtml(" href=\"").Append(PublicUrls.GetUrlFromNode(request, node)).AppendHtml("\"");
			string target = PublicUrls.GetLinkTarget(node);
			if (target != null)
			{
				html.AppendHtml(" target=\"").Append(target).AppendHtml("\"");
			}
			html.AppendHtml(">").Append(node.Title).AppendHtml("</a>");
		}
	}
}
