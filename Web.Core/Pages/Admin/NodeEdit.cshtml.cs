using System.Collections;
using Cuyahoga.Core.DataAccess;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Persistence;
using Cuyahoga.Core.Service;
using Cuyahoga.Core.Service.SiteStructure;
using Cuyahoga.Core.Util;
using Cuyahoga.Web.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Web.Core.Pages.Admin
{
	/// <summary>
	/// Edit a node (page): properties, position, template, custom menus (root nodes), sections and permissions.
	/// NodeId=-1 with ParentNodeId or SiteId creates a new node.
	/// </summary>
	public class NodeEditModel : AdminPageModel
	{
		private readonly INodeService _nodeService;
		private readonly ISectionService _sectionService;
		private readonly ICommonDao _commonDao;
		private readonly ISessionManager _sessionManager;
		private readonly ModuleLoader _moduleLoader;
		private readonly TemplatePlaceholderProvider _placeholderProvider;
		private readonly SearchIndexUpdater _searchIndexUpdater;
		private readonly ILogger<NodeEditModel> _logger;

		public NodeEditModel(INodeService nodeService, ISectionService sectionService, ICommonDao commonDao,
			ISessionManager sessionManager, ModuleLoader moduleLoader, TemplatePlaceholderProvider placeholderProvider,
			SearchIndexUpdater searchIndexUpdater, ILogger<NodeEditModel> logger)
		{
			this._nodeService = nodeService;
			this._sectionService = sectionService;
			this._commonDao = commonDao;
			this._sessionManager = sessionManager;
			this._moduleLoader = moduleLoader;
			this._placeholderProvider = placeholderProvider;
			this._searchIndexUpdater = searchIndexUpdater;
			this._logger = logger;
		}

		[BindProperty(SupportsGet = true)] public int NodeId { get; set; }
		[BindProperty] public string NodeTitle { get; set; }
		[BindProperty] public string ShortDescription { get; set; }
		[BindProperty] public string Culture { get; set; }
		[BindProperty] public bool ShowInNavigation { get; set; }
		[BindProperty] public string MetaDescription { get; set; }
		[BindProperty] public string MetaKeywords { get; set; }
		[BindProperty] public bool IsLink { get; set; }
		[BindProperty] public string LinkUrl { get; set; }
		[BindProperty] public string LinkTarget { get; set; } = nameof(Cuyahoga.Core.Domain.LinkTarget.Self);
		[BindProperty] public int TemplateId { get; set; }
		[BindProperty] public List<int> ViewRoleIds { get; set; } = new List<int>();
		[BindProperty] public List<int> EditRoleIds { get; set; } = new List<int>();
		[BindProperty] public bool PropagateToSections { get; set; }
		[BindProperty] public bool PropagateToChildNodes { get; set; }

		public bool IsNewNode
		{
			get { return ActiveNode.Id == -1; }
		}

		public List<KeyValuePair<string, string>> Cultures { get; private set; }
		public List<Template> Templates { get; private set; }
		public List<Role> Roles { get; private set; }
		public List<Section> Sections { get; private set; } = new List<Section>();
		public List<CustomMenu> Menus { get; private set; }

		/// <summary>
		/// The placeholders of the node template, to mark sections whose placeholder isn't in the template.
		/// </summary>
		public List<string> TemplatePlaceholders { get; private set; }

		public string TitleError { get; private set; }
		public string ShortDescriptionError { get; private set; }
		public bool ShortDescriptionRequiredError { get; private set; }

		public async Task<IActionResult> OnGetAsync()
		{
			if (!LoadNode())
			{
				return NotFound();
			}
			// A section movement in the request (the up and down links of the sections).
			string action = Request.Query["Action"];
			if (ActiveSection != null && action != null)
			{
				return MoveSection(action);
			}
			if (IsNewNode && ActiveNode.ParentNode != null)
			{
				// Defaults of a new child node.
				ActiveNode.Template = ActiveNode.ParentNode.Template;
				ActiveNode.Culture = ActiveNode.ParentNode.Culture;
				ActiveNode.CopyRolesFromParent();
			}
			BindNodeValues();
			ViewRoleIds = Roles.Where(role => ActiveNode.ViewAllowed(role)).Select(role => role.Id).ToList();
			EditRoleIds = Roles.Where(role => ActiveNode.EditAllowed(role)).Select(role => role.Id).ToList();
			await LoadTemplatePlaceholdersAsync();
			return Page();
		}

		public async Task<IActionResult> OnPostAsync()
		{
			if (!LoadNode())
			{
				return NotFound();
			}
			IActionResult result = null;
			string refresh = Request.Form["Refresh"];
			string sectionToDelete = Request.Form["deleteSection"];
			string sectionToDetach = Request.Form["detachSection"];
			NodePositionMovement? movement = GetPositionMovement();

			if (movement.HasValue)
			{
				result = MoveNode(movement.Value);
			}
			else if (IsButton("btnNew"))
			{
				result = Redirect("NodeEdit.aspx?NodeId=-1&ParentNodeId=" + ActiveNode.Id);
			}
			else if (IsButton("btnCancel"))
			{
				result = Redirect(IsNewNode && ActiveNode.ParentNode != null
					? "NodeEdit.aspx?NodeId=" + ActiveNode.ParentNode.Id
					: "Default.aspx");
			}
			else if (IsButton("btnSave"))
			{
				result = Save();
			}
			else if (IsButton("btnOffline"))
			{
				result = ChangeStatus();
			}
			else if (IsButton("btnDelete"))
			{
				result = Delete();
			}
			else if (IsButton("btnDuplicate"))
			{
				result = Duplicate();
			}
			else if (refresh == "template")
			{
				result = SaveWithTemplate();
			}
			else if (!String.IsNullOrEmpty(sectionToDelete))
			{
				DeleteSection(Int32.Parse(sectionToDelete));
			}
			else if (!String.IsNullOrEmpty(sectionToDetach))
			{
				DetachSection(Int32.Parse(sectionToDetach));
			}
			// Otherwise the external link checkbox changed: show the form for it.
			if (result != null)
			{
				return result;
			}
			LoadLists();
			await LoadTemplatePlaceholdersAsync();
			return Page();
		}

		private bool IsButton(string name)
		{
			return Request.Form.ContainsKey(name) || Request.Form.ContainsKey(name + "2");
		}

		private NodePositionMovement? GetPositionMovement()
		{
			foreach (NodePositionMovement movement in Enum.GetValues<NodePositionMovement>())
			{
				// Image buttons post their click position (btnUp.x).
				if (Request.Form.ContainsKey("btn" + movement + ".x"))
				{
					return movement;
				}
			}
			return null;
		}

		private bool LoadNode()
		{
			Title = "Edit node";
			if (NodeId == -1)
			{
				// A new node, under a parent node or at root level of a site.
				ActiveNode = new Node();
				if (Int32.TryParse(Request.Query["ParentNodeId"], out int parentNodeId))
				{
					ActiveNode.ParentNode = this._nodeService.GetNodeById(parentNodeId);
					ActiveNode.Site = ActiveNode.ParentNode.Site;
				}
				else if (ActiveSite != null)
				{
					ActiveNode.Site = ActiveSite;
					ActiveNode.Culture = ActiveSite.DefaultCulture;
					ActiveNode.Template = ActiveSite.DefaultTemplate;
				}
			}
			if (ActiveNode == null)
			{
				return false;
			}
			LoadLists();
			return true;
		}

		private void LoadLists()
		{
			Templates = this._commonDao.GetAll(typeof(Template), "Name").Cast<Template>().ToList();
			Roles = this._commonDao.GetAll(typeof(Role), "PermissionLevel").Cast<Role>().ToList();
			Cultures = Globalization.GetOrderedCultures().Cast<DictionaryEntry>()
				.Select(culture => new KeyValuePair<string, string>((string)culture.Key, (string)culture.Value))
				.ToList();
			// .NET 10 knows other cultures than .NET Framework: keep the culture of the node when it's missing.
			string culture = Culture ?? ActiveNode.Culture;
			if (!String.IsNullOrEmpty(culture) && !Cultures.Any(c => c.Key == culture))
			{
				Cultures.Insert(0, new KeyValuePair<string, string>(culture, culture));
			}
			if (!IsNewNode)
			{
				// Use the sorted sections as the sections of the node, otherwise there are two lists with the same
				// sections.
				ActiveNode.Sections = this._sectionService.GetSortedSectionsByNode(ActiveNode);
				Sections = ActiveNode.Sections.ToList();
			}
			Menus = ActiveNode.IsRootNode
				? this._nodeService.GetMenusByRootNode(ActiveNode).Cast<CustomMenu>().ToList()
				: null;
		}

		private void BindNodeValues()
		{
			NodeTitle = ActiveNode.Title;
			ShortDescription = ActiveNode.ShortDescription;
			Culture = ActiveNode.Culture;
			ShowInNavigation = ActiveNode.ShowInNavigation;
			MetaDescription = ActiveNode.MetaDescription;
			MetaKeywords = ActiveNode.MetaKeywords;
			IsLink = ActiveNode.IsExternalLink;
			LinkUrl = ActiveNode.LinkUrl;
			LinkTarget = ActiveNode.LinkTarget.ToString();
			TemplateId = ActiveNode.Template?.Id ?? 0;
		}

		private async Task LoadTemplatePlaceholdersAsync()
		{
			TemplatePlaceholders = ActiveNode.Template != null && Sections.Count > 0
				? await this._placeholderProvider.GetPlaceholdersAsync(ActiveNode.Template.Id)
				: null;
		}

		private bool Validate()
		{
			TitleError = String.IsNullOrEmpty(NodeTitle) ? "Title is required" : null;
			if (!IsNewNode)
			{
				if (String.IsNullOrEmpty(ShortDescription))
				{
					ShortDescriptionRequiredError = true;
				}
				else if (!System.Text.RegularExpressions.Regex.IsMatch(ShortDescription, @"^\S+$"))
				{
					ShortDescriptionError = "No spaces are allowed";
				}
			}
			return TitleError == null && ShortDescriptionError == null && !ShortDescriptionRequiredError;
		}

		private void SetTemplate()
		{
			// External links have no template.
			if (!IsLink && TemplateId > 0)
			{
				ActiveNode.Template = this._commonDao.GetObjectById(typeof(Template), TemplateId) as Template;
			}
		}

		private void SetPermissions()
		{
			ActiveNode.NodePermissions.Clear();
			foreach (Role role in Roles)
			{
				bool view = ViewRoleIds.Contains(role.Id);
				bool edit = SectionEditModel.CanEdit(role) && EditRoleIds.Contains(role.Id);
				if (view || edit)
				{
					ActiveNode.NodePermissions.Add(new NodePermission { Node = ActiveNode, Role = role, ViewAllowed = view, EditAllowed = edit });
				}
			}
		}

		private void SetShortDescription()
		{
			if (!IsNewNode)
			{
				ActiveNode.ShortDescription = ShortDescription;
			}
			else
			{
				// Generated for new nodes.
				ActiveNode.CreateShortDescription();
			}
		}

		/// <summary>
		/// Saves the node. Returns the redirect after creating a node, or null.
		/// </summary>
		private IActionResult SaveNode()
		{
			if (!IsNewNode)
			{
				this._nodeService.UpdateNode(ActiveNode, PropagateToChildNodes, PropagateToSections);
				return null;
			}
			ActiveNode.CalculateNewPosition(this._nodeService.GetRootNodes(ActiveNode.Site));
			ActiveNode.ParentNode?.ChildNodes.Add(ActiveNode);
			this._commonDao.SaveObject(ActiveNode);
			return Redirect($"NodeEdit.aspx?NodeId={ActiveNode.Id}&message={Uri.EscapeDataString("Node created sucessfully")}");
		}

		private IActionResult Save()
		{
			try
			{
				SetTemplate();
				if (!Validate())
				{
					return null;
				}
				ActiveNode.Title = NodeTitle;
				ActiveNode.Culture = Culture;
				ActiveNode.ShowInNavigation = ShowInNavigation;
				ActiveNode.MetaDescription = String.IsNullOrWhiteSpace(MetaDescription) ? null : MetaDescription.Trim();
				ActiveNode.MetaKeywords = String.IsNullOrWhiteSpace(MetaKeywords) ? null : MetaKeywords.Trim();
				if (IsLink)
				{
					ActiveNode.LinkUrl = LinkUrl;
					ActiveNode.LinkTarget = Enum.Parse<LinkTarget>(LinkTarget);
				}
				else
				{
					ActiveNode.LinkUrl = null;
					ActiveNode.LinkTarget = Cuyahoga.Core.Domain.LinkTarget.Self;
				}
				ActiveNode.Validate();
				SetShortDescription();
				SetPermissions();
				IActionResult result = SaveNode();
				if (result != null)
				{
					return result;
				}
				ShowMessage("Node saved.");
			}
			catch (Exception ex)
			{
				ShowError(ex.InnerException != null ? ex.Message + ", " + ex.InnerException.Message : ex.Message);
				this._logger.LogError(ex, "Error saving node.");
			}
			return null;
		}

		/// <summary>
		/// Choosing another template saves the node (with only title and culture from the form, like the WebForms page).
		/// </summary>
		private IActionResult SaveWithTemplate()
		{
			try
			{
				SetTemplate();
				ActiveNode.Title = NodeTitle;
				ActiveNode.Culture = Culture;
				if (Validate())
				{
					SetShortDescription();
					SetPermissions();
					IActionResult result = SaveNode();
					if (result != null)
					{
						return result;
					}
					ShowMessage("Node saved while setting the template.");
				}
			}
			catch (Exception ex)
			{
				ShowError(ex.Message);
				this._logger.LogError(ex, "Error while switching the template.");
			}
			return null;
		}

		private IActionResult ChangeStatus()
		{
			if (ActiveNode.Status != (int)NodeStatus.Online && ActiveNode.Status != (int)NodeStatus.Offline)
			{
				ShowError("Actual node status is not recognized.");
				return null;
			}
			if (ActiveNode.Site.OfflineTemplate == null)
			{
				ShowError("It is not possibile to take node offline, because no Offline templase was selected on site edit page.");
				return null;
			}
			ActiveNode.Status = (int)(ActiveNode.Status == (int)NodeStatus.Online ? NodeStatus.Offline : NodeStatus.Online);
			try
			{
				IActionResult result = SaveNode();
				if (result != null)
				{
					return result;
				}
				BindNodeValues();
				ShowMessage(ActiveNode.Status == (int)NodeStatus.Online ? "Node is now online." : "Node is now offline.");
			}
			catch (Exception ex)
			{
				ShowError(ex.Message);
				this._logger.LogError(ex, "Error while changing the node status.");
			}
			return null;
		}

		private IActionResult Delete()
		{
			if (ActiveNode.Sections.Count > 0)
			{
				ShowError("Can't delete a node when there are sections attached. Please delete or detach all sections first.");
				return null;
			}
			if (ActiveNode.ChildNodes.Count > 0)
			{
				ShowError("Can't delete a node when there are child nodes attached. Please delete all childnodes first.");
				return null;
			}
			try
			{
				bool hasParentNode = ActiveNode.ParentNode != null;
				if (hasParentNode)
				{
					ActiveNode.ParentNode.ChildNodes.Remove(ActiveNode);
				}
				else
				{
					this._nodeService.GetRootNodes(ActiveNode.Site).Remove(ActiveNode);
				}
				this._nodeService.DeleteNode(ActiveNode);
				// Close the gap in the positions of the other nodes.
				ActiveNode.ReOrderNodePositions(
					ActiveNode.Level == 0 ? this._nodeService.GetRootNodes(ActiveNode.Site) : ActiveNode.ParentNode.ChildNodes,
					ActiveNode.Position);
				this._sessionManager.OpenSession().Flush();
				return Redirect(hasParentNode ? "NodeEdit.aspx?NodeId=" + ActiveNode.ParentNode.Id : "Default.aspx");
			}
			catch (Exception ex)
			{
				ShowError(ex.Message);
				this._logger.LogError(ex, "Error deleting node {NodeId}.", ActiveNode.Id);
			}
			return null;
		}

		/// <summary>
		/// A copy of the node next to it, with copies of its sections (without their content).
		/// </summary>
		private IActionResult Duplicate()
		{
			try
			{
				var node = new Node
				{
					ParentNode = ActiveNode.ParentNode,
					Site = ActiveNode.Site,
					Title = "Copy of " + ActiveNode.Title,
					Template = ActiveNode.Template,
					Culture = ActiveNode.Culture,
					LinkUrl = ActiveNode.LinkUrl,
					MetaDescription = ActiveNode.MetaDescription,
					MetaKeywords = ActiveNode.MetaKeywords,
					LinkTarget = ActiveNode.LinkTarget,
					ShowInNavigation = ActiveNode.ShowInNavigation
				};
				node.CreateShortDescription();
				foreach (NodePermission permission in ActiveNode.NodePermissions)
				{
					node.NodePermissions.Add(new NodePermission
					{
						Node = node, Role = permission.Role, ViewAllowed = permission.ViewAllowed, EditAllowed = permission.EditAllowed
					});
				}
				node.CalculateNewPosition(this._nodeService.GetRootNodes(node.Site));
				ActiveNode.ChildNodes.Add(node);
				this._commonDao.SaveObject(node);
				foreach (Section section in ActiveNode.Sections)
				{
					var copy = new Section
					{
						Node = node,
						CacheDuration = section.CacheDuration,
						ModuleType = section.ModuleType,
						PlaceholderId = section.PlaceholderId,
						Position = section.Position,
						ShowTitle = section.ShowTitle,
						Title = section.Title
					};
					foreach (KeyValuePair<string, Section> connection in section.Connections)
					{
						copy.Connections.Add(connection.Key, connection.Value);
					}
					foreach (KeyValuePair<string, string> setting in section.Settings)
					{
						copy.Settings.Add(setting.Key, setting.Value);
					}
					copy.CopyRolesFromNode();
					copy.CalculateNewPosition();
					this._commonDao.SaveObject(copy);
				}
				return Redirect($"NodeEdit.aspx?NodeId={node.Id}&message={Uri.EscapeDataString("Node has been duplicated.")}");
			}
			catch (Exception ex)
			{
				ShowError(ex.InnerException != null ? ex.Message + " " + ex.InnerException.Message : ex.Message);
			}
			return null;
		}

		private IActionResult MoveNode(NodePositionMovement movement)
		{
			ActiveNode.Move(this._nodeService.GetRootNodes(ActiveNode.Site), movement);
			this._sessionManager.OpenSession().Flush();
			return Redirect(Request.Path + Request.QueryString);
		}

		private IActionResult MoveSection(string action)
		{
			Section section = ActiveSection;
			if (action == "MoveUp")
			{
				section.MoveUp();
			}
			else if (action == "MoveDown")
			{
				section.MoveDown();
			}
			this._sessionManager.OpenSession().Flush();
			// The same page without the section movement parameters.
			return Redirect(Request.Path + "?NodeId=" + ActiveNode.Id);
		}

		private void DeleteSection(int sectionId)
		{
			Section section = this._sectionService.GetSectionById(sectionId);
			section.Node = ActiveNode;
			try
			{
				// First tell the module to remove its content.
				ModuleBase module = this._moduleLoader.GetModuleFromSection(section)
					?? throw new InvalidOperationException($"The module {section.ModuleType.Name} of the section is not available.");
				module.DeleteModuleContent();
				// No gap in the positions of the sections (the method wasn't made for this, but it works).
				section.ChangeAndUpdatePositionsAfterPlaceholderChange(section.PlaceholderId, section.Position, false);
				ActiveNode.Sections.Remove(section);
				this._commonDao.DeleteObject(section);
			}
			catch (Exception ex)
			{
				ShowError(ex.Message);
				this._logger.LogError(ex, "Error deleting section {SectionId}.", section.Id);
			}
		}

		private void DetachSection(int sectionId)
		{
			Section section = this._sectionService.GetSectionById(sectionId);
			try
			{
				// No gap in the positions of the sections.
				section.ChangeAndUpdatePositionsAfterPlaceholderChange(section.PlaceholderId, section.Position, false);
				ActiveNode.Sections.Remove(section);
				section.Node = null;
				section.PlaceholderId = null;
				this._commonDao.UpdateObject(section);
				// The content of a detached section no longer shows up in a search.
				this._searchIndexUpdater.UpdateIndexFromSection(section);
			}
			catch (Exception ex)
			{
				ShowError(ex.Message);
				this._logger.LogError(ex, "Error detaching section {SectionId}.", section.Id);
			}
		}

		/// <summary>
		/// The public url of the node (UrlHelper.GetUrlFromNode), for the preview button.
		/// </summary>
		public string NodeUrl
		{
			get
			{
				if (ActiveNode.IsExternalLink)
				{
					return ActiveNode.LinkUrl;
				}
				string applicationPath = Request.PathBase + "/";
				return ActiveNode.Site != null && ActiveNode.Site.UseFriendlyUrls
					? applicationPath + ActiveNode.ShortDescription + ".aspx"
					: applicationPath + ActiveNode.Id + "/view.aspx";
			}
		}

		public string ModuleAdminUrl(Section section)
		{
			return String.IsNullOrEmpty(section.ModuleType.EditPath)
				? null
				: $"{Request.PathBase}/{section.ModuleType.EditPath}?NodeId={section.Node.Id}&SectionId={section.Id}";
		}

		public bool ShowUp => ActiveNode.Position > 0;
		public bool ShowDown => ActiveNode.ParentNode != null && ActiveNode.Position != ActiveNode.ParentNode.ChildNodes.Count - 1 && !IsNewNode;
		public bool ShowLeft => ActiveNode.Level > 0 && !IsNewNode;
		public bool ShowRight => ActiveNode.Position > 0;
	}
}
