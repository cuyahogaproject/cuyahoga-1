using System;
using System.Web;
using Cuyahoga.Core.Persistence;

namespace Cuyahoga.Web.HttpModules
{
	/// <summary>
	/// Closes the NHibernate session of the request (session per request).
	/// </summary>
	public class NHibernateSessionModule : IHttpModule
	{
		public void Init(HttpApplication context)
		{
			context.EndRequest += new EventHandler(Context_EndRequest);
		}

		public void Dispose()
		{
		}

		private void Context_EndRequest(object sender, EventArgs e)
		{
			SessionManager.CloseCurrentSession();
		}
	}
}
