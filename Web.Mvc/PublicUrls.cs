using System.Globalization;
using Cuyahoga.Core.Domain;
using Microsoft.AspNetCore.Http;

namespace Cuyahoga.Web.Mvc
{
	/// <summary>
	/// Urls of the public pages, the same as the WebForms site builds them (UrlHelper).
	/// </summary>
	public static class PublicUrls
	{
		/// <summary>
		/// The path of the site root, with a trailing slash.
		/// </summary>
		public static string GetApplicationPath(HttpRequest request)
		{
			return request.PathBase + "/";
		}

		/// <summary>
		/// The url of a node: the external link, {shortdescription}.aspx (friendly urls) or {id}/view.aspx.
		/// </summary>
		public static string GetUrlFromNode(HttpRequest request, Node node)
		{
			if (node.IsExternalLink)
			{
				return node.LinkUrl;
			}
			if (node.Site.UseFriendlyUrls)
			{
				return GetApplicationPath(request) + node.ShortDescription + ".aspx";
			}
			return GetApplicationPath(request) + node.Id.ToString(CultureInfo.InvariantCulture) + "/view.aspx";
		}

		/// <summary>
		/// The target attribute for a link to the node: only external links have one.
		/// </summary>
		public static string GetLinkTarget(Node node)
		{
			if (!node.IsExternalLink)
			{
				return null;
			}
			return node.LinkTarget switch
			{
				LinkTarget.Self => "_self",
				LinkTarget.New => "_blank",
				_ => null
			};
		}

		public static string GetUrlFromSection(HttpRequest request, Section section)
		{
			return GetApplicationPath(request) + section.Id.ToString(CultureInfo.InvariantCulture) + "/section.aspx";
		}

		/// <summary>
		/// The absolute url of the RSS feed of a section.
		/// </summary>
		public static string GetRssUrlFromSection(HttpRequest request, Section section)
		{
			return request.Scheme + "://" + request.Host + GetApplicationPath(request)
				+ section.Id.ToString(CultureInfo.InvariantCulture) + "/feed.aspx";
		}
	}
}
