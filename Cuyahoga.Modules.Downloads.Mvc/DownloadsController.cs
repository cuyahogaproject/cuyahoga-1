using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NHibernate;
using File = Cuyahoga.Modules.Downloads.Domain.File;

namespace Cuyahoga.Modules.Downloads.Mvc
{
	/// <summary>
	/// Requests for a Downloads section: {id}/section.aspx/download/{fileId} downloads a file, everything else shows
	/// the page with the list of files.
	/// </summary>
	public class DownloadsController : ModuleController<DownloadsModule>
	{
		[HttpGet]
		[HttpHead]
		public IActionResult Index()
		{
			if (Module.CurrentAction != DownloadsModuleActions.Download || Module.CurrentFileId <= 0)
			{
				return PublicPageView();
			}

			File file = Module.GetFileById(Module.CurrentFileId);
			try
			{
				if (!file.IsDownloadAllowed(PublicPage.User))
				{
					return StatusCode(StatusCodes.Status403Forbidden);
				}
			}
			catch (ObjectNotFoundException)
			{
				// GetFileById returns a proxy that is loaded on first use.
				return NotFound();
			}

			Stream contents;
			try
			{
				contents = Module.GetFileAsStream(file, PublicPage.User);
			}
			catch (FileNotFoundException)
			{
				return NotFound();
			}
			// Range requests resume interrupted downloads.
			FileStreamResult download = File(contents, file.ContentType ?? "application/octet-stream", file.FilePath,
				enableRangeProcessing: true);
			return new CountedDownloadResult(download, () => Module.IncreaseNrOfDownloads(file));
		}

		/// <summary>
		/// Sends a file and counts the download when the whole response has been sent (not for HEAD requests or when
		/// the browser broke off the download).
		/// </summary>
		private class CountedDownloadResult : IActionResult
		{
			private readonly FileStreamResult _download;
			private readonly Action _countDownload;

			public CountedDownloadResult(FileStreamResult download, Action countDownload)
			{
				this._download = download;
				this._countDownload = countDownload;
			}

			public async Task ExecuteResultAsync(ActionContext context)
			{
				await this._download.ExecuteResultAsync(context);
				HttpContext httpContext = context.HttpContext;
				if (!HttpMethods.IsHead(httpContext.Request.Method) && !httpContext.RequestAborted.IsCancellationRequested
					&& httpContext.Response.StatusCode < 400)
				{
					this._countDownload();
				}
			}
		}
	}
}
