using Cuyahoga.Core.Util;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Cuyahoga.Web.Core.Services
{
	/// <summary>
	/// Where the files of the site are (the virtual paths ~/... of Cuyahoga, ApplicationPaths.MapPath), and which of them
	/// the site serves:
	/// - ~/Admin, ~/Images, ~/Support, ~/Templates: the public files in wwwroot.
	/// - ~/Modules, ~/Install: the install scripts and other files of the site and the modules, in the folder of the site
	///   (the modules deploy theirs into Modules when they're built). Only Modules/{module}/Images is served.
	/// - everything else (~/files, ~/index, ~/UserFiles): the data of the site, in the data folder (the DataPath setting,
	///   absolute or relative to the site; App_Data by default). Only UserFiles is served (the files of the editor).
	/// </summary>
	public static class SitePaths
	{
		private static readonly string[] PublicFolders = { "Admin", "Images", "Support", "Templates" };
		private static readonly string[] SiteFolders = { "Modules", "Install" };

		/// <summary>
		/// Sets ApplicationPaths.MapPath, and returns the data folder.
		/// </summary>
		public static string Configure(WebApplicationBuilder builder)
		{
			string contentRoot = builder.Environment.ContentRootPath;
			string webRoot = builder.Environment.WebRootPath ?? Path.Combine(contentRoot, "wwwroot");
			string dataPath = Path.GetFullPath(Path.Combine(contentRoot, builder.Configuration["DataPath"] ?? "App_Data"));
			Directory.CreateDirectory(Path.Combine(dataPath, "UserFiles"));
			ApplicationPaths.MapPath = virtualPath =>
			{
				string path = (virtualPath ?? String.Empty).TrimStart('~', '/').Replace('/', Path.DirectorySeparatorChar);
				string firstFolder = path.Split(Path.DirectorySeparatorChar)[0];
				string root = PublicFolders.Contains(firstFolder, StringComparer.OrdinalIgnoreCase) ? webRoot
					: SiteFolders.Contains(firstFolder, StringComparer.OrdinalIgnoreCase) ? contentRoot
					: dataPath;
				return Path.Combine(root, path);
			};
			return dataPath;
		}

		/// <summary>
		/// Serves wwwroot, the files of the editor (/UserFiles) and the images of the modules (/Modules/*/Images).
		/// </summary>
		public static void UseSiteFiles(this WebApplication app, string dataPath)
		{
			app.UseStaticFiles();
			app.UseStaticFiles(new StaticFileOptions
			{
				RequestPath = "/UserFiles",
				FileProvider = new PhysicalFileProvider(Path.Combine(dataPath, "UserFiles"))
			});
			string modulesPath = Path.Combine(app.Environment.ContentRootPath, "Modules");
			if (Directory.Exists(modulesPath))
			{
				app.UseStaticFiles(new StaticFileOptions
				{
					RequestPath = "/Modules",
					FileProvider = new ModuleImagesFileProvider(new PhysicalFileProvider(modulesPath))
				});
			}
		}

		/// <summary>
		/// Only the files in {module}/Images/ (not the install scripts or e-mail templates of the modules).
		/// </summary>
		private sealed class ModuleImagesFileProvider : IFileProvider
		{
			private readonly IFileProvider _inner;

			public ModuleImagesFileProvider(IFileProvider inner)
			{
				this._inner = inner;
			}

			public IFileInfo GetFileInfo(string subpath)
			{
				string[] parts = subpath.Trim('/').Split('/');
				return parts.Length >= 3 && parts[1].Equals("Images", StringComparison.OrdinalIgnoreCase)
					? this._inner.GetFileInfo(subpath)
					: new NotFoundFileInfo(subpath);
			}

			public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;

			public IChangeToken Watch(string filter) => NullChangeToken.Singleton;
		}
	}
}
