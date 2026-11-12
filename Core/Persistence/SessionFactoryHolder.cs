using System.Reflection;
using NHibernate;
using NHibernate.Cfg;

namespace Cuyahoga.Core.Persistence
{
	/// <summary>
	/// Holds the NHibernate configuration and the current session factory. The session factory is rebuilt
	/// when modules add their mappings.
	/// </summary>
	public class SessionFactoryHolder
	{
		private readonly object _syncRoot = new object();
		private readonly Configuration _configuration;
		private volatile ISessionFactory _sessionFactory;

		public SessionFactoryHolder(Configuration configuration)
		{
			this._configuration = configuration;
			this._sessionFactory = configuration.BuildSessionFactory();
		}

		public Configuration Configuration
		{
			get { return this._configuration; }
		}

		/// <summary>
		/// The current session factory. Sessions that are already open keep using the factory they came from.
		/// </summary>
		public ISessionFactory SessionFactory
		{
			get { return this._sessionFactory; }
			set { this._sessionFactory = value; }
		}

		/// <summary>
		/// Adds the mappings of an assembly and rebuilds the session factory.
		/// </summary>
		public void AddAssembly(Assembly assembly)
		{
			lock (this._syncRoot)
			{
				this._configuration.AddAssembly(assembly);
				this._sessionFactory = this._configuration.BuildSessionFactory();
			}
		}
	}
}
