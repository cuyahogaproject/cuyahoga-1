using Cuyahoga.Core.DataAccess;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service.SiteStructure;
using Cuyahoga.Core.Util;
using Cuyahoga.Web.Core.PublicSite;
using Cuyahoga.Web.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Web.Core.Pages.Admin
{
	/// <summary>
	/// Edit a template: its name, the folder with its files (base path), the template control and the stylesheet, and
	/// the sections in its placeholders. The stylesheets and images are in wwwroot/{base path}, the layouts in Templates.
	/// </summary>
	public class TemplateEditModel : AdminPageModel
	{
		private readonly ICommonDao _commonDao;
		private readonly INodeService _nodeService;
		private readonly TemplatePlaceholderProvider _placeholderProvider;
		private readonly TemplateLayouts _templateLayouts;
		private Template _template;

		public TemplateEditModel(ICommonDao commonDao, INodeService nodeService, TemplatePlaceholderProvider placeholderProvider,
			TemplateLayouts templateLayouts)
		{
			this._commonDao = commonDao;
			this._nodeService = nodeService;
			this._placeholderProvider = placeholderProvider;
			this._templateLayouts = templateLayouts;
		}

		[BindProperty(SupportsGet = true)] public int TemplateId { get; set; }
		[BindProperty] public string Name { get; set; }
		[BindProperty] public string BasePath { get; set; }
		[BindProperty] public string TemplateControl { get; set; }
		[BindProperty] public string Css { get; set; }

		/// <summary>
		/// The base path that the file lists were made for (the WebForms page kept the lists in the ViewState until
		/// the base path was verified again).
		/// </summary>
		[BindProperty] public string ListedBasePath { get; set; }

		public bool IsExistingTemplate
		{
			get { return TemplateId > 0; }
		}

		public Template Template
		{
			get { return this._template; }
		}

		public bool CanDelete { get; private set; }
		public List<string> TemplateControls { get; private set; } = new List<string>();
		public List<string> Stylesheets { get; private set; } = new List<string>();
		public string TemplateControlWarning { get; private set; }
		public string CssWarning { get; private set; }

		/// <summary>
		/// The placeholders of the template control (existing templates), or null when it can't be loaded.
		/// </summary>
		public List<string> Placeholders { get; private set; }

		public string NameError { get; private set; }
		public string BasePathError { get; private set; }

		public async Task<IActionResult> OnGetAsync()
		{
			if (!LoadTemplate())
			{
				return NotFound();
			}
			Name = this._template.Name;
			BasePath = this._template.BasePath;
			TemplateControl = this._template.TemplateControl;
			Css = this._template.Css;
			ListFiles(BasePath, false);
			await LoadPlaceholdersAsync();
			return Page();
		}

		public async Task<IActionResult> OnPostAsync(string detachPlaceholder)
		{
			if (!LoadTemplate())
			{
				return NotFound();
			}
			if (Request.Form.ContainsKey("btnBack"))
			{
				return Redirect("Templates.aspx");
			}
			if (Request.Form.ContainsKey("btnDelete") && IsExistingTemplate)
			{
				try
				{
					this._commonDao.DeleteObject(this._template);
					return Redirect("Templates.aspx");
				}
				catch (Exception ex)
				{
					ShowError(ex.Message);
				}
			}

			if (Request.Form.ContainsKey("btnVerifyBasePath"))
			{
				VerifyBasePath();
			}
			else
			{
				ListFiles(ListedBasePath, true);
				if (Request.Form.ContainsKey("btnSave"))
				{
					IActionResult result = Save();
					if (result != null)
					{
						return result;
					}
				}
				else if (!String.IsNullOrEmpty(detachPlaceholder))
				{
					this._template.Sections.Remove(detachPlaceholder);
					try
					{
						this._commonDao.UpdateObject(this._template);
						ShowMessage(String.Format("Section in Placeholder {0} detached", detachPlaceholder));
					}
					catch (Exception ex)
					{
						ShowError(ex.Message);
					}
				}
			}
			await LoadPlaceholdersAsync();
			return Page();
		}

		private bool LoadTemplate()
		{
			Title = "Edit template";
			this._template = TemplateId == -1
				? new Template()
				: this._commonDao.GetObjectById(typeof(Template), TemplateId, true) as Template;
			if (this._template == null)
			{
				return false;
			}
			CanDelete = IsExistingTemplate && this._nodeService.GetNodesByTemplate(this._template).Count <= 0;
			return true;
		}

		private void VerifyBasePath()
		{
			if (String.IsNullOrWhiteSpace(BasePath))
			{
				ShowError("The base path can not be empty.");
				ListFiles(ListedBasePath, true);
			}
			else if (!Directory.Exists(MapPath(BasePath)))
			{
				ShowError("The base path you entered could not be found on the server.");
				ListFiles(ListedBasePath, true);
			}
			else
			{
				ListFiles(BasePath, true);
			}
		}

		/// <summary>
		/// The template controls in the base path and the stylesheets in its Css folder (BindTemplateUserControls and
		/// BindCss of the WebForms page, with the same warnings).
		/// </summary>
		private void ListFiles(string basePath, bool isPostBack)
		{
			ListedBasePath = basePath;
			var templateDir = new DirectoryInfo(MapPath(basePath));
			if (templateDir.Exists)
			{
				// The template controls are the Razor layouts of the base path (Templates/AnotherRed/Cuyahoga.cshtml),
				// with the names of the user controls of the WebForms site (Cuyahoga.ascx).
				IReadOnlyList<string> templateControls = this._templateLayouts.GetTemplateControls(basePath);
				if (templateControls.Count == 0 && isPostBack)
				{
					TemplateControlWarning = "No template user controls found at the [base path] location.";
				}
				else
				{
					TemplateControls = templateControls.ToList();
				}
			}
			var cssDir = new DirectoryInfo(MapPath(basePath + "/Css"));
			if (cssDir.Exists)
			{
				string[] stylesheets = cssDir.GetFiles("*.css").Select(file => file.Name).ToArray();
				if (stylesheets.Length == 0 && isPostBack)
				{
					CssWarning = "No stylesheet files found at the [base path]/Css location.";
				}
				else
				{
					Stylesheets = stylesheets.ToList();
				}
			}
			else
			{
				CssWarning = "The location for the stylesheets ([base path]/Css) could not be found.";
			}
		}

		private IActionResult Save()
		{
			NameError = String.IsNullOrEmpty(Name) ? "Name is required" : null;
			BasePathError = String.IsNullOrEmpty(BasePath) ? "Base path is required" : null;
			if (NameError != null || BasePathError != null)
			{
				return null;
			}
			if (!TemplateControls.Contains(TemplateControl) || !Stylesheets.Contains(Css))
			{
				ShowError("No template control or css selected.");
				return null;
			}
			this._template.Name = Name;
			this._template.BasePath = BasePath;
			this._template.TemplateControl = TemplateControl;
			this._template.Css = Css;
			try
			{
				if (this._template.Id == -1)
				{
					this._commonDao.SaveObject(this._template);
					return Redirect("Templates.aspx");
				}
				this._commonDao.UpdateObject(this._template);
				ShowMessage("Template saved");
			}
			catch (Exception ex)
			{
				ShowError(ex.Message);
			}
			return null;
		}

		private async Task LoadPlaceholdersAsync()
		{
			if (!IsExistingTemplate)
			{
				return;
			}
			string templateControlPath = "/" + this._template.Path;
			if (!this._templateLayouts.HasLayout(this._template))
			{
				ShowError("Unable to load the template control " + templateControlPath);
				return;
			}
			try
			{
				Placeholders = await this._placeholderProvider.GetPlaceholdersAsync(this._template.Id);
			}
			catch (Exception ex)
			{
				ShowError(ex.Message);
			}
		}

		/// <summary>
		/// The physical path of a path of the site (the public files of a template: Templates/AnotherRed).
		/// </summary>
		private static string MapPath(string path)
		{
			return ApplicationPaths.MapPath("~/" + (path ?? String.Empty).TrimStart('/'));
		}
	}
}
