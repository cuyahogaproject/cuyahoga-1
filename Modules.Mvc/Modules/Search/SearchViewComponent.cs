using System.Globalization;
using Cuyahoga.Core.Search;
using Cuyahoga.Core.Util;
using Cuyahoga.Modules.Search;
using Cuyahoga.Web.Mvc;
using log4net;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Modules.Mvc.Search
{
	/// <summary>
	/// The full-text search (Search.ascx in the WebForms site): the search box, and the results for the query in the
	/// url ({section url}/Search?q=..., also the url of the search boxes of SearchInput sections). The WebForms site
	/// searched with a postback; here the form sends the query in the url, and the pages of the results are links
	/// (...&amp;page=2, which the Pager control of the WebForms site also understands).
	/// </summary>
	public class SearchViewComponent : ModuleViewComponent<SearchModule>
	{
		private static readonly ILog Logger = LogManager.GetLogger(typeof(SearchViewComponent));

		private readonly CuyahogaAppSettings _settings;

		public SearchViewComponent(CuyahogaAppSettings settings)
		{
			this._settings = settings;
		}

		protected override Task<IViewComponentResult> RenderAsync()
		{
			string sectionUrl = PublicUrls.GetUrlFromSection(Request, Module.Section);
			string query = Request.Query["q"].FirstOrDefault();
			var model = new SearchModel
			{
				ShowInputPanel = Module.ShowInputPanel,
				FormUrl = sectionUrl + "/Search",
				Query = query,
				SearchButtonText = GetText("Search:btnSearch")
			};
			if (!String.IsNullOrWhiteSpace(query))
			{
				AddResults(model, query, sectionUrl);
			}
			return Task.FromResult<IViewComponentResult>(View("~/Modules/Search/Search.cshtml", model));
		}

		private void AddResults(SearchModel model, string query, string sectionUrl)
		{
			SearchResultCollection results;
			try
			{
				results = Module.GetSearchResults(query, ApplicationPaths.MapPath(this._settings.SearchIndexDir),
					PublicPage.User);
			}
			catch (SearchException ex)
			{
				Logger.Error(ex.Message, ex);
				model.InvalidQueryText = GetText("INVALIDQUERY");
				return;
			}
			if (results.Count == 0)
			{
				model.NotFoundText = GetText("NOTFOUND");
				return;
			}

			List<SearchResult> allResults = results.Cast<SearchResult>().ToList();
			var pager = new UrlPager(UriHelper.BuildRelative(Request.PathBase, Request.Path, Request.QueryString),
				sectionUrl, allResults.Count, Module.ResultsPerPage, hideWhenOnePage: false, PagerUrlMode.QueryString);
			// Like the WebForms site: the numbers are of the first page when the page number is past the last page.
			int start = pager.CurrentPageIndex < pager.PageCount ? pager.CurrentPageIndex * Module.ResultsPerPage : 0;
			int end = Math.Min(start + Module.ResultsPerPage, allResults.Count);
			model.Results = new SearchResultsModel
			{
				DisplayingResultsText = GetText("DISPLAYING_RESULTS"),
				OfText = GetText("OF"),
				ForText = GetText("FOR"),
				SecondsText = GetText("SECONDS"),
				From = (start + 1).ToString(CultureInfo.CurrentCulture),
				To = end.ToString(CultureInfo.CurrentCulture),
				Total = allResults.Count.ToString(CultureInfo.CurrentCulture),
				Duration = (results.ExecutionTime * 0.0000001F).ToString(CultureInfo.CurrentCulture),
				Items = pager.GetPage(allResults)
					.Select(result => new SearchResultsModel.Item
					{
						Title = result.Title,
						Summary = result.Summary,
						Path = result.Path,
						DateCreated = TimeZoneUtil.AdjustDateToUserTimeZone(result.DateCreated, PublicPage.User).ToString()
					})
					.ToList(),
				PagerLinks = pager.GetLinks()
			};
		}
	}

	public class SearchModel
	{
		public bool ShowInputPanel { get; init; }
		/// <summary>
		/// The url that the search box sends the query to (with ?q=).
		/// </summary>
		public string FormUrl { get; init; }
		/// <summary>
		/// The query of the url, or null.
		/// </summary>
		public string Query { get; init; }
		public string SearchButtonText { get; init; }
		/// <summary>
		/// The results, or null when there are none (or no query).
		/// </summary>
		public SearchResultsModel Results { get; set; }
		/// <summary>
		/// The text when nothing was found, or null.
		/// </summary>
		public string NotFoundText { get; set; }
		/// <summary>
		/// The text when the query is invalid, or null.
		/// </summary>
		public string InvalidQueryText { get; set; }
	}

	public class SearchResultsModel
	{
		public string DisplayingResultsText { get; init; }
		public string OfText { get; init; }
		public string ForText { get; init; }
		public string SecondsText { get; init; }
		public string From { get; init; }
		public string To { get; init; }
		public string Total { get; init; }
		public string Duration { get; init; }
		public IReadOnlyList<Item> Items { get; init; }
		public IReadOnlyList<PagerLink> PagerLinks { get; init; }

		/// <summary>
		/// A result. All texts are text (the WebForms site didn't encode them).
		/// </summary>
		public class Item
		{
			public string Title { get; init; }
			public string Summary { get; init; }
			public string Path { get; init; }
			public string DateCreated { get; init; }
		}
	}
}
