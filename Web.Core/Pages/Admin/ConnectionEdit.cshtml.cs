using System.Collections;
using Cuyahoga.Core.Communication;
using Cuyahoga.Core.DataAccess;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Util;
using Cuyahoga.Core.Service;
using Cuyahoga.Core.Service.SiteStructure;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Web.Core.Pages.Admin
{
	/// <summary>
	/// Connect an outbound action of a section (IActionProvider) to a section whose module handles that action
	/// (IActionConsumer), for example a search input to a search section.
	/// </summary>
	public class ConnectionEditModel : AdminPageModel
	{
		private readonly ISectionService _sectionService;
		private readonly ICommonDao _commonDao;
		private readonly ModuleLoader _moduleLoader;
		private IActionProvider _actionProvider;

		public ConnectionEditModel(ISectionService sectionService, ICommonDao commonDao, ModuleLoader moduleLoader)
		{
			this._sectionService = sectionService;
			this._commonDao = commonDao;
			this._moduleLoader = moduleLoader;
		}

		[BindProperty] public string Action { get; set; }
		[BindProperty] public int SectionToId { get; set; }

		public Section Section
		{
			get { return ActiveSection; }
		}

		/// <summary>
		/// The outbound actions that aren't connected yet. Null when the module doesn't support connections.
		/// </summary>
		public List<string> Actions { get; private set; }

		/// <summary>
		/// The sections that can handle the chosen action.
		/// </summary>
		public List<Section> CompatibleSections { get; private set; } = new List<Section>();

		public IActionResult OnGet()
		{
			return Load() ? Page() : NotFound();
		}

		public IActionResult OnPost()
		{
			if (!Load())
			{
				return NotFound();
			}
			if (Request.Form.ContainsKey("btnBack"))
			{
				return RedirectToSectionEdit();
			}
			if (Request.Form.ContainsKey("btnSave") && this._actionProvider != null)
			{
				try
				{
					this.Section.Connections[Action] = this._sectionService.GetSectionById(SectionToId);
					this._commonDao.UpdateObject(this.Section);
					return RedirectToSectionEdit();
				}
				catch (Exception ex)
				{
					ShowError(ex.Message);
				}
			}
			// Otherwise another action was chosen: show the sections that can handle it.
			return Page();
		}

		private bool Load()
		{
			if (this.Section == null)
			{
				return false;
			}
			Title = "Add connection";
			this._actionProvider = this._moduleLoader.GetModuleFromSection(this.Section) as IActionProvider;
			if (this._actionProvider == null)
			{
				ShowError("The module that is connected to the section doesn't support outgoing connections.");
				return true;
			}
			Actions = new List<string>();
			foreach (ModuleAction action in this._actionProvider.GetOutboundActions())
			{
				// Only actions that are not connected yet.
				if (this.Section.Connections.GetValueOrDefault(action.Name) == null)
				{
					Actions.Add(action.Name);
				}
			}
			if (Action == null || !Actions.Contains(Action))
			{
				Action = Actions.FirstOrDefault();
			}
			LoadCompatibleSections();
			return true;
		}

		private void LoadCompatibleSections()
		{
			ModuleAction currentAction = this._actionProvider.GetOutboundActions().FindByName(Action ?? String.Empty);
			var compatibleModuleTypes = new ArrayList();
			foreach (ModuleType moduleType in this._commonDao.GetAll(typeof(ModuleType)))
			{
				if (this._moduleLoader.GetModuleFromType(moduleType) is IActionConsumer actionConsumer
					&& actionConsumer.GetInboundActions().Contains(currentAction))
				{
					compatibleModuleTypes.Add(moduleType);
				}
			}
			if (compatibleModuleTypes.Count > 0)
			{
				CompatibleSections = this._sectionService.GetSectionsByModuleTypes(compatibleModuleTypes).Cast<Section>().ToList();
			}
		}

		private IActionResult RedirectToSectionEdit()
		{
			return Redirect(ActiveNode != null
				? $"SectionEdit.aspx?NodeId={ActiveNode.Id}&SectionId={this.Section.Id}"
				: $"SectionEdit.aspx?SectionId={this.Section.Id}");
		}
	}
}
