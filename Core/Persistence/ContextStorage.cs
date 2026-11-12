using System.Collections;
using System.Web;

namespace Cuyahoga.Core.Persistence
{
	/// <summary>
	/// Stores objects for the current unit of work: the current HTTP request when there is one,
	/// otherwise the current thread.
	/// </summary>
	internal static class ContextStorage
	{
		[System.ThreadStatic]
		private static Hashtable _threadItems;

		public static object Get(string key)
		{
			HttpContext httpContext = HttpContext.Current;
			if (httpContext != null)
			{
				return httpContext.Items[key];
			}
			return _threadItems != null ? _threadItems[key] : null;
		}

		public static void Set(string key, object value)
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
