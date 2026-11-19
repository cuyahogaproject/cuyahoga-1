using System;

namespace Cuyahoga.Core.Util
{
	/// <summary>
	/// Maps virtual paths of the WebForms site (~/files) to physical paths, for code that has no web context of its own
	/// (modules). The host sets <see cref="MapPath"/> at application start, like ContextStorage.Current.
	/// </summary>
	public static class ApplicationPaths
	{
		private static Func<string, string> _mapPath;

		/// <summary>
		/// Maps a virtual path (~/files) to a physical path.
		/// </summary>
		public static Func<string, string> MapPath
		{
			get
			{
				if (_mapPath == null)
				{
					throw new InvalidOperationException(
						"No path mapping for the application. Set ApplicationPaths.MapPath at application start, for example in Global.asax.");
				}
				return _mapPath;
			}
			set
			{
				if (value == null)
				{
					throw new ArgumentNullException(nameof(value));
				}
				_mapPath = value;
			}
		}
	}
}
