using Cuyahoga.Core.Persistence;
using Microsoft.AspNetCore.Http;

namespace Cuyahoga.Web.Core.Persistence
{
	/// <summary>
	/// Closes the NHibernate session of the request when the request is done (like NHibernateSessionModule in the
	/// WebForms site). Sessions are opened on first use.
	/// </summary>
	public class NHibernateSessionMiddleware
	{
		private readonly RequestDelegate _next;

		public NHibernateSessionMiddleware(RequestDelegate next)
		{
			this._next = next;
		}

		public async Task InvokeAsync(HttpContext context)
		{
			try
			{
				await this._next(context);
			}
			finally
			{
				SessionManager.CloseCurrentSession();
			}
		}
	}
}
