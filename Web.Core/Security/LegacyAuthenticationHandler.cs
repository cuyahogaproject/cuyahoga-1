using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service.Membership;
using Cuyahoga.Web.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Cuyahoga.Web.Core.Security
{
	/// <summary>
	/// Authenticates requests with the Forms authentication cookie of the WebForms site, which this site can't read
	/// itself: it asks the WebForms site (CurrentUser.axd) who is logged in and loads that user. Users still log in and
	/// out on the WebForms site.
	/// </summary>
	public class LegacyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
	{
		public const string SchemeName = "CuyahogaLegacy";
		// The name of the Forms authentication cookie (Web.config, <forms name="...">).
		private const string FormsCookieName = "CuyahogaUser";
		private static readonly object UserKey = new object();

		private readonly LegacySiteClient _legacySiteClient;
		private readonly IUserService _userService;

		public LegacyAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
			UrlEncoder encoder, LegacySiteClient legacySiteClient, IUserService userService)
			: base(options, logger, encoder)
		{
			this._legacySiteClient = legacySiteClient;
			this._userService = userService;
		}

		/// <summary>
		/// The logged-in Cuyahoga user of the request, or null.
		/// </summary>
		public static User GetUser(HttpContext context)
		{
			return context.Items.TryGetValue(UserKey, out object user) ? (User)user : null;
		}

		protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
		{
			if (!Request.Cookies.ContainsKey(FormsCookieName))
			{
				return AuthenticateResult.NoResult();
			}

			int? userId = await GetLegacyUserIdAsync();
			if (userId == null)
			{
				return AuthenticateResult.NoResult();
			}
			User user = this._userService.GetUserById(userId.Value);
			if (user == null || !user.IsActive)
			{
				return AuthenticateResult.Fail("The logged-in user doesn't exist or is disabled.");
			}
			user.IsAuthenticated = true;
			Context.Items[UserKey] = user;

			var identity = new ClaimsIdentity(new[]
			{
				new Claim(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
				new Claim(ClaimTypes.Name, user.UserName)
			}, SchemeName);
			return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
		}

		/// <summary>
		/// Not logged in: go to the login page of the WebForms site, like Forms authentication does.
		/// </summary>
		protected override Task HandleChallengeAsync(AuthenticationProperties properties)
		{
			string returnUrl = Request.PathBase + Request.Path + Request.QueryString;
			Response.Redirect(Request.PathBase + "/Login.aspx?ReturnUrl=" + Uri.EscapeDataString(returnUrl));
			return Task.CompletedTask;
		}

		/// <summary>
		/// Logged in without the rights for the page (the WebForms site shows an error page for this).
		/// </summary>
		protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
		{
			Response.StatusCode = StatusCodes.Status403Forbidden;
			Response.ContentType = "text/plain";
			await Response.WriteAsync("The logged-in user has insufficient rights to access the site administration.");
		}

		private async Task<int?> GetLegacyUserIdAsync()
		{
			var (statusCode, body) = await this._legacySiteClient.GetAsync("CurrentUser.axd");
			switch (statusCode)
			{
				case HttpStatusCode.OK:
					if (Int32.TryParse(body.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int userId))
					{
						return userId;
					}
					throw new InvalidOperationException("CurrentUser.axd of the WebForms site returned an invalid user id.");
				case HttpStatusCode.NoContent:
					return null;
				default:
					throw new InvalidOperationException($"CurrentUser.axd of the WebForms site returned HTTP {(int)statusCode}.");
			}
		}
	}
}
