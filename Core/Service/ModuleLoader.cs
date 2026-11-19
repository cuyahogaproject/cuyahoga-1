using System;
using System.Collections;
using System.Collections.Generic;
using Castle.Core;
using Castle.MicroKernel;
using Castle.MicroKernel.Registration;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Persistence;
using Cuyahoga.Core.Service.SiteStructure;
using log4net;

namespace Cuyahoga.Core.Service
{
	/// <summary>
	/// Responsible for loading module assemblies and registering these in the application.
	/// </summary>
	public class ModuleLoader
	{
		private static readonly ILog log = LogManager.GetLogger(typeof(ModuleLoader));
		private readonly IKernel _kernel;
		private readonly SessionFactoryHolder _sessionFactoryHolder;
		private readonly IModuleTypeService _moduleTypeService;
		private readonly ISectionUrlProvider _sectionUrlProvider;
		// Assemblies that are already added to the NHibernate mappings.
		private readonly HashSet<string> _mappedAssemblies = new HashSet<string>();
		private readonly object _lockObject = new object();

		public ModuleLoader(IKernel kernel, SessionFactoryHolder sessionFactoryHolder, IModuleTypeService moduleTypeService,
			ISectionUrlProvider sectionUrlProvider)
		{
			this._kernel = kernel;
			this._sessionFactoryHolder = sessionFactoryHolder;
			this._moduleTypeService = moduleTypeService;
			this._sectionUrlProvider = sectionUrlProvider;
		}

		/// <summary>
		/// Get the module instance that is associated with the given section.
		/// </summary>
		public ModuleBase GetModuleFromSection(Section section)
		{
			ModuleBase module = this.GetModuleFromType(section.ModuleType);
			if (module != null)
			{
				module.Section = section;
				string sectionUrl = this._sectionUrlProvider.GetUrlFromSection(section);
				if (sectionUrl != null)
				{
					module.SectionUrl = sectionUrl;
				}
				module.ReadSectionSettings();
			}
			return module;
		}

		/// <summary>
		/// Get the module instance by its type
		/// </summary>
		public ModuleBase GetModuleFromType(ModuleType moduleType)
		{
			string modulekey = string.Concat("module.", moduleType.ClassName);
			if (this._kernel.HasComponent(modulekey))
			{
				ModuleBase module = (ModuleBase)this._kernel.Resolve(modulekey, typeof(ModuleBase));
				module.SectionUrlProvider = this._sectionUrlProvider;
				return module;
			}
			if (log.IsDebugEnabled)
			{
				log.DebugFormat("Unable to find module with key '{0}' in the container.", modulekey);
			}
			return null;
		}

		/// <summary>
		/// Checks if the module is registered in the container.
		/// </summary>
		public bool IsModuleActive(ModuleType moduleType)
		{
			return this._kernel.HasComponent(string.Concat("module.", moduleType.ClassName));
		}

		/// <summary>
		/// Activate all modules that have the AutoActivate property set to true
		/// </summary>
		public void RegisterActivatedModules()
		{
			if (log.IsDebugEnabled)
			{
				log.Debug("Entering module loading.");
			}
			IList moduleTypes = this._moduleTypeService.GetAllModuleTypes();
			foreach (ModuleType mt in moduleTypes)
			{
				if (mt.AutoActivate)
				{
					ActivateModule(mt);
				}
			}
			if (log.IsDebugEnabled)
			{
				log.Debug("Finished module loading.");
			}
		}

		/// <summary>
		/// Registers the module, its services and its NHibernate mappings.
		/// </summary>
		public void ActivateModule(ModuleType moduleType)
		{
			string assemblyQualifiedName = moduleType.ClassName + ", " + moduleType.AssemblyName;
			if (log.IsDebugEnabled)
			{
				log.DebugFormat("Loading module {0}.", assemblyQualifiedName);
			}
			Type moduleTypeType = Type.GetType(assemblyQualifiedName);
			if (moduleTypeType == null)
			{
				throw new Exception("Could not find module: " + assemblyQualifiedName);
			}

			// Only one thread at a time
			lock (this._lockObject)
			{
				if (this._kernel.HasComponent(moduleTypeType))
				{
					if (log.IsDebugEnabled)
					{
						log.DebugFormat("The module with type {0} is already registered in the container.", moduleTypeType);
					}
					return;
				}

				// First, register optional module services that the module might depend on.
				foreach (ModuleService moduleService in moduleType.ModuleServices)
				{
					Type serviceType = Type.GetType(moduleService.ServiceType);
					Type classType = Type.GetType(moduleService.ClassType);
					if (log.IsDebugEnabled)
					{
						log.DebugFormat("Loading module service {0}, ({1}).", moduleService.ServiceKey, moduleService.ClassType);
					}
					LifestyleType lifestyle = LifestyleType.Singleton;
					if (moduleService.Lifestyle != null)
					{
						try
						{
							lifestyle = (LifestyleType)Enum.Parse(typeof(LifestyleType), moduleService.Lifestyle);
						}
						catch (ArgumentException ex)
						{
							throw new Exception(String.Format("Unable to load module service {0} with invalid lifestyle {1}.",
								moduleService.ServiceKey, moduleService.Lifestyle), ex);
						}
					}
					this._kernel.Register(Component.For(serviceType).ImplementedBy(classType).Named(moduleService.ServiceKey).LifeStyle.Is(lifestyle));
				}

				// Register the module
				this._kernel.Register(Component.For(moduleTypeType).Named("module." + moduleTypeType.FullName));

				// Add the NHibernate mappings, once per assembly
				if (typeof(INHibernateModule).IsAssignableFrom(moduleTypeType) && !this._mappedAssemblies.Contains(moduleType.AssemblyName))
				{
					if (log.IsDebugEnabled)
					{
						log.DebugFormat("Adding module assembly {0} to the NHibernate mappings.", moduleTypeType.Assembly);
					}
					this._sessionFactoryHolder.AddAssembly(moduleTypeType.Assembly);
					this._mappedAssemblies.Add(moduleType.AssemblyName);
				}
			}
		}
	}
}
