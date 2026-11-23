using System.Collections.Concurrent;
using Cuyahoga.Core.Domain;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Cuyahoga.Web.Core.PublicSite
{
	/// <summary>
	/// Finds the controller of a module type: the controller that derives from ModuleController of that type.
	/// </summary>
	public class ModuleControllerRegistry
	{
		private readonly IActionDescriptorCollectionProvider _actionDescriptorProvider;
		private readonly ConcurrentDictionary<string, ModuleControllerInfo> _controllersByModuleType =
			new ConcurrentDictionary<string, ModuleControllerInfo>();
		private Dictionary<Type, ModuleControllerInfo> _controllersByModuleClass;

		public ModuleControllerRegistry(IActionDescriptorCollectionProvider actionDescriptorProvider)
		{
			this._actionDescriptorProvider = actionDescriptorProvider;
		}

		/// <summary>
		/// The controller name for the module type, or null when the module has no controller or its controller has
		/// no Index action for the http method.
		/// </summary>
		public string GetControllerName(ModuleType moduleType, string httpMethod)
		{
			ModuleControllerInfo controller = this._controllersByModuleType.GetOrAdd(
				moduleType.ClassName + ", " + moduleType.AssemblyName, key =>
				{
					Type moduleClass = Type.GetType(key, throwOnError: false);
					return moduleClass != null
						&& GetControllersByModuleClass().TryGetValue(moduleClass, out ModuleControllerInfo info)
						? info
						: null;
				});
			return controller != null && controller.Accepts(httpMethod) ? controller.Name : null;
		}

		private Dictionary<Type, ModuleControllerInfo> GetControllersByModuleClass()
		{
			return this._controllersByModuleClass ??= this._actionDescriptorProvider.ActionDescriptors.Items
				.OfType<ControllerActionDescriptor>()
				.Where(action => action.ActionName == "Index")
				.Select(action => (Action: action,
					Module: ModuleTypes.GetModuleClass(action.ControllerTypeInfo.AsType(), typeof(ModuleController<>))))
				.Where(pair => pair.Module != null)
				.GroupBy(pair => pair.Module)
				.ToDictionary(group => group.Key, group => new ModuleControllerInfo(group.First().Action.ControllerName,
					group.Select(pair => GetHttpMethods(pair.Action)).ToList()));
		}

		/// <summary>
		/// The http methods of the action, or null when it accepts all.
		/// </summary>
		private static IReadOnlyList<string> GetHttpMethods(ControllerActionDescriptor action)
		{
			return action.ActionConstraints?.OfType<HttpMethodActionConstraint>().FirstOrDefault()?.HttpMethods.ToList();
		}

		private sealed class ModuleControllerInfo
		{
			private readonly IReadOnlyList<IReadOnlyList<string>> _httpMethodsPerAction;

			public ModuleControllerInfo(string name, IReadOnlyList<IReadOnlyList<string>> httpMethodsPerAction)
			{
				Name = name;
				this._httpMethodsPerAction = httpMethodsPerAction;
			}

			public string Name { get; }

			public bool Accepts(string httpMethod)
			{
				return this._httpMethodsPerAction.Any(methods =>
					methods == null || methods.Contains(httpMethod, StringComparer.OrdinalIgnoreCase));
			}
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
