using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Cuyahoga.Web.Mvc
{
	/// <summary>
	/// Renders the public page of the request: its sections in the template. The site executes it.
	/// </summary>
	public class PublicPageResult : IActionResult
	{
		public Task ExecuteResultAsync(ActionContext context)
		{
			return context.HttpContext.RequestServices.GetRequiredService<IActionResultExecutor<PublicPageResult>>()
				.ExecuteAsync(context, this);
		}
	}
}
