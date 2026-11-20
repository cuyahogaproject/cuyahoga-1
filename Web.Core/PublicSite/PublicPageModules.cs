using Castle.Windsor;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service;
using Cuyahoga.Web.Mvc;

namespace Cuyahoga.Web.Core.PublicSite
{
	/// <summary>
	/// The modules of the sections on the public page of the request (scoped): created by the ModuleLoader once per
	/// section, released at the end of the request.
	/// </summary>
	public sealed class PublicPageModules : IPublicPageModules, IDisposable
	{
		private readonly ModuleLoader _moduleLoader;
		private readonly IWindsorContainer _container;
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly Dictionary<int, ModuleBase> _modules = new Dictionary<int, ModuleBase>();

		public PublicPageModules(ModuleLoader moduleLoader, IWindsorContainer container, IHttpContextAccessor httpContextAccessor)
		{
			this._moduleLoader = moduleLoader;
			this._container = container;
			this._httpContextAccessor = httpContextAccessor;
		}

		public ModuleBase GetModule(Section section)
		{
			if (!this._modules.TryGetValue(section.Id, out ModuleBase module))
			{
				module = this._moduleLoader.GetModuleFromSection(section);
				PublicPage page = this._httpContextAccessor.HttpContext?.GetPublicPage();
				// The path info of the url is for the module of the active section only.
				if (module != null && page != null && page.ModulePathInfo.Length > 0 && page.ActiveSection?.Id == section.Id)
				{
					module.ModulePathInfo = page.ModulePathInfo;
				}
				this._modules.Add(section.Id, module);
			}
			return module;
		}

		public void Dispose()
		{
			foreach (ModuleBase module in this._modules.Values)
			{
				if (module != null)
				{
					this._container.Release(module);
				}
			}
			this._modules.Clear();
		}
	}
}
