using Castle.Windsor;
using Cuyahoga.Core.Persistence;
using NHibernate;

namespace Cuyahoga.Web.Core.PublicSite
{
	/// <summary>
	/// Is the Cuyahoga database installed? Until it is, the WebForms site handles every public page, because it runs
	/// the installer (Install/Install.aspx).
	/// </summary>
	public class InstallationState
	{
		private readonly IWindsorContainer _container;
		private readonly ILogger<InstallationState> _logger;
		private volatile bool _isInstalled;

		public InstallationState(IWindsorContainer container, ILogger<InstallationState> logger)
		{
			this._container = container;
			this._logger = logger;
		}

		/// <summary>
		/// True when the database has the version record of Cuyahoga.Core. Once true, it isn't checked again.
		/// </summary>
		public bool IsInstalled()
		{
			if (!this._isInstalled)
			{
				this._isInstalled = HasCoreVersion();
			}
			return this._isInstalled;
		}

		private bool HasCoreVersion()
		{
			try
			{
				// A separate session: a failing query must not affect the session of the request.
				using IStatelessSession session = this._container.Resolve<SessionFactoryHolder>().SessionFactory
					.OpenStatelessSession();
				object count = session
					.CreateSQLQuery("SELECT COUNT(*) FROM cuyahoga_version WHERE assembly = 'Cuyahoga.Core'")
					.UniqueResult();
				return Convert.ToInt32(count) > 0;
			}
			catch (Exception ex)
			{
				this._logger.LogInformation(ex, "The Cuyahoga database isn't installed (yet).");
				return false;
			}
		}
	}
}
