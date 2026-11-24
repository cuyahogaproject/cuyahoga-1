using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Web.Core.PublicSite
{
	/// <summary>
	/// The error page (Error.aspx of the WebForms site): unhandled errors, and urls that aren't anything.
	/// </summary>
	public class ErrorController : Controller
	{
		private readonly ILogger<ErrorController> _logger;

		public ErrorController(ILogger<ErrorController> logger)
		{
			this._logger = logger;
		}

		/// <summary>
		/// An unhandled error (the exception handler of the site).
		/// </summary>
		[Route("Error.aspx")]
		public IActionResult Index()
		{
			Exception exception = HttpContext.Features.Get<IExceptionHandlerFeature>()?.Error;
			if (exception == null)
			{
				return ErrorPage(StatusCodes.Status200OK, "Something strange happened...", null);
			}
			this._logger.LogError(exception, "An unexpected error occured");
			return ErrorPage(StatusCodes.Status500InternalServerError, "500 An error occured:", exception.Message);
		}

		/// <summary>
		/// A url that isn't a page or a file of the site.
		/// </summary>
		public IActionResult NotFoundPage()
		{
			return ErrorPage(StatusCodes.Status404NotFound, "404 Page not found", "The requested page could not be found.");
		}

		internal static IActionResult ErrorPage(int statusCode, string title, string message)
		{
			return new ViewResult
			{
				ViewName = "~/Views/Error/Error.cshtml",
				StatusCode = statusCode,
				ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(
					new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),
					new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary())
				{
					Model = new ErrorModel(title, message)
				}
			};
		}
	}

	public record ErrorModel(string Title, string Message);
}
