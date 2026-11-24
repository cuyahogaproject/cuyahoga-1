using Cuyahoga.Core.Domain;
using Cuyahoga.Modules.Search;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Modules.Mvc.Search
{
	/// <summary>
	/// A search box (SearchInput.ascx in the WebForms site) that sends the query to the search section it is connected
	/// to ({search section url}/Search?q=...). Without a connection the button does nothing, like in the WebForms site.
	/// </summary>
	public class SearchInputViewComponent : ModuleViewComponent<SearchInputModule>
	{
		protected override Task<IViewComponentResult> RenderAsync()
		{
			Module.Section.Connections.TryGetValue("Search", out Section searchSection);
			var model = new SearchInputModel
			{
				FormUrl = searchSection != null ? PublicUrls.GetUrlFromSection(Request, searchSection) + "/Search" : null,
				SearchButtonText = GetText("SearchInput:btnSearch")
			};
			return Task.FromResult<IViewComponentResult>(View("~/Modules/Search/SearchInput.cshtml", model));
		}
	}

	public class SearchInputModel
	{
		/// <summary>
		/// The url of the connected search section that gets the query (with ?q=), or null.
		/// </summary>
		public string FormUrl { get; init; }
		public string SearchButtonText { get; init; }
	}
}
