using Cuyahoga.Core.Domain;

namespace Cuyahoga.Web.Mvc
{
	/// <summary>
	/// The cached output of the sections (for sections with a cache duration, for anonymous users).
	/// </summary>
	public interface ISectionOutputCache
	{
		/// <summary>
		/// Removes the cached output of the section of the module (for all path infos: the list and the items), so a
		/// change shows immediately. Like InvalidateCache of the WebForms module controls.
		/// </summary>
		void Remove(ModuleBase module);
	}
}
