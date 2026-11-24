using Cuyahoga.Web.Core.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Cuyahoga.Web.Core.Pages.Admin
{
	/// <summary>
	/// The "Log out" link of the admin header: logs out and goes back to the page, which then asks for a login.
	/// </summary>
	public class LogoutModel : PageModel
	{
		public IActionResult OnGet()
		{
			return NotFound();
		}

		public async Task<IActionResult> OnPostAsync(string returnUrl)
		{
			await CuyahogaAuthentication.SignOutAsync(HttpContext);
			return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "~/Admin/Default.aspx");
		}
	}
}
