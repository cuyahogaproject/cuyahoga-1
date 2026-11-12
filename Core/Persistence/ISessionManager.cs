using NHibernate;

namespace Cuyahoga.Core.Persistence
{
	/// <summary>
	/// Provides the NHibernate session of the current unit of work (the current HTTP request or thread).
	/// </summary>
	public interface ISessionManager
	{
		/// <summary>
		/// Returns the session of the current unit of work, opening it when necessary. Closing or disposing
		/// the returned session has no effect: the session is closed at the end of the unit of work.
		/// </summary>
		ISession OpenSession();
	}
}
