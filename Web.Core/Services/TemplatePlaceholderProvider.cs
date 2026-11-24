using Cuyahoga.Core.Service.SiteStructure;
using Cuyahoga.Web.Core.PublicSite;

namespace Cuyahoga.Web.Core.Services
{
	/// <summary>
	/// The placeholders of a template: the &lt;cuyahoga-placeholder /&gt; elements of its layout, in their order.
	/// </summary>
	public class TemplatePlaceholderProvider
	{
		private readonly ITemplateService _templateService;
		private readonly TemplateRenderer _templateRenderer;

		public TemplatePlaceholderProvider(ITemplateService templateService, TemplateRenderer templateRenderer)
		{
			this._templateService = templateService;
			this._templateRenderer = templateRenderer;
		}

		/// <summary>
		/// The placeholder names. A template without a layout has none.
		/// </summary>
		public async Task<List<string>> GetPlaceholdersAsync(int templateId)
		{
			var rendered = await this._templateRenderer.RenderAsync(this._templateService.GetTemplateById(templateId));
			return rendered?.Placeholders.ToList() ?? new List<string>();
		}
	}
}
