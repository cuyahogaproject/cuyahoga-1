using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using Cuyahoga.Core.Util;
using Xunit;

namespace Cuyahoga.Core.Tests.Util
{
	// Environment variables override the appSettings, so these tests don't run in parallel with others.
	[Collection(ProcessStateCollection.Name)]
	public class CuyahogaAppSettingsTests
	{
		private static NameValueCollection AppSettings(params string[] keysAndValues)
		{
			var appSettings = new NameValueCollection { { "Cuyahoga.ConnectionString", "Server=test" } };
			for (int i = 0; i < keysAndValues.Length; i += 2)
			{
				appSettings[keysAndValues[i]] = keysAndValues[i + 1];
			}
			return appSettings;
		}

		[Fact]
		public void Defaults()
		{
			var settings = CuyahogaAppSettings.Load(AppSettings());

			Assert.Equal("Server=test", settings.ConnectionString);
			Assert.Equal("NHibernate.Driver.SqlClientDriver", settings.NHibernateDriver);
			Assert.Equal("NHibernate.Dialect.MsSql2005Dialect", settings.NHibernateDialect);
			Assert.Equal("localhost", settings.SmtpHost);
			Assert.Null(settings.SmtpPort);
			Assert.Null(settings.TempDir);
			Assert.Equal("~/Templates", settings.TemplateDir);
			Assert.Equal("admin", settings.SuperUser);
			Assert.Equal("Administrator", settings.AdministratorRole);
			Assert.True(settings.RedirectToLoginWhenAccessDenied);
			Assert.Equal("~/index", settings.SearchIndexDir);
			Assert.True(settings.InstantIndexing);
			Assert.Equal(0.5, settings.SitemapDefaultPagePriority);
			Assert.Equal("daily", settings.SitemapDefaultPageUpdateFrequency);
		}

		[Fact]
		public void Values_from_appSettings()
		{
			var settings = CuyahogaAppSettings.Load(AppSettings(
				"Cuyahoga.SmtpPort", "2525",
				"Cuyahoga.InstantIndexing", "false",
				"Cuyahoga.SitemapDefaultPagePriority", "0.6",
				"Cuyahoga.TempDir", "  C:\\Temp  "));

			Assert.Equal(2525, settings.SmtpPort);
			Assert.False(settings.InstantIndexing);
			Assert.Equal(0.6, settings.SitemapDefaultPagePriority);
			Assert.Equal("C:\\Temp", settings.TempDir);
		}

		[Fact]
		public void Empty_values_mean_the_default()
		{
			var settings = CuyahogaAppSettings.Load(AppSettings("Cuyahoga.SmtpHost", " ", "Cuyahoga.SmtpPort", ""));

			Assert.Equal("localhost", settings.SmtpHost);
			Assert.Null(settings.SmtpPort);
		}

		[Fact]
		public void Connection_string_is_required()
		{
			var exception = Assert.Throws<ConfigurationErrorsException>(() => CuyahogaAppSettings.Load(new NameValueCollection()));
			Assert.Contains("Cuyahoga.ConnectionString", exception.Message);
		}

		[Fact]
		public void Sitemap_priority_must_be_between_0_and_1()
		{
			Assert.Throws<ConfigurationErrorsException>(() =>
				CuyahogaAppSettings.Load(AppSettings("Cuyahoga.SitemapDefaultPagePriority", "1.5")));
		}

		[Fact]
		public void Environment_variable_overrides_the_appSetting()
		{
			Assert.Equal("CUYAHOGA_SMTPHOST", CuyahogaAppSettings.EnvironmentVariableName("Cuyahoga.SmtpHost"));
			Environment.SetEnvironmentVariable("CUYAHOGA_SMTPHOST", "smtp.example.com");
			try
			{
				var settings = CuyahogaAppSettings.Load(AppSettings("Cuyahoga.SmtpHost", "mail.local"));
				Assert.Equal("smtp.example.com", settings.SmtpHost);
			}
			finally
			{
				Environment.SetEnvironmentVariable("CUYAHOGA_SMTPHOST", null);
			}
		}
	}

	public class DictionaryExtensionsTests
	{
		[Fact]
		public void GetValueOrDefault_returns_the_default_for_a_missing_key()
		{
			IDictionary<string, string> settings = new Dictionary<string, string> { { "SHOW_TITLE", "true" } };

			Assert.Equal("true", settings.GetValueOrDefault("SHOW_TITLE"));
			Assert.Null(settings.GetValueOrDefault("MISSING"));
		}
	}
}
