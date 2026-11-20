using System.Collections.Concurrent;
using Cuyahoga.Core.Domain;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Cuyahoga.Web.Core.PublicSite
{
	/// <summary>
	/// Finds the controller of a module type: the controller that derives from ModuleController of that type.
	/// </summary>
	public class ModuleControllerRegistry
	{
		private readonly IActionDescriptorCollectionProvider _actionDescriptorProvider;
		private readonly ConcurrentDictionary<string, string> _controllersByModuleType = new ConcurrentDictionary<string, string>();
		private Dictionary<Type, string> _controllersByModuleClass;

		public ModuleControllerRegistry(IActionDescriptorCollectionProvider actionDescriptorProvider)
		{
			this._actionDescriptorProvider = actionDescriptorProvider;
		}

		/// <summary>
		/// The controller name for the module type, or null when the module has no controller.
		/// </summary>
		public string GetControllerName(ModuleType moduleType)
		{
			return this._controllersByModuleType.GetOrAdd(moduleType.ClassName + ", " + moduleType.AssemblyName, key =>
			{
				Type moduleClass = Type.GetType(key, throwOnError: false);
				return moduleClass != null && GetControllersByModuleClass().TryGetValue(moduleClass, out string controller)
					? controller
					: null;
			});
		}

		private Dictionary<Type, string> GetControllersByModuleClass()
		{
			return this._controllersByModuleClass ??= this._actionDescriptorProvider.ActionDescriptors.Items
				.OfType<ControllerActionDescriptor>()
				.Select(action => (action.ControllerName, Module: ModuleTypes.GetModuleClass(action.ControllerTypeInfo.AsType(), typeof(ModuleController<>))))
				.Where(pair => pair.Module != null)
				.DistinctBy(pair => pair.Module)
				.ToDictionary(pair => pair.Module, pair => pair.ControllerName);
		}
	}

	internal static class ModuleTypes
	{
		/// <summary>
		/// The module type argument of a generic module base class (ModuleController&lt;TModule&gt;,
		/// ModuleViewComponent&lt;TModule&gt;) of the type, or null.
		/// </summary>
		public static Type GetModuleClass(Type type, Type genericBaseClass)
		{
			for (; type != null; type = type.BaseType)
			{
				if (type.IsGenericType && type.GetGenericTypeDefinition() == genericBaseClass)
				{
					return type.GetGenericArguments()[0];
				}
			}
			return null;
		}
	}
}
