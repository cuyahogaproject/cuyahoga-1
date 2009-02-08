using System;
using System.Collections;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Web;
using System.Web.SessionState;
using System.Xml;
using Castle.Windsor;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service.SiteStructure;
using Cuyahoga.Core.Util;
using Cuyahoga.Web.Util;
using log4net;

namespace Cuyahoga.Web.HttpHandlers
{
	/// <summary>
	/// This class handles all sitemap file requests for Cuyahoga.
	/// 
	/// Remember to map sitemap.xml to asp.net in IIS settings
	/// <add verb="*" path="sitemap.xml" type="Cuyahoga.Web.HttpHandlers.SitemapHandler"/>
	/// 
	/// Reference: https://www.google.com/webmasters/tools/docs/en/protocol.html
	/// </summary>
	public class SitemapHandler : IHttpHandler, IRequiresSessionState
	{
		private static readonly ILog log = LogManager.GetLogger(typeof (SitemapHandler));
		private string _baseSiteURL = "";
		private IWindsorContainer _container;

		private double _defaultPagePriority = 0.5;
		private string _defaultPageUpdateFrequency = "";
		private ISiteService _siteService;

        #region Public Properties

		/// <summary>
		/// A reference to the Windsor container that can be used as a Service Locator for service classes.
		/// </summary>
		protected IWindsorContainer Container
		{
			get { return _container; }
		}

		/// <summary>
		/// Gets or sets the default page priority.
		/// 
		/// The priority of this URL relative to other URLs on your site. Valid values range from 0.0 to 1.0. 
		/// This value has no effect on your pages compared to pages on other sites, and only lets the search engines know 
		/// which of your pages you deem most important so they can order the crawl of your pages in the way you would most like.
		/// The default priority of a page is 0.5
		/// </summary>
		/// <value>The default page priority.</value>
		public double DefaultPagePriority
		{
			get { return _defaultPagePriority; }
			set { _defaultPagePriority = value; }
		}

		/// <summary>
		/// Gets or sets the default page update frequency.
		/// How frequently the page is likely to change. 
		/// This value provides general information to search engines and may not correlate exactly to how often they crawl the page
		/// </summary>
		/// <value>The default page update frequency.</value>
		public ChangeFrequencies DefaultPageUpdateFrequency
		{
			get { return SitePage.ConvertChangeFrequencyToEnum(_defaultPageUpdateFrequency); }
			set { _defaultPageUpdateFrequency = SitePage.ConvertChangeFrequencyToString(value); }
		}

		#endregion

		#region IHttpHandler Members

		public void ProcessRequest(HttpContext context)
		{
			string rawUrl = context.Request.RawUrl;
			log.Info( "SitemapHandler::Starting request for " + rawUrl );
			DateTime startTime = DateTime.Now;

			try
			{
				if (Config.GetConfiguration()["SitemapHandler:defaultPagePriorty"] != null)
				{
					double tempPagePriority =
						Double.Parse(Config.GetConfiguration()["SitemapHandler:defaultPagePriorty"],
									 new System.Globalization.CultureInfo("en-US"));
					if (tempPagePriority >= 0 && tempPagePriority <= 1.0)
					{
						_defaultPagePriority = tempPagePriority;
					}
					else
					{
						log.ErrorFormat(
							"SitemapHandler:defaultPagePriorty setting has wrong value [{0}]. Valid values range from 0.0 to 1.0..",
							tempPagePriority);
					}
				}

				if (Config.GetConfiguration()["SitemapHandler:defaultPageUpdateFrequency"] != null)
				{
					//set the default page update frequency
					_defaultPageUpdateFrequency =
						Config.GetConfiguration()["SitemapHandler:defaultPageUpdateFrequency"];
				}

				_container = ContainerAccessorUtil.GetContainer();
				_siteService = Container.Resolve<ISiteService>();

				HttpRequest request = context.Request;
				HttpResponse response = context.Response;
				response.ClearHeaders();
				response.ClearContent();
				response.ContentType = "text/xml";
				response.Write(GetGoogleSiteMap(request.Url.ToString()));
			}
			catch(Exception exc)
			{
				log.Error( "SitemapHandler::Error occured during ProcessRequest", exc );
			}

			// Log duration
			TimeSpan duration = DateTime.Now - startTime;
			log.Info( String.Format( "SitemapHandler::Request finshed. Total duration: {0} ms.", duration.Milliseconds ) );
		}

		public bool IsReusable
		{
			get { return true; }
		}

		#endregion


		public string GetGoogleSiteMap(string siteURL)
		{
			//strip any resource off the end of the URL to get the base URL
			SetBaseURL(siteURL);
			//instantiate the XML Text Writer for writing the SiteMap document
			StringWriter stringWriter = new StringWriter();
			XmlTextWriter writer = new XmlTextWriter(stringWriter);
			//write out the header
			WriteSiteMapHeader(writer);

			Site site;
			string siteUrl = UrlHelper.GetSiteUrl();
			SiteAlias currentSiteAlias = this._siteService.GetSiteAliasByUrl(siteUrl);
			if (currentSiteAlias != null)
				site = currentSiteAlias.Site;
			else
				site = _siteService.GetSiteBySiteUrl(siteUrl);

			if (site != null)
			{
				IList nodes = _siteService.GetNodesBySite(site);

				//for each page, write out the XML 
				int urlCount = 0;
				// foreach( SitePage sitePage in sitePages )
				foreach (Node node in nodes)
				{
					if (node.Status == (int) NodeStatus.Online
						&& !node.IsExternalLink
						&& node.AnonymousViewAllowed
						&& node.ShowInNavigation)
					{
						SitePage sitePage = new SitePage();
						sitePage.ChangeFreq = DefaultPageUpdateFrequency;
						sitePage.LastMod = node.UpdateTimestamp;
						sitePage.Loc = UrlHelper.GetSiteUrl() + UrlHelper.GetUrlFromNode(node);
						sitePage.Priority = DefaultPagePriority;
						sitePage.Title = node.Title;

						GetGoogleSiteMapPageEntry(writer, sitePage);
						urlCount++;
						if (urlCount > 10000)
						{
							WriteURLCountExceeded(writer);
							break;
						}
					}
				}
			}

			WriteSiteMapFooter(writer);
			writer.Close();
			return stringWriter.ToString();
		}

		internal void SetBaseURL(string siteURL)
		{
			int httpEnd = siteURL.IndexOf("http://") + "http://".Length;
			if (siteURL.Contains("http://") == false)
				httpEnd = 0;

			int end = siteURL.Length;
			for (int pos = siteURL.Length - 1; pos >= httpEnd; pos--)
			{
				if (siteURL.Substring(pos, 1) == "/")
				{
					end = pos + 1;
					break;
				}
			}
			_baseSiteURL = siteURL.Substring(0, end);
		}

		internal void WriteSiteMapHeader(XmlWriter writer)
		{
			//start off the site map
			writer.WriteProcessingInstruction("xml", "version=\"1.0\" encoding=\"UTF-8\"");
			writer.WriteStartElement("urlset");
			writer.WriteAttributeString("xmlns", "http://www.google.com/schemas/sitemap/0.9");



		}

		internal void WriteSiteMapFooter(XmlWriter writer)
		{
			writer.WriteEndElement();
		}

		internal void GetGoogleSiteMapPageEntry(XmlWriter writer, SitePage sitePage)
		{
			writer.WriteStartElement("url");
			byte[] locBytes;
			locBytes = Encoding.UTF8.GetBytes(sitePage.Loc);
			writer.WriteElementString("loc", Encoding.UTF8.GetString(locBytes));

			locBytes = Encoding.UTF8.GetBytes(sitePage.Title);
			writer.WriteElementString("title", Encoding.UTF8.GetString(locBytes));

			writer.WriteElementString("lastmod", SitePage.FormatISODate(sitePage.LastMod));
			writer.WriteElementString("changefreq", sitePage.ChangeFreq.ToString());
			writer.WriteElementString( "priority", sitePage.Priority.ToString( "N1",new System.Globalization.CultureInfo( "en-US" ) ) );
			writer.WriteEndElement();
		}

		internal void WriteURLCountExceeded(XmlWriter writer)
		{
			writer.WriteComment("The URL Count has reached 10,000.  The Google limit for a sitemap is 10,000 entries");
		}


	}


	public class SitePage
	{
		private ChangeFrequencies _changeFreq;
		private DateTime _lastMod;
		private string _loc;
		private double _priority;
		private string title;

		public SitePage()
		{
		}

		public SitePage(string loc, DateTime lastMod, ChangeFrequencies changeFreq, double priority)
		{
			_loc = loc;
			_lastMod = lastMod;
			_changeFreq = changeFreq;
			_priority = priority;
		}

		public string Loc
		{
			get { return _loc; }
			set { _loc = value; }
		}

		public DateTime LastMod
		{
			get { return _lastMod; }
			set { _lastMod = value; }
		}

		public ChangeFrequencies ChangeFreq
		{
			get { return _changeFreq; }
			set { _changeFreq = value; }
		}

		public double Priority
		{
			get { return _priority; }
			set { _priority = value; }
		}

		public string Title
		{
			get { return title; }
			set { title = value; }
		}

		public void SetLastMod(string value)
		{
			_lastMod = DateTime.Parse(value);
		}

		internal static string FormatISODate(string dateValue)
		{
			return FormatISODate(DateTime.Parse(dateValue));
		}

		internal static string FormatISODate(DateTime date)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(date.Year);
			sb.Append("-");
			if (date.Month < 10)
			{
				sb.Append("0");
			}
			sb.Append(date.Month);
			sb.Append("-");
			if (date.Day < 10)
			{
				sb.Append("0");
			}
			sb.Append(date.Day);

			return sb.ToString();
		}

		internal static string ConvertChangeFrequencyToString(ChangeFrequencies changeFreq)
		{
			string retVal = "";
			switch (changeFreq)
			{
				case ChangeFrequencies.always:
					retVal = "always";
					break;
				case ChangeFrequencies.daily:
					retVal = "daily";
					break;
				case ChangeFrequencies.hourly:
					retVal = "hourly";
					break;
				case ChangeFrequencies.monthly:
					retVal = "monthly";
					break;
				case ChangeFrequencies.never:
					retVal = "never";
					break;
				case ChangeFrequencies.weekly:
					retVal = "weekly";
					break;
				case ChangeFrequencies.yearly:
					retVal = "yearly";
					break;
			}
			return retVal;
		}

		internal static ChangeFrequencies ConvertChangeFrequencyToEnum(string changeFreq)
		{
			ChangeFrequencies retVal = ChangeFrequencies.daily;
			switch (changeFreq)
			{
				case "always":
					retVal = ChangeFrequencies.always;
					break;
				case "daily":
					retVal = ChangeFrequencies.daily;
					break;
				case "hourly":
					retVal = ChangeFrequencies.hourly;
					break;
				case "monthly":
					retVal = ChangeFrequencies.monthly;
					break;
				case "never":
					retVal = ChangeFrequencies.never;
					break;
				case "weekly":
					retVal = ChangeFrequencies.weekly;
					break;
				case "yearly":
					retVal = ChangeFrequencies.yearly;
					break;
			}
			return retVal;
		}
	}

	public enum ChangeFrequencies
	{
		always,
		hourly,
		daily,
		weekly,
		monthly,
		yearly,
		never
	}
}