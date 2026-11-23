using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Html;

namespace Cuyahoga.Web.Core.PublicSite
{
	/// <summary>
	/// The model of a public page and of its template (the layout): the page and the rendered content of the
	/// placeholders.
	/// </summary>
	public class PublicPageModel
	{
		private readonly Dictionary<string, HtmlContentBuilder> _placeholders = new Dictionary<string, HtmlContentBuilder>();
		private readonly List<string> _renderedPlaceholders = new List<string>();

		public PublicPageModel(PublicPage page, string layoutPath)
		{
			Page = page;
			LayoutPath = layoutPath;
		}

		public PublicPage Page { get; }

		/// <summary>
		/// The layout of the template.
		/// </summary>
		public string LayoutPath { get; }

		/// <summary>
		/// The preview of a template in the site administration: each placeholder is a button that puts its name into
		/// this select element of the page that opened the preview (window.opener.setPlaceholderValue). Null on public
		/// pages.
		/// </summary>
		public string PlaceholderPickerControl { get; init; }

		/// <summary>
		/// The placeholders of the template, in the order that it rendered them.
		/// </summary>
		public IReadOnlyList<string> RenderedPlaceholders => this._renderedPlaceholders;

		internal void AddRenderedPlaceholder(string placeholder)
		{
			if (!this._renderedPlaceholders.Contains(placeholder))
			{
				this._renderedPlaceholders.Add(placeholder);
			}
		}

		/// <summary>
		/// The rendered content of a placeholder, or null when there is nothing in it.
		/// </summary>
		public IHtmlContent GetPlaceholderContent(string placeholder)
		{
			return this._placeholders.TryGetValue(placeholder, out HtmlContentBuilder content) ? content : null;
		}

		internal HtmlContentBuilder GetPlaceholder(string placeholder)
		{
			if (!this._placeholders.TryGetValue(placeholder, out HtmlContentBuilder content))
			{
				content = new HtmlContentBuilder();
				this._placeholders.Add(placeholder, content);
			}
			return content;
		}
	}
}
