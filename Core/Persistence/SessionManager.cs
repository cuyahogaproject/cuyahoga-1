using Castle.DynamicProxy;
using NHibernate;

namespace Cuyahoga.Core.Persistence
{
	/// <summary>
	/// Session-per-request <see cref="ISessionManager"/>. Outside an HTTP request, the session belongs to the
	/// current thread and has to be closed with <see cref="CloseCurrentSession"/>.
	/// </summary>
	public class SessionManager : ISessionManager
	{
		private const string SessionKey = "Cuyahoga.Persistence.Session";
		private static readonly ProxyGenerator ProxyGenerator = new ProxyGenerator();

		private readonly SessionFactoryHolder _sessionFactoryHolder;

		public SessionManager(SessionFactoryHolder sessionFactoryHolder)
		{
			this._sessionFactoryHolder = sessionFactoryHolder;
		}

		public ISession OpenSession()
		{
			SessionEntry entry = ContextStorage.Get(SessionKey) as SessionEntry;
			if (entry == null || ! entry.Session.IsOpen)
			{
				ISession session = this._sessionFactoryHolder.SessionFactory.OpenSession();
				ISession proxy = ProxyGenerator.CreateInterfaceProxyWithTarget<ISession>(session, new KeepOpenInterceptor());
				entry = new SessionEntry(session, proxy);
				ContextStorage.Set(SessionKey, entry);
			}
			return entry.Proxy;
		}

		/// <summary>
		/// Closes the session of the current unit of work, if one was opened.
		/// </summary>
		public static void CloseCurrentSession()
		{
			SessionEntry entry = ContextStorage.Get(SessionKey) as SessionEntry;
			if (entry != null)
			{
				ContextStorage.Set(SessionKey, null);
				entry.Session.Dispose();
			}
		}

		private class SessionEntry
		{
			public readonly ISession Session;
			public readonly ISession Proxy;

			public SessionEntry(ISession session, ISession proxy)
			{
				this.Session = session;
				this.Proxy = proxy;
			}
		}

		/// <summary>
		/// Callers share the session of the unit of work, so they must not close it (the Castle NHibernate
		/// facility behaved the same way).
		/// </summary>
		private class KeepOpenInterceptor : Castle.DynamicProxy.IInterceptor
		{
			public void Intercept(IInvocation invocation)
			{
				string methodName = invocation.Method.Name;
				if ((methodName == "Close" || methodName == "Dispose") && invocation.Arguments.Length == 0)
				{
					return;
				}
				invocation.Proceed();
			}
		}
	}
}
