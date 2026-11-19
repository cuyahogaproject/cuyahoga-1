using System.Globalization;
using System.Net;

namespace Cuyahoga.Web.Core.Services
{
	/// <summary>
	/// The placeholders of a template. Templates are WebForms user controls, so the WebForms site finds them
	/// (TemplatePlaceholders.axd).
	/// </summary>
	public class TemplatePlaceholderProvider
	{
		private readonly LegacySiteClient _legacySiteClient;

		public TemplatePlaceholderProvider(LegacySiteClient legacySiteClient)
		{
			this._legacySiteClient = legacySiteClient;
		}

		/// <summary>
		/// The placeholder names, in the order the WebForms admin pages showed them.
		/// </summary>
		public async Task<List<string>> GetPlaceholdersAsync(int templateId)
		{
			var (statusCode, body) = await this._legacySiteClient.GetAsync(
				"TemplatePlaceholders.axd?TemplateId=" + templateId.ToString(CultureInfo.InvariantCulture));
			if (statusCode != HttpStatusCode.OK)
			{
				throw new InvalidOperationException(
					$"Unable to get the placeholders of template {templateId} (HTTP {(int)statusCode} from the WebForms site).");
			}
			return body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
		}
	}
}
