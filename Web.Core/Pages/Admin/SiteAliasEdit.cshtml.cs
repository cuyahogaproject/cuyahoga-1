using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service.SiteStructure;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Web.Core.Pages.Admin
{
	public class SiteAliasEditModel : AdminPageModel
	{
		public const int InheritFromSite = -1;

		private readonly ISiteService _siteService;
		private readonly INodeService _nodeService;
		private SiteAlias _siteAlias;

		public SiteAliasEditModel(ISiteService siteService, INodeService nodeService)
		{
			this._siteService = siteService;
			this._nodeService = nodeService;
		}

		[BindProperty(SupportsGet = true)]
		public int SiteAliasId { get; set; }

		[BindProperty] public string Url { get; set; }
		[BindProperty] public int EntryNodeId { get; set; } = InheritFromSite;

		public bool IsExistingSiteAlias
		{
			get { return SiteAliasId > 0; }
		}

		public string UrlError { get; private set; }

		/// <summary>
		/// The nodes of the site (depth first), for the entry node list.
		/// </summary>
		public List<Node> Nodes { get; private set; } = new List<Node>();

		public IActionResult OnGet()
		{
			if (!LoadSiteAlias())
			{
				return NotFound();
			}
			Url = this._siteAlias.Url;
			EntryNodeId = this._siteAlias.EntryNode?.Id ?? InheritFromSite;
			return Page();
		}

		public IActionResult OnPost()
		{
			if (!LoadSiteAlias())
			{
				return NotFound();
			}
			string siteUrl = "SiteEdit.aspx?SiteId=" + this._siteAlias.Site.Id;
			if (Request.Form.ContainsKey("btnCancel"))
			{
				return Redirect(siteUrl);
			}
			try
			{
				if (Request.Form.ContainsKey("btnDelete"))
				{
					this._siteService.DeleteSiteAlias(this._siteAlias);
					return Redirect(siteUrl);
				}
				if (String.IsNullOrEmpty(Url))
				{
					UrlError = "Url is required";
					return Page();
				}
				this._siteAlias.Url = Url;
				this._siteAlias.EntryNode = EntryNodeId != InheritFromSite ? this._nodeService.GetNodeById(EntryNodeId) : null;
				this._siteService.SaveSiteAlias(this._siteAlias);
				return Redirect(siteUrl);
			}
			catch (Exception ex)
			{
				ShowError(ex.Message);
			}
			return Page();
		}

		private bool LoadSiteAlias()
		{
			Title = "Edit site alias";
			if (SiteAliasId == -1)
			{
				Site site = ActiveSite ?? throw new InvalidOperationException("No site specified for the new alias.");
				this._siteAlias = new SiteAlias { Site = site };
			}
			else
			{
				this._siteAlias = this._siteService.GetSiteAliasById(SiteAliasId);
			}
			if (this._siteAlias == null)
			{
				return false;
			}
			AddNodes(this._siteAlias.Site.RootNodes);
			return true;
		}

		private void AddNodes(IList<Node> nodes)
		{
			foreach (Node node in nodes)
			{
				Nodes.Add(node);
				AddNodes(node.ChildNodes);
			}
		}

		/// <summary>
		/// The node title, indented by its level (five non-breaking spaces per level).
		/// </summary>
		public static string IndentedTitle(Node node)
		{
			return new string(' ', node.Level * 5) + node.Title;
		}
	}
}
