using System.Collections.Concurrent;
using Cuyahoga.Core.Domain;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace Cuyahoga.Web.Core.PublicSite
{
	/// <summary>
	/// Finds the view of a module type: the view component that derives from ModuleViewComponent of that type.
	/// </summary>
	public class ModuleViewRegistry
	{
		private readonly IViewComponentDescriptorCollectionProvider _descriptorProvider;
		private readonly ConcurrentDictionary<string, Type> _viewsByModuleType = new ConcurrentDictionary<string, Type>();
		private Dictionary<Type, Type> _viewsByModuleClass;

		public ModuleViewRegistry(IViewComponentDescriptorCollectionProvider descriptorProvider)
		{
			this._descriptorProvider = descriptorProvider;
		}

		/// <summary>
		/// The view component type for the module type, or null when the module has no view on this site (yet).
		/// </summary>
		public Type GetViewComponentType(ModuleType moduleType)
		{
			return this._viewsByModuleType.GetOrAdd(moduleType.ClassName + ", " + moduleType.AssemblyName, key =>
			{
				Type moduleClass = Type.GetType(key, throwOnError: false);
				return moduleClass != null && GetViewsByModuleClass().TryGetValue(moduleClass, out Type view) ? view : null;
			});
		}

		private Dictionary<Type, Type> GetViewsByModuleClass()
		{
			return this._viewsByModuleClass ??= this._descriptorProvider.ViewComponents.Items
				.Select(descriptor => (View: descriptor.TypeInfo.AsType(), Module: GetModuleClass(descriptor.TypeInfo.AsType())))
				.Where(pair => pair.Module != null)
				.ToDictionary(pair => pair.Module, pair => pair.View);
		}

		private static Type GetModuleClass(Type viewComponentType)
		{
			for (Type type = viewComponentType; type != null; type = type.BaseType)
			{
				if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ModuleViewComponent<>))
				{
					return type.GetGenericArguments()[0];
				}
			}
			return null;
		}
	}
}
