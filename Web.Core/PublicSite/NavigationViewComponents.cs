using Cuyahoga.Core.Domain;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace Cuyahoga.Web.Core.PublicSite
{
	/// <summary>
	/// The main menu of the Classic templates (Controls/Navigation/NavigationLevelZeroOne.ascx in the WebForms site): the
	/// home page and its children, and a link to the site administration for administrators. In a template:
	/// <c>&lt;vc:navigation-level-zero-one /&gt;</c>.
	/// </summary>
	public class NavigationLevelZeroOneViewComponent : ViewComponent
	{
		public IViewComponentResult Invoke()
		{
			PublicPage page = HttpContext.GetPublicPage();
			if (page == null)
			{
				return Content(String.Empty);
			}
			var html = new HtmlContentBuilder();
			html.AppendHtml("<div id=\"mainmenuarea\">\n\t<ul id=\"mainmenu\">\n\t\t<li>");
			Node rootNode = page.RootNode;
			if (rootNode.ShowInNavigation && rootNode.ViewAllowed(page.User))
			{
				html.AppendHtml("<a href=\"").Append(PublicUrls.GetUrlFromNode(Request, rootNode)).AppendHtml("\">")
					.Append(rootNode.Title).AppendHtml("</a>");
			}
			html.AppendHtml("</li>");
			// Whitespace between the items, like the repeater of the user control (the items can be inline).
			foreach (Node node in rootNode.ChildNodes)
			{
				if (node.ShowInNavigation && node.ViewAllowed(page.User))
				{
					html.AppendHtml("\n\t\t\t<li>");
					NavigationLinks.AppendLink(html, Request, page, node, "selected", null);
					html.AppendHtml("</li>");
				}
			}
			if (page.UserIsAdministrator)
			{
				html.AppendHtml("\n\t\t<li><a href=\"").Append(PublicUrls.GetApplicationPath(Request) + "Admin")
					.AppendHtml("\">Admin</a></li>");
			}
			html.AppendHtml("\n\t</ul>\n</div>");
			return new HtmlContentViewComponentResult(html);
		}
	}

	/// <summary>
	/// The submenu of the Cuyahoga Standard template (Controls/Navigation/NavigationLevelTwo.ascx in the WebForms site):
	/// the children of the level 1 page above the active page. In a template: <c>&lt;vc:navigation-level-two /&gt;</c>.
	/// </summary>
	public class NavigationLevelTwoViewComponent : ViewComponent
	{
		public IViewComponentResult Invoke()
		{
			PublicPage page = HttpContext.GetPublicPage();
			if (page == null)
			{
				return Content(String.Empty);
			}
			var html = new HtmlContentBuilder();
			html.AppendHtml("<ul class=\"submenu\">");
			if (page.ActiveNode.Level > 0)
			{
				foreach (Node node in page.ActiveNode.NodePath[1].ChildNodes)
				{
					if (node.ShowInNavigation && node.ViewAllowed(page.User))
					{
						html.AppendHtml("\n\t\t\t<li>");
						NavigationLinks.AppendLink(html, Request, page, node, "subselected", "submenulink");
						html.AppendHtml("</li>");
					}
				}
			}
			html.AppendHtml("\n</ul>");
			return new HtmlContentViewComponentResult(html);
		}
	}

	internal static class NavigationLinks
	{
		/// <summary>
		/// A link to a node, with the selected class when the node is on the path to the active node.
		/// </summary>
		public static void AppendLink(HtmlContentBuilder html, HttpRequest request, PublicPage page, Node node,
			string selectedClass, string normalClass)
		{
			bool isSelected = node.Level <= page.ActiveNode.Level && node.Id == page.ActiveNode.Trail[node.Level];
			string cssClass = isSelected ? selectedClass : normalClass;
			html.AppendHtml("<a");
			if (cssClass != null)
			{
				html.AppendHtml(" class=\"").Append(cssClass).AppendHtml("\"");
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
