namespace Cuyahoga.Web.Mvc
{
	/// <summary>
	/// Stylesheets, scripts and meta tags for the head of a public page. The sections are rendered before the template,
	/// so modules can register these while they render.
	/// </summary>
	public class PageAssets
	{
		private readonly OrderedDictionary<string, string> _stylesheets = new OrderedDictionary<string, string>();
		private readonly OrderedDictionary<string, string> _javaScripts = new OrderedDictionary<string, string>();
		private readonly OrderedDictionary<string, string> _metaTags = new OrderedDictionary<string, string>();

		public IEnumerable<string> Stylesheets => this._stylesheets.Values;

		public IEnumerable<string> JavaScripts => this._javaScripts.Values;

		public IEnumerable<KeyValuePair<string, string>> MetaTags => this._metaTags;

		/// <summary>
		/// Registers a stylesheet once per key. The template stylesheet has the key 'maincss'.
		/// </summary>
		/// <param name="key">The unique key of the stylesheet.</param>
		/// <param name="url">The url of the stylesheet (starting with /).</param>
		public void RegisterStylesheet(string key, string url)
		{
			this._stylesheets.TryAdd(key, url);
		}

		/// <summary>
		/// Registers a script once per key.
		/// </summary>
		/// <param name="key">The unique key of the script.</param>
		/// <param name="url">The url of the script (starting with /).</param>
		public void RegisterJavaScript(string key, string url)
		{
			this._javaScripts.TryAdd(key, url);
		}

		/// <summary>
		/// Registers a meta tag. A later value replaces an earlier one; an empty value is ignored.
		/// </summary>
		public void RegisterMetaTag(string name, string content)
		{
			if (!String.IsNullOrEmpty(content))
			{
				this._metaTags[name] = content;
			}
		}
	}
}
