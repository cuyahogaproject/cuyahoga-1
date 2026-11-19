using Cuyahoga.Core.DataAccess;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service.SiteStructure;
using Cuyahoga.Web.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Web.Core.Pages.Admin
{
	/// <summary>
	/// Attach a standalone section to a placeholder of a node.
	/// </summary>
	public class SectionAttachNodeModel : AdminPageModel
	{
		private readonly ICommonDao _commonDao;
		private readonly INodeService _nodeService;
		private readonly TemplatePlaceholderProvider _placeholderProvider;
		private readonly SearchIndexUpdater _searchIndexUpdater;

		public SectionAttachNodeModel(ICommonDao commonDao, INodeService nodeService,
			TemplatePlaceholderProvider placeholderProvider, SearchIndexUpdater searchIndexUpdater)
		{
			this._commonDao = commonDao;
			this._nodeService = nodeService;
			this._placeholderProvider = placeholderProvider;
			this._searchIndexUpdater = searchIndexUpdater;
		}

		[BindProperty] public int SelectedSiteId { get; set; }
		[BindProperty] public int? SelectedNodeId { get; set; }
		[BindProperty] public string Placeholder { get; set; }

		public Section Section
		{
			get { return ActiveSection; }
		}

		public List<Site> Sites { get; private set; }

		/// <summary>
		/// The nodes of the selected site (depth first).
		/// </summary>
		public List<Node> Nodes { get; private set; } = new List<Node>();

		/// <summary>
		/// The placeholders of the template of the selected node. Null when no node with a template is selected.
		/// </summary>
		public List<string> Placeholders { get; private set; }

		public async Task<IActionResult> OnGetAsync()
		{
			if (this.Section == null)
			{
				return NotFound();
			}
			await LoadAsync();
			return Page();
		}

		public async Task<IActionResult> OnPostAsync()
		{
			if (this.Section == null)
			{
				return NotFound();
			}
			if (Request.Form.ContainsKey("btnBack"))
			{
				return Redirect("Sections.aspx");
			}
			if (Request.Form["Refresh"] == "site")
			{
				// Another site: choose a node of that site.
				SelectedNodeId = null;
			}
			else if (Request.Form.ContainsKey("btnSave") && SelectedNodeId.HasValue)
			{
				Node node = this._nodeService.GetNodeById(SelectedNodeId.Value);
				this.Section.Node = node;
				this.Section.PlaceholderId = Placeholder;
				node.Sections.Add(this.Section);
				this.Section.CalculateNewPosition();
				try
				{
					this._commonDao.UpdateObject(this.Section);
					// Update the full text index to make sure that the content can be found.
					this._searchIndexUpdater.UpdateIndexFromSection(this.Section);
					return Redirect("NodeEdit.aspx?NodeId=" + node.Id);
				}
				catch (Exception ex)
				{
					ShowError(ex.Message);
				}
			}
			await LoadAsync();
			return Page();
		}

		private async Task LoadAsync()
		{
			Title = "Attach section";
			Sites = this._commonDao.GetAll(typeof(Site)).Cast<Site>().ToList();
			Site site = Sites.FirstOrDefault(s => s.Id == SelectedSiteId) ?? Sites.FirstOrDefault();
			if (site == null)
			{
				return;
			}
			SelectedSiteId = site.Id;
			AddNodes(this._nodeService.GetRootNodes(site));

			Node selectedNode = Nodes.FirstOrDefault(node => node.Id == SelectedNodeId);
			if (selectedNode?.Template != null)
			{
				try
				{
					Placeholders = await this._placeholderProvider.GetPlaceholdersAsync(selectedNode.Template.Id);
				}
				catch (Exception ex)
				{
					ShowError(ex.Message);
				}
			}
		}

		private void AddNodes(IList<Node> nodes)
		{
			foreach (Node node in nodes)
			{
				Nodes.Add(node);
				AddNodes(node.ChildNodes);
			}
		}

		public Node SelectedNode
		{
			get { return Nodes.FirstOrDefault(node => node.Id == SelectedNodeId); }
		}
	}
}
