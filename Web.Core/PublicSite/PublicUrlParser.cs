using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Primitives;

namespace Cuyahoga.Web.Core.PublicSite
{
	/// <summary>
	/// What a public page url asks for. The WebForms site rewrites these urls to Default.aspx with query string
	/// parameters (UrlMappings in Web.config).
	/// </summary>
	/// <param name="NodeId">{id}/view.aspx or Default.aspx?NodeId={id}.</param>
	/// <param name="SectionId">{id}/section.aspx or Default.aspx?SectionId={id}.</param>
	/// <param name="ShortDescription">{shortdescription}.aspx or Default.aspx?ShortDescription={shortdescription}.</param>
	/// <param name="PathInfo">The path after .aspx (/12/section.aspx/archive -> /archive), or an empty string.</param>
	public record PageRequest(int? NodeId, int? SectionId, string ShortDescription, string PathInfo);

	/// <summary>
	/// Turns a url into a <see cref="PageRequest"/> the way the url mappings of the WebForms site do, or returns null
	/// when the url isn't a public page or when the WebForms site would handle it in its own (sometimes broken) way, so
	/// the ASP.NET Core site leaves the url to the WebForms site.
	/// </summary>
	public static class PublicUrlParser
	{
		private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

		// The mappings of the WebForms site, in their order. They are matched anywhere in the path and query.
		// Not rewritten (Default.aspx is the page engine itself).
		private static readonly Regex NotRewritten = new Regex(
			@"/(Default.aspx|Login.aspx|Rss.aspx|Error.aspx|Profile.aspx?|Modules?|Admin?|Support?|Install?)", Options);
		private static readonly Regex View = new Regex(@"/(\d+)\/view.aspx", Options);
		private static readonly Regex Section = new Regex(@"/(\d+)\/section.aspx", Options);
		private static readonly Regex Feed = new Regex(@"/(\d+)\/feed.aspx", Options);
		private static readonly Regex ShortDescription = new Regex(@"/([\w|\/|-]+).aspx", Options);

		// The forms of these urls that the WebForms site rewrites correctly.
		private static readonly Regex DefaultPageUrl = new Regex(@"^/Default\.aspx(?<pathInfo>[\w|/]*)$", Options);
		private static readonly Regex ViewUrl = new Regex(@"^/(?<id>\d+)/view\.aspx(?<pathInfo>[\w|/]*)$", Options);
		private static readonly Regex SectionUrl = new Regex(@"^/(?<id>\d+)/section\.aspx(?<pathInfo>[\w|/]*)$", Options);
		private static readonly Regex ShortDescriptionUrl =
			new Regex(@"^/(?<shortDescription>[\w|/-]+)\.aspx(?<pathInfo>[\w|/]*)$", Options);

		/// <param name="path">The path of the url, without the path base of the site (starts with /).</param>
		/// <param name="queryString">The query string, including the '?', or an empty string.</param>
		/// <param name="query">The parsed query string.</param>
		public static PageRequest Parse(string path, string queryString, IEnumerable<KeyValuePair<string, StringValues>> query)
		{
			if (String.IsNullOrEmpty(path) || path == "/")
			{
				// The default document of the site (Default.aspx).
				return FromQuery(query, null, null, null, String.Empty);
			}

			string pathAndQuery = path + queryString;
			Match match;
			if (NotRewritten.IsMatch(pathAndQuery))
			{
				match = DefaultPageUrl.Match(path);
				return match.Success ? FromQuery(query, null, null, null, match.Groups["pathInfo"].Value) : null;
			}
			if (View.IsMatch(pathAndQuery))
			{
				match = ViewUrl.Match(path);
				return match.Success
					? FromQuery(query, ParseId(match.Groups["id"].Value), null, null, match.Groups["pathInfo"].Value)
					: null;
			}
			if (Section.IsMatch(pathAndQuery))
			{
				match = SectionUrl.Match(path);
				return match.Success
					? FromQuery(query, null, ParseId(match.Groups["id"].Value), null, match.Groups["pathInfo"].Value)
					: null;
			}
			if (Feed.IsMatch(pathAndQuery))
			{
				// RSS feeds (Rss.aspx).
				return null;
			}
			if (ShortDescription.IsMatch(pathAndQuery))
			{
				match = ShortDescriptionUrl.Match(path);
				return match.Success
					? FromQuery(query, null, null, match.Groups["shortDescription"].Value, match.Groups["pathInfo"].Value)
					: null;
			}
			return null;
		}

		/// <summary>
		/// Adds the parameters of the query string to the ones of the path. The WebForms site adds the parameters of
		/// the path to the query string, so a parameter in both places becomes an invalid value there: those urls go to
		/// the WebForms site, like urls with invalid ids.
		/// </summary>
		private static PageRequest FromQuery(IEnumerable<KeyValuePair<string, StringValues>> query, int? nodeId,
			int? sectionId, string shortDescription, string pathInfo)
		{
			bool hasNodeId = nodeId != null;
			bool hasSectionId = sectionId != null;
			bool hasShortDescription = shortDescription != null;
			foreach (KeyValuePair<string, StringValues> parameter in query)
			{
				if (String.Equals(parameter.Key, "NodeId", StringComparison.OrdinalIgnoreCase))
				{
					if (hasNodeId || parameter.Value.Count != 1 || (nodeId = ParseId(parameter.Value[0])) == null)
					{
						return null;
					}
					hasNodeId = true;
				}
				else if (String.Equals(parameter.Key, "SectionId", StringComparison.OrdinalIgnoreCase))
				{
					if (hasSectionId || parameter.Value.Count != 1 || (sectionId = ParseId(parameter.Value[0])) == null)
					{
						return null;
					}
					hasSectionId = true;
				}
				else if (String.Equals(parameter.Key, "ShortDescription", StringComparison.OrdinalIgnoreCase))
				{
					if (hasShortDescription || parameter.Value.Count != 1)
					{
						return null;
					}
					shortDescription = parameter.Value[0];
					hasShortDescription = true;
				}
			}
			return new PageRequest(nodeId, sectionId, shortDescription, pathInfo);
		}

		private static int? ParseId(string value)
		{
			return Int32.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int id) ? id : null;
		}
	}
}
