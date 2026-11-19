using Cuyahoga.Core.DataAccess;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Util;
using Cuyahoga.Web.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Web.Core.Pages.Admin
{
	/// <summary>
	/// Attach a standalone section to placeholders of templates (the section then shows on every page with that
	/// template).
	/// </summary>
	public class SectionAttachTemplateModel : AdminPageModel
	{
		private readonly ICommonDao _commonDao;
		private readonly TemplatePlaceholderProvider _placeholderProvider;

		public SectionAttachTemplateModel(ICommonDao commonDao, TemplatePlaceholderProvider placeholderProvider)
		{
			this._commonDao = commonDao;
			this._placeholderProvider = placeholderProvider;
		}

		public Section Section
		{
			get { return ActiveSection; }
		}

		/// <summary>
		/// Per template: the placeholders that are free or taken by this section, and the one of this section (if
		/// attached).
		/// </summary>
		public List<(Template Template, List<string> Placeholders, string AttachedPlaceholder)> Templates { get; private set; }

		public async Task<IActionResult> OnGetAsync()
		{
			if (this.Section == null)
			{
				return NotFound();
			}
			await LoadTemplatesAsync();
			return Page();
		}

		public async Task<IActionResult> OnPostAsync(List<int> attached)
		{
			if (this.Section == null)
			{
				return NotFound();
			}
			if (Request.Form.ContainsKey("btnBack"))
			{
				return Redirect("Sections.aspx");
			}
			try
			{
				foreach (Template template in GetTemplates())
				{
					string selectedPlaceholder = Request.Form["placeholder_" + template.Id];
					string attachedPlaceholder = GetAttachedPlaceholder(template);
					if (attached.Contains(template.Id))
					{
						if (attachedPlaceholder != null && attachedPlaceholder != selectedPlaceholder)
						{
							template.Sections.Remove(attachedPlaceholder);
						}
						template.Sections[selectedPlaceholder] = this.Section;
					}
					else if (attachedPlaceholder != null)
					{
						template.Sections.Remove(attachedPlaceholder);
					}
					this._commonDao.UpdateObject(template);
				}
				return Redirect("Sections.aspx");
			}
			catch (Exception ex)
			{
				ShowError(ex.InnerException != null ? ex.Message + " " + ex.InnerException.Message : ex.Message);
			}
			await LoadTemplatesAsync();
			return Page();
		}

		private List<Template> GetTemplates()
		{
			return this._commonDao.GetAll(typeof(Template), "Name").Cast<Template>().ToList();
		}

		private string GetAttachedPlaceholder(Template template)
		{
			return template.Sections.FirstOrDefault(entry => entry.Value == this.Section).Key;
		}

		private async Task LoadTemplatesAsync()
		{
			Title = "Attach section to template(s)";
			Templates = new List<(Template, List<string>, string)>();
			foreach (Template template in GetTemplates())
			{
				var placeholders = new List<string>();
				foreach (string placeholder in await this._placeholderProvider.GetPlaceholdersAsync(template.Id))
				{
					// Placeholders with another section are taken.
					Section section = template.Sections.GetValueOrDefault(placeholder);
					if (section == null || section == this.Section)
					{
						placeholders.Add(placeholder);
					}
				}
				Templates.Add((template, placeholders, GetAttachedPlaceholder(template)));
			}
		}
	}
}
