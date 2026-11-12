using System;
using System.Collections.Specialized;
using System.Configuration;

namespace Cuyahoga.Core.Util
{
	/// <summary>
	/// The Cuyahoga settings from the appSettings section of Web.config (keys starting with 'Cuyahoga.').
	/// Machine-specific values can be overridden in the optional, git-ignored Web.local.config.
	/// </summary>
	public class CuyahogaAppSettings
	{
		public const string DefaultNHibernateDriver = "NHibernate.Driver.SqlClientDriver";
		public const string DefaultNHibernateDialect = "NHibernate.Dialect.MsSql2005Dialect";

		/// <summary>The database connection string (required).</summary>
		public string ConnectionString { get; set; }
		/// <summary>The NHibernate driver class.</summary>
		public string NHibernateDriver { get; set; }
		/// <summary>The NHibernate dialect class.</summary>
		public string NHibernateDialect { get; set; }

		/// <summary>The SMTP host for outgoing email.</summary>
		public string SmtpHost { get; set; }
		/// <summary>The SMTP port (null: default port 25).</summary>
		public int? SmtpPort { get; set; }
		public string SmtpUsername { get; set; }
		public string SmtpPassword { get; set; }
		/// <summary>The email encoding, for example utf-8 or iso-8859-1 (null: the system default).</summary>
		public string EmailEncoding { get; set; }

		/// <summary>Directory for temporary files of transactional uploads (null: the TEMP directory).</summary>
		public string TempDir { get; set; }

		/// <summary>
		/// Reads the settings from the appSettings of the application configuration.
		/// </summary>
		public static CuyahogaAppSettings Load()
		{
			return Load(ConfigurationManager.AppSettings);
		}

		public static CuyahogaAppSettings Load(NameValueCollection appSettings)
		{
			CuyahogaAppSettings settings = new CuyahogaAppSettings();
			settings.ConnectionString = Get(appSettings, "Cuyahoga.ConnectionString");
			if (settings.ConnectionString == null)
			{
				throw new ConfigurationErrorsException("The appSetting 'Cuyahoga.ConnectionString' is required.");
			}
			settings.NHibernateDriver = Get(appSettings, "Cuyahoga.NHibernateDriver") ?? DefaultNHibernateDriver;
			settings.NHibernateDialect = Get(appSettings, "Cuyahoga.NHibernateDialect") ?? DefaultNHibernateDialect;
			settings.SmtpHost = Get(appSettings, "Cuyahoga.SmtpHost") ?? "localhost";
			string smtpPort = Get(appSettings, "Cuyahoga.SmtpPort");
			settings.SmtpPort = smtpPort != null ? Int32.Parse(smtpPort) : (int?)null;
			settings.SmtpUsername = Get(appSettings, "Cuyahoga.SmtpUsername");
			settings.SmtpPassword = Get(appSettings, "Cuyahoga.SmtpPassword");
			settings.EmailEncoding = Get(appSettings, "Cuyahoga.EmailEncoding");
			settings.TempDir = Get(appSettings, "Cuyahoga.TempDir");
			return settings;
		}

		/// <summary>Returns the trimmed value, or null when the setting is missing or empty.</summary>
		private static string Get(NameValueCollection appSettings, string key)
		{
			string value = appSettings[key];
			return String.IsNullOrWhiteSpace(value) ? null : value.Trim();
		}
	}
}
