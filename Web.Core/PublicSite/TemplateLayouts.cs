using System.Collections.Concurrent;
using Cuyahoga.Core.Domain;
using Microsoft.AspNetCore.Mvc.Razor;

namespace Cuyahoga.Web.Core.PublicSite
{
	/// <summary>
	/// The Razor layouts of the templates. A template in the database points to the user control of the WebForms site
	/// (Templates/AnotherRed/Cuyahoga.ascx); its layout here has the same path with .cshtml.
	/// </summary>
	public class TemplateLayouts
	{
		private readonly IRazorViewEngine _viewEngine;
		private readonly ConcurrentDictionary<string, bool> _exists = new ConcurrentDictionary<string, bool>();

		public TemplateLayouts(IRazorViewEngine viewEngine)
		{
			this._viewEngine = viewEngine;
		}

		public static string GetLayoutPath(Template template)
		{
			return "/" + Path.ChangeExtension(template.BasePath + "/" + template.TemplateControl, ".cshtml");
		}

		/// <summary>
		/// Is there a layout for the template (yet)?
		/// </summary>
		public bool HasLayout(Template template)
		{
			return this._exists.GetOrAdd(GetLayoutPath(template),
				path => this._viewEngine.GetView(executingFilePath: null, viewPath: path, isMainPage: false).Success);
		}
	}
}
