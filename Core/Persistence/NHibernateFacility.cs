using System;
using Castle.Core.Configuration;
using Castle.MicroKernel.Facilities;
using Castle.MicroKernel.Registration;
using NHibernate;
using NHibernate.Cfg;

namespace Cuyahoga.Core.Persistence
{
	/// <summary>
	/// Registers the NHibernate configuration, session factory, session manager and transaction support.
	/// Configured in Config/facilities.config:
	/// <code>
	/// &lt;facility id="nhibernate" type="Cuyahoga.Core.Persistence.NHibernateFacility, Cuyahoga.Core"&gt;
	///   &lt;factory id="nhibernate.factory"&gt;
	///     &lt;settings&gt;&lt;item key="dialect"&gt;...&lt;/item&gt;&lt;/settings&gt;
	///     &lt;assemblies&gt;&lt;assembly&gt;Cuyahoga.Core&lt;/assembly&gt;&lt;/assemblies&gt;
	///   &lt;/factory&gt;
	/// &lt;/facility&gt;
	/// </code>
	/// </summary>
	public class NHibernateFacility : AbstractFacility
	{
		/// <summary>
		/// The component name of the session factory (some modules resolve it by name).
		/// </summary>
		public const string SessionFactoryKey = "nhibernate.factory";

		protected override void Init()
		{
			SessionFactoryHolder sessionFactoryHolder = new SessionFactoryHolder(BuildConfiguration());

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

		private Configuration BuildConfiguration()
		{
			IConfiguration factoryConfig = FacilityConfig != null ? FacilityConfig.Children["factory"] : null;
			if (factoryConfig == null)
			{
				throw new FacilityException("The NHibernate facility requires a <factory> element with the NHibernate settings.");
			}

			Configuration configuration = new Configuration();
			IConfiguration settings = factoryConfig.Children["settings"];
			if (settings != null)
			{
				foreach (IConfiguration item in settings.Children)
				{
					configuration.SetProperty(item.Attributes["key"], item.Value);
				}
			}
			IConfiguration assemblies = factoryConfig.Children["assemblies"];
			if (assemblies != null)
			{
				foreach (IConfiguration assembly in assemblies.Children)
				{
					configuration.AddAssembly(assembly.Value);
				}
			}
			return configuration;
		}
	}
}
