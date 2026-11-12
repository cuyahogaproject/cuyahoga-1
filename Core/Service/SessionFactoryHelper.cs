using System;
using System.Reflection;
using Castle.MicroKernel;
using Cuyahoga.Core.Persistence;

namespace Cuyahoga.Core.Service
{
	/// <summary>
	/// Provides utility methods to maintain the NHibernate SessionFactory.
	/// </summary>
	public class SessionFactoryHelper
	{
		private readonly IKernel _kernel;
		private readonly SessionFactoryHolder _sessionFactoryHolder;

		/// <summary>
		/// Default constructor.
		/// </summary>
		public SessionFactoryHelper(IKernel kernel, SessionFactoryHolder sessionFactoryHolder)
		{
			this._kernel = kernel;
			this._sessionFactoryHolder = sessionFactoryHolder;
		}

		/// <summary>
		/// Add a new assembly to the configuration and build a new SessionFactory.
		/// </summary>
		/// <param name="assembly"></param>
		public void AddAssembly(Assembly assembly)
		{
			this._sessionFactoryHolder.AddAssembly(assembly);
		}

		/// <summary>
		/// Configure the 'old' Cuyahoga SessionFactory wrapper.
		/// </summary>
		public void ConfigureLegacySessionFactory()
		{
			// TODO: get rid of this solution as soon as possible!
			this._kernel.AddComponent("core.legacysessionfactory", typeof(SessionFactory));
			SessionFactory cuyahogaSessionFactory = this._kernel[typeof(SessionFactory)] as SessionFactory;
			cuyahogaSessionFactory.ExternalInitialize(this._sessionFactoryHolder.SessionFactory);
			cuyahogaSessionFactory.SessionFactoryRebuilt += new EventHandler(cuyahogaSessionFactory_SessionFactoryRebuilt);
		}

		private void cuyahogaSessionFactory_SessionFactoryRebuilt(object sender, EventArgs e)
		{
			this._sessionFactoryHolder.SessionFactory = ((SessionFactory)sender).GetNHibernateFactory();
		}
	}
}
