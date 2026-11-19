using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service;
using Microsoft.AspNetCore.Http;

namespace Cuyahoga.Web.Core.Services
{
	/// <summary>
	/// Section urls ({PathBase}/{SectionId}/section.aspx, like the WebForms site). Returns null outside a request
	/// because the path base is unknown there.
	/// </summary>
	public class SectionUrlProvider : ISectionUrlProvider
	{
		private readonly IHttpContextAccessor _httpContextAccessor;

		public SectionUrlProvider(IHttpContextAccessor httpContextAccessor)
		{
			this._httpContextAccessor = httpContextAccessor;
		}

		public string GetUrlFromSection(Section section)
		{
			HttpContext httpContext = this._httpContextAccessor.HttpContext;
			return httpContext != null ? httpContext.Request.PathBase + "/" + section.Id + "/section.aspx" : null;
		}

		public string GetFullUrlFromSection(Section section)
		{
			HttpContext httpContext = this._httpContextAccessor.HttpContext;
			return httpContext != null
				? httpContext.Request.Scheme + "://" + httpContext.Request.Host + GetUrlFromSection(section)
				: null;
		}
	}
}
