using System.Collections.Specialized;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Cuyahoga.Core.Persistence;
using Cuyahoga.Core.Service;
using Cuyahoga.Core.Util;
using Cuyahoga.Web.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Cuyahoga.Web.Core
{
	/// <summary>
	/// The Windsor container with the Cuyahoga core services, like the one of the WebForms site. Windsor stays because
	/// modules are registered while the site runs (module activation), which the ASP.NET Core container can't do.
	/// ASP.NET Core code gets the core services through <see cref="CuyahogaServiceCollectionExtensions"/>.
	/// </summary>
	public static class CuyahogaContainer
	{
		public static IWindsorContainer Create(CuyahogaAppSettings settings, IHttpContextAccessor httpContextAccessor)
		{
			var container = new WindsorContainer();
			container.AddFacility(new NHibernateFacility(NHibernateFacility.CreateConfiguration(settings)));
			container.Install(new CoreServicesInstaller(settings));
			container.Register(
				Component.For<ISectionUrlProvider>().Instance(new SectionUrlProvider(httpContextAccessor)));
			return container;
		}

		/// <summary>
		/// Reads the Cuyahoga settings from the "Cuyahoga" configuration section (appsettings.json). They have the
		/// same names as the appSettings of the WebForms site without the "Cuyahoga." prefix, and the same
		/// CUYAHOGA_* environment variables override them.
		/// </summary>
		public static CuyahogaAppSettings LoadSettings(IConfiguration configuration)
		{
			var appSettings = new NameValueCollection();
			foreach (IConfigurationSection setting in configuration.GetSection("Cuyahoga").GetChildren())
			{
				appSettings["Cuyahoga." + setting.Key] = setting.Value;
			}
			return CuyahogaAppSettings.Load(appSettings);
		}
	}
}
