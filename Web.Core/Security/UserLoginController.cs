using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Web.Core.Security
{
	/// <summary>
	/// The login and logout of the login form of the User module (POST UserLogin.axd: action=login with username,
	/// password and persist, or action=logout), which goes back to returnUrl. A failed login goes back with
	/// ?loginerror=missing|invalid|disabled|error. Only from pages of this site (the Origin header).
	/// </summary>
	public class UserLoginController : Controller
	{
		[HttpPost]
		[Route("UserLogin.axd")]
		public async Task<IActionResult> Index([FromForm] string action, [FromForm] string username,
			[FromForm] string password, [FromForm] string persist, [FromForm] string returnUrl)
		{
			if (!Url.IsLocalUrl(returnUrl) || !IsSameOrigin())
			{
				return BadRequest();
			}
			string error = null;
			if (action == "logout")
			{
				await CuyahogaAuthentication.SignOutAsync(HttpContext);
			}
			else if (String.IsNullOrWhiteSpace(username) || String.IsNullOrWhiteSpace(password))
			{
				error = "missing";
			}
			else
			{
				try
				{
					error = await CuyahogaAuthentication.SignInAsync(HttpContext, username, password, persist == "true");
				}
				catch (Exception)
				{
					error = "error";
				}
			}
			if (error != null)
			{
				returnUrl += (returnUrl.Contains('?') ? "&" : "?") + "loginerror=" + error;
			}
			return LocalRedirect(returnUrl);
		}

		/// <summary>
		/// The form comes from a page of this site (when the browser sends an Origin header).
		/// </summary>
		private bool IsSameOrigin()
		{
			string origin = Request.Headers.Origin;
			if (String.IsNullOrEmpty(origin))
			{
				return true;
			}
			return Uri.TryCreate(origin, UriKind.Absolute, out Uri originUri)
				&& String.Equals(originUri.Authority, Request.Host.Value, StringComparison.OrdinalIgnoreCase);
		}
	}
}
