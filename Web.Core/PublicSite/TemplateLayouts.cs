using System.Collections.Concurrent;
using Cuyahoga.Core.Domain;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Razor.Compilation;

namespace Cuyahoga.Web.Core.PublicSite
{
	/// <summary>
	/// The Razor layouts of the templates. A template in the database points to the user control of the WebForms site
	/// (Templates/AnotherRed/Cuyahoga.ascx); its layout here has the same path with .cshtml.
	/// </summary>
	public class TemplateLayouts
	{
		private readonly IRazorViewEngine _viewEngine;
		private readonly ApplicationPartManager _partManager;
		private readonly ConcurrentDictionary<string, bool> _exists = new ConcurrentDictionary<string, bool>();

		public TemplateLayouts(IRazorViewEngine viewEngine, ApplicationPartManager partManager)
		{
			this._viewEngine = viewEngine;
			this._partManager = partManager;
		}

		/// <summary>
		/// The template controls (names of the user controls of the WebForms site: Cuyahoga.ascx) that have a layout in
		/// the base path of a template (Templates/AnotherRed), for the template administration.
		/// </summary>
		public IReadOnlyList<string> GetTemplateControls(string basePath)
		{
			var views = new ViewsFeature();
			this._partManager.PopulateFeature(views);
			string folder = "/" + (basePath ?? String.Empty).Trim('/') + "/";
			return views.ViewDescriptors
				.Select(view => view.RelativePath)
				.Where(path => path.StartsWith(folder, StringComparison.OrdinalIgnoreCase)
					&& path.IndexOf('/', folder.Length) < 0 && !Path.GetFileName(path).StartsWith('_'))
				.Select(path => Path.ChangeExtension(Path.GetFileName(path), ".ascx"))
				.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
				.ToList();
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
