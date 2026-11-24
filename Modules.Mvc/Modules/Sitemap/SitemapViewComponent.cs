using Cuyahoga.Core.Domain;
using Cuyahoga.Modules.Sitemap;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Modules.Mvc.Sitemap
{
	/// <summary>
	/// All pages of the site that the user may view, as nested lists (SitemapControl.ascx in the WebForms site): the
	/// home page, then its children with all their descendants.
	/// </summary>
	public class SitemapViewComponent : ModuleViewComponent<SitemapModule>
	{
		protected override Task<IViewComponentResult> RenderAsync()
		{
			Node rootNode = PublicPage.RootNode;
			var html = new HtmlContentBuilder();
			html.AppendHtml("<ul>");
			// The home page always shows, at the same level as its children.
			AppendListItem(html, rootNode, withChildren: false);
			foreach (Node node in rootNode.ChildNodes)
			{
				if (node.ViewAllowed(PublicPage.User))
				{
					AppendListItem(html, node, withChildren: true);
				}
			}
			html.AppendHtml("</ul>");
			return Task.FromResult<IViewComponentResult>(View("~/Modules/Sitemap/Sitemap.cshtml", html));
		}

		private void AppendListItem(HtmlContentBuilder html, Node node, bool withChildren)
		{
			html.AppendHtml("<li><a href=\"").Append(PublicUrls.GetUrlFromNode(Request, node)).AppendHtml("\"");
			string target = PublicUrls.GetLinkTarget(node);
			if (target != null)
			{
				html.AppendHtml(" target=\"").Append(target).AppendHtml("\"");
			}
			html.AppendHtml(">").Append(node.Title).AppendHtml("</a>");
			if (withChildren && node.ChildNodes.Count > 0)
			{
				// Like the WebForms site: a list, even when the user may view none of the children.
				html.AppendHtml("<ul>");
				foreach (Node childNode in node.ChildNodes)
				{
					if (childNode.ViewAllowed(PublicPage.User))
					{
						AppendListItem(html, childNode, withChildren: true);
					}
				}
				html.AppendHtml("</ul>");
			}
			html.AppendHtml("</li>");
		}
	}
}
