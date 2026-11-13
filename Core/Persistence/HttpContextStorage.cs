#if NETFRAMEWORK
using System.Collections;
using System.Web;

namespace Cuyahoga.Core.Persistence
{
	/// <summary>
	/// Stores the objects of the current unit of work in the current HTTP request (System.Web), or in the current
	/// thread when there is no request (background work, tests).
	/// </summary>
	public class HttpContextStorage : IContextStorage
	{
		[System.ThreadStatic]
		private static Hashtable _threadItems;

		public object Get(string key)
		{
			HttpContext httpContext = HttpContext.Current;
			if (httpContext != null)
			{
				return httpContext.Items[key];
			}
			return _threadItems != null ? _threadItems[key] : null;
		}

		public void Set(string key, object value)
		{
			HttpContext httpContext = HttpContext.Current;
			IDictionary items = httpContext != null ? httpContext.Items : (_threadItems ?? (_threadItems = new Hashtable()));
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
#endif
