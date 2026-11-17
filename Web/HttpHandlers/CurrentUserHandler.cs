using System.Web;
using Cuyahoga.Core.Domain;

namespace Cuyahoga.Web.HttpHandlers
{
	/// <summary>
	/// Returns the id of the logged-in user (text/plain), or 204 No Content when nobody is logged in.
	/// </summary>
	/// <remarks>
	/// The ASP.NET Core site (Web.Core) can't read the Forms authentication cookie of this site. It calls this handler
	/// with the cookies of the browser to find out who is logged in. Forms authentication may renew the cookie in the
	/// response (sliding expiration); the ASP.NET Core site passes that on to the browser.
	/// </remarks>
	public class CurrentUserHandler : IHttpHandler
	{
		public bool IsReusable
		{
			get { return true; }
		}

		public void ProcessRequest(HttpContext context)
		{
			context.Response.Cache.SetCacheability(HttpCacheability.NoCache);
			context.Response.Cache.SetNoStore();
			// AuthenticationModule replaces the identity of a logged-in user with the Cuyahoga user.
			User user = context.User != null && context.User.Identity.IsAuthenticated ? context.User.Identity as User : null;
			if (user == null)
			{
				context.Response.StatusCode = 204;
				return;
			}
			context.Response.ContentType = "text/plain";
			context.Response.Write(user.Id.ToString());
		}
	}
}
