using System.Globalization;
using System.Text.RegularExpressions;

namespace Cuyahoga.Web.Mvc
{
	/// <summary>
	/// Where the pager puts the page number in the url.
	/// </summary>
	public enum PagerUrlMode
	{
		/// <summary>
		/// In the path info: .../page/2.
		/// </summary>
		PathInfo,
		/// <summary>
		/// At the end of the query string: ...?q=text&amp;page=2.
		/// </summary>
		QueryString
	}

	/// <summary>
	/// Paging with the page number in the url (.../page/2 or ...?page=2), like the Pager server control of the WebForms
	/// site (which also reads the page number from these urls), with the same links: &lt;&lt; &lt; ... 1 2 3 ... &gt;
	/// &gt;&gt;.
	/// </summary>
	public class UrlPager
	{
		private static readonly Regex PathInfoPageNumber = new Regex(@"\/page\/(\d+)$", RegexOptions.CultureInvariant);
		private static readonly Regex QueryStringPageNumber = new Regex(@"(\?|\&)page=(\d+)$", RegexOptions.CultureInvariant);

		/// <summary>
		/// The maximum number of page numbers that the pager shows.
		/// </summary>
		public const int MaxDisplayPages = 10;

		private readonly string _pageUrl;
		private readonly PagerUrlMode _mode;

		/// <param name="requestUrl">The path and query of the request (url encoded, with the path base).</param>
		/// <param name="pageUrl">The url of the pages (the section url). When the request url contains it, the request
		/// url is the url of the pages (with path info and query string), like the Pager control does.</param>
		/// <param name="itemCount">The number of items to page.</param>
		/// <param name="pageSize">The number of items per page. 0 shows all items on one page.</param>
		/// <param name="hideWhenOnePage">Don't show the page number when there is only one page.</param>
		/// <param name="mode">Where the page number is in the url.</param>
		public UrlPager(string requestUrl, string pageUrl, int itemCount, int pageSize, bool hideWhenOnePage,
			PagerUrlMode mode = PagerUrlMode.PathInfo)
		{
			this._pageUrl = requestUrl.Contains(pageUrl, StringComparison.Ordinal) ? requestUrl : pageUrl;
			this._mode = mode;
			PageSize = pageSize;
			HideWhenOnePage = hideWhenOnePage;
			PageCount = pageSize > 0 && itemCount > 0 ? (itemCount + pageSize - 1) / pageSize : 1;
			Match match = PageNumber.Match(this._pageUrl);
			if (match.Success && Int32.TryParse(match.Groups[match.Groups.Count - 1].Value, NumberStyles.None, CultureInfo.InvariantCulture,
				out int pageNumber) && pageNumber > 0)
			{
				CurrentPageIndex = pageNumber - 1;
			}
		}

		public int PageSize { get; }

		public bool HideWhenOnePage { get; }

		public int PageCount { get; }

		/// <summary>
		/// The page to show (from the url), starting at 0. It can be past the last page.
		/// </summary>
		public int CurrentPageIndex { get; }

		private Regex PageNumber => this._mode == PagerUrlMode.PathInfo ? PathInfoPageNumber : QueryStringPageNumber;

		private bool IsPagingEnabled => PageSize > 0;

		private bool IsFirstPage => !IsPagingEnabled || CurrentPageIndex == 0;

		private bool IsLastPage => !IsPagingEnabled || CurrentPageIndex == PageCount - 1;

		/// <summary>
		/// The items of the current page.
		/// </summary>
		public IEnumerable<T> GetPage<T>(IEnumerable<T> items)
		{
			return IsPagingEnabled ? items.Skip(CurrentPageIndex * PageSize).Take(PageSize) : items;
		}

		/// <summary>
		/// The url of a page.
		/// </summary>
		/// <param name="pageIndex">The page, starting at 0.</param>
		public string GetPageUrl(int pageIndex)
		{
			string url = PageNumber.Replace(this._pageUrl, String.Empty);
			string number = (pageIndex + 1).ToString(CultureInfo.InvariantCulture);
			if (this._mode == PagerUrlMode.PathInfo)
			{
				return url + "/page/" + number;
			}
			return url + (url.Contains('?') ? "&page=" : "?page=") + number;
		}

		/// <summary>
		/// The links of the pager, in order. The current page has no url.
		/// </summary>
		public IReadOnlyList<PagerLink> GetLinks()
		{
			var links = new List<PagerLink>();
			int currentPageGroupIndex = CurrentPageIndex / MaxDisplayPages;
			int totalPageGroups = (PageCount + MaxDisplayPages - 1) / MaxDisplayPages;

			if (!IsFirstPage)
			{
				links.Add(Link("<<", 0));
				links.Add(Link("<", CurrentPageIndex - 1));
			}
			if (currentPageGroupIndex > 0)
			{
				links.Add(Link("...", (currentPageGroupIndex - 1) * MaxDisplayPages));
			}

			// The page numbers of the current group (the last group shows one more, like the Pager control).
			int beginPageIndex = currentPageGroupIndex * MaxDisplayPages;
			int endPageIndex = beginPageIndex + MaxDisplayPages - 1;
			if (PageCount <= endPageIndex)
			{
				endPageIndex = PageCount - 1;
				if (endPageIndex - MaxDisplayPages >= 0)
				{
					beginPageIndex = endPageIndex - MaxDisplayPages;
				}
			}
			for (int i = beginPageIndex; i <= endPageIndex; i++)
			{
				string number = (i + 1).ToString(CultureInfo.InvariantCulture);
				if (i != CurrentPageIndex)
				{
					links.Add(Link(number, i));
				}
				else if (!(PageCount == 1 && HideWhenOnePage))
				{
					links.Add(new PagerLink(number, null));
				}
			}

			if (currentPageGroupIndex < totalPageGroups - 1)
			{
				links.Add(Link("...", (currentPageGroupIndex + 1) * MaxDisplayPages));
			}
			if (!IsLastPage)
			{
				links.Add(Link(">", CurrentPageIndex + 1));
				links.Add(Link(">>", PageCount - 1));
			}
			return links;
		}

		private PagerLink Link(string text, int pageIndex)
		{
			return new PagerLink(text, GetPageUrl(pageIndex));
		}
	}

	/// <summary>
	/// A link of the pager.
	/// </summary>
	/// <param name="Text">The text of the link.</param>
	/// <param name="Url">The url of the page, or null for the current page.</param>
	public record PagerLink(string Text, string Url);
}
