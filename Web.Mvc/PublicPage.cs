using Cuyahoga.Core.Domain;

namespace Cuyahoga.Web.Mvc
{
	/// <summary>
	/// The public page that is being rendered: the site, the node and the sections to show, for the template and the
	/// module views. This is what the PageEngine of the WebForms site offered to templates and module controls.
	/// </summary>
	public class PublicPage
	{
		public PublicPage(Site site, Node activeNode, Section activeSection, string modulePathInfo, User user,
			Template template, bool isOffline, IReadOnlyList<PageSection> sections, IReadOnlyList<CustomMenu> menus)
		{
			Site = site;
			ActiveNode = activeNode;
			ActiveSection = activeSection;
			ModulePathInfo = modulePathInfo;
			User = user;
			Template = template;
			IsOffline = isOffline;
			Sections = sections;
			Menus = menus;
		}

		public Site Site { get; }

		/// <summary>
		/// The root node of the active node (the home page of the culture of the active node).
		/// </summary>
		public Node RootNode => ActiveNode.NodePath[0];

		public Node ActiveNode { get; }

		/// <summary>
		/// The section from the url ({id}/section.aspx), or null.
		/// </summary>
		public Section ActiveSection { get; }

		/// <summary>
		/// The path info of the url (/12/section.aspx/archive -> /archive), for the module of the active section.
		/// </summary>
		public string ModulePathInfo { get; }

		/// <summary>
		/// The logged-in user, or null.
		/// </summary>
		public User User { get; }

		public bool UserIsAdministrator => User != null && User.HasPermission(AccessLevel.Administrator);

		/// <summary>
		/// The template that renders the page: the template of the active node, or the offline template of the site
		/// when the page is offline for the user.
		/// </summary>
		public Template Template { get; }

		/// <summary>
		/// The active node or one of the nodes above it is offline. Administrators still see the page as usual.
		/// </summary>
		public bool IsOffline { get; }

		/// <summary>
		/// The sections to show that the user may view, in order: the sections of the template, then the ones of the
		/// node.
		/// </summary>
		public IReadOnlyList<PageSection> Sections { get; }

		/// <summary>
		/// The custom menus of the root node.
		/// </summary>
		public IReadOnlyList<CustomMenu> Menus { get; }

		/// <summary>
		/// Stylesheets, scripts and meta tags that the template and the modules register for the head of the page.
		/// </summary>
		public PageAssets Assets { get; } = new PageAssets();

		public string Title => Site.Name + " - " + ActiveNode.Title;
	}

	/// <summary>
	/// A section on the page and the placeholder of the template where it goes.
	/// </summary>
	public record PageSection(string Placeholder, Section Section);
}
