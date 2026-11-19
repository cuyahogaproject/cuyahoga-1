using System.Collections;
using Cuyahoga.Core.DataAccess;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service;
using Cuyahoga.Core.Service.SiteStructure;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Web.Core.Pages.Admin
{
	/// <summary>
	/// The sections without node: detached sections and sections on templates.
	/// </summary>
	public class SectionsModel : AdminPageModel
	{
		private readonly ISectionService _sectionService;
		private readonly ICommonDao _commonDao;
		private readonly ModuleLoader _moduleLoader;
		private readonly ILogger<SectionsModel> _logger;

		public SectionsModel(ISectionService sectionService, ICommonDao commonDao, ModuleLoader moduleLoader,
			ILogger<SectionsModel> logger)
		{
			this._sectionService = sectionService;
			this._commonDao = commonDao;
			this._moduleLoader = moduleLoader;
			this._logger = logger;
		}

		public List<(Section Section, List<Template> Templates)> Sections { get; private set; }

		public void OnGet()
		{
			LoadSections();
		}

		public void OnPost(int deleteSectionId)
		{
			Section section = this._sectionService.GetSectionById(deleteSectionId);
			try
			{
				// First tell the module to remove its content.
				ModuleBase module = this._moduleLoader.GetModuleFromSection(section);
				if (module == null)
				{
					throw new InvalidOperationException($"The module {section.ModuleType.Name} of the section is not available.");
				}
				module.DeleteModuleContent();
				// Remove from all templates
				foreach (Template template in this._sectionService.GetTemplatesBySection(section))
				{
					string placeholder = template.Sections.FirstOrDefault(entry => entry.Value == section).Key;
					template.Sections.Remove(placeholder);
					this._commonDao.UpdateObject(template);
				}
				this._commonDao.DeleteObject(section);
			}
			catch (Exception ex)
			{
				ShowError(ex.Message);
				this._logger.LogError(ex, "Error deleting section {SectionId}.", section.Id);
			}
			LoadSections();
		}

		private void LoadSections()
		{
			Title = "Sections";
			Sections = new List<(Section, List<Template>)>();
			foreach (Section section in this._sectionService.GetUnconnectedSections())
			{
				IList templates = this._sectionService.GetTemplatesBySection(section);
				Sections.Add((section, templates.Cast<Template>().ToList()));
			}
		}
	}
}
