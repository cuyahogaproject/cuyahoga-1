using System.Text;
using System.Text.Encodings.Web;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Cuyahoga.Web.Core.PublicSite
{
	/// <summary>
	/// The content of a placeholder of a template: <c>&lt;cuyahoga-placeholder id="maincontent" /&gt;</c>.
	/// </summary>
	[HtmlTargetElement("cuyahoga-placeholder", TagStructure = TagStructure.WithoutEndTag)]
	public class PlaceholderTagHelper : TagHelper
	{
		[ViewContext]
		[HtmlAttributeNotBound]
		public ViewContext ViewContext { get; set; }

		/// <summary>
		/// The name of the placeholder (the placeholder of sections and custom menus).
		/// </summary>
		public string Id { get; set; }

		public override void Process(TagHelperContext context, TagHelperOutput output)
		{
			output.TagName = null;
			var model = ViewContext.ViewData.Model as PublicPageModel;
			model?.AddRenderedPlaceholder(Id);
			if (model?.PlaceholderPickerControl != null)
			{
				// The preview of the template: a button that picks the placeholder (TemplatePreview.aspx).
				string onclick = $"window.opener.setPlaceholderValue('{JavaScriptEncoder.Default.Encode(model.PlaceholderPickerControl)}','{JavaScriptEncoder.Default.Encode(Id)}');self.close()";
				output.Content.SetHtmlContent($"<input type=\"button\" value=\"{HtmlEncoder.Default.Encode(Id)}\" onclick=\"{HtmlEncoder.Default.Encode(onclick)}\" />");
				return;
			}
			IHtmlContent content = model?.GetPlaceholderContent(Id);
			if (content != null)
			{
				output.Content.SetHtmlContent(content);
			}
			else
			{
				output.SuppressOutput();
			}
		}
	}

	/// <summary>
	/// The meta tags, stylesheets and scripts of the page, for the head of a template:
	/// <c>&lt;cuyahoga-head-assets /&gt;</c>.
	/// </summary>
	[HtmlTargetElement("cuyahoga-head-assets", TagStructure = TagStructure.WithoutEndTag)]
	public class HeadAssetsTagHelper : TagHelper
	{
		private readonly HtmlEncoder _htmlEncoder;

		public HeadAssetsTagHelper(HtmlEncoder htmlEncoder)
		{
			this._htmlEncoder = htmlEncoder;
		}

		[ViewContext]
		[HtmlAttributeNotBound]
		public ViewContext ViewContext { get; set; }

		public override void Process(TagHelperContext context, TagHelperOutput output)
		{
			output.TagName = null;
			PageAssets assets = (ViewContext.ViewData.Model as PublicPageModel)?.Page.Assets;
			if (assets == null)
			{
				output.SuppressOutput();
				return;
			}
			var html = new StringBuilder();
			foreach (KeyValuePair<string, string> metaTag in assets.MetaTags)
			{
				html.Append($"<meta name=\"{Encode(metaTag.Key)}\" content=\"{Encode(metaTag.Value)}\" />\n\t");
			}
			foreach (string stylesheet in assets.Stylesheets)
			{
				html.Append($"<link rel=\"stylesheet\" type=\"text/css\" href=\"{Encode(stylesheet)}\" />\n\t");
			}
			foreach (string javaScript in assets.JavaScripts)
			{
				html.Append($"<script src=\"{Encode(javaScript)}\" type=\"text/javascript\"></script>\n\t");
			}
			output.Content.SetHtmlContent(html.ToString());
		}

		private string Encode(string value) => this._htmlEncoder.Encode(value);
	}
}
