using System;
using System.Collections.Specialized;
using System.Configuration;
using System.Globalization;

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

		/// <summary>Virtual path of the templates directory.</summary>
		public string TemplateDir { get; set; }
		/// <summary>The user name of the administrator account that can't be deleted or lose its administrator role.</summary>
		public string SuperUser { get; set; }
		/// <summary>The name of the administrator role.</summary>
		public string AdministratorRole { get; set; }
		/// <summary>Redirect to the login page when access is denied (customErrors must be enabled).</summary>
		public bool RedirectToLoginWhenAccessDenied { get; set; }
		/// <summary>Virtual path of the search index directory.</summary>
		public string SearchIndexDir { get; set; }
		/// <summary>Update the search index immediately when content changes.</summary>
		public bool InstantIndexing { get; set; }
		/// <summary>Default priority of pages in sitemap.xml (0.0 - 1.0).</summary>
		public double SitemapDefaultPagePriority { get; set; }
		/// <summary>Default update frequency of pages in sitemap.xml (always, hourly, daily, weekly, monthly, yearly, never).</summary>
		public string SitemapDefaultPageUpdateFrequency { get; set; }

		private static readonly Lazy<CuyahogaAppSettings> current = new Lazy<CuyahogaAppSettings>(() => Load());

		/// <summary>
		/// The settings of the application, read once from the appSettings of Web.config.
		/// </summary>
		public static CuyahogaAppSettings Current
		{
			get { return current.Value; }
		}

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
			settings.TemplateDir = Get(appSettings, "Cuyahoga.TemplateDir") ?? "~/Templates";
			settings.SuperUser = Get(appSettings, "Cuyahoga.SuperUser") ?? "admin";
			settings.AdministratorRole = Get(appSettings, "Cuyahoga.AdministratorRole") ?? "Administrator";
			settings.RedirectToLoginWhenAccessDenied = GetBoolean(appSettings, "Cuyahoga.RedirectToLoginWhenAccessDenied", true);
			settings.SearchIndexDir = Get(appSettings, "Cuyahoga.SearchIndexDir") ?? "~/index";
			settings.InstantIndexing = GetBoolean(appSettings, "Cuyahoga.InstantIndexing", true);
			string priority = Get(appSettings, "Cuyahoga.SitemapDefaultPagePriority");
			settings.SitemapDefaultPagePriority = priority != null ? Double.Parse(priority, CultureInfo.InvariantCulture) : 0.5;
			if (settings.SitemapDefaultPagePriority < 0.0 || settings.SitemapDefaultPagePriority > 1.0)
			{
				throw new ConfigurationErrorsException("The appSetting 'Cuyahoga.SitemapDefaultPagePriority' must be between 0.0 and 1.0.");
			}
			settings.SitemapDefaultPageUpdateFrequency = Get(appSettings, "Cuyahoga.SitemapDefaultPageUpdateFrequency") ?? "daily";
			return settings;
		}

		private static bool GetBoolean(NameValueCollection appSettings, string key, bool defaultValue)
		{
			string value = Get(appSettings, key);
			return value != null ? Boolean.Parse(value) : defaultValue;
		}

		/// <summary>Returns the trimmed value, or null when the setting is missing or empty.</summary>
		private static string Get(NameValueCollection appSettings, string key)
		{
			string value = appSettings[key];
			return String.IsNullOrWhiteSpace(value) ? null : value.Trim();
		}
	}
}
