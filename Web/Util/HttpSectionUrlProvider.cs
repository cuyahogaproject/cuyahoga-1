using System.Web;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service;

namespace Cuyahoga.Web.Util
{
	/// <summary>
	/// Section urls for the WebForms site. Returns null outside a web request because the application path is unknown
	/// there.
	/// </summary>
	public class HttpSectionUrlProvider : ISectionUrlProvider
	{
		public string GetUrlFromSection(Section section)
		{
			return HttpContext.Current != null ? UrlHelper.GetUrlFromSection(section) : null;
		}
	}
}
