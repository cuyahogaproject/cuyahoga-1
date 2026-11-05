using System;
using System.Web;
using System.Web.Security;

using Cuyahoga.Core.Service;
using Castle.Windsor;
using Cuyahoga.Web.Util;

namespace Cuyahoga.Web.UI
{
	/// <summary>
	/// Base class for all aspx pages in Cuyahoga (public and admin).
	/// </summary>
	public class CuyahogaPage : System.Web.UI.Page, ICuyahogaPage
	{
		private const string AntiXsrfTokenKey = "__AntiXsrfToken";

		private IWindsorContainer _container;

		/// <summary>
		/// A reference to the Windsor container that can be used as a Service Locator for service classes.
		/// </summary>
		protected IWindsorContainer Container
		{
			get { return this._container; }
		}

		/// <summary>
		/// Constructor.
		/// </summary>
		public CuyahogaPage()
		{
			this._container = ContainerAccessorUtil.GetContainer();
		}

		/// <summary>
		/// Protects postbacks against cross-site request forgery by tying the ViewState to a random
		/// per-browser token that is stored in a cookie. A forged postback from another site carries
		/// ViewState that was generated for a different token and fails validation.
		/// </summary>
		/// <param name="e"></param>
		protected override void OnInit(EventArgs e)
		{
			HttpCookie requestCookie = Request.Cookies[AntiXsrfTokenKey];
			Guid requestCookieGuidValue;
			if (requestCookie != null && Guid.TryParse(requestCookie.Value, out requestCookieGuidValue))
			{
				this.ViewStateUserKey = requestCookie.Value;
			}
			else
			{
				string token = Guid.NewGuid().ToString("N");
				this.ViewStateUserKey = token;

				HttpCookie responseCookie = new HttpCookie(AntiXsrfTokenKey, token);
				responseCookie.HttpOnly = true;
				responseCookie.Secure = FormsAuthentication.RequireSSL && Request.IsSecureConnection;
				Response.Cookies.Set(responseCookie);
			}
			base.OnInit(e);
		}
	}
}
