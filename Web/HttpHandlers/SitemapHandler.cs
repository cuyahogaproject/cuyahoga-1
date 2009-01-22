using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Web;
using System.Web.SessionState;
using System.Xml;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service;
using Cuyahoga.Web.Util;
using log4net;

namespace Cuyahoga.Web.HttpHandlers
{
    /// <summary>
    /// This class handles all sitemap file requests for Cuyahoga.
    /// Remember to map sitemap.xml to asp.net in IIS settings
    /// </summary>
    public class SitemapHandler : IHttpHandler, IRequiresSessionState
    {
        private static readonly ILog log = LogManager.GetLogger(typeof (PageHandler));
        private string _baseSiteURL = "";

        private double _defaultPagePriority;
        private string _defaultPageUpdateFrequency = "";
        private string _sitePageTypes = "";
        private bool _subPathsInMap = true;

        #region Public Properties

        public double DefaultPagePriority
        {
            get { return _defaultPagePriority; }
            set { _defaultPagePriority = value; }
        }

        public ChangeFrequencies DefaultPageUpdateFrequency
        {
            get { return SitePage.ConvertChangeFrequencyToEnum(_defaultPageUpdateFrequency); }
            set { _defaultPageUpdateFrequency = SitePage.ConvertChangeFrequencyToString(value); }
        }

        public string SitePageTypes
        {
            get { return _sitePageTypes; }
            set { _sitePageTypes = value; }
        }

        public bool SubPathsInMap
        {
            get { return _subPathsInMap; }
            set { _subPathsInMap = value; }
        }

        #endregion

        #region IHttpHandler Members

        /// <summary>
        /// Process the aspx request. This means (eventually) rewriting the url and registering the page 
        /// in the container.
        /// </summary>
        /// <param name="context"></param>
        public void ProcessRequest(HttpContext context)
        {
            string rawUrl = context.Request.RawUrl;
            log.Info("Starting request for " + rawUrl);
            DateTime startTime = DateTime.Now;

            HttpRequest request = context.Request;
            HttpResponse response = context.Response;
            response.ClearHeaders();
            response.ClearContent();
            response.ContentType = "text/xml";
            response.Write(GetGoogleSiteMap(request.Url.ToString()));

            // Log duration
            TimeSpan duration = DateTime.Now - startTime;
            log.Info(String.Format("Request finshed. Total duration: {0} ms.", duration.Milliseconds));
        }

        /// <summary>
        /// 
        /// </summary>
        public bool IsReusable
        {
            get { return true; }
        }

        #endregion

        /// <summary>
        /// Initialise sets the initial values of the Provider based on the information contained in the config file.
        /// </summary>
        /// <param name="name">The name of the Provider</param>
        /// <param name="config">The config section of the config file.</param>
        public void Initialize(string name, NameValueCollection config)
        {
            if (config == null)
                throw new ArgumentNullException("config");

            //base.Initialize( name, config );

            if (config["defaultPagePriority"] != null)
            {
                //set the default page priority
                _defaultPagePriority = Convert.ToDouble(config["defaultPagePriorty"]);
            }

            if (config["defaultPageUpdateFrequency"] != null)
            {
                //set the default page update frequency
                _defaultPageUpdateFrequency = config["defaultPageUpdateFrequency"];
            }
            if (config["sitePageTypes"] != null)
            {
                //set the site page types
                _sitePageTypes = config["sitePageTypes"];
            }
            if (config["subPathsInMap"] != null)
            {
                _subPathsInMap = Convert.ToBoolean(config["subPathsInMap"]);
            }
        }

        // The Public Members region contains the publicly accessible members of the class

        /// <summary>
        /// The main procedure for the provider.  This procedure, when called, will return a string containing an XML document in the Google Sitemap 0.84 schema.
        /// </summary>
        /// <param name="siteURL">The site URL to index.</param>
        /// <returns>string value containing an XML document.</returns>
        public string GetGoogleSiteMap(string siteURL)
        {
            //strip any resource off the end of the URL to get the base URL
            SetBaseURL(siteURL);
            //instantiate the XML Text Writer for writing the SiteMap document
            StringWriter stringWriter = new StringWriter();
            XmlTextWriter writer = new XmlTextWriter(stringWriter);
            //write out the header
            WriteSiteMapHeader(writer);

            //<?xml version="1.0" encoding="utf-8" ?>
            //<siteMap xmlns="http://schemas.microsoft.com/AspNet/SiteMap-File-1.0" >
            //  <siteMapNode url="~/Default.aspx" title="Home" description="Home Page" 
            //  LastChangeDate="2006-04-26" Priority="0.9" ChangeFrequency="weekly" >
            //    <siteMapNode url="~/Profile.aspx" title="Profile" description="Company"
            //    LastChangeDate="2007-07-16T19:20:30+01:00" Priority="0.4"/>
            //    <siteMapNode url="~/Info.aspx" title="Info" description="Info" 
            //    Priority="0.8" LastChangeDate="2007-07-16T19:20:40+01:00" 
            //    ChangeFrequency="yearly"/>     
            //  </siteMapNode>
            //</siteMap>

            //get the list of site pages
            CoreRepository cr = (CoreRepository) HttpContext.Current.Items["CoreRepository"];

            IList nodes = cr.GetAll(typeof (Node));

            //for each page, write out the XML 
            int urlCount = 0;
            // foreach( SitePage sitePage in sitePages )
            foreach (Node node in nodes)
            {
                if( node.Status != ( int ) NodeStatus.Offline
                    && !node.IsExternalLink
                    && node.AnonymousViewAllowed)
                {
                    SitePage sitePage = new SitePage();
                    sitePage.ChangeFreq = DefaultPageUpdateFrequency;
                    sitePage.LastMod = node.UpdateTimestamp;
                    sitePage.Loc = UrlHelper.GetUrlFromNode(node);
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
            //write the footer and close.
            WriteSiteMapFooter(writer);
            writer.Close();
            return stringWriter.ToString();
        }

        // The Internal Members region contains code which is meant to be used within the provider.

        /// <summary>
        /// Takes the siteURL (which may contain the SiteMaps handler resource) and strips it back to the base site URL.
        /// </summary>
        /// <param name="siteURL"></param>
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

        /// <summary>
        /// The SitePages method is designed to be overriden by deriving provider classes.  This method
        /// should be overriden to provide a new implementation of the way to index all the pages in the site.  This
        /// will be different for different ASP.NET site architectures.  The only requirement is that the method returns
        /// a typed list of SitePage objects representing an instance for every page on the site to go into the Google sitemap.
        /// </summary>
        /// <param name="siteURL">String value representing the Site URL to index</param>
        /// <returns>A Typed List of SitePage objects, one instance per page to be included in the Google Sitemap</returns>
        internal List<SitePage> SitePages(string siteURL)
        {
            //this method should be overriden by an inheriting provider
            //in this (the default provider) it indexes all of the subdirectories of the site and builds a list of pages
            //matching the _sitePageTypes values
            return IteratePages();
        }

        /// <summary>
        /// This method writes out the standard 0.84 schema Google Sitemap header.
        /// </summary>
        /// <param name="writer">An instance of an XMLWriter being used to construct the Sitemap XML document.</param>
        internal void WriteSiteMapHeader(XmlWriter writer)
        {
            //start off the site map
            writer.WriteProcessingInstruction("xml", "version=\"1.0\" encoding=\"UTF-8\"");
            writer.WriteStartElement("urlset");
            writer.WriteAttributeString("xmlns", "http://www.google.com/schemas/sitemap/0.84");
        }

        /// <summary>
        /// This methods writes out the standard 0.84 schema Google Sitemap header.
        /// </summary>
        /// <param name="writer">An instance of an XMLWriter being used to construct the Sitemap XML document.</param>
        internal void WriteSiteMapFooter(XmlWriter writer)
        {
            writer.WriteEndElement();
        }

        /// <summary>
        /// Transforms the SitePage object into the 0.84 schema XML string and writes it into the Sitemap XML document.
        /// </summary>
        /// <param name="writer">An instance of an XMLWriter being used to construct the Sitemap XML document.</param>
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
            writer.WriteElementString("priority", sitePage.Priority.ToString());
            writer.WriteEndElement();
        }

        /// <summary>
        /// Writes in a comment into the XML File if the number of URL's in the Sitemap document has exceeded 10,000.
        /// </summary>
        /// <param name="writer">An instance of an XMLWriter being used to construct the Sitemap XML document.</param>
        internal void WriteURLCountExceeded(XmlWriter writer)
        {
            writer.WriteComment("The URL Count has reached 10,000.  The Google limit for a sitemap is 10,000 entries");
        }

        /// <summary>
        /// IteratePages() is the default builder for the Provider.  It scans the base directory on the server and puts relevant
        /// page types into the List of SitePages in the website.  
        /// </summary>
        /// <returns>A list of SitePage objects representing all the matching, found pages in the website.</returns>
        internal List<SitePage> IteratePages()
        {
            List<SitePage> myPages = new List<SitePage>();
            string sitePath = HttpContext.Current.Server.MapPath(".");

            string[] sitePageTypes;
            sitePageTypes = (_sitePageTypes.Split(',', ';'));
            foreach (string sitePageType in sitePageTypes)
            {
                IEnumerable<string> files = GetFiles(sitePath, "*." + sitePageType);


                foreach (string file in files)
                {
                    string subPath = "";
                    if (Path.GetDirectoryName(file).Length != sitePath.Length)
                    {
                        //a subdirectory in the making somewhere
                        subPath =
                            file.Substring(sitePath.Length + 1, Path.GetDirectoryName(file).Length - sitePath.Length - 1) +
                            "/";
                        subPath = subPath.Replace("\\", "/");
                    }
                    string url = _baseSiteURL + subPath + Path.GetFileName(file);
                    myPages.Add(new SitePage(url, DateTime.Now, DefaultPageUpdateFrequency, DefaultPagePriority));
                }
            }
            return myPages;
        }

        /// <summary>
        /// GetFiles returns the files in a given directory path, for a given string filter.  This works in a recursive pattern.
        /// </summary>
        /// <param name="path">The path to search in.</param>
        /// <param name="filter"></param>
        /// <returns>An IEnumerable list of strings, representing the paths.</returns>
        internal static IEnumerable<string> GetFiles(string path, string filter)
        {
            foreach (string s in Directory.GetFiles(path, filter))
            {
                yield return s;
            }
            foreach (string s in Directory.GetDirectories(path))
            {
                foreach (string s1 in GetFiles(s, filter))
                {
                    yield return s1;
                }
            }
        }
    }

    /// <summary>
    /// A page of site class
    /// </summary>
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

        /// <summary>
        /// This date should be in ISO 8601 format. 
        /// This format allows you to omit the time portion, use YYYY-MM-DD.
        /// </summary>
        /// <param name="dateValue">string date value</param>
        /// <returns>ISO 8601 date format</returns>
        internal static string FormatISODate(string dateValue)
        {
            return FormatISODate(DateTime.Parse(dateValue));
        }

        /// <summary>
        /// This date should be in ISO 8601 format.
        /// This format allows you to omit the time portion, use YYYY-MM-DD.
        /// </summary>
        /// <param name="date">The date.</param>
        /// <returns>ISO 8601 date format</returns>
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