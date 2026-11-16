using Cuyahoga.Core.Domain;

namespace Cuyahoga.Core.Service
{
	/// <summary>
	/// Builds the url of a section. Section urls depend on the web application that hosts Cuyahoga, so each host
	/// provides its own implementation.
	/// </summary>
	public interface ISectionUrlProvider
	{
		/// <summary>
		/// The url of the section, or null when no url can be built (for example outside a web request).
		/// </summary>
		string GetUrlFromSection(Section section);
	}
}
