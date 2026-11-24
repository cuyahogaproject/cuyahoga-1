using Cuyahoga.Modules.RemoteContent.Domain;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Modules.RemoteContent.Mvc.Pages.Modules.RemoteContent
{
	/// <summary>
	/// The feeds of a RemoteContent section, and getting their items now (AdminRemoteContent.aspx in the WebForms site).
	/// </summary>
	public class AdminRemoteContentModel : ModuleAdminPageModel<RemoteContentModule>
	{
		public IReadOnlyList<Feed> Feeds { get; private set; }

		public void OnGet()
		{
			BindFeeds();
		}

		/// <summary>
		/// Gets the items of a feed (Refresh).
		/// </summary>
		public void OnPost([FromForm] int? refreshFeedId)
		{
			Feed feed = refreshFeedId != null
				? Module.GetAllFeeds().Cast<Feed>().FirstOrDefault(candidate => candidate.Id == refreshFeedId)
				: null;
			if (feed != null)
			{
				try
				{
					Module.RefreshFeedContents(feed);
					Module.SaveFeed(feed);
				}
				catch (Exception ex)
				{
					ShowError(ex.Message);
				}
			}
			BindFeeds();
		}

		private void BindFeeds()
		{
			Feeds = Module.GetAllFeeds().Cast<Feed>().ToList();
		}
	}
}
