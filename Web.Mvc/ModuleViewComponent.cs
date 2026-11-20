using System.Collections.Concurrent;
using System.Globalization;
using System.Resources;
using Cuyahoga.Core.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Web.Mvc
{
	/// <summary>
	/// Base class for the view of a module on the public pages (the successor of BaseModuleControl in the WebForms
	/// site). The page renders one per section and wraps the output in the section block (title, edit links). A module
	/// type gets its view from the view component that derives from ModuleViewComponent of that module type.
	/// </summary>
	/// <remarks>
	/// Return views by their full path (View("~/Modules/Name/View.cshtml", model)): the page renders the views of
	/// several modules in one request, so views can't be found by the controller or area of the request.
	/// </remarks>
	/// <typeparam name="TModule">The module type.</typeparam>
	public abstract class ModuleViewComponent<TModule> : ViewComponent where TModule : ModuleBase
	{
		private static readonly ConcurrentDictionary<Type, ResourceManager> Resources =
			new ConcurrentDictionary<Type, ResourceManager>();

		private ModuleViewContext _context;

		/// <summary>
		/// The module of the section, with the settings of the section and the path info of the url (when the section
		/// is the active section).
		/// </summary>
		protected TModule Module => (TModule)this._context.Module;

		/// <summary>
		/// The page that is being rendered.
		/// </summary>
		protected PublicPage PublicPage => this._context.Page;

		/// <summary>
		/// Show the RSS icon above the section. Off by default.
		/// </summary>
		protected bool DisplaySyndicationIcon
		{
			get => this._context.DisplaySyndicationIcon;
			set => this._context.DisplaySyndicationIcon = value;
		}

		/// <summary>
		/// Called by the page. Don't call it yourself.
		/// </summary>
		public Task<IViewComponentResult> InvokeAsync(ModuleViewContext context)
		{
			this._context = context;
			return RenderAsync();
		}

		/// <summary>
		/// Renders the content of the section.
		/// </summary>
		protected abstract Task<IViewComponentResult> RenderAsync();

		/// <summary>
		/// A text in the language of the page, from the resources {namespace of the view component}.Resources.Strings
		/// in the assembly of the view component (like the GetText of the WebForms module controls).
		/// </summary>
		protected string GetText(string key)
		{
			ResourceManager resources = Resources.GetOrAdd(GetType(),
				type => new ResourceManager(type.Namespace + ".Resources.Strings", type.Assembly));
			return resources.GetString(key, CultureInfo.CurrentUICulture);
		}

		/// <summary>
		/// Registers a stylesheet for the head of the page.
		/// </summary>
		/// <param name="key">The unique key of the stylesheet. The template stylesheet has the key 'maincss'.</param>
		/// <param name="url">The url of the stylesheet (starting with /).</param>
		protected void RegisterStylesheet(string key, string url)
		{
			this._context.RegisterStylesheet(key, url);
		}

		/// <summary>
		/// Registers a script for the head of the page.
		/// </summary>
		/// <param name="key">The unique key of the script.</param>
		/// <param name="url">The url of the script (starting with /).</param>
		protected void RegisterJavaScript(string key, string url)
		{
			this._context.RegisterJavaScript(key, url);
		}
	}

	/// <summary>
	/// What a module view gets from the page and gives back to it. Remembered with the output when the section is
	/// cached, so a cached section registers the same stylesheets and scripts.
	/// </summary>
	public class ModuleViewContext
	{
		private readonly List<KeyValuePair<string, string>> _stylesheets = new List<KeyValuePair<string, string>>();
		private readonly List<KeyValuePair<string, string>> _javaScripts = new List<KeyValuePair<string, string>>();

		public ModuleViewContext(ModuleBase module, PublicPage page)
		{
			Module = module;
			Page = page;
		}

		public ModuleBase Module { get; }

		public PublicPage Page { get; }

		public bool DisplaySyndicationIcon { get; set; }

		public IReadOnlyList<KeyValuePair<string, string>> Stylesheets => this._stylesheets;

		public IReadOnlyList<KeyValuePair<string, string>> JavaScripts => this._javaScripts;

		public void RegisterStylesheet(string key, string url)
		{
			this._stylesheets.Add(new KeyValuePair<string, string>(key, url));
			Page.Assets.RegisterStylesheet(key, url);
		}

		public void RegisterJavaScript(string key, string url)
		{
			this._javaScripts.Add(new KeyValuePair<string, string>(key, url));
			Page.Assets.RegisterJavaScript(key, url);
		}
	}
}
