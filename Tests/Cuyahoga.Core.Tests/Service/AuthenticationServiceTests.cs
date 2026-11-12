using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cuyahoga.Core.DataAccess;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service.Membership;
using Cuyahoga.Core.Util;
using Xunit;

namespace Cuyahoga.Core.Tests.Service
{
	public class AuthenticationServiceTests
	{
		/// <summary>
		/// In-memory IUserDao: finds users by name and records saves.
		/// </summary>
		private class FakeUserDao : IUserDao
		{
			private readonly List<User> _users;
			public readonly List<User> Saved = new List<User>();

			public FakeUserDao(params User[] users) => _users = users.ToList();

			public User GetUserByUsername(string username) => _users.FirstOrDefault(u => u.UserName == username);
			public void SaveOrUpdateUser(User user) => Saved.Add(user);

			public User GetUserByUsernameAndEmail(string username, string email) => throw new NotImplementedException();
			public IList FindUsersByUsername(string searchString) => throw new NotImplementedException();
			public void DeleteUser(User user) => throw new NotImplementedException();
		}

		private static User UserWithPassword(string userName, string passwordHash) =>
			new User { UserName = userName, Password = passwordHash };

		[Fact]
		public void Valid_login_returns_the_user_and_records_the_login()
		{
			var user = UserWithPassword("ann", User.HashPassword("secret"));
			var dao = new FakeUserDao(user);

			var result = new DefaultAuthenticationService(dao).AuthenticateUser("ann", "secret", "10.0.0.1");

			Assert.Same(user, result);
			Assert.Equal("10.0.0.1", user.LastIp);
			Assert.InRange(user.LastLogin.Value, DateTime.Now.AddMinutes(-1), DateTime.Now);
			Assert.Equal(new[] { user }, dao.Saved);
		}

		[Fact]
		public void Wrong_password_or_unknown_user_returns_null()
		{
			var dao = new FakeUserDao(UserWithPassword("ann", User.HashPassword("secret")));
			var service = new DefaultAuthenticationService(dao);

			Assert.Null(service.AuthenticateUser("ann", "wrong", "10.0.0.1"));
			Assert.Null(service.AuthenticateUser("bob", "secret", "10.0.0.1"));
			Assert.Empty(dao.Saved);
		}

		[Fact]
		public void Login_saves_the_upgraded_hash_of_a_legacy_password()
		{
			var user = UserWithPassword("ann", Encryption.StringToMD5Hash("secret"));
			var dao = new FakeUserDao(user);

			new DefaultAuthenticationService(dao).AuthenticateUser("ann", "secret", "10.0.0.1");

			var saved = Assert.Single(dao.Saved);
			Assert.StartsWith("PBKDF2$", saved.Password);
		}
	}
}
