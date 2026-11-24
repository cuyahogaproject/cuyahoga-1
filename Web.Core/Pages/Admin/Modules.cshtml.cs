using System.Globalization;
using System.Reflection;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service;
using Cuyahoga.Core.Service.SiteStructure;
using Cuyahoga.Core.Util;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Web.Core.Pages.Admin
{
	/// <summary>
	/// The modules: the installed module types and the module folders of the site (~/Modules) that aren't installed
	/// yet, with their activation and installation status; install, upgrade and uninstall them (the install scripts in
	/// ~/Modules/{name}/Install) and switch the activation on startup.
	/// </summary>
	/// <remarks>
	/// A module that is switched on is activated at the start of the next request (ModuleActivation): activating it in
	/// the middle of a request doesn't work with the NHibernate session that is already open.
	/// </remarks>
	public class ModulesModel : AdminPageModel
	{
		private readonly IModuleTypeService _moduleTypeService;
		private readonly ModuleLoader _moduleLoader;

		public ModulesModel(IModuleTypeService moduleTypeService, ModuleLoader moduleLoader)
		{
			this._moduleTypeService = moduleTypeService;
			this._moduleLoader = moduleLoader;
		}

		public IReadOnlyList<ModuleRow> Modules { get; private set; }

		public record ModuleRow(ModuleType ModuleType, bool IsActive, bool CanInstall, bool CanUpgrade, bool CanUninstall,
			string InstallationStatus);

		public void OnGet()
		{
			BindModules();
		}

		/// <summary>
		/// Switches the activation on startup of a module type.
		/// </summary>
		public IActionResult OnPostActivation(int moduleTypeId, bool activate)
		{
			ModuleType moduleType = this._moduleTypeService.GetModuleById(moduleTypeId);
			moduleType.AutoActivate = activate;
			this._moduleTypeService.SaveOrUpdateModuleType(moduleType);
			return RedirectToPage();
		}

		/// <summary>
		/// Installs, upgrades or uninstalls a module (the argument is name:assembly).
		/// </summary>
		public IActionResult OnPostCommand(string command, string argument)
		{
			string[] arguments = (argument ?? String.Empty).Split(':');
			string moduleName = arguments[0];
			string assemblyName = arguments.Length > 1 ? arguments[1] : String.Empty;
			try
			{
				DatabaseInstaller installer = CreateInstaller(moduleName, assemblyName);
				switch (command)
				{
					case "Install":
						installer.Install();
						break;
					case "Upgrade":
						installer.Upgrade();
						break;
					case "Uninstall":
						installer.Uninstall();
						break;
					default:
						return BadRequest();
				}
				return RedirectToPage(new { message = $"{command}: operation succeeded for {moduleName}." });
			}
			catch (Exception ex)
			{
				ShowError($"{command}: operation failed for {moduleName}. {ex.Message}");
				BindModules();
				return Page();
			}
		}

		private void BindModules()
		{
			Title = "Modules";
			List<ModuleType> moduleTypes = this._moduleTypeService.GetAllModuleTypes().Cast<ModuleType>().ToList();
			// The module folders that aren't installed yet are candidates.
			var moduleRoot = new DirectoryInfo(ApplicationPaths.MapPath("~/Modules"));
			if (moduleRoot.Exists)
			{
				foreach (DirectoryInfo directory in moduleRoot.GetDirectories())
				{
					if ((directory.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden
						&& moduleTypes.All(moduleType => moduleType.Name != directory.Name))
					{
						moduleTypes.Add(new ModuleType { Name = directory.Name });
					}
				}
			}
			Modules = moduleTypes.Select(CreateRow).ToList();
		}

		private ModuleRow CreateRow(ModuleType moduleType)
		{
			DatabaseInstaller installer = CreateInstaller(moduleType.Name, moduleType.AssemblyName);
			string status;
			if (installer.CurrentVersionInDatabase != null)
			{
				Version version = installer.CurrentVersionInDatabase;
				status = String.Format(CultureInfo.InvariantCulture, "Installed ({0}.{1}.{2})", version.Major, version.Minor,
					version.Build);
				if (installer.CanUpgrade)
				{
					status += " (upgrade available) ";
				}
			}
			else
			{
				status = "Uninstalled";
			}
			return new ModuleRow(moduleType, this._moduleLoader.IsModuleActive(moduleType), installer.CanInstall,
				installer.CanUpgrade, installer.CanUninstall, status);
		}

		private static DatabaseInstaller CreateInstaller(string moduleName, string assemblyName)
		{
			string installDirectory = Path.Combine(ApplicationPaths.MapPath("~/Modules/" + moduleName), "Install");
			Assembly assembly = !String.IsNullOrEmpty(assemblyName) ? Assembly.Load(assemblyName) : null;
			return new DatabaseInstaller(installDirectory, assembly);
		}
	}
}
