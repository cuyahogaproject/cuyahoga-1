using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using Cuyahoga.Core;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service;
using Cuyahoga.Core.Service.SiteStructure;
using Cuyahoga.Core.Util;
using Cuyahoga.Web.Core.Security;
using Cuyahoga.Web.Core.Services;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using NHibernate;

namespace Cuyahoga.Web.Core.PublicSite
{
	/// <summary>
	/// The RSS 2.0 feed of a section whose module is syndicatable ({id}/feed.aspx, with the path info of the module:
	/// {id}/feed.aspx/category/2), the same as Rss.aspx in the WebForms site writes it. Cached with the cache duration of
	/// the section.
	/// </summary>
	public class FeedController : Controller
	{
		private readonly InstallationState _installationState;
		private readonly ModuleActivation _moduleActivation;
		private readonly ISectionService _sectionService;
		private readonly ModuleLoader _moduleLoader;
		private readonly IMemoryCache _cache;

		public FeedController(InstallationState installationState, ModuleActivation moduleActivation,
			ISectionService sectionService, ModuleLoader moduleLoader, IMemoryCache cache)
		{
			this._installationState = installationState;
			this._moduleActivation = moduleActivation;
			this._sectionService = sectionService;
			this._moduleLoader = moduleLoader;
			this._cache = cache;
		}

		[HttpGet]
		[HttpHead]
		[Route("{sectionId:int}/feed.aspx/{**pathInfo}")]
		public async Task<IActionResult> Index(int sectionId, string pathInfo)
		{
			if (!this._installationState.IsInstalled())
			{
				return NotFound();
			}
			this._moduleActivation.ActivateModules();
			Section section = GetSection(sectionId);
			if (section == null)
			{
				return NotFound();
			}
			// The WebForms site gave the feed of any section to anyone.
			await HttpContext.AuthenticateAsync(LegacyAuthenticationHandler.SchemeName);
			if (!section.ViewAllowed(LegacyAuthenticationHandler.GetUser(HttpContext)))
			{
				return StatusCode(StatusCodes.Status403Forbidden);
			}

			pathInfo = String.IsNullOrEmpty(pathInfo) ? String.Empty : "/" + pathInfo;
			string cacheKey = "Cuyahoga.Rss." + sectionId.ToString(CultureInfo.InvariantCulture) + pathInfo;
			if (!this._cache.TryGetValue(cacheKey, out string content))
			{
				ModuleBase module = this._moduleLoader.GetModuleFromSection(section);
				if (module is not ISyndicatable syndicatable)
				{
					return NotFound();
				}
				module.ModulePathInfo = pathInfo;
				content = WriteFeed(syndicatable.GetRssFeed(), section, pathInfo);
				if (section.CacheDuration > 0)
				{
					this._cache.Set(cacheKey, content, DateTimeOffset.Now.AddSeconds(section.CacheDuration));
				}
			}
			return Content(content, "text/xml; charset=utf-8");
		}

		private Section GetSection(int sectionId)
		{
			Section section = this._sectionService.GetSectionById(sectionId);
			try
			{
				// GetSectionById returns a proxy that is loaded on first use.
				NHibernateUtil.Initialize(section);
				return section;
			}
			catch (ObjectNotFoundException)
			{
				return null;
			}
		}

		/// <summary>
		/// The XML of the feed, like Rss.aspx writes it.
		/// </summary>
		private string WriteFeed(RssChannel channel, Section section, string pathInfo)
		{
			string siteUrl = Request.Scheme + "://" + Request.Host + Request.PathBase;
			using var stringWriter = new StringWriter(CultureInfo.InvariantCulture);
			using (var writer = new XmlTextWriter(stringWriter))
			{
				writer.Formatting = Formatting.Indented;
				writer.WriteStartElement("rss");
				writer.WriteAttributeString("version", "2.0");
				writer.WriteAttributeString("xmlns:dc", "http://purl.org/dc/elements/1.1/");
				writer.WriteStartElement("channel");
				writer.WriteElementString("title", channel.Title);
				writer.WriteElementString("link",
					Request.Scheme + "://" + Request.Host + PublicUrls.GetUrlFromSection(Request, section) + pathInfo);
				writer.WriteElementString("description", channel.Description);
				writer.WriteElementString("language", channel.Language);
				writer.WriteElementString("pubDate", channel.PubDate.ToUniversalTime().ToString("r"));
				writer.WriteElementString("lastBuildDate", channel.LastBuildDate.ToUniversalTime().ToString("r"));
				writer.WriteElementString("generator", channel.Generator);
				writer.WriteElementString("ttl", channel.Ttl.ToString(CultureInfo.CurrentCulture));

				// Relative links in the descriptions become absolute.
				var relativeUrl = new Regex("=[\"']" + Regex.Escape(PublicUrls.GetApplicationPath(Request)),
					RegexOptions.Singleline | RegexOptions.CultureInvariant);
				foreach (RssItem item in channel.RssItems)
				{
					string description = item.Description != null
						? relativeUrl.Replace(item.Description, "=\"" + siteUrl + "/")
						: null;
					writer.WriteStartElement("item");
					writer.WriteElementString("title", item.Title);
					writer.WriteElementString("link", item.Link);
					writer.WriteElementString("description", description);
					writer.WriteElementString("dc:creator", item.Author);
					writer.WriteElementString("pubDate", item.PubDate.ToUniversalTime().ToString("r"));
					writer.WriteElementString("category", item.Category);
					writer.WriteEndElement();
				}
				writer.WriteEndElement();
				writer.WriteEndElement();
			}
			return stringWriter.ToString();
		}
	}
}
