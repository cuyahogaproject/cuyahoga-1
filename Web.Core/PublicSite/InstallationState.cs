using System.Reflection;
using Castle.Windsor;
using Cuyahoga.Core.Persistence;
using Cuyahoga.Core.Service;
using Cuyahoga.Core.Util;
using NHibernate;

namespace Cuyahoga.Web.Core.PublicSite
{
	/// <summary>
	/// Is the Cuyahoga database installed, or does it need an upgrade (a newer Cuyahoga.Core than the database)? The
	/// installer (Install/Install.aspx and Upgrade.aspx) takes over until then (InstallerMiddleware).
	/// </summary>
	public class InstallationState
	{
		private readonly IWindsorContainer _container;
		private readonly ILogger<InstallationState> _logger;
		private volatile bool _isInstalled;
		private bool? _needsUpgrade;

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

		/// <summary>
		/// The installed database is older than Cuyahoga.Core. Checked once; the upgrade page resets it.
		/// </summary>
		public bool NeedsUpgrade()
		{
			if (this._needsUpgrade == null)
			{
				this._needsUpgrade = IsInstalled() && CreateCoreInstaller().CanUpgrade;
			}
			return this._needsUpgrade.Value;
		}

		/// <summary>
		/// The database was upgraded (or installed): check again.
		/// </summary>
		public void Reset()
		{
			this._needsUpgrade = null;
		}

		/// <summary>
		/// The installer of the database of Cuyahoga.Core (~/Install/Core).
		/// </summary>
		public static DatabaseInstaller CreateCoreInstaller()
		{
			return new DatabaseInstaller(ApplicationPaths.MapPath("~/Install/Core"), Assembly.Load("Cuyahoga.Core"));
		}

		/// <summary>
		/// The installer of the database of the built-in modules (~/Install/Modules).
		/// </summary>
		public static DatabaseInstaller CreateModulesInstaller()
		{
			return new DatabaseInstaller(ApplicationPaths.MapPath("~/Install/Modules"), Assembly.Load("Cuyahoga.Modules"));
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
