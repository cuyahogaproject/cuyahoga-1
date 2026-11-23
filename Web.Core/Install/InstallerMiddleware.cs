using Cuyahoga.Web.Core.PublicSite;

namespace Cuyahoga.Web.Core.Install
{
	/// <summary>
	/// Sends page requests to the installer while the database isn't installed, and during an upgrade the admin pages
	/// to the upgrade page and the public pages to "Sorry" (CheckInstaller in Global.asax of the WebForms site).
	/// </summary>
	public class InstallerMiddleware
	{
		private readonly RequestDelegate _next;

		public InstallerMiddleware(RequestDelegate next)
		{
			this._next = next;
		}

		public async Task InvokeAsync(HttpContext context, InstallationState installationState)
		{
			string path = context.Request.Path.Value ?? "/";
			if (IsPage(path) && !path.StartsWith("/Install/", StringComparison.OrdinalIgnoreCase)
				&& !path.Equals("/Login.aspx", StringComparison.OrdinalIgnoreCase))
			{
				if (!installationState.IsInstalled())
				{
					if (InstallationState.CreateCoreInstaller().CanInstall)
					{
						context.Response.Redirect(context.Request.PathBase + "/Install/Install.aspx");
						return;
					}
				}
				else if (installationState.NeedsUpgrade())
				{
					bool isAdmin = path.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase);
					context.Response.Redirect(context.Request.PathBase + (isAdmin ? "/Install/Upgrade.aspx" : "/Install/Sorry.aspx"));
					return;
				}
			}
			await this._next(context);
		}

		/// <summary>
		/// A page (not a stylesheet, image or script): .aspx, or no extension (a folder).
		/// </summary>
		private static bool IsPage(string path)
		{
			string extension = Path.GetExtension(path);
			return extension.Length == 0 || extension.Equals(".aspx", StringComparison.OrdinalIgnoreCase);
		}
	}
}
