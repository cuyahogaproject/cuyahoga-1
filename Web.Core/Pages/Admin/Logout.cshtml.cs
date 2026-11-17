using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Cuyahoga.Web.Core.Pages.Admin
{
	/// <summary>
	/// The "Log out" link of the admin header. Removes the Forms authentication cookie of the WebForms site (like
	/// FormsAuthentication.SignOut) and goes back to the page, which then asks for a login.
	/// </summary>
	public class LogoutModel : PageModel
	{
		public IActionResult OnGet()
		{
			return NotFound();
		}

		public IActionResult OnPost(string returnUrl)
		{
			Response.Cookies.Delete("CuyahogaUser", new CookieOptions { Path = "/", HttpOnly = true });
			return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "~/Admin/Default.aspx");
		}
	}
}
