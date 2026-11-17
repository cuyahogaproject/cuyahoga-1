using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service.Membership;
using Cuyahoga.Core.Util;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Web.Core.Pages.Admin
{
	public class UsersModel : AdminPageModel
	{
		public const int PageSize = 10;

		private readonly IUserService _userService;

		public UsersModel(IUserService userService)
		{
			this._userService = userService;
		}

		/// <summary>
		/// The search text (an empty search finds all users).
		/// </summary>
		[BindProperty(SupportsGet = true, Name = "username")]
		public string Username { get; set; }

		/// <summary>
		/// The zero-based page of the search results.
		/// </summary>
		[BindProperty(SupportsGet = true, Name = "pageIndex")]
		public int PageIndex { get; set; }

		/// <summary>
		/// False on the first visit, before a search.
		/// </summary>
		public bool HasSearched
		{
			get { return Request.Query.ContainsKey("username"); }
		}

		public List<User> PageOfUsers { get; private set; } = new List<User>();

		public int TotalPages { get; private set; }

		public void OnGet()
		{
			Title = "Users";
			if (!HasSearched)
			{
				return;
			}
			List<User> users = this._userService.FindUsersByUsername(Username ?? String.Empty).Cast<User>().ToList();
			TotalPages = (int)Math.Ceiling(users.Count / (double)PageSize);
			PageIndex = Math.Max(0, Math.Min(PageIndex, TotalPages - 1));
			PageOfUsers = users.Skip(PageIndex * PageSize).Take(PageSize).ToList();
		}

		/// <summary>
		/// The last login date in the time zone of the logged-in administrator.
		/// </summary>
		public string FormatLastLogin(User user)
		{
			return user.LastLogin.HasValue
				? TimeZoneUtil.AdjustDateToUserTimeZone(user.LastLogin.Value, CurrentUser).ToString()
				: String.Empty;
		}

		/// <summary>
		/// The url of a page of the search results (for the pager).
		/// </summary>
		public string PageUrl(int pageIndex)
		{
			return "Users.aspx?username=" + Uri.EscapeDataString(Username ?? String.Empty) + "&pageIndex=" + pageIndex;
		}
	}
}
