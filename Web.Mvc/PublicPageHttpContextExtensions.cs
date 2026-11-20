using Microsoft.AspNetCore.Http;

namespace Cuyahoga.Web.Mvc
{
	public static class PublicPageHttpContextExtensions
	{
		/// <summary>
		/// The public page of the request, or null when the request isn't for a public page.
		/// </summary>
		public static PublicPage GetPublicPage(this HttpContext httpContext)
		{
			return httpContext.Features.Get<PublicPage>();
		}

		public static void SetPublicPage(this HttpContext httpContext, PublicPage page)
		{
			httpContext.Features.Set(page);
		}
	}
}
