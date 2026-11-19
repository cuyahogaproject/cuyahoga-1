using System.Collections;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service.Membership;
using Cuyahoga.Core.Service.SiteStructure;
using Cuyahoga.Core.Util;
using Cuyahoga.Web.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Web.Core.Pages.Admin
{
	public class SiteEditModel : AdminPageModel
	{
		public const int NoTemplate = -1;

		private readonly ISiteService _siteService;
		private readonly ITemplateService _templateService;
		private readonly IUserService _userService;
		private readonly TemplatePlaceholderProvider _placeholderProvider;
		private Site _site;

		public SiteEditModel(ISiteService siteService, ITemplateService templateService, IUserService userService,
			TemplatePlaceholderProvider placeholderProvider)
		{
			this._siteService = siteService;
			this._templateService = templateService;
			this._userService = userService;
			this._placeholderProvider = placeholderProvider;
		}

		[BindProperty(SupportsGet = true)]
		public int SiteId { get; set; }

		[BindProperty] public string Name { get; set; }
		[BindProperty] public string SiteUrl { get; set; }
		[BindProperty] public string WebmasterEmail { get; set; }
		[BindProperty] public bool UseFriendlyUrls { get; set; }
		[BindProperty] public int TemplateId { get; set; } = NoTemplate;
		[BindProperty] public string Placeholder { get; set; }
		[BindProperty] public int OfflineTemplateId { get; set; } = NoTemplate;
		[BindProperty] public string Culture { get; set; }
		[BindProperty] public int RoleId { get; set; }
		[BindProperty] public string MetaDescription { get; set; }
		[BindProperty] public string MetaKeywords { get; set; }

		public bool IsExistingSite
		{
			get { return SiteId > 0; }
		}

		public List<Template> Templates { get; private set; }
		public List<string> Placeholders { get; private set; } = new List<string>();
		public List<KeyValuePair<string, string>> Cultures { get; private set; }
		public List<Role> Roles { get; private set; }
		public List<SiteAlias> Aliases { get; private set; } = new List<SiteAlias>();

		public string NameError { get; private set; }
		public string SiteUrlError { get; private set; }
		public string WebmasterEmailError { get; private set; }

		public async Task<IActionResult> OnGetAsync()
		{
			if (!LoadSite())
			{
				return NotFound();
			}
			Name = this._site.Name;
			SiteUrl = this._site.SiteUrl;
			WebmasterEmail = this._site.WebmasterEmail;
			UseFriendlyUrls = this._site.UseFriendlyUrls;
			TemplateId = this._site.DefaultTemplate?.Id ?? NoTemplate;
			Placeholder = this._site.DefaultPlaceholder;
			OfflineTemplateId = this._site.OfflineTemplate?.Id ?? NoTemplate;
			Culture = this._site.DefaultCulture;
			RoleId = this._site.DefaultRole?.Id ?? 0;
			MetaDescription = this._site.MetaDescription;
			MetaKeywords = this._site.MetaKeywords;
			await LoadListsAsync();
			return Page();
		}

		public async Task<IActionResult> OnPostAsync()
		{
			if (!LoadSite())
			{
				return NotFound();
			}
			if (Request.Form.ContainsKey("btnCancel"))
			{
				return Redirect("Default.aspx");
			}
			if (Request.Form.ContainsKey("btnDelete"))
			{
				try
				{
					this._siteService.DeleteSite(this._site);
					return Redirect("Default.aspx");
				}
				catch (Exception ex)
				{
					ShowError(ex.Message);
				}
			}
			else if (Request.Form["Refresh"] != "1")
			{
				if (Save() && !IsExistingSite)
				{
					// Continue on the page of the new site: saving again must not create another site.
					return Redirect("SiteEdit.aspx?SiteId=" + this._site.Id + "&message=" + Uri.EscapeDataString("Site saved."));
				}
			}
			// Otherwise another template was chosen: show its placeholders.
			await LoadListsAsync();
			return Page();
		}

		private bool LoadSite()
		{
			Title = "Edit site";
			this._site = SiteId == -1 ? new Site() : this._siteService.GetSiteById(SiteId);
			return this._site != null;
		}

		private async Task LoadListsAsync()
		{
			Templates = this._templateService.GetAllTemplates().Cast<Template>().ToList();
			if (TemplateId != NoTemplate)
			{
				try
				{
					Placeholders = await this._placeholderProvider.GetPlaceholdersAsync(TemplateId);
				}
				catch (Exception ex)
				{
					ShowError(ex.Message);
				}
			}
			Cultures = Globalization.GetOrderedCultures().Cast<DictionaryEntry>()
				.Select(culture => new KeyValuePair<string, string>((string)culture.Key, (string)culture.Value))
				.ToList();
			// .NET 10 knows other cultures than .NET Framework: keep the culture of the site when it's missing, so a
			// save doesn't change it.
			if (!String.IsNullOrEmpty(Culture) && !Cultures.Any(culture => culture.Key == Culture))
			{
				Cultures.Insert(0, new KeyValuePair<string, string>(Culture, Culture));
			}
			// GetAllRoles has no order, so the database decides which role comes first. Sort, and give a new site the
			// least privileged role of logged-in users (Authenticated user) instead of whichever role comes first,
			// which could be Administrator.
			Roles = this._userService.GetAllRoles().Cast<Role>().OrderBy(role => role.PermissionLevel).ToList();
			if (RoleId == 0)
			{
				RoleId = Roles.FirstOrDefault(role => role.HasPermission(AccessLevel.Authenticated))?.Id ?? 0;
			}
			if (IsExistingSite)
			{
				Aliases = this._siteService.GetSiteAliasesBySite(this._site).Cast<SiteAlias>().ToList();
			}
		}

		/// <summary>
		/// Validates and saves the site. Returns true when the site is saved.
		/// </summary>
		private bool Save()
		{
			NameError = String.IsNullOrEmpty(Name) ? "Name is required" : null;
			SiteUrlError = String.IsNullOrEmpty(SiteUrl) ? "Site url is required" : null;
			WebmasterEmailError = String.IsNullOrEmpty(WebmasterEmail) ? "Webmaster email is required" : null;
			if (NameError != null || SiteUrlError != null || WebmasterEmailError != null)
			{
				return false;
			}

			this._site.Name = Name;
			this._site.SiteUrl = SiteUrl;
			this._site.WebmasterEmail = WebmasterEmail;
			this._site.UseFriendlyUrls = UseFriendlyUrls;
			if (TemplateId != NoTemplate)
			{
				this._site.DefaultTemplate = this._templateService.GetTemplateById(TemplateId);
				// Without placeholders in the list, the placeholder stays as it was.
				if (Placeholder != null)
				{
					this._site.DefaultPlaceholder = Placeholder;
				}
			}
			else
			{
				this._site.DefaultTemplate = null;
				this._site.DefaultPlaceholder = null;
			}
			this._site.OfflineTemplate = OfflineTemplateId != NoTemplate
				? this._templateService.GetTemplateById(OfflineTemplateId)
				: null;
			this._site.DefaultCulture = Culture;
			this._site.DefaultRole = this._userService.GetRoleById(RoleId);
			this._site.MetaDescription = String.IsNullOrWhiteSpace(MetaDescription) ? null : MetaDescription.Trim();
			this._site.MetaKeywords = String.IsNullOrWhiteSpace(MetaKeywords) ? null : MetaKeywords.Trim();
			try
			{
				this._siteService.SaveSite(this._site);
				ShowMessage("Site saved.");
				return true;
			}
			catch (Exception ex)
			{
				ShowError(ex.Message);
				return false;
			}
		}

		public static string FormatEntryNode(SiteAlias alias)
		{
			return alias.EntryNode == null ? "Inherited from site" : alias.EntryNode.Title + " (" + alias.EntryNode.Culture + ")";
		}
	}
}
