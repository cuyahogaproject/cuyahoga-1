using Cuyahoga.Core.Service;
using Cuyahoga.Web.Core.PublicSite;
using Cuyahoga.Web.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Cuyahoga.Web.Core.Pages.Install
{
	/// <summary>
	/// Upgrades the database of Cuyahoga.Core and the built-in modules to the versions of the assemblies
	/// (Install/Upgrade.aspx of the WebForms site). For administrators.
	/// </summary>
	[Authorize(Policy = AdministratorRequirement.PolicyName)]
	public class UpgradeModel : PageModel
	{
		private readonly InstallationState _installationState;

		public UpgradeModel(InstallationState installationState)
		{
			this._installationState = installationState;
		}

		public bool CanUpgrade { get; private set; }
		public bool Finished { get; private set; }
		public string Error { get; private set; }
		public string CoreCurrent { get; private set; }
		public string CoreNew { get; private set; }
		public string ModulesCurrent { get; private set; }
		public string ModulesNew { get; private set; }

		public void OnGet()
		{
			try
			{
				DatabaseInstaller coreInstaller = InstallationState.CreateCoreInstaller();
				if (coreInstaller.CanUpgrade)
				{
					DatabaseInstaller modulesInstaller = InstallationState.CreateModulesInstaller();
					CanUpgrade = true;
					CoreCurrent = "Cuyahoga Core " + coreInstaller.CurrentVersionInDatabase.ToString(3);
					CoreNew = "Cuyahoga Core " + coreInstaller.NewAssemblyVersion.ToString(3);
					ModulesCurrent = "Cuyahoga Core Modules " + modulesInstaller.CurrentVersionInDatabase.ToString(3);
					ModulesNew = modulesInstaller.CanUpgrade
						? "Cuyahoga Core Modules " + modulesInstaller.NewAssemblyVersion.ToString(3)
						: "Cuyahoga Core Modules - no upgrade available";
				}
				else
				{
					Error = "Nothing to upgrade.";
				}
			}
			catch (Exception ex)
			{
				Error = "An error occured: " + ex;
			}
		}

		public void OnPost()
		{
			DatabaseInstaller coreInstaller = InstallationState.CreateCoreInstaller();
			DatabaseInstaller modulesInstaller = InstallationState.CreateModulesInstaller();
			try
			{
				coreInstaller.Upgrade();
				if (modulesInstaller.CanUpgrade)
				{
					modulesInstaller.Upgrade();
				}
				// The pages are available again.
				this._installationState.Reset();
				Finished = true;
			}
			catch (Exception ex)
			{
				Error = "An error occured while installing the database tables: " + ex;
			}
		}
	}
}
