using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Tests;
using Cuyahoga.Web.Core.PublicSite;
using Cuyahoga.Web.Mvc;
using Xunit;

namespace Cuyahoga.Web.Core.Tests.PublicSite
{
	public class PageResolverTests
	{
		private const string SiteUrl = "http://localhost:8081";

		private readonly Role _anonymous = Build.Anonymous();
		private readonly Role _administrator = Build.Role(1, "Administrator", AccessLevel.Authenticated | AccessLevel.Administrator);
		private readonly Template _template = new Template { Name = "Another Red", BasePath = "Templates/AnotherRed", TemplateControl = "Cuyahoga.ascx" };
		private readonly Template _offlineTemplate = new Template { Name = "Offline", BasePath = "Templates/AnotherRed", TemplateControl = "Offline.ascx" };
		private readonly Site _site;
		private readonly Node _home;
		private readonly Node _about;
		private readonly Node _team;
		private readonly FakePageLookup _lookup = new FakePageLookup();

		public PageResolverTests()
		{
			this._site = new Site { Name = "Sample", SiteUrl = SiteUrl, DefaultCulture = "en-US", OfflineTemplate = this._offlineTemplate };
			this._home = Page(1, "Home", null);
			this._about = Page(2, "About", this._home);
			this._team = Page(3, "Team", this._about);
			this._lookup.Site = this._site;
			this._lookup.RootNodes["en-US"] = this._home;
		}

		private Node Page(int id, string title, Node parent)
		{
			Node node = Build.Node(id, title, parent).Allow(this._anonymous).Allow(this._administrator);
			node.Site = this._site;
			node.Template = this._template;
			node.Culture = "en-US";
			node.ShortDescription = title.ToLowerInvariant();
			this._lookup.Nodes.Add(node);
			return node;
		}

		private Section SectionOn(int id, Node node, string placeholder = "maincontent")
		{
			Section section = Build.Section(id, node, placeholder).Allow(this._anonymous).Allow(this._administrator);
			this._lookup.Sections.Add(section);
			return section;
		}

		private User Administrator() => Build.User(this._administrator);

		private PageResolution Resolve(PageRequest request, User user = null, string cultureCookie = null) =>
			new PageResolver(this._lookup).Resolve(request, SiteUrl, cultureCookie, user);

		private PublicPage ResolvePage(PageRequest request, User user = null)
		{
			PageResolution resolution = Resolve(request, user);
			Assert.Equal(PageResolutionKind.Page, resolution.Kind);
			return resolution.Page;
		}

		private static PageRequest NodeRequest(int nodeId) => new PageRequest(nodeId, null, null, "");

		[Fact]
		public void Without_a_node_in_the_url_the_page_is_the_home_page_of_the_site_culture()
		{
			PublicPage page = ResolvePage(new PageRequest(null, null, null, ""));

			Assert.Same(this._site, page.Site);
			Assert.Same(this._home, page.ActiveNode);
			Assert.Same(this._home, page.RootNode);
			Assert.Null(page.ActiveSection);
		}

		[Fact]
		public void The_culture_cookie_selects_the_home_page_of_another_language()
		{
			Node thuis = Page(10, "Thuis", null);
			thuis.Culture = "nl-NL";
			this._lookup.RootNodes["nl-NL"] = thuis;

			PageResolution resolution = Resolve(new PageRequest(null, null, null, ""), cultureCookie: "nl-NL");

			Assert.Same(thuis, resolution.Page.ActiveNode);
		}

		[Fact]
		public void A_site_alias_can_have_its_own_entry_page()
		{
			this._lookup.Site = null;
			this._lookup.SiteAlias = new SiteAlias { Site = this._site, EntryNode = this._about, Url = SiteUrl };

			PublicPage page = ResolvePage(new PageRequest(null, null, null, ""));

			Assert.Same(this._about, page.ActiveNode);
			Assert.Same(this._home, page.RootNode);
		}

		[Fact]
		public void A_section_url_makes_the_section_active_on_its_node()
		{
			Section section = SectionOn(20, this._about);

			PublicPage page = ResolvePage(new PageRequest(null, 20, null, "/archive"));

			Assert.Same(this._about, page.ActiveNode);
			Assert.Same(section, page.ActiveSection);
			Assert.Equal("/archive", page.ModulePathInfo);
		}

		[Fact]
		public void The_section_goes_before_the_short_description_and_the_node()
		{
			SectionOn(20, this._team);

			PublicPage page = ResolvePage(new PageRequest(1, 20, "about", ""));

			Assert.Same(this._team, page.ActiveNode);
			Assert.Same(this._about, ResolvePage(new PageRequest(1, null, "about", "")).ActiveNode);
		}

		[Fact]
		public void Unknown_sites_and_pages_are_not_found()
		{
			Assert.Equal(PageResolutionKind.NotFound, Resolve(NodeRequest(99)).Kind);
			Assert.Equal(PageResolutionKind.NotFound, Resolve(new PageRequest(null, 99, null, "")).Kind);
			Assert.Equal(PageResolutionKind.NotFound, Resolve(new PageRequest(null, null, "nothing", "")).Kind);

			this._lookup.Site = null;
			Assert.Equal(PageResolutionKind.SiteNotFound, Resolve(NodeRequest(1)).Kind);
		}

		[Fact]
		public void Pages_for_members_only_are_forbidden_for_anonymous_users()
		{
			Node members = Build.Node(4, "Members", this._home).Allow(this._administrator);
			members.Site = this._site;
			this._lookup.Nodes.Add(members);

			Assert.Equal(PageResolutionKind.Forbidden, Resolve(NodeRequest(4)).Kind);
			Assert.Equal(PageResolutionKind.Page, Resolve(NodeRequest(4), Administrator()).Kind);
		}

		[Fact]
		public void An_external_link_redirects()
		{
			this._about.LinkUrl = "https://example.com/";

			PageResolution resolution = Resolve(NodeRequest(2));

			Assert.Equal(PageResolutionKind.ExternalLink, resolution.Kind);
			Assert.Equal("https://example.com/", resolution.RedirectUrl);
		}

		[Fact]
		public void The_sections_of_the_template_go_before_the_ones_of_the_node_and_hidden_sections_are_left_out()
		{
			Section templateSection = Build.Section(30, Build.Node(99, "Elsewhere"), null).Allow(this._anonymous);
			this._template.Sections["side1content"] = templateSection;
			Section first = SectionOn(20, this._about);
			Section hidden = Build.Section(21, this._about, "maincontent").Allow(this._administrator);
			Section second = SectionOn(22, this._about, "side1content");

			PublicPage page = ResolvePage(NodeRequest(2));

			Assert.Equal(
				new[] { new PageSection("side1content", templateSection), new PageSection("maincontent", first), new PageSection("side1content", second) },
				page.Sections);
			Assert.Contains(new PageSection("maincontent", hidden), ResolvePage(NodeRequest(2), Administrator()).Sections);
		}

		[Fact]
		public void The_meta_tags_of_the_node_replace_the_ones_of_the_site()
		{
			this._site.MetaKeywords = "site keywords";
			this._site.MetaDescription = "site description";
			this._about.MetaDescription = "about description";

			PublicPage page = ResolvePage(NodeRequest(2));

			Assert.Equal(
				new Dictionary<string, string> { ["keywords"] = "site keywords", ["description"] = "about description" },
				page.Assets.MetaTags);
		}

		[Fact]
		public void An_offline_page_shows_the_offline_template_without_sections_to_visitors()
		{
			SectionOn(20, this._about);
			this._about.Status = (int)NodeStatus.Offline;

			PublicPage page = ResolvePage(NodeRequest(2));

			Assert.True(page.IsOffline);
			Assert.Same(this._offlineTemplate, page.Template);
			Assert.Empty(page.Sections);
		}

		[Fact]
		public void Resolving_an_offline_page_leaves_the_node_unchanged()
		{
			// The WebForms site put the offline template and the offline status on the loaded node itself, where the
			// session could save them.
			this._about.Status = (int)NodeStatus.Offline;

			ResolvePage(NodeRequest(3));

			Assert.Same(this._template, this._team.Template);
			Assert.Equal((int)NodeStatus.Online, this._team.Status);
			Assert.Same(this._template, this._about.Template);
		}

		[Theory]
		[InlineData(1)] // the home page: the whole site is offline
		[InlineData(2)] // the parent
		[InlineData(3)] // the page itself
		public void A_page_is_offline_when_it_or_a_page_above_it_is_offline(int offlineNodeId)
		{
			this._lookup.Nodes.Single(n => n.Id == offlineNodeId).Status = (int)NodeStatus.Offline;

			PublicPage page = ResolvePage(NodeRequest(3));

			Assert.True(page.IsOffline);
			Assert.Same(this._offlineTemplate, page.Template);
		}

		[Fact]
		public void Pages_below_an_online_page_stay_online()
		{
			this._team.Status = (int)NodeStatus.Offline;

			Assert.False(ResolvePage(NodeRequest(2)).IsOffline);
		}

		[Fact]
		public void Administrators_see_an_offline_page_as_usual()
		{
			Section section = SectionOn(20, this._about);
			this._about.Status = (int)NodeStatus.Offline;

			PublicPage page = ResolvePage(NodeRequest(2), Administrator());

			Assert.True(page.IsOffline);
			Assert.Same(this._template, page.Template);
			Assert.Equal(new[] { new PageSection("maincontent", section) }, page.Sections);
		}

		[Fact]
		public void Without_an_offline_template_an_offline_page_has_its_own_template_without_sections()
		{
			this._site.OfflineTemplate = null;
			SectionOn(20, this._about);
			this._about.Status = (int)NodeStatus.Offline;

			PublicPage page = ResolvePage(NodeRequest(2));

			Assert.Same(this._template, page.Template);
			Assert.Empty(page.Sections);
		}

		private class FakePageLookup : IPageLookup
		{
			public Site Site { get; set; }
			public SiteAlias SiteAlias { get; set; }
			public List<Node> Nodes { get; } = new List<Node>();
			public List<Section> Sections { get; } = new List<Section>();
			public Dictionary<string, Node> RootNodes { get; } = new Dictionary<string, Node>();

			public SiteAlias GetSiteAliasByUrl(string url) => SiteAlias?.Url == url ? SiteAlias : null;

			public Site GetSiteBySiteUrl(string siteUrl) => Site?.SiteUrl == siteUrl ? Site : null;

			public Section GetSectionById(int sectionId) => Sections.SingleOrDefault(s => s.Id == sectionId);

			public Node GetNodeById(int nodeId) => Nodes.SingleOrDefault(n => n.Id == nodeId);

			public Node GetNodeByShortDescriptionAndSite(string shortDescription, Site site) =>
				Nodes.SingleOrDefault(n => n.ShortDescription == shortDescription && n.Site == site);

			public Node GetRootNodeByCultureAndSite(string culture, Site site) => RootNodes.GetValueOrDefault(culture);

			public IEnumerable<CustomMenu> GetMenusByRootNode(Node rootNode) => [];
		}
	}
}
