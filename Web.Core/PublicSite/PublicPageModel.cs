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
