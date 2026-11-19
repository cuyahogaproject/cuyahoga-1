using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service.SiteStructure;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Web.Core.Pages.Admin
{
	/// <summary>
	/// The menu of the site administration (Navigation.ascx in the WebForms site): the sites with their node tree,
	/// expanded to the active node, and the links to the other admin sections.
	/// </summary>
	public class AdminNavigationViewComponent : ViewComponent
	{
		private readonly ISiteService _siteService;

		public AdminNavigationViewComponent(ISiteService siteService)
		{
			this._siteService = siteService;
		}

		public IViewComponentResult Invoke(Node activeNode)
		{
			return View(new AdminNavigationModel(this._siteService.GetAllSites().Cast<Site>().ToList(), activeNode));
		}
	}

	public record AdminNavigationModel(List<Site> Sites, Node ActiveNode);
}
