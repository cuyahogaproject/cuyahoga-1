using System.Globalization;
using Cuyahoga.Core.Util;
using Cuyahoga.Modules.Downloads.Util;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Mvc;
using File = Cuyahoga.Modules.Downloads.Domain.File;

namespace Cuyahoga.Modules.Downloads.Mvc
{
	/// <summary>
	/// The list of files of a Downloads section that the user may download.
	/// </summary>
	public class DownloadsViewComponent : ModuleViewComponent<DownloadsModule>
	{
		protected override Task<IViewComponentResult> RenderAsync()
		{
			string sectionUrl = PublicUrls.GetUrlFromSection(Request, Module.Section);
			string imagesPath = PublicUrls.GetApplicationPath(Request) + "Modules/Downloads/Images/";
			var model = new DownloadsModel
			{
				ShowDetails = Module.ShowDateModified || Module.ShowPublisher || Module.ShowNumberOfDownloads,
				ShowDateModified = Module.ShowDateModified,
				ShowPublisher = Module.ShowPublisher,
				ShowNumberOfDownloads = Module.ShowNumberOfDownloads,
				PublishedByText = GetText("PUBLISHEDBY"),
				NumberOfDownloadsText = GetText("NUMBEROFDOWNLOADS"),
				Files = Module.GetAllFiles().Cast<File>()
					.Where(file => file.IsDownloadAllowed(PublicPage.User))
					.Select(file => new DownloadsModel.FileItem
					{
						Url = sectionUrl + "/download/" + file.Id.ToString(CultureInfo.InvariantCulture),
						IconUrl = imagesPath + FileTypesMap.GetIconFilename(Path.GetExtension(file.FilePath)),
						Details = file.FilePath + " (" + file.Size.ToString(CultureInfo.CurrentCulture) + " bytes)",
						Title = file.DisplayTitle,
						DateModified = TimeZoneUtil.AdjustDateToUserTimeZone(file.DatePublished, PublicPage.User).ToString(),
						Publisher = Module.ShowPublisher ? file.Publisher.FullName : null,
						NumberOfDownloads = file.NrOfDownloads
					})
					.ToList()
			};
			return Task.FromResult<IViewComponentResult>(View("~/Modules/Downloads/Downloads.cshtml", model));
		}
	}

	public class DownloadsModel
	{
		public IReadOnlyList<FileItem> Files { get; init; }
		public bool ShowDetails { get; init; }
		public bool ShowDateModified { get; init; }
		public bool ShowPublisher { get; init; }
		public bool ShowNumberOfDownloads { get; init; }
		public string PublishedByText { get; init; }
		public string NumberOfDownloadsText { get; init; }

		public class FileItem
		{
			public string Url { get; init; }
			public string IconUrl { get; init; }
			/// <summary>
			/// File name and size.
			/// </summary>
			public string Details { get; init; }
			public string Title { get; init; }
			public string DateModified { get; init; }
			public string Publisher { get; init; }
			public int NumberOfDownloads { get; init; }
		}
	}
}
