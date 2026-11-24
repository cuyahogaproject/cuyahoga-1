using System.Globalization;
using System.Security.Claims;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service.Membership;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using UserAuthenticationService = Cuyahoga.Core.Service.Membership.IAuthenticationService;

namespace Cuyahoga.Web.Core.Security
{
	/// <summary>
	/// The login of the site: a cookie with the id of the user (it replaced the Forms authentication cookie of the
	/// WebForms site). Every request loads the user, like AuthenticationModule of the WebForms site did; a user that
	/// was deleted or disabled is logged out.
	/// </summary>
	public static class CuyahogaAuthentication
	{
		public const string SchemeName = CookieAuthenticationDefaults.AuthenticationScheme;
		private const string CookieName = "Cuyahoga.Auth";

		/// <summary>
		/// The logged-in Cuyahoga user of the request, or null.
		/// </summary>
		public static User GetUser(HttpContext context)
		{
			return context.GetCuyahogaUser();
		}

		public static void Configure(CookieAuthenticationOptions options)
		{
			options.Cookie.Name = CookieName;
			options.Cookie.HttpOnly = true;
			options.Cookie.SameSite = SameSiteMode.Lax;
			// Like the Forms authentication of the WebForms site (timeout 30 minutes, sliding).
			options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
			options.SlidingExpiration = true;
			options.LoginPath = "/Login.aspx";
			options.ReturnUrlParameter = "ReturnUrl";
			options.Events = new CookieAuthenticationEvents
			{
				OnValidatePrincipal = ValidatePrincipalAsync,
				// Logged in without the rights for the page.
				OnRedirectToAccessDenied = async context =>
				{
					context.Response.StatusCode = StatusCodes.Status403Forbidden;
					context.Response.ContentType = "text/plain";
					await context.Response.WriteAsync("The logged-in user has insufficient rights to access this page.");
				}
			};
		}

		/// <summary>
		/// Checks the username and password and logs the user in. Returns null when that worked, otherwise why not:
		/// "invalid" (wrong username or password) or "disabled" (the account is disabled).
		/// </summary>
		public static async Task<string> SignInAsync(HttpContext context, string username, string password, bool persistent)
		{
			UserAuthenticationService authenticationService =
				context.RequestServices.GetRequiredService<UserAuthenticationService>();
			User user = authenticationService.AuthenticateUser(username, password,
				context.Connection.RemoteIpAddress?.ToString());
			if (user == null)
			{
				return "invalid";
			}
			if (!user.IsActive)
			{
				return "disabled";
			}
			var identity = new ClaimsIdentity(new[]
			{
				new Claim(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
				new Claim(ClaimTypes.Name, user.UserName)
			}, SchemeName);
			await context.SignInAsync(SchemeName, new ClaimsPrincipal(identity),
				new AuthenticationProperties { IsPersistent = persistent });
			user.IsAuthenticated = true;
			context.SetCuyahogaUser(user);
			return null;
		}

		public static Task SignOutAsync(HttpContext context)
		{
			context.SetCuyahogaUser(null);
			return context.SignOutAsync(SchemeName);
		}

		private static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
		{
			string id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
			User user = null;
			if (Int32.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out int userId))
			{
				user = context.HttpContext.RequestServices.GetRequiredService<IUserService>().GetUserById(userId);
			}
			if (user == null || !user.IsActive)
			{
				context.RejectPrincipal();
				await context.HttpContext.SignOutAsync(SchemeName);
				return;
			}
			user.IsAuthenticated = true;
			context.HttpContext.SetCuyahogaUser(user);
		}
	}
}
