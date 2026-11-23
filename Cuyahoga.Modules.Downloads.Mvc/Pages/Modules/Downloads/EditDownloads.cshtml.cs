using Cuyahoga.Web.Mvc;
using File = Cuyahoga.Modules.Downloads.Domain.File;

namespace Cuyahoga.Modules.Downloads.Mvc.Pages.Modules.Downloads
{
	/// <summary>
	/// The files of a Downloads section (EditDownloads.aspx in the WebForms site).
	/// </summary>
	public class EditDownloadsModel : ModuleAdminPageModel<DownloadsModule>
	{
		public IReadOnlyList<File> Files { get; private set; }

		public void OnGet()
		{
			Files = Module.GetAllFiles().Cast<File>().ToList();
		}
	}
}
