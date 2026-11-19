using Cuyahoga.Core.DataAccess;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service.SiteStructure;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Web.Core.Pages.Admin
{
	/// <summary>
	/// Attach a standalone section to a placeholder of a template.
	/// </summary>
	public class TemplateSectionModel : AdminPageModel
	{
		private readonly ICommonDao _commonDao;
		private readonly ISectionService _sectionService;

		public TemplateSectionModel(ICommonDao commonDao, ISectionService sectionService)
		{
			this._commonDao = commonDao;
			this._sectionService = sectionService;
		}

		[BindProperty(SupportsGet = true)] public int TemplateId { get; set; }
		[BindProperty(SupportsGet = true)] public string Placeholder { get; set; }
		[BindProperty] public int SectionId { get; set; }

		public Template Template { get; private set; }
		public List<Section> Sections { get; private set; } = new List<Section>();

		public IActionResult OnGet()
		{
			Load();
			return Page();
		}

		public IActionResult OnPost()
		{
			Load();
			if (Template == null)
			{
				return Page();
			}
			string templateUrl = "TemplateEdit.aspx?TemplateId=" + Template.Id;
			if (Request.Form.ContainsKey("btnBack"))
			{
				return Redirect(templateUrl);
			}
			Template.Sections[Placeholder] = this._sectionService.GetSectionById(SectionId);
			try
			{
				this._commonDao.UpdateObject(Template);
				return Redirect(templateUrl);
			}
			catch (Exception ex)
			{
				ShowError(ex.Message);
			}
			return Page();
		}

		private void Load()
		{
			Title = "Attach Section to Template Placeholder";
			if (TemplateId <= 0 || Placeholder == null)
			{
				ShowError("Passed invalid template or placeholder");
				return;
			}
			Template = (Template)this._commonDao.GetObjectById(typeof(Template), TemplateId);
			Sections = this._sectionService.GetUnconnectedSections().Cast<Section>().ToList();
		}
	}
}
