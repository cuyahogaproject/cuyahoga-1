using Cuyahoga.Core.Domain;
using Microsoft.AspNetCore.Http;

namespace Cuyahoga.Web.Mvc
{
	public static class CuyahogaUserHttpContextExtensions
	{
		private static readonly object UserKey = new object();

		/// <summary>
		/// The logged-in Cuyahoga user of the request, or null (set by the authentication of the site).
		/// </summary>
		public static User GetCuyahogaUser(this HttpContext httpContext)
		{
			return httpContext.Items.TryGetValue(UserKey, out object user) ? (User)user : null;
		}

		public static void SetCuyahogaUser(this HttpContext httpContext, User user)
		{
			httpContext.Items[UserKey] = user;
		}
	}
}
