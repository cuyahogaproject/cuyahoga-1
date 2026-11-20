using Cuyahoga.Core.Domain;

namespace Cuyahoga.Web.Mvc
{
	/// <summary>
	/// The modules of the sections on the public page of the request: one instance per section for the whole request,
	/// so a module controller and the module view of the active section share the module and its state.
	/// </summary>
	public interface IPublicPageModules
	{
		/// <summary>
		/// The module of the section, with the settings of the section and, for the active section, the path info of
		/// the url. Null when the module isn't activated.
		/// </summary>
		ModuleBase GetModule(Section section);
	}
}
