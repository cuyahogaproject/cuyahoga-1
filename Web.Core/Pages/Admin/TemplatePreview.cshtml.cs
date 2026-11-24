using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service.SiteStructure;
using Cuyahoga.Web.Core.PublicSite;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Cuyahoga.Web.Core.Pages.Admin
{
	/// <summary>
	/// The preview of a template (a popup of the section and site pages): its layout with a button in each
	/// placeholder that picks the placeholder in the select element Control of the page that opened the preview.
	/// </summary>
	public class TemplatePreviewModel : PageModel
	{
		private readonly ITemplateService _templateService;
		private readonly TemplateRenderer _templateRenderer;

		public TemplatePreviewModel(ITemplateService templateService, TemplateRenderer templateRenderer)
		{
			this._templateService = templateService;
			this._templateRenderer = templateRenderer;
		}

		public async Task<IActionResult> OnGetAsync(int templateId, string control)
		{
			Template template = this._templateService.GetTemplateById(templateId);
			var rendered = template != null ? await this._templateRenderer.RenderAsync(template, control) : null;
			return rendered != null ? Content(rendered.Value.Html, "text/html; charset=utf-8") : NotFound();
		}
	}
}
