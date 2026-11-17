using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Web.Core.Components
{
	/// <summary>
	/// Page links for a paged list, with the layout of Cuyahoga.ServerControls.Pager in the WebForms site: first,
	/// previous, the previous group of pages, the page numbers (the current one in bold), the next group, next and last.
	/// </summary>
	/// <example>
	/// @await Component.InvokeAsync("Pager", new { id = "p_pgrUsers", pageIndex = Model.PageIndex,
	///     totalPages = Model.TotalPages, pageUrl = (Func&lt;int, string&gt;)Model.PageUrl })
	/// </example>
	public class PagerViewComponent : ViewComponent
	{
		/// <summary>
		/// The number of page links (Cuyahoga.ServerControls.Pager.MaxDisplayPages).
		/// </summary>
		public const int DefaultMaxDisplayPages = 10;

		/// <param name="id">The id of the pager element.</param>
		/// <param name="pageIndex">The zero-based current page.</param>
		/// <param name="totalPages">The number of pages.</param>
		/// <param name="pageUrl">The url of a (zero-based) page.</param>
		/// <param name="maxDisplayPages">The number of page links.</param>
		public IViewComponentResult Invoke(string id, int pageIndex, int totalPages, Func<int, string> pageUrl,
			int maxDisplayPages = DefaultMaxDisplayPages)
		{
			List<PagerItem> items = GetItems(pageIndex, totalPages, maxDisplayPages)
				.Select(item => new PagerItem(item.Text, item.PageIndex.HasValue ? pageUrl(item.PageIndex.Value) : null))
				.ToList();
			return View(new PagerViewModel(id, items));
		}

		/// <summary>
		/// The pager items. The current page has no page index (it isn't a link).
		/// </summary>
		public static List<(string Text, int? PageIndex)> GetItems(int pageIndex, int totalPages, int maxDisplayPages)
		{
			var items = new List<(string Text, int? PageIndex)>();
			bool isFirstPage = pageIndex == 0;
			bool isLastPage = totalPages == 0 || pageIndex == totalPages - 1;
			int currentGroup = pageIndex / maxDisplayPages;
			int totalGroups = (int)Math.Ceiling(totalPages / (double)maxDisplayPages);

			if (!isFirstPage)
			{
				items.Add(("<<", 0));
				items.Add(("<", pageIndex - 1));
			}
			if (currentGroup > 0)
			{
				items.Add(("...", (currentGroup - 1) * maxDisplayPages));
			}
			// Near the end, the WebForms pager shifts the numbers back (and shows one number more).
			int begin = currentGroup * maxDisplayPages;
			int end = begin + maxDisplayPages - 1;
			if (totalPages <= end)
			{
				end = totalPages - 1;
				if (end - maxDisplayPages >= 0)
				{
					begin = end - maxDisplayPages;
				}
			}
			for (int i = begin; i <= end; i++)
			{
				items.Add(((i + 1).ToString(), i == pageIndex ? null : i));
			}
			if (currentGroup < totalGroups - 1)
			{
				items.Add(("...", (currentGroup + 1) * maxDisplayPages));
			}
			if (!isLastPage)
			{
				items.Add((">", pageIndex + 1));
				items.Add((">>", totalPages - 1));
			}
			return items;
		}
	}

	/// <summary>
	/// A pager link; the current page has no url.
	/// </summary>
	public record PagerItem(string Text, string Url);

	public record PagerViewModel(string Id, List<PagerItem> Items);
}
