using Cuyahoga.Core.Util;
using Cuyahoga.Modules.RemoteContent.Domain;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Modules.RemoteContent.Mvc
{
	/// <summary>
	/// The items of the RSS feeds of a section (RemoteContent.ascx in the WebForms site). Feeds come from other sites
	/// and can't be trusted: everything is text (the contents without html) and links only go to safe urls.
	/// </summary>
	public class RemoteContentViewComponent : ModuleViewComponent<RemoteContentModule>
	{
		protected override Task<IViewComponentResult> RenderAsync()
		{
			var model = new RemoteContentModel
			{
				ShowContents = Module.ShowContents,
				ShowDates = Module.ShowDates,
				ShowAuthors = Module.ShowAuthors,
				ShowSources = Module.ShowSources,
				Items = Module.GetAllFeedItems().Cast<FeedItem>()
					.Select(item => new RemoteContentModel.Item
					{
						Url = Text.IsSafeUrl(item.Url) ? item.Url : null,
						Title = item.Title,
						Content = Text.StripHtml(item.Content),
						PubDate = TimeZoneUtil.AdjustDateToUserTimeZone(item.PubDate, PublicPage.User).ToString(),
						Author = item.Author,
						Source = item.Feed.Title
					})
					.ToList()
			};
			return Task.FromResult<IViewComponentResult>(View("~/Modules/RemoteContent/RemoteContent.cshtml", model));
		}
	}

	public class RemoteContentModel
	{
		public bool ShowContents { get; init; }
		public bool ShowDates { get; init; }
		public bool ShowAuthors { get; init; }
		public bool ShowSources { get; init; }
		public IReadOnlyList<Item> Items { get; init; }

		public class Item
		{
			/// <summary>
			/// The link of the item, or null when it isn't safe (the title is then a link without url).
			/// </summary>
			public string Url { get; init; }
			public string Title { get; init; }
			/// <summary>
			/// The contents without html.
			/// </summary>
			public string Content { get; init; }
			public string PubDate { get; init; }
			public string Author { get; init; }
			public string Source { get; init; }
		}
	}
}
