using System.Globalization;
using Cuyahoga.Modules.RemoteContent.Domain;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Modules.RemoteContent.Mvc.Pages.Modules.RemoteContent
{
	/// <summary>
	/// Add, edit and delete a feed of a RemoteContent section (EditFeed.aspx in the WebForms site). A new feed is
	/// verified (read from its url) before it can be saved.
	/// </summary>
	public class EditFeedModel : ModuleAdminPageModel<RemoteContentModule>
	{
		private Feed _feed;

		[BindProperty(Name = "txtUrl")]
		public string FeedUrl { get; set; }

		[BindProperty(Name = "txtTitle")]
		public string FeedTitle { get; set; }

		[BindProperty(Name = "txtNumberOfItems")]
		public string NumberOfItems { get; set; }

		/// <summary>
		/// The url of the new feed was verified.
		/// </summary>
		[BindProperty]
		public bool Verified { get; set; }

		public string PubDate { get; private set; }

		public string UpdateTimestamp { get; private set; }

		public bool IsExistingFeed { get; private set; }

		public bool CanSave => IsExistingFeed || Verified;

		public bool UrlMissing { get; private set; }

		public bool TitleMissing { get; private set; }

		public bool NumberOfItemsMissing { get; private set; }

		public bool NumberOfItemsInvalid { get; private set; }

		public IActionResult OnGet()
		{
			if (!LoadFeed())
			{
				return NotFound();
			}
			if (IsExistingFeed)
			{
				BindFeed(this._feed);
			}
			return Page();
		}

		public IActionResult OnPost()
		{
			if (!LoadFeed())
			{
				return NotFound();
			}
			if (Request.Form.ContainsKey("btnVerify"))
			{
				Verify();
			}
			else if (Request.Form.ContainsKey("btnDelete") && IsExistingFeed)
			{
				try
				{
					Module.DeleteFeed(this._feed);
					return BackToList();
				}
				catch (Exception ex)
				{
					ShowError(ex.Message);
				}
			}
			else if (CanSave && Save())
			{
				return BackToList();
			}
			if (IsExistingFeed)
			{
				BindDates(this._feed);
			}
			return Page();
		}

		/// <summary>
		/// The feed of the url (FeedId) in this section, or a new feed. False when the feed doesn't exist.
		/// </summary>
		private bool LoadFeed()
		{
			if (!Int32.TryParse(Request.Query["FeedId"], NumberStyles.Integer, CultureInfo.InvariantCulture, out int feedId))
			{
				return false;
			}
			IsExistingFeed = feedId > 0;
			if (!IsExistingFeed)
			{
				this._feed = new Feed();
				return true;
			}
			this._feed = Module.GetAllFeeds().Cast<Feed>().FirstOrDefault(feed => feed.Id == feedId);
			return this._feed != null;
		}

		private void Verify()
		{
			try
			{
				Feed verified = Module.VerifyFeed(FeedUrl);
				BindFeed(verified);
				Verified = true;
				ShowMessage("Feed is valid");
			}
			catch (Exception ex)
			{
				Verified = false;
				ShowError(ex.Message);
			}
		}

		private bool Save()
		{
			UrlMissing = String.IsNullOrWhiteSpace(FeedUrl);
			TitleMissing = String.IsNullOrWhiteSpace(FeedTitle);
			NumberOfItemsMissing = String.IsNullOrWhiteSpace(NumberOfItems);
			int numberOfItems = 0;
			NumberOfItemsInvalid = !NumberOfItemsMissing
				&& !Int32.TryParse(NumberOfItems, NumberStyles.Integer, CultureInfo.CurrentCulture, out numberOfItems);
			if (UrlMissing || TitleMissing || NumberOfItemsMissing || NumberOfItemsInvalid)
			{
				return false;
			}
			this._feed.Url = FeedUrl;
			this._feed.Title = FeedTitle;
			this._feed.NumberOfItems = numberOfItems;
			this._feed.Section = Section;
			try
			{
				Module.SaveFeed(this._feed);
				return true;
			}
			catch (Exception ex)
			{
				ShowError(ex.Message);
				return false;
			}
		}

		private void BindFeed(Feed feed)
		{
			FeedUrl = feed.Url;
			FeedTitle = feed.Title;
			NumberOfItems = feed.NumberOfItems.ToString(CultureInfo.CurrentCulture);
			BindDates(feed);
		}

		private void BindDates(Feed feed)
		{
			PubDate = feed.PubDate != DateTime.MinValue ? feed.PubDate.ToString() : String.Empty;
			UpdateTimestamp = feed.UpdateTimestamp != DateTime.MinValue ? feed.UpdateTimestamp.ToString() : String.Empty;
		}

		private IActionResult BackToList()
		{
			return Redirect("AdminRemoteContent.aspx" + BaseQueryString);
		}
	}
}
