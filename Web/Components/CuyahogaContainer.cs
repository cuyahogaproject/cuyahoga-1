using Castle.Core;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Castle.Windsor.Configuration.Interpreters;
using Cuyahoga.Core.Service;

namespace Cuyahoga.Web.Components
{
	/// <summary>
	/// The CuyahogaContainer serves as the IoC container for Cuyahoga.
	/// </summary>
	public class CuyahogaContainer : WindsorContainer
	{
		/// <summary>
		/// Constructor. The configuration is read from the web.config.
		/// </summary>
		public CuyahogaContainer() : base(new XmlInterpreter())
		{
			RegisterFacilities();
			RegisterServices();
			ConfigureLegacySessionFactory();
		}

		private void RegisterFacilities()
		{
		}

		private void RegisterServices()
		{
			// The core services are registrated via services.config
			
			// Utility services
			Register(Component.For<ModuleLoader>().LifestyleSingleton());
			Register(Component.For<SessionFactoryHelper>().LifestyleSingleton());

			// Legacy
			AddComponent("corerepositoryadapter", typeof(CoreRepositoryAdapter));
		}

		private void ConfigureLegacySessionFactory()
		{
			SessionFactoryHelper sessionFactoryHelper = this.Resolve<SessionFactoryHelper>();
			sessionFactoryHelper.ConfigureLegacySessionFactory();
		}
	}
}
