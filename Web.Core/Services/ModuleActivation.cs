using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Persistence;
using Cuyahoga.Core.Service;
using Cuyahoga.Core.Service.SiteStructure;

namespace Cuyahoga.Web.Core.Services
{
	/// <summary>
	/// Activates the modules that are activated in the database (ModuleType.AutoActivate), like the WebForms site does
	/// at its first request (Global.LoadModules).
	/// </summary>
	/// <remarks>
	/// Runs before every admin page, so modules that are activated on the WebForms site in the meantime are picked up
	/// too. A module that can't be loaded (for example its assembly isn't there) is logged and skipped: the admin pages
	/// keep working, sections of that module just have no module instance.
	/// </remarks>
	public class ModuleActivation
	{
		private readonly ModuleLoader _moduleLoader;
		private readonly IModuleTypeService _moduleTypeService;
		private readonly ILogger<ModuleActivation> _logger;
		private readonly HashSet<int> _failedModuleTypes = new HashSet<int>();
		private readonly object _lock = new object();

		public ModuleActivation(ModuleLoader moduleLoader, IModuleTypeService moduleTypeService, ILogger<ModuleActivation> logger)
		{
			this._moduleLoader = moduleLoader;
			this._moduleTypeService = moduleTypeService;
			this._logger = logger;
		}

		/// <summary>
		/// Activates the modules that aren't active yet. Call it before anything else in the request opens an NHibernate
		/// session: activating a module adds its mappings and builds a new session factory, and a session that was
		/// opened before doesn't know these mappings.
		/// </summary>
		public void ActivateModules()
		{
			lock (this._lock)
			{
				foreach (ModuleType moduleType in this._moduleTypeService.GetAllModuleTypes())
				{
					if (!moduleType.AutoActivate || this._failedModuleTypes.Contains(moduleType.ModuleTypeId)
						|| this._moduleLoader.IsModuleActive(moduleType))
					{
						continue;
					}
					try
					{
						this._moduleLoader.ActivateModule(moduleType);
					}
					catch (Exception ex)
					{
						this._failedModuleTypes.Add(moduleType.ModuleTypeId);
						this._logger.LogWarning(ex, "Unable to activate module {Module} ({ClassName}, {AssemblyName}).",
							moduleType.Name, moduleType.ClassName, moduleType.AssemblyName);
					}
				}
			}
			// The session of the query above comes from the session factory without the new mappings.
			SessionManager.CloseCurrentSession();
		}
	}
}
