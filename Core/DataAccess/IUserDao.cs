using System;
using System.Collections;

using Cuyahoga.Core.Domain;

namespace Cuyahoga.Core.DataAccess
{
	/// <summary>
	/// Provides data access for user-related components.
	/// </summary>
	public interface IUserDao
	{
		/// <summary>
		/// Get a single user by username. Use User.CheckPassword() to verify the password.
		/// </summary>
		/// <param name="username"></param>
		/// <returns></returns>
		User GetUserByUsername(string username);

		/// <summary>
		/// Get a single user by username and email.
		/// </summary>
		/// <param name="username"></param>
		/// <param name="email"></param>
		/// <returns></returns>
		User GetUserByUsernameAndEmail(string username, string email);

		/// <summary>
		/// Find users by username (begins with).
		/// </summary>
		/// <param name="searchString"></param>
		/// <returns></returns>
		IList FindUsersByUsername(string searchString);

		/// <summary>
		/// Save or update a user.
		/// </summary>
		/// <param name="user"></param>
		void SaveOrUpdateUser(User user);

		/// <summary>
		/// Delete a user.
		/// </summary>
		/// <param name="user"></param>
		void DeleteUser(User user);

	}
}
