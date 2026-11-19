using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Search;
using Cuyahoga.Core.Service;
using Cuyahoga.Core.Util;

namespace Cuyahoga.Web.Core.Services
{
	/// <summary>
	/// Updates the full-text index of the WebForms site (SearchHelper in the WebForms site). The index is in the folder
	/// of the WebForms site (Cuyahoga.SearchIndexDir), so LegacySitePath must be set.
	/// </summary>
	/// <remarks>
	/// Both sites write to the same index. Lucene's write lock keeps it consistent, but when both write at the same
	/// moment, one of them fails after waiting for the lock.
	/// </remarks>
	public class SearchIndexUpdater
	{
		private readonly ModuleLoader _moduleLoader;
		private readonly CuyahogaAppSettings _settings;

		public SearchIndexUpdater(ModuleLoader moduleLoader, CuyahogaAppSettings settings)
		{
			this._moduleLoader = moduleLoader;
			this._settings = settings;
		}

		/// <summary>
		/// Updates the index with the content of the section (when its module is searchable).
		/// </summary>
		public void UpdateIndexFromSection(Section section)
		{
			string indexDir = ApplicationPaths.MapPath(this._settings.SearchIndexDir);
			IndexBuilder indexBuilder = new IndexBuilder(indexDir, false);
			try
			{
				ModuleBase module = this._moduleLoader.GetModuleFromSection(section);
				if (module is ISearchable)
				{
					indexBuilder.UpdateContentFromModule(module);
				}
			}
			finally
			{
				indexBuilder.Close();
			}
		}
	}
}
