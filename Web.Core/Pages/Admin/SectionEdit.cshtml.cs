using Cuyahoga.Core.Communication;
using Cuyahoga.Core.DataAccess;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service;
using Cuyahoga.Core.Service.SiteStructure;
using Cuyahoga.Web.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Web.Core.Pages.Admin
{
	/// <summary>
	/// Edit a section of a node (NodeId) or a standalone section: general properties, the settings of its module,
	/// connections to other sections and permissions.
	/// </summary>
	public class SectionEditModel : AdminPageModel
	{
		/// <summary>
		/// The form field prefix of module settings (setting_NUMBER_OF_ARTICLES_IN_LIST).
		/// </summary>
		public const string SettingPrefix = "setting_";

		private readonly ISectionService _sectionService;
		private readonly ICommonDao _commonDao;
		private readonly ModuleLoader _moduleLoader;
		private readonly TemplatePlaceholderProvider _placeholderProvider;
		private Section _section;

		public SectionEditModel(ISectionService sectionService, ICommonDao commonDao, ModuleLoader moduleLoader,
			TemplatePlaceholderProvider placeholderProvider)
		{
			this._sectionService = sectionService;
			this._commonDao = commonDao;
			this._moduleLoader = moduleLoader;
			this._placeholderProvider = placeholderProvider;
		}

		[BindProperty(SupportsGet = true)]
		public int SectionId { get; set; }

		[BindProperty] public string SectionTitle { get; set; }
		[BindProperty] public bool ShowTitle { get; set; }
		[BindProperty] public int ModuleTypeId { get; set; }
		[BindProperty] public string Placeholder { get; set; }
		[BindProperty] public string CacheDuration { get; set; }
		[BindProperty] public List<int> ViewRoleIds { get; set; } = new List<int>();
		[BindProperty] public List<int> EditRoleIds { get; set; } = new List<int>();

		public Section Section
		{
			get { return this._section; }
		}

		/// <summary>
		/// A section that has a module can't get another one (there could be content already).
		/// </summary>
		public bool CanChooseModule
		{
			get { return this._section.ModuleType == null; }
		}

		public List<ModuleType> ModuleTypes { get; private set; }

		/// <summary>
		/// The module type whose settings are shown: the one of the section, or the chosen one for a new section.
		/// </summary>
		public ModuleType SettingsModuleType { get; private set; }

		/// <summary>
		/// The values of the module settings (by setting name) as shown in the form.
		/// </summary>
		public Dictionary<string, string> SettingValues { get; private set; } = new Dictionary<string, string>();

		/// <summary>
		/// The placeholders of the node template. Null without node (no placeholders can be chosen).
		/// </summary>
		public List<string> Placeholders { get; private set; }

		public Template NodeTemplate
		{
			get { return ActiveNode?.Template; }
		}

		public List<Role> Roles { get; private set; }

		/// <summary>
		/// True when the module can be connected to other sections (IActionProvider).
		/// </summary>
		public bool HasConnections { get; private set; }

		public bool CanAddConnection { get; private set; }

		public string TitleError { get; private set; }
		public string CacheDurationError { get; private set; }

		public async Task<IActionResult> OnGetAsync()
		{
			if (!LoadSection())
			{
				return NotFound();
			}
			if (SectionId == -1)
			{
				this._section.CopyRolesFromNode();
			}
			SectionTitle = this._section.Title;
			ShowTitle = this._section.ShowTitle;
			Placeholder = this._section.PlaceholderId;
			CacheDuration = this._section.CacheDuration.ToString();
			foreach (KeyValuePair<string, string> setting in this._section.Settings)
			{
				SettingValues[setting.Key] = setting.Value;
			}
			ViewRoleIds = Roles.Where(role => this._section.ViewAllowed(role)).Select(role => role.Id).ToList();
			EditRoleIds = Roles.Where(role => this._section.EditAllowed(role)).Select(role => role.Id).ToList();
			await LoadPageDataAsync();
			return Page();
		}

		public async Task<IActionResult> OnPostAsync()
		{
			if (!LoadSection())
			{
				return NotFound();
			}
			if (Request.Form.Keys.Any(key => key.StartsWith("btnBack")))
			{
				return Redirect(ActiveNode != null ? "NodeEdit.aspx?NodeId=" + ActiveNode.Id : "Sections.aspx");
			}
			// The settings as entered, for showing the form again.
			foreach (ModuleSetting setting in SettingsModuleType?.ModuleSettings ?? new List<ModuleSetting>())
			{
				SettingValues[setting.Name] = Request.Form[SettingPrefix + setting.Name];
			}

			string connectionToDelete = Request.Form["deleteConnection"];
			if (!String.IsNullOrEmpty(connectionToDelete))
			{
				try
				{
					this._section.Connections.Remove(connectionToDelete);
					this._commonDao.UpdateObject(this._section);
				}
				catch (Exception ex)
				{
					ShowError(ex.Message);
				}
			}
			else if (Request.Form["Refresh"] != "1")
			{
				IActionResult result = Save();
				if (result != null)
				{
					return result;
				}
			}
			// Otherwise another module was chosen: show its settings.
			await LoadPageDataAsync();
			return Page();
		}

		private bool LoadSection()
		{
			Title = "Edit section";
			if (SectionId == -1)
			{
				this._section = new Section { Node = ActiveNode };
			}
			else
			{
				this._section = ActiveSection;
			}
			if (this._section == null)
			{
				return false;
			}
			ModuleTypes = this._commonDao.GetAll(typeof(ModuleType), "Name").Cast<ModuleType>().ToList();
			Roles = this._commonDao.GetAll(typeof(Role), "PermissionLevel").Cast<Role>().ToList();
			// The settings of the module of the section, of the chosen module, or of the first module in the list.
			SettingsModuleType = this._section.ModuleType
				?? ModuleTypes.FirstOrDefault(moduleType => moduleType.ModuleTypeId == ModuleTypeId)
				?? ModuleTypes.FirstOrDefault();
			return true;
		}

		private async Task LoadPageDataAsync()
		{
			ModuleTypeId = SettingsModuleType?.ModuleTypeId ?? 0;
			if (NodeTemplate != null)
			{
				try
				{
					Placeholders = await this._placeholderProvider.GetPlaceholdersAsync(NodeTemplate.Id);
				}
				catch (Exception ex)
				{
					ShowError(ex.Message);
				}
			}
			else if (ActiveNode != null)
			{
				Placeholders = new List<string>();
			}

			if (this._section.ModuleType != null
				&& this._moduleLoader.GetModuleFromSection(this._section) is IActionProvider actionProvider)
			{
				HasConnections = true;
				CanAddConnection = this._section.Connections.Count < actionProvider.GetOutboundActions().Count;
			}
		}

		/// <summary>
		/// Saves the section. Returns the redirect after saving a new section, or null to show the page again.
		/// </summary>
		private IActionResult Save()
		{
			if (String.IsNullOrEmpty(SectionTitle))
			{
				TitleError = "Title is required";
			}
			int cacheDuration = 0;
			if (String.IsNullOrEmpty(CacheDuration))
			{
				CacheDurationError = "Cache duration is required (0 for no cache)";
			}
			else if (!Int32.TryParse(CacheDuration, out cacheDuration) || cacheDuration < 0)
			{
				CacheDurationError = "Only positive integers allowed";
			}
			if (TitleError != null || CacheDurationError != null)
			{
				return null;
			}

			// Remember the previous placeholder and position to detect changes.
			string oldPlaceholderId = this._section.PlaceholderId;
			int oldPosition = this._section.Position;
			try
			{
				this._section.Title = SectionTitle;
				this._section.ShowTitle = ShowTitle;
				this._section.Node = ActiveNode;
				if (ActiveNode != null)
				{
					ActiveNode.Sections.Add(this._section);
				}
				if (CanChooseModule)
				{
					this._section.ModuleType = SettingsModuleType;
				}
				// Without placeholders (no node), the placeholder list isn't posted.
				this._section.PlaceholderId = Placeholder ?? String.Empty;
				this._section.CacheDuration = cacheDuration;
				// Calculate a new position if the section is new or when the placeholder has changed.
				if (this._section.Id == -1 || this._section.PlaceholderId != oldPlaceholderId)
				{
					this._section.CalculateNewPosition();
				}
				SetSettings();
				ValidateSettings();
				SetPermissions();
				// When the placeholder changed, the sections after it in the old placeholder move up.
				if (oldPosition != -1 && oldPlaceholderId != this._section.PlaceholderId)
				{
					this._section.ChangeAndUpdatePositionsAfterPlaceholderChange(oldPlaceholderId, oldPosition, true);
				}

				if (this._section.Id > 0)
				{
					this._commonDao.UpdateObject(this._section);
				}
				else
				{
					this._commonDao.SaveObject(this._section);
					return Redirect(ActiveNode != null ? "NodeEdit.aspx?NodeId=" + ActiveNode.Id : "Sections.aspx");
				}
				ShowMessage("Section saved.");
			}
			catch (Exception ex)
			{
				ShowError(ex.Message);
			}
			return null;
		}

		/// <summary>
		/// The module settings from the form, checked against the type of each setting.
		/// </summary>
		private void SetSettings()
		{
			foreach (ModuleSetting setting in this._section.ModuleType.ModuleSettings)
			{
				Type type = setting.GetRealType();
				object value;
				if (type == typeof(bool))
				{
					value = SettingValues[setting.Name] == "true";
				}
				else
				{
					string text = SettingValues[setting.Name] ?? String.Empty;
					if (!type.IsEnum && setting.IsRequired && text == String.Empty)
					{
						throw new Exception(String.Format("The value for {0} is required.", setting.FriendlyName));
					}
					value = text;
				}
				try
				{
					if (type.IsEnum && value is string)
					{
						value = Enum.Parse(type, value.ToString());
					}
					else if (value.ToString().Length > 0)
					{
						Convert.ChangeType(value, type);
					}
				}
				catch (InvalidCastException ex)
				{
					throw new Exception(String.Format("Invalid value entered for {0}: {1}", setting.FriendlyName, value), ex);
				}
				this._section.Settings[setting.Name] = value.ToString();
			}
		}

		private void ValidateSettings()
		{
			ModuleBase module = this._moduleLoader.GetModuleFromSection(this._section)
				?? throw new InvalidOperationException($"The module {this._section.ModuleType.Name} is not available.");
			module.ValidateSectionSettings();
		}

		private void SetPermissions()
		{
			this._section.SectionPermissions.Clear();
			foreach (Role role in Roles)
			{
				bool view = ViewRoleIds.Contains(role.Id);
				bool edit = CanEdit(role) && EditRoleIds.Contains(role.Id);
				if (view || edit)
				{
					this._section.SectionPermissions.Add(new SectionPermission
					{
						Section = this._section, Role = role, ViewAllowed = view, EditAllowed = edit
					});
				}
			}
		}

		/// <summary>
		/// Only editors and administrators can get edit rights.
		/// </summary>
		public static bool CanEdit(Role role)
		{
			return role.HasPermission(AccessLevel.Editor) || role.HasPermission(AccessLevel.Administrator);
		}

		public string AddConnectionUrl
		{
			get
			{
				return ActiveNode != null
					? $"ConnectionEdit.aspx?NodeId={ActiveNode.Id}&SectionId={this._section.Id}"
					: $"ConnectionEdit.aspx?SectionId={this._section.Id}";
			}
		}
	}
}
