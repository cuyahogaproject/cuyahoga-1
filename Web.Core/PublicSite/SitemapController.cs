using System.Collections;
using System.Globalization;
using System.Text;
using System.Xml;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service.SiteStructure;
using Cuyahoga.Core.Util;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Web.Core.PublicSite
{
	/// <summary>
	/// The XML sitemap of the site for search engines (sitemap.aspx and sitemap.xml, in any folder), like SitemapHandler
	/// of the WebForms site: the pages that are online, that anyone may view and that show in the navigation.
	/// </summary>
	public class SitemapController : Controller
	{
		private const int MaxUrls = 10000;

		private readonly InstallationState _installationState;
		private readonly IPageLookup _pageLookup;
		private readonly ISiteService _siteService;
		private readonly CuyahogaAppSettings _settings;

		public SitemapController(InstallationState installationState, IPageLookup pageLookup, ISiteService siteService,
			CuyahogaAppSettings settings)
		{
			this._installationState = installationState;
			this._pageLookup = pageLookup;
			this._siteService = siteService;
			this._settings = settings;
		}

		[HttpGet]
		[HttpHead]
		[Route("{**path:regex(^(.*/)?sitemap\\.[[^/]]*$)}")]
		public IActionResult Index()
		{
			if (!this._installationState.IsInstalled())
			{
				return NotFound();
			}
			string hostUrl = Request.Scheme + "://" + Request.Host;
			string siteUrl = hostUrl + Request.PathBase.Value?.ToLowerInvariant();
			Site site = this._pageLookup.GetSiteAliasByUrl(siteUrl)?.Site ?? this._pageLookup.GetSiteBySiteUrl(siteUrl);

			var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), OmitXmlDeclaration = true };
			using var stream = new MemoryStream();
			// The declaration of the WebForms site (XmlWriter writes "utf-8").
			byte[] declaration = Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
			stream.Write(declaration, 0, declaration.Length);
			using (XmlWriter writer = XmlWriter.Create(stream, settings))
			{
				writer.WriteStartElement("urlset", "http://www.google.com/schemas/sitemap/0.9");
				if (site != null)
				{
					int count = 0;
					foreach (Node node in this._siteService.GetNodesBySite(site).Cast<Node>())
					{
						if (node.Status != (int)NodeStatus.Online || node.IsExternalLink || !node.AnonymousViewAllowed
							|| !node.ShowInNavigation)
						{
							continue;
						}
						if (++count > MaxUrls)
						{
							writer.WriteComment("The URL Count has reached 10,000.  The Google limit for a sitemap is 10,000 entries");
							break;
						}
						writer.WriteStartElement("url");
						writer.WriteElementString("loc", hostUrl + PublicUrls.GetUrlFromNode(Request, node));
						writer.WriteElementString("title", node.Title);
						writer.WriteElementString("lastmod", node.UpdateTimestamp.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
						writer.WriteElementString("changefreq", this._settings.SitemapDefaultPageUpdateFrequency);
						writer.WriteElementString("priority",
							this._settings.SitemapDefaultPagePriority.ToString("N1", CultureInfo.InvariantCulture));
						writer.WriteEndElement();
					}
				}
				writer.WriteEndElement();
			}
			return File(stream.ToArray(), "text/xml");
		}
	}
}
