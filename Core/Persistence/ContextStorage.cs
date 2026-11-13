using System;

namespace Cuyahoga.Core.Persistence
{
	/// <summary>
	/// The storage for the objects of the current unit of work. There is no default: the host sets it at
	/// application start (the web site does this in Global.asax), so a missing storage fails at the first
	/// unit of work instead of silently mixing up sessions.
	/// </summary>
	public static class ContextStorage
	{
		private static IContextStorage _current;

		/// <summary>
		/// The storage in use. Set it at application start, before the first unit of work.
		/// </summary>
		public static IContextStorage Current
		{
			get
			{
				if (_current == null)
				{
					throw new InvalidOperationException(
						"No context storage for the unit of work. Set ContextStorage.Current at application start, for example in Global.asax.");
				}
				return _current;
			}
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
			return Current.Get(key);
		}

		internal static void Set(string key, object value)
		{
			Current.Set(key, value);
		}
	}
}
