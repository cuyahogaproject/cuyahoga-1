using System;
using System.Linq;
using System.Reflection;
using Castle.Core.Internal;
using Castle.MicroKernel.Registration;
using NHibernate;
using NHibernate.Cfg;
using Castle.MicroKernel;

namespace Cuyahoga.Core.Service
{
	/// <summary>
	/// Provides utility methods to maintain the NHibernate SessionFactory.
	/// </summary>
	public class SessionFactoryHelper
	{
		private IKernel _kernel;

		/// <summary>
		/// Default constructor.
		/// </summary>
		/// <param name="kernel"></param>
		public SessionFactoryHelper(IKernel kernel)
		{
			this._kernel = kernel;
			this._kernel.AddHandlerSelector(new SessionFactoryHandlerSelector());
		}

		/// <summary>
		/// Add a new assembly to the configuration and build a new SessionFactory.
		/// </summary>
		/// <param name="assembly"></param>
		public void AddAssembly(Assembly assembly)
		{
			Configuration nhConfiguration = this._kernel[typeof(Configuration)] as Configuration;
			nhConfiguration.AddAssembly(assembly);
			ISessionFactory newSessionFactory = nhConfiguration.BuildSessionFactory();
			ReplaceSessionFactory(newSessionFactory);
		}

		/// <summary>
		/// Configure the 'old' Cuyahoga SessionFactory wrapper.
		/// </summary>
		public void ConfigureLegacySessionFactory()
		{
			// TODO: get rid of this solution as soon as possible!
			this._kernel.AddComponent("core.legacysessionfactory", typeof(SessionFactory));
			SessionFactory cuyahogaSessionFactory = this._kernel[typeof(SessionFactory)] as SessionFactory;
			// We can't auto-wire the ISessionFactory via the constructor because it's
			// impossible to remove the old ISessonFactory from the container after a rebuild.
			cuyahogaSessionFactory.ExternalInitialize(this._kernel[typeof(ISessionFactory)] as ISessionFactory);
			cuyahogaSessionFactory.SessionFactoryRebuilt += new EventHandler(cuyahogaSessionFactory_SessionFactoryRebuilt);
		}

		private void cuyahogaSessionFactory_SessionFactoryRebuilt(object sender, EventArgs e)
		{
			ISessionFactory newNhSessionFactory = ((SessionFactory)this._kernel[typeof(SessionFactory)]).GetNHibernateFactory();
			ReplaceSessionFactory(newNhSessionFactory);
		}

		private void ReplaceSessionFactory(ISessionFactory nhSessionFactory)
		{
			// Since Castle 3.0, RemoveComponent is not supported anymore, so we can't just swap our ISessionFactory instance.
			// Release the old one, add the new one and set that one as default. A custom IHandlerSelector SessionFactoryHandlerSelector
			// selects the correct implementation.
			var oldSessionFactory = this._kernel.Resolve<ISessionFactory>();
			this._kernel.ReleaseComponent(oldSessionFactory);
			var newKey = Guid.NewGuid().ToString();
			this._kernel.Register(Component.For<ISessionFactory>().Named(newKey).Instance(nhSessionFactory).IsDefault());
		}
	}

	public class SessionFactoryHandlerSelector : IHandlerSelector
	{
		public bool HasOpinionAbout(string key, Type service)
		{
			return key == "nhibernate.factory" || service == typeof(ISessionFactory);
		}

		public IHandler SelectHandler(string key, Type service, IHandler[] handlers)
		{
			var handlersForService = handlers.Where(h => h.ComponentModel.Name == key || h.ComponentModel.Services.Any(t => t == typeof(ISessionFactory))).ToArray();

			if (handlersForService.Count() > 1)
			{
				// HACK: The default handler should result in one service, but seems not to be the case. Return the last one of the default ones. 
				var defaultHandler =
					handlersForService.LastOrDefault(
						h => h.ComponentModel.ExtendedProperties.Contains(Constants.DefaultComponentForServiceFilter));
				return defaultHandler ?? handlersForService.FirstOrDefault();
			}
			return handlersForService.FirstOrDefault();
		}
	}
}
