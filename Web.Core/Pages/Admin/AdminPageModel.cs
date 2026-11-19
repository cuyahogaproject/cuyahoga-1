using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service.SiteStructure;
using Cuyahoga.Core.Util;
using Cuyahoga.Web.Core.Security;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Cuyahoga.Web.Core.Pages.Admin
{
	/// <summary>
	/// Base class of the site administration pages (AdminBasePage in the WebForms site): the page title, the message box
	/// and the active site, node and section of the admin layout. Only administrators get here (see Program.cs).
	/// </summary>
	public abstract class AdminPageModel : PageModel
	{
		/// <summary>
		/// The title of the page (browser title and heading).
		/// </summary>
		public string Title { get; protected set; }

		/// <summary>
		/// The text of the message box, or null when there is no message.
		/// </summary>
		public string Message { get; private set; }

		public bool MessageIsError { get; private set; }

		/// <summary>
		/// The site that the page is about (SiteId, or the site of the active node), or null.
		/// </summary>
		public Site ActiveSite { get; private set; }

		/// <summary>
		/// The node that the page is about (NodeId, or the node of the active section), or null.
		/// </summary>
		public Node ActiveNode { get; private set; }

		/// <summary>
		/// The section that the page is about (SectionId), or null.
		/// </summary>
		public Section ActiveSection { get; private set; }

		/// <summary>
		/// The logged-in administrator.
		/// </summary>
		public User CurrentUser
		{
			get { return LegacyAuthenticationHandler.GetUser(HttpContext); }
		}

		/// <summary>
		/// The public address of the active section, node or site ("View the current site" in the header). Null when
		/// the page has none of them, or when it has no address (an external link, a section without node).
		/// </summary>
		public string CurrentSiteUrl
		{
			get
			{
				if (ActiveSection != null)
				{
					return ActiveSection.Node != null
						? Text.EnsureTrailingSlash(ActiveSection.Node.Site.SiteUrl) + ActiveSection.Id + "/section.aspx"
						: null;
				}
				if (ActiveNode != null)
				{
					return !ActiveNode.IsExternalLink
						? Text.EnsureTrailingSlash(ActiveNode.Site.SiteUrl) + ActiveNode.Id + "/view.aspx"
						: null;
				}
				return ActiveSite?.SiteUrl;
			}
		}

		/// <summary>
		/// True when the header shows the "View the current site" link.
		/// </summary>
		public bool HasActiveSiteStructure
		{
			get { return ActiveSection != null || ActiveNode != null || ActiveSite != null; }
		}

		protected void ShowMessage(string message)
		{
			Message = message;
			MessageIsError = false;
		}

		protected void ShowError(string errorText)
		{
			Message = "An error occured: " + errorText;
			MessageIsError = true;
		}

		public override void OnPageHandlerExecuting(PageHandlerExecutingContext context)
		{
			LoadActiveSiteStructure();

			// Pages that redirect after a change pass the message in the url.
			string message = Request.Query["message"];
			if (!String.IsNullOrEmpty(message))
			{
				ShowMessage(message);
			}
		}

		/// <summary>
		/// Like AdminBasePage: the active section, node and site from SectionId, NodeId and SiteId in the url.
		/// </summary>
		private void LoadActiveSiteStructure()
		{
			IServiceProvider services = HttpContext.RequestServices;
			if (TryGetId("SectionId", out int sectionId))
			{
				ActiveSection = services.GetRequiredService<ISectionService>().GetSectionById(sectionId);
				if (ActiveSection?.Node != null)
				{
					ActiveNode = ActiveSection.Node;
					ActiveSite = ActiveNode.Site;
				}
			}
			if (ActiveNode == null && TryGetId("NodeId", out int nodeId))
			{
				ActiveNode = services.GetRequiredService<INodeService>().GetNodeById(nodeId);
				ActiveSite = ActiveNode?.Site;
			}
			if (ActiveSite == null && TryGetId("SiteId", out int siteId))
			{
				ActiveSite = services.GetRequiredService<ISiteService>().GetSiteById(siteId);
			}
		}

		private bool TryGetId(string name, out int id)
		{
			return Int32.TryParse(Request.Query[name], out id) && id > 0;
		}
	}
}
