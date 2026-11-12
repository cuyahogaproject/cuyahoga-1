using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Cuyahoga.Core.Persistence;
using Cuyahoga.Core.Service;
using Cuyahoga.Core.Util;

namespace Cuyahoga.Web.Components
{
	/// <summary>
	/// The CuyahogaContainer serves as the IoC container for Cuyahoga. Settings come from the appSettings in
	/// Web.config (see <see cref="CuyahogaAppSettings"/>).
	/// </summary>
	public class CuyahogaContainer : WindsorContainer
	{
		public CuyahogaContainer() : this(CuyahogaAppSettings.Current)
		{
		}

		public CuyahogaContainer(CuyahogaAppSettings settings)
		{
			AddFacility(new NHibernateFacility(NHibernateFacility.CreateConfiguration(settings)));
			Install(new CoreServicesInstaller(settings));
			Register(
				Component.For<ModuleLoader>().LifestyleSingleton(),
				Component.For<SessionFactoryHelper>().LifestyleSingleton(),
				// Legacy
				Component.For<CoreRepositoryAdapter>().Named("corerepositoryadapter").LifestyleSingleton());

			Resolve<SessionFactoryHelper>().ConfigureLegacySessionFactory();
		}
	}
}
