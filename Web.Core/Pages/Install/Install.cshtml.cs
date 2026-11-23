using System.Collections;
using Cuyahoga.Core.DataAccess;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service;
using Cuyahoga.Core.Util;
using Cuyahoga.Web.Core.PublicSite;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Cuyahoga.Web.Core.Pages.Install
{
	public enum InstallStep
	{
		None,
		Database,
		Modules,
		Administrator,
		Site,
		Finished
	}

	/// <summary>
	/// The installer (Install/Install.aspx of the WebForms site): the database of Cuyahoga.Core and the built-in modules,
	/// the optional modules, the password of the administrator and the sample site.
	/// </summary>
	/// <remarks>
	/// Anyone can open the installer of an empty database. The steps after the database are only for the browser that
	/// installed it (a protected cookie, for an hour; the WebForms page relied on its ViewState), and an administrator
	/// is only added when there are no users at all.
	/// </remarks>
	public class InstallModel : PageModel
	{
		private const string InstallCookie = "CuyahogaInstall";
		private const string InstallPurpose = "Cuyahoga.Install";

		private readonly ICommonDao _commonDao;
		private readonly InstallationState _installationState;
		private readonly ITimeLimitedDataProtector _protector;

		public InstallModel(ICommonDao commonDao, InstallationState installationState, IDataProtectionProvider dataProtection)
		{
			this._commonDao = commonDao;
			this._installationState = installationState;
			this._protector = dataProtection.CreateProtector(InstallPurpose).ToTimeLimitedDataProtector();
		}

		public InstallStep Step { get; private set; }
		public string Error { get; private set; }
		public string Message { get; private set; }
		public string CoreAssembly { get; private set; }
		public string ModulesAssembly { get; private set; }
		public IReadOnlyList<string> OptionalModules { get; private set; } = new List<string>();
		public bool PasswordMissing { get; private set; }
		public bool ConfirmPasswordMissing { get; private set; }
		public bool PasswordsDiffer { get; private set; }

		public void OnGet()
		{
			try
			{
				DatabaseInstaller coreInstaller = InstallationState.CreateCoreInstaller();
				DatabaseInstaller modulesInstaller = InstallationState.CreateModulesInstaller();
				if (coreInstaller.CanInstall && modulesInstaller.CanInstall)
				{
					CoreAssembly = "Cuyahoga Core " + coreInstaller.NewAssemblyVersion.ToString(3);
					ModulesAssembly = "Cuyahoga Core Modules " + modulesInstaller.NewAssemblyVersion.ToString(3);
					Step = InstallStep.Database;
				}
				else if (this._commonDao.GetAll(typeof(User)).Count == 0)
				{
					// Installed without an administrator.
					StartInstallation();
					Step = InstallStep.Administrator;
				}
				else
				{
					Error = "Can't install Cuyahoga. Check if the install.sql file exists in the /Install/Core|Modules/Database/DATABASE_TYPE/ directory and that there isn't already a version installed.";
				}
			}
			catch (Exception ex)
			{
				Error = "An error occured: " + ex;
			}
		}

		public IActionResult OnPost(string password, string confirmPassword, List<string> modules)
		{
			if (Request.Form.ContainsKey("btnInstallDatabase"))
			{
				InstallDatabase();
				return Page();
			}
			if (!IsInstalling())
			{
				return StatusCode(StatusCodes.Status403Forbidden);
			}
			if (Request.Form.ContainsKey("btnInstallModules"))
			{
				try
				{
					List<string> installable = GetInstallableModules();
					foreach (string module in modules.Where(installable.Contains))
					{
						CreateModuleInstaller(module).Install();
					}
					Step = InstallStep.Administrator;
				}
				catch (Exception ex)
				{
					Error = "An error occured while installing additional modules: " + ex;
					Step = InstallStep.Modules;
					OptionalModules = GetInstallableModules();
				}
			}
			else if (Request.Form.ContainsKey("btnSkipInstallModules"))
			{
				Step = InstallStep.Administrator;
			}
			else if (Request.Form.ContainsKey("btnAdmin"))
			{
				CreateAdministrator(password, confirmPassword);
			}
			else if (Request.Form.ContainsKey("btnCreateSite"))
			{
				try
				{
					// Only on an installation without a site.
					if (this._commonDao.GetAll(typeof(Site)).Count == 0)
					{
						CreateSite();
					}
					FinishInstallation();
				}
				catch (Exception ex)
				{
					Error = "An error occured while creating the site: " + ex;
					Step = InstallStep.Site;
				}
			}
			else
			{
				FinishInstallation();
			}
			return Page();
		}

		private void InstallDatabase()
		{
			DatabaseInstaller coreInstaller = InstallationState.CreateCoreInstaller();
			DatabaseInstaller modulesInstaller = InstallationState.CreateModulesInstaller();
			if (!coreInstaller.CanInstall || !modulesInstaller.CanInstall)
			{
				OnGet();
				return;
			}
			try
			{
				coreInstaller.Install();
				modulesInstaller.Install();
				this._installationState.Reset();
				StartInstallation();
				Step = InstallStep.Modules;
				OptionalModules = GetInstallableModules();
				Message = "Database tables successfully created.";
			}
			catch (Exception ex)
			{
				Error = "An error occured while installing the database tables: " + ex;
			}
		}

		private void CreateAdministrator(string password, string confirmPassword)
		{
			Step = InstallStep.Administrator;
			PasswordMissing = String.IsNullOrEmpty(password);
			ConfirmPasswordMissing = String.IsNullOrEmpty(confirmPassword);
			PasswordsDiffer = !ConfirmPasswordMissing && password != confirmPassword;
			if (PasswordMissing || ConfirmPasswordMissing || PasswordsDiffer)
			{
				return;
			}
			// Only when there are really no users.
			if (this._commonDao.GetAll(typeof(User)).Count > 0)
			{
				Error = "There is already a user in the database. For security reasons Cuyahoga won't add a new user!";
				return;
			}
			try
			{
				var administrator = new User
				{
					UserName = "admin",
					Email = "webmaster@yourdomain.com",
					Password = Cuyahoga.Core.Domain.User.HashPassword(password),
					IsActive = true,
					TimeZone = 0
				};
				administrator.Roles.Add((Role)this._commonDao.GetObjectById(typeof(Role), 1));
				this._commonDao.SaveOrUpdateObject(administrator);
				Step = InstallStep.Site;
			}
			catch (Exception ex)
			{
				Error = "An error occured while creating the administrator: " + ex;
			}
		}

		#region The installation of this browser

		private void StartInstallation()
		{
			Response.Cookies.Append(InstallCookie, this._protector.Protect("install", TimeSpan.FromHours(1)),
				new CookieOptions { HttpOnly = true, Path = Request.PathBase + "/Install/" });
		}

		private bool IsInstalling()
		{
			try
			{
				return Request.Cookies.TryGetValue(InstallCookie, out string value) && this._protector.Unprotect(value) == "install";
			}
			catch (Exception)
			{
				return false;
			}
		}

		private void FinishInstallation()
		{
			Response.Cookies.Delete(InstallCookie, new CookieOptions { Path = Request.PathBase + "/Install/" });
			Step = InstallStep.Finished;
		}

		#endregion

		#region Optional modules

		/// <summary>
		/// The module folders (~/Modules) that aren't installed yet and have an install script.
		/// </summary>
		private List<string> GetInstallableModules()
		{
			List<string> installed = this._commonDao.GetAll(typeof(ModuleType), "Name").Cast<ModuleType>()
				.Select(moduleType => moduleType.Name).ToList();
			var moduleRoot = new DirectoryInfo(ApplicationPaths.MapPath("~/Modules"));
			if (!moduleRoot.Exists)
			{
				return new List<string>();
			}
			return moduleRoot.GetDirectories()
				.Where(directory => (directory.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden
					&& !installed.Contains(directory.Name)
					&& CreateModuleInstaller(directory.Name).CanInstall)
				.Select(directory => directory.Name)
				.ToList();
		}

		private static DatabaseInstaller CreateModuleInstaller(string module)
		{
			return new DatabaseInstaller(Path.Combine(ApplicationPaths.MapPath("~/Modules/" + module), "Install"), null);
		}

		#endregion

		#region The sample site

		private void CreateSite()
		{
			var adminUser = (User)this._commonDao.GetObjectById(typeof(User), 1);
			var defaultTemplate = (Template)this._commonDao.GetObjectByDescription(typeof(Template), "Name", "Another Red");
			var offlineTemplate = (Template)this._commonDao.GetObjectByDescription(typeof(Template), "Name", "Offline");
			var authenticatedRole = (Role)this._commonDao.GetObjectByDescription(typeof(Role), "Name", "Authenticated user");

			var site = new Site
			{
				Name = "Cuyahoga Sample Site",
				SiteUrl = Request.Scheme + "://" + Request.Host + Request.PathBase,
				WebmasterEmail = "webmaster@localhost",
				UseFriendlyUrls = true,
				DefaultCulture = "en-US",
				DefaultTemplate = defaultTemplate,
				OfflineTemplate = offlineTemplate,
				DefaultPlaceholder = "maincontent",
				DefaultRole = authenticatedRole
			};
			this._commonDao.SaveOrUpdateObject(site);

			var rootNode = new Node
			{
				Culture = site.DefaultCulture,
				Position = 0,
				ShortDescription = "home",
				ShowInNavigation = true,
				Site = site,
				Template = defaultTemplate,
				Title = "Home"
			};
			foreach (Role role in this._commonDao.GetAll(typeof(Role)))
			{
				rootNode.NodePermissions.Add(new NodePermission
				{
					Node = rootNode,
					Role = role,
					ViewAllowed = true,
					EditAllowed = role.HasPermission(AccessLevel.Administrator)
				});
			}
			this._commonDao.SaveOrUpdateObject(rootNode);

			Section loginSection = CreateSection(rootNode, "User", "Login", "side1content", showTitle: true);
			loginSection.Settings.Add("SHOW_EDIT_PROFILE", "True");
			loginSection.Settings.Add("SHOW_RESET_PASSWORD", "True");
			loginSection.Settings.Add("SHOW_REGISTER", "True");
			SaveSection(rootNode, loginSection);
			SaveSection(rootNode, CreateSection(rootNode, "StaticHtml", "Welcome", "maincontent", showTitle: true));

			Node page1 = CreatePage(site, rootNode, defaultTemplate, "page1", "Articles", 0, showInNavigation: true);
			if (this._commonDao.GetObjectByDescription(typeof(ModuleType), "Name", "Articles") != null)
			{
				Section articleSection = CreateSection(page1, "Articles", "Articles", "maincontent", showTitle: true);
				articleSection.Settings.Add("DISPLAY_TYPE", "FullContent");
				articleSection.Settings.Add("ALLOW_ANONYMOUS_COMMENTS", "True");
				articleSection.Settings.Add("ALLOW_COMMENTS", "True");
				articleSection.Settings.Add("SORT_BY", "DateOnline");
				articleSection.Settings.Add("SORT_DIRECTION", "DESC");
				articleSection.Settings.Add("ALLOW_SYNDICATION", "True");
				articleSection.Settings.Add("NUMBER_OF_ARTICLES_IN_LIST", "5");
				SaveSection(page1, articleSection);
			}

			Node page2 = CreatePage(site, rootNode, defaultTemplate, "page2", "Page 2", 1, showInNavigation: true);
			// Like the WebForms installer, the section of page 2 is also added to the sections of the home page object.
			SaveSection(rootNode, CreateSection(page2, "StaticHtml", "Page 2", "maincontent", showTitle: true));

			Node userProfileNode = CreatePage(site, rootNode, defaultTemplate, "userprofile", "User Profile", 2,
				showInNavigation: false);
			Section userProfileSection = CreateSection(userProfileNode, "UserProfile", "User Profile", "maincontent",
				showTitle: false);
			SaveSection(userProfileNode, userProfileSection);

			// The login box links to the profile pages.
			loginSection.Connections.Add("Register", userProfileSection);
			loginSection.Connections.Add("ResetPassword", userProfileSection);
			loginSection.Connections.Add("ViewProfile", userProfileSection);
			loginSection.Connections.Add("EditProfile", userProfileSection);
			this._commonDao.SaveOrUpdateObject(loginSection);
		}

		private Node CreatePage(Site site, Node rootNode, Template template, string shortDescription, string title,
			int position, bool showInNavigation)
		{
			var node = new Node
			{
				Culture = site.DefaultCulture,
				Position = position,
				ShortDescription = shortDescription,
				ShowInNavigation = showInNavigation,
				Site = site,
				Template = template,
				Title = title,
				ParentNode = rootNode
			};
			node.CopyRolesFromParent();
			this._commonDao.SaveOrUpdateObject(node);
			return node;
		}

		private Section CreateSection(Node node, string moduleName, string title, string placeholder, bool showTitle)
		{
			var section = new Section
			{
				ModuleType = (ModuleType)this._commonDao.GetObjectByDescription(typeof(ModuleType), "Name", moduleName),
				Title = title,
				CacheDuration = 0,
				Node = node,
				PlaceholderId = placeholder,
				Position = 0,
				ShowTitle = showTitle
			};
			section.CopyRolesFromNode();
			return section;
		}

		private void SaveSection(Node node, Section section)
		{
			node.Sections.Add(section);
			this._commonDao.SaveOrUpdateObject(section);
		}

		#endregion
	}
}
