using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service.SiteStructure;
using Cuyahoga.Web.Mvc;

namespace Cuyahoga.Web.Core.PublicSite
{
	/// <summary>
	/// The site structure lookups that the <see cref="PageResolver"/> needs.
	/// </summary>
	public interface IPageLookup
	{
		SiteAlias GetSiteAliasByUrl(string url);
		Site GetSiteBySiteUrl(string siteUrl);
		Section GetSectionById(int sectionId);
		Node GetNodeById(int nodeId);
		Node GetNodeByShortDescriptionAndSite(string shortDescription, Site site);
		Node GetRootNodeByCultureAndSite(string culture, Site site);
		IEnumerable<CustomMenu> GetMenusByRootNode(Node rootNode);
	}

	public class ServicePageLookup : IPageLookup
	{
		private readonly ISiteService _siteService;
		private readonly INodeService _nodeService;
		private readonly ISectionService _sectionService;

		public ServicePageLookup(ISiteService siteService, INodeService nodeService, ISectionService sectionService)
		{
			this._siteService = siteService;
			this._nodeService = nodeService;
			this._sectionService = sectionService;
		}

		public SiteAlias GetSiteAliasByUrl(string url) => this._siteService.GetSiteAliasByUrl(url);

		public Site GetSiteBySiteUrl(string siteUrl) => this._siteService.GetSiteBySiteUrl(siteUrl);

		public Section GetSectionById(int sectionId) => this._sectionService.GetSectionById(sectionId);

		public Node GetNodeById(int nodeId) => this._nodeService.GetNodeById(nodeId);

		public Node GetNodeByShortDescriptionAndSite(string shortDescription, Site site) =>
			this._nodeService.GetNodeByShortDescriptionAndSite(shortDescription, site);

		public Node GetRootNodeByCultureAndSite(string culture, Site site) =>
			this._nodeService.GetRootNodeByCultureAndSite(culture, site);

		public IEnumerable<CustomMenu> GetMenusByRootNode(Node rootNode) =>
			this._nodeService.GetMenusByRootNode(rootNode).Cast<CustomMenu>();
	}

	public enum PageResolutionKind
	{
		Page,
		SiteNotFound,
		NotFound,
		Forbidden,
		ExternalLink
	}

	/// <summary>
	/// The outcome of resolving a url: a page, or why there is none.
	/// </summary>
	public record PageResolution(PageResolutionKind Kind, PublicPage Page = null, string RedirectUrl = null);

	/// <summary>
	/// Finds the site, the node and the sections of a public page: the first part of the PageEngine of the WebForms
	/// site.
	/// </summary>
	public class PageResolver
	{
		private readonly IPageLookup _lookup;

		public PageResolver(IPageLookup lookup)
		{
			this._lookup = lookup;
		}

		/// <param name="request">What the url asks for.</param>
		/// <param name="siteUrl">The url of the site root (scheme, host and path base, without a trailing slash).</param>
		/// <param name="cultureCookie">The value of the CuyahogaCulture cookie (the language switcher), or null.</param>
		/// <param name="user">The logged-in user, or null.</param>
		public PageResolution Resolve(PageRequest request, string siteUrl, string cultureCookie, User user)
		{
			Site site;
			Node entryNode = null;
			SiteAlias siteAlias = this._lookup.GetSiteAliasByUrl(siteUrl);
			if (siteAlias != null)
			{
				site = siteAlias.Site;
				entryNode = siteAlias.EntryNode;
			}
			else
			{
				site = this._lookup.GetSiteBySiteUrl(siteUrl);
			}
			if (site == null)
			{
				return new PageResolution(PageResolutionKind.SiteNotFound);
			}

			Section activeSection = null;
			Node activeNode;
			if (request.SectionId != null)
			{
				activeSection = this._lookup.GetSectionById(request.SectionId.Value);
				activeNode = activeSection?.Node;
			}
			else if (request.ShortDescription != null)
			{
				activeNode = this._lookup.GetNodeByShortDescriptionAndSite(request.ShortDescription, site);
			}
			else if (request.NodeId != null)
			{
				activeNode = this._lookup.GetNodeById(request.NodeId.Value);
			}
			else if (entryNode != null)
			{
				activeNode = entryNode;
			}
			else
			{
				// No particular node: the home page of the culture of the language switcher, or of the site.
				activeNode = this._lookup.GetRootNodeByCultureAndSite(cultureCookie ?? site.DefaultCulture, site);
			}
			if (activeNode == null)
			{
				return new PageResolution(PageResolutionKind.NotFound);
			}
			if (!activeNode.ViewAllowed(user))
			{
				return new PageResolution(PageResolutionKind.Forbidden);
			}
			if (activeNode.IsExternalLink)
			{
				return new PageResolution(PageResolutionKind.ExternalLink, RedirectUrl: activeNode.LinkUrl);
			}

			bool isOffline = IsOffline(activeNode);
			bool userIsAdministrator = user != null && user.HasPermission(AccessLevel.Administrator);
			bool showContent = !isOffline || userIsAdministrator;
			Template template = !showContent && site.OfflineTemplate != null ? site.OfflineTemplate : activeNode.Template;
			IReadOnlyList<PageSection> sections = showContent ? GetSections(activeNode, template, user) : [];
			IReadOnlyList<CustomMenu> menus = this._lookup.GetMenusByRootNode(activeNode.NodePath[0]).ToList();

			var page = new PublicPage(site, activeNode, activeSection, request.PathInfo, user, template, isOffline,
				sections, menus);
			page.Assets.RegisterMetaTag("keywords", activeNode.MetaKeywords ?? site.MetaKeywords);
			page.Assets.RegisterMetaTag("description", activeNode.MetaDescription ?? site.MetaDescription);
			return new PageResolution(PageResolutionKind.Page, page);
		}

		/// <summary>
		/// A node is offline when it or one of the nodes above it is offline.
		/// </summary>
		public static bool IsOffline(Node node)
		{
			return node.NodePath.Any(n => n.Status == (int)NodeStatus.Offline);
		}

		/// <summary>
		/// The sections that the user may view: first the ones of the template, then the ones of the node.
		/// </summary>
		private static List<PageSection> GetSections(Node node, Template template, User user)
		{
			var sections = new List<PageSection>();
			if (template != null)
			{
				foreach (KeyValuePair<string, Section> templateSection in template.Sections)
				{
					if (templateSection.Value != null && templateSection.Value.ViewAllowed(user))
					{
						sections.Add(new PageSection(templateSection.Key, templateSection.Value));
					}
				}
			}
			foreach (Section section in node.Sections)
			{
				if (section.ViewAllowed(user))
				{
					sections.Add(new PageSection(section.PlaceholderId, section));
				}
			}
			return sections;
		}
	}
}
