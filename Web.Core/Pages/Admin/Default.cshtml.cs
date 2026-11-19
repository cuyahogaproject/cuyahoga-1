namespace Cuyahoga.Web.Core.Pages.Admin
{
	/// <summary>
	/// The home page of the site administration.
	/// </summary>
	public class DefaultModel : AdminPageModel
	{
		public void OnGet()
		{
			// The WebForms page had no title of its own and showed the default title of the admin section.
			Title = "Cuyahoga Site Administration";
		}
	}
}
