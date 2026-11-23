using Cuyahoga.Core.Domain;
using Cuyahoga.Modules.User;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Modules.Mvc.User
{
	/// <summary>
	/// The login form, or the logged-in user with a logout button (User.ascx in the WebForms site), and links to
	/// register, reset the password and edit the profile.
	/// </summary>
	/// <remarks>
	/// The forms post to UserLogin.axd of the WebForms site, which creates or removes its login cookie and comes back
	/// to this page (with ?loginerror=... when the login failed).
	/// </remarks>
	public class UserViewComponent : ModuleViewComponent<UserModule>
	{
		protected override Task<IViewComponentResult> RenderAsync()
		{
			Core.Domain.User user = PublicPage.User;
			string applicationPath = PublicUrls.GetApplicationPath(Request);
			var model = new UserModel
			{
				LoginUrl = applicationPath + "UserLogin.axd",
				ReturnUrl = GetReturnUrl(),
				UserName = user?.UserName,
				LoginError = user == null ? GetLoginError(Request.Query["loginerror"].FirstOrDefault()) : null,
				RegisterUrl = GetSetting("SHOW_REGISTER")
					? GetActionUrl("Register", "/Register", applicationPath + "Profile.aspx/register")
					: null,
				ResetPasswordUrl = GetSetting("SHOW_RESET_PASSWORD")
					? GetActionUrl("ResetPassword", "/ResetPassword", applicationPath + "Profile.aspx/reset")
					: null,
				EditProfileUrl = GetSetting("SHOW_EDIT_PROFILE")
					? GetActionUrl("EditProfile", "/EditProfile", applicationPath + "Profile.aspx/edit")
					: null,
				UsernameText = GetText("USERNAME"),
				PasswordText = GetText("PASSWORD"),
				PersistLoginText = GetText("PERSISTLOGIN"),
				LoginText = GetText("LOGIN"),
				LogoutText = GetText("LOGOUT"),
				LoggedInText = GetText("LOGGEDINTEXT"),
				RegisterText = GetText("REGISTER"),
				ResetPasswordText = GetText("RESET"),
				EditProfileText = GetText("EDITACCOUNT")
			};
			return Task.FromResult<IViewComponentResult>(View("~/Modules/User/User.cshtml", model));
		}

		/// <summary>
		/// The current page, without the error of a previous login.
		/// </summary>
		private string GetReturnUrl()
		{
			var query = new QueryBuilder(Request.Query
				.Where(parameter => !String.Equals(parameter.Key, "loginerror", StringComparison.OrdinalIgnoreCase)));
			return Request.PathBase + Request.Path + query.ToQueryString();
		}

		private string GetLoginError(string code)
		{
			return code switch
			{
				"missing" => GetText("USERNAMEPASSWORDMISSING"),
				"invalid" => GetText("USERNAMEPASSWORDERROR"),
				"disabled" => GetText("LOGINERROR") + " The account is disabled.",
				"error" => GetText("LOGINERROR"),
				_ => null
			};
		}

		private bool GetSetting(string name)
		{
			return Module.Section.Settings.TryGetValue(name, out string text) && Boolean.TryParse(text, out bool value) && value;
		}

		/// <summary>
		/// The url of the section that is connected to the action, or the old Profile.aspx url without a connection.
		/// </summary>
		private string GetActionUrl(string action, string pathInfo, string profileUrl)
		{
			return Module.Section.Connections.TryGetValue(action, out Section sectionTo)
				? PublicUrls.GetUrlFromSection(Request, sectionTo) + pathInfo
				: profileUrl;
		}
	}

	public class UserModel
	{
		/// <summary>
		/// The url of the login and logout handler of the WebForms site.
		/// </summary>
		public string LoginUrl { get; init; }
		public string ReturnUrl { get; init; }
		/// <summary>
		/// The logged-in user, or null.
		/// </summary>
		public string UserName { get; init; }
		/// <summary>
		/// Why the login failed, or null.
		/// </summary>
		public string LoginError { get; init; }
		/// <summary>
		/// The link to register, or null when it doesn't show. The same for the other links.
		/// </summary>
		public string RegisterUrl { get; init; }
		public string ResetPasswordUrl { get; init; }
		public string EditProfileUrl { get; init; }
		public string UsernameText { get; init; }
		public string PasswordText { get; init; }
		public string PersistLoginText { get; init; }
		public string LoginText { get; init; }
		public string LogoutText { get; init; }
		public string LoggedInText { get; init; }
		public string RegisterText { get; init; }
		public string ResetPasswordText { get; init; }
		public string EditProfileText { get; init; }
	}
}
