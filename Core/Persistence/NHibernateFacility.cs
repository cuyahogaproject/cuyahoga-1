using Castle.MicroKernel.Facilities;
using Castle.MicroKernel.Registration;
using Cuyahoga.Core.Util;
using NHibernate;
using NHibernate.Cfg;

namespace Cuyahoga.Core.Persistence
{
	/// <summary>
	/// Registers the NHibernate configuration, session factory, session manager and transaction support.
	/// </summary>
	public class NHibernateFacility : AbstractFacility
	{
		/// <summary>
		/// The component name of the session factory (some modules resolve it by name).
		/// </summary>
		public const string SessionFactoryKey = "nhibernate.factory";

		private readonly Configuration _configuration;

		public NHibernateFacility(Configuration configuration)
		{
			this._configuration = configuration;
		}

		/// <summary>
		/// Creates the NHibernate configuration for Cuyahoga with the mappings of Cuyahoga.Core. Modules add their
		/// mappings when they are activated.
		/// </summary>
		public static Configuration CreateConfiguration(CuyahogaAppSettings settings)
		{
			Configuration configuration = new Configuration();
			configuration.SetProperty(Environment.ConnectionProvider, "NHibernate.Connection.DriverConnectionProvider");
			configuration.SetProperty(Environment.ConnectionDriver, settings.NHibernateDriver);
			configuration.SetProperty(Environment.Dialect, settings.NHibernateDialect);
			configuration.SetProperty(Environment.ConnectionString, settings.ConnectionString);
			configuration.SetProperty(Environment.CacheProvider, "NHibernate.Caches.SysCache.SysCacheProvider, NHibernate.Caches.SysCache");
			configuration.SetProperty(Environment.UseQueryCache, "true");
			configuration.AddAssembly(typeof(NHibernateFacility).Assembly);
			return configuration;
		}

		protected override void Init()
		{
			SessionFactoryHolder sessionFactoryHolder = new SessionFactoryHolder(this._configuration);

			Kernel.ComponentModelBuilder.AddContributor(new TransactionalComponentInspector());
			Kernel.Register(
				Component.For<Configuration>().Instance(sessionFactoryHolder.Configuration),
				Component.For<SessionFactoryHolder>().Instance(sessionFactoryHolder),
				// Resolved on every request, because the session factory is replaced when modules add their mappings.
				Component.For<ISessionFactory>().Named(SessionFactoryKey)
					.UsingFactoryMethod(() => sessionFactoryHolder.SessionFactory, true)
					.LifestyleTransient(),
				Component.For<ISessionManager>().ImplementedBy<SessionManager>().LifestyleSingleton(),
				Component.For<TransactionInterceptor>().LifestyleTransient());
		}
	}
}
