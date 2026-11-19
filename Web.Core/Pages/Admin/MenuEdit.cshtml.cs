using Cuyahoga.Core.DataAccess;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service.SiteStructure;
using Cuyahoga.Web.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Web.Core.Pages.Admin
{
	/// <summary>
	/// A custom menu of a root node (NodeId): a list of nodes in a placeholder of the node template.
	/// </summary>
	/// <remarks>
	/// The WebForms page kept the chosen nodes in the ViewState of a list box while nodes were added, removed and moved;
	/// here they are in the form (MenuNodeIds, in menu order).
	/// </remarks>
	public class MenuEditModel : AdminPageModel
	{
		private readonly ICommonDao _commonDao;
		private readonly INodeService _nodeService;
		private readonly TemplatePlaceholderProvider _placeholderProvider;
		private CustomMenu _menu;

		public MenuEditModel(ICommonDao commonDao, INodeService nodeService, TemplatePlaceholderProvider placeholderProvider)
		{
			this._commonDao = commonDao;
			this._nodeService = nodeService;
			this._placeholderProvider = placeholderProvider;
		}

		[BindProperty(SupportsGet = true)] public int MenuId { get; set; }
		[BindProperty] public string Name { get; set; }
		[BindProperty] public string Placeholder { get; set; }
		[BindProperty] public List<int> MenuNodeIds { get; set; } = new List<int>();
		[BindProperty] public int? AvailableNodeId { get; set; }
		[BindProperty] public int? SelectedNodeId { get; set; }

		public bool IsExistingMenu
		{
			get { return MenuId > 0; }
		}

		public List<string> Placeholders { get; private set; } = new List<string>();

		/// <summary>
		/// The nodes of the site that aren't in the menu (depth first).
		/// </summary>
		public List<Node> AvailableNodes { get; private set; }

		/// <summary>
		/// The nodes of the menu, in menu order.
		/// </summary>
		public List<Node> MenuNodes { get; private set; }

		public string NameError { get; private set; }

		public async Task<IActionResult> OnGetAsync()
		{
			if (!LoadMenu())
			{
				return NotFound();
			}
			Name = this._menu.Name;
			Placeholder = IsExistingMenu ? this._menu.Placeholder : null;
			MenuNodeIds = this._menu.Nodes.Select(node => node.Id).ToList();
			await LoadListsAsync();
			return Page();
		}

		public async Task<IActionResult> OnPostAsync()
		{
			if (!LoadMenu())
			{
				return NotFound();
			}
			string nodeEditUrl = "NodeEdit.aspx?NodeId=" + ActiveNode.Id;
			if (Request.Form.ContainsKey("btnBack"))
			{
				return Redirect(nodeEditUrl);
			}
			if (Request.Form.ContainsKey("btnDelete"))
			{
				try
				{
					this._commonDao.DeleteObject(this._menu);
					return Redirect(nodeEditUrl);
				}
				catch (Exception ex)
				{
					ShowError(ex.Message);
				}
			}
			else if (Request.Form.ContainsKey("btnSave"))
			{
				IActionResult result = Save(nodeEditUrl);
				if (result != null)
				{
					return result;
				}
			}
			else
			{
				ChangeMenuNodes();
			}
			await LoadListsAsync();
			return Page();
		}

		private bool LoadMenu()
		{
			Title = "Edit custom menu";
			if (ActiveNode == null)
			{
				return false;
			}
			this._menu = MenuId == -1
				? new CustomMenu { RootNode = ActiveNode }
				: this._commonDao.GetObjectById(typeof(CustomMenu), MenuId, true) as CustomMenu;
			return this._menu != null;
		}

		/// <summary>
		/// The add, remove, up and down buttons.
		/// </summary>
		private void ChangeMenuNodes()
		{
			if (Request.Form.ContainsKey("btnAdd") && AvailableNodeId.HasValue && !MenuNodeIds.Contains(AvailableNodeId.Value))
			{
				MenuNodeIds.Add(AvailableNodeId.Value);
			}
			else if (Request.Form.ContainsKey("btnRemove") && SelectedNodeId.HasValue)
			{
				MenuNodeIds.Remove(SelectedNodeId.Value);
				SelectedNodeId = null;
			}
			else if (SelectedNodeId.HasValue && (Request.Form.ContainsKey("btnUp") || Request.Form.ContainsKey("btnDown")))
			{
				int position = MenuNodeIds.IndexOf(SelectedNodeId.Value);
				int newPosition = Request.Form.ContainsKey("btnUp") ? position - 1 : position + 1;
				if (position > -1 && newPosition >= 0 && newPosition < MenuNodeIds.Count)
				{
					MenuNodeIds.RemoveAt(position);
					MenuNodeIds.Insert(newPosition, SelectedNodeId.Value);
				}
			}
		}

		private IActionResult Save(string nodeEditUrl)
		{
			if (String.IsNullOrEmpty(Name))
			{
				NameError = "Name is required";
				return null;
			}
			this._menu.Name = Name;
			this._menu.Placeholder = Placeholder;
			try
			{
				this._menu.Nodes.Clear();
				foreach (int nodeId in MenuNodeIds)
				{
					this._menu.Nodes.Add(this._nodeService.GetNodeById(nodeId));
				}
				if (this._menu.Id > 0)
				{
					this._commonDao.UpdateObject(this._menu);
					ShowMessage("Menu saved");
				}
				else
				{
					this._commonDao.SaveObject(this._menu);
					return Redirect(nodeEditUrl);
				}
			}
			catch (Exception ex)
			{
				ShowError(ex.Message);
			}
			return null;
		}

		private async Task LoadListsAsync()
		{
			if (ActiveNode.Template != null)
			{
				Placeholders = await this._placeholderProvider.GetPlaceholdersAsync(ActiveNode.Template.Id);
			}
			var siteNodes = new List<Node>();
			AddNodes(ActiveNode.Site.RootNodes, siteNodes);
			MenuNodes = MenuNodeIds.Select(id => siteNodes.FirstOrDefault(node => node.Id == id) ?? this._nodeService.GetNodeById(id)).ToList();
			AvailableNodes = siteNodes.Where(node => !MenuNodeIds.Contains(node.Id)).ToList();
		}

		private static void AddNodes(IList<Node> nodes, List<Node> result)
		{
			foreach (Node node in nodes)
			{
				result.Add(node);
				AddNodes(node.ChildNodes, result);
			}
		}
	}
}
