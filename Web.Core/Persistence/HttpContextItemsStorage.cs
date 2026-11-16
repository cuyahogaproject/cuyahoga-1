using System.Collections.Generic;
using System.Threading;
using Cuyahoga.Core.Persistence;
using Microsoft.AspNetCore.Http;

namespace Cuyahoga.Web.Core.Persistence
{
	/// <summary>
	/// Keeps the NHibernate session and transaction of the unit of work in HttpContext.Items, so they live for one
	/// request. Outside a request (background work) they live in the current async flow.
	/// </summary>
	/// <remarks>
	/// HttpContext.Items and not an AsyncLocal for requests: a session that is opened in an awaited call is then still
	/// visible to <see cref="NHibernateSessionMiddleware"/>, which closes it at the end of the request.
	/// </remarks>
	public class HttpContextItemsStorage : IContextStorage
	{
		private static readonly AsyncLocal<Dictionary<string, object>> flowItems = new AsyncLocal<Dictionary<string, object>>();
		private readonly IHttpContextAccessor _httpContextAccessor;

		public HttpContextItemsStorage(IHttpContextAccessor httpContextAccessor)
		{
			this._httpContextAccessor = httpContextAccessor;
		}

		public object Get(string key)
		{
			HttpContext httpContext = this._httpContextAccessor.HttpContext;
			if (httpContext != null)
			{
				return httpContext.Items.TryGetValue(key, out object value) ? value : null;
			}
			Dictionary<string, object> items = flowItems.Value;
			return items != null && items.TryGetValue(key, out object flowValue) ? flowValue : null;
		}

		public void Set(string key, object value)
		{
			IDictionary<object, object> requestItems = this._httpContextAccessor.HttpContext?.Items;
			if (requestItems != null)
			{
				if (value == null)
				{
					requestItems.Remove(key);
				}
				else
				{
					requestItems[key] = value;
				}
				return;
			}
			Dictionary<string, object> items = flowItems.Value ??= new Dictionary<string, object>();
			if (value == null)
			{
				items.Remove(key);
			}
			else
			{
				items[key] = value;
			}
		}
	}
}
