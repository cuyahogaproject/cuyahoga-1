using Cuyahoga.Core.Domain;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace Cuyahoga.Web.Core.PublicSite
{
	/// <summary>
	/// The navigation of a template (Controls/Navigation/HierarchicalMenu.ascx in the WebForms site): the root node and
	/// its children, with the children of the nodes on the path to the active node, and a link to the site
	/// administration for administrators. In a template: <c>&lt;vc:hierarchical-menu /&gt;</c>.
	/// </summary>
	public class HierarchicalMenuViewComponent : ViewComponent
	{
		public IViewComponentResult Invoke()
		{
			PublicPage page = HttpContext.GetPublicPage();
			if (page == null)
			{
				return Content(String.Empty);
			}
			var html = new HtmlContentBuilder();
			html.AppendHtml("<ul>");
			if (page.RootNode.ShowInNavigation && page.RootNode.ViewAllowed(page.User))
			{
				AppendListItem(html, page, page.RootNode, withChildren: false);
			}
			foreach (Node node in page.RootNode.ChildNodes)
			{
				if (node.ShowInNavigation && node.ViewAllowed(page.User))
				{
					AppendListItem(html, page, node, withChildren: true);
				}
			}
			if (page.UserIsAdministrator)
			{
				html.AppendHtml("<li><a href=\"").Append(PublicUrls.GetApplicationPath(Request) + "Admin")
					.AppendHtml("\">Admin</a></li>");
			}
			html.AppendHtml("</ul>");
			return new HtmlContentViewComponentResult(html);
		}

		private void AppendListItem(HtmlContentBuilder html, PublicPage page, Node node, bool withChildren)
		{
			html.AppendHtml("<li>");
			SectionRenderer.AppendNodeLink(html, node, Request, node.Id == page.ActiveNode.Id);
			// The children of the nodes on the path to the active node.
			if (withChildren && node.Level <= page.ActiveNode.Level && node.Id == page.ActiveNode.Trail[node.Level]
				&& node.ChildNodes.Count > 0)
			{
				html.AppendHtml("<ul>");
				foreach (Node childNode in node.ChildNodes)
				{
					if (childNode.ViewAllowed(page.User) && childNode.ShowInNavigation)
					{
						AppendListItem(html, page, childNode, withChildren: true);
					}
				}
				html.AppendHtml("</ul>");
			}
			html.AppendHtml("</li>");
		}
	}
}
