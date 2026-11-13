using System;

namespace Cuyahoga.Core.Persistence
{
	/// <summary>
	/// The storage for the objects of the current unit of work. The default stores them per web request on
	/// .NET Framework (<see cref="HttpContextStorage"/>) and per async flow elsewhere
	/// (<see cref="AsyncLocalContextStorage"/>). A host can set its own storage at application start.
	/// </summary>
	public static class ContextStorage
	{
#if NETFRAMEWORK
		private static IContextStorage _current = new HttpContextStorage();
#else
		private static IContextStorage _current = new AsyncLocalContextStorage();
#endif

		/// <summary>
		/// The storage in use. Set it at application start, before the first unit of work.
		/// </summary>
		public static IContextStorage Current
		{
			get { return _current; }
			set
			{
				if (value == null)
				{
					throw new ArgumentNullException(nameof(value));
				}
				_current = value;
			}
		}

		internal static object Get(string key)
		{
			return _current.Get(key);
		}

		internal static void Set(string key, object value)
		{
			_current.Set(key, value);
		}
	}
}
