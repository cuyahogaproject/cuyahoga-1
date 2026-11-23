using System;
using System.Web;
using Cuyahoga.Core;
using Cuyahoga.Web.HttpModules;

namespace Cuyahoga.Web.HttpHandlers
{
	/// <summary>
	/// Logs a user in or out (POST: action=login with username, password and persist, or action=logout) and goes back
	/// to returnUrl. A failed login goes back with ?loginerror=missing|invalid|disabled|error.
	/// </summary>
	/// <remarks>
	/// The login form of the User module on the pages of the ASP.NET Core site (Web.Core) posts here, because only this
	/// site can create (and remove) its Forms authentication cookie.
	/// </remarks>
	public class UserLoginHandler : IHttpHandler
	{
		public bool IsReusable
		{
			get { return true; }
		}

		public void ProcessRequest(HttpContext context)
		{
			HttpRequest request = context.Request;
			context.Response.Cache.SetCacheability(HttpCacheability.NoCache);
			string returnUrl = request.Form["returnUrl"];
			if (request.HttpMethod != "POST" || !IsLocalUrl(returnUrl) || !IsSameOrigin(request))
			{
				context.Response.StatusCode = 400;
				return;
			}

			AuthenticationModule authentication = (AuthenticationModule)context.ApplicationInstance.Modules["AuthenticationModule"];
			string error = null;
			if (request.Form["action"] == "logout")
			{
				authentication.Logout();
			}
			else
			{
				string username = request.Form["username"];
				string password = request.Form["password"];
				if (String.IsNullOrWhiteSpace(username) || String.IsNullOrWhiteSpace(password))
				{
					error = "missing";
				}
				else
				{
					try
					{
						if (!authentication.AuthenticateUser(username, password, request.Form["persist"] == "true"))
						{
							error = "invalid";
						}
					}
					catch (AccessForbiddenException)
					{
						error = "disabled";
					}
					catch (Exception)
					{
						error = "error";
					}
				}
			}
			if (error != null)
			{
				returnUrl += (returnUrl.Contains("?") ? "&" : "?") + "loginerror=" + error;
			}
			context.Response.Redirect(returnUrl, false);
		}

		/// <summary>
		/// Only urls of this site (no other host, no protocol-relative urls).
		/// </summary>
		private static bool IsLocalUrl(string url)
		{
			return !String.IsNullOrEmpty(url) && url[0] == '/'
				&& (url.Length == 1 || (url[1] != '/' && url[1] != '\\'));
		}

		/// <summary>
		/// The form comes from a page of this site (the Origin header, when the browser sends it, has the address that
		/// the browser used: the one of the ASP.NET Core site in front of this site).
		/// </summary>
		private static bool IsSameOrigin(HttpRequest request)
		{
			string origin = request.Headers["Origin"];
			if (String.IsNullOrEmpty(origin))
			{
				return true;
			}
			Uri originUri;
			if (!Uri.TryCreate(origin, UriKind.Absolute, out originUri))
			{
				return false;
			}
			string host = request.Headers["X-Forwarded-Host"];
			if (String.IsNullOrEmpty(host))
			{
				host = request.Url.Authority;
			}
			else
			{
				host = host.Split(',')[0].Trim();
			}
			return String.Equals(originUri.Authority, host, StringComparison.OrdinalIgnoreCase);
		}
	}
}
