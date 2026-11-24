using Cuyahoga.Core.DataAccess;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Search;
using Cuyahoga.Core.Service;
using Cuyahoga.Core.Util;

namespace Cuyahoga.Web.Core.Pages.Admin
{
	/// <summary>
	/// Rebuilds the full-text index of the site (SearchIndexDir in the data folder) from the content of all
	/// searchable modules.
	/// </summary>
	public class RebuildIndexModel : AdminPageModel
	{
		private readonly ICommonDao _commonDao;
		private readonly ModuleLoader _moduleLoader;
		private readonly CuyahogaAppSettings _settings;
		private readonly ILogger<RebuildIndexModel> _logger;

		public RebuildIndexModel(ICommonDao commonDao, ModuleLoader moduleLoader, CuyahogaAppSettings settings,
			ILogger<RebuildIndexModel> logger)
		{
			this._commonDao = commonDao;
			this._moduleLoader = moduleLoader;
			this._settings = settings;
			this._logger = logger;
		}

		public bool IsRebuilt { get; private set; }

		public void OnGet()
		{
			Title = "Rebuild fulltext index";
		}

		public void OnPost()
		{
			Title = "Rebuild fulltext index";
			var indexBuilder = new IndexBuilder(ApplicationPaths.MapPath(this._settings.SearchIndexDir), true);
			try
			{
				foreach (Site site in this._commonDao.GetAll(typeof(Site)))
				{
					foreach (Node node in site.RootNodes)
					{
						try
						{
							AddNode(node, indexBuilder);
						}
						catch (Exception ex)
						{
							this._logger.LogError(ex, "Indexing contents of node {NodeId} - {Title} failed.", node.Id, node.Title);
						}
					}
				}
			}
			finally
			{
				indexBuilder.Close();
			}
			IsRebuilt = true;
		}

		private void AddNode(Node node, IndexBuilder indexBuilder)
		{
			foreach (Section section in node.Sections)
			{
				ModuleBase module = null;
				try
				{
					module = this._moduleLoader.GetModuleFromSection(section);
				}
				catch (Exception ex)
				{
					this._logger.LogError(ex, "Unable to create the module for section {SectionId} - {Title}.", section.Id, section.Title);
				}
				if (module is ISearchable searchableModule)
				{
					try
					{
						foreach (SearchContent content in searchableModule.GetAllSearchableContent())
						{
							indexBuilder.AddContent(content);
						}
					}
					catch (Exception ex)
					{
						this._logger.LogError(ex, "Indexing contents of section {SectionId} - {Title} failed.", section.Id, section.Title);
					}
				}
			}
			foreach (Node childNode in node.ChildNodes)
			{
				AddNode(childNode, indexBuilder);
			}
		}
	}
}
