using System.Collections;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service.SiteStructure;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Web.Core.Pages.Admin
{
	/// <summary>
	/// The menu of the site administration (Navigation.ascx in the WebForms site): the sites with their node tree and
	/// the links to the other admin sections.
	/// </summary>
	public class AdminNavigationViewComponent : ViewComponent
	{
		private readonly ISiteService _siteService;

		public AdminNavigationViewComponent(ISiteService siteService)
		{
			this._siteService = siteService;
		}

		public IViewComponentResult Invoke()
		{
			IList sites = this._siteService.GetAllSites();
			return View(sites.Cast<Site>().ToList());
		}
	}
}
