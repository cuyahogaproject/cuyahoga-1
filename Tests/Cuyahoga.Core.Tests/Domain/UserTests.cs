using System;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Util;
using Xunit;

namespace Cuyahoga.Core.Tests.Domain
{
	public class UserTests
	{
		[Theory]
		[InlineData("12345", true)]
		[InlineData("1234", false)]
		public void Passwords_need_at_least_5_characters(string password, bool valid)
		{
			Assert.Equal(valid, User.ValidatePassword(password));
		}

		[Fact]
		public void HashPassword_rejects_invalid_passwords()
		{
			Assert.Throws<ArgumentException>(() => User.HashPassword("1234"));
		}

		[Fact]
		public void CheckPassword()
		{
			var user = new User { Password = User.HashPassword("secret") };

			Assert.True(user.CheckPassword("secret"));
			Assert.False(user.CheckPassword("wrong"));
		}

		[Fact]
		public void CheckPassword_upgrades_a_legacy_hash()
		{
			// Old Cuyahoga versions stored unsalted MD5 hashes, also of passwords that are too short now.
			var user = new User { Password = Encryption.StringToMD5Hash("abc") };

			Assert.True(user.CheckPassword("abc"));
			Assert.StartsWith("PBKDF2$", user.Password);
			Assert.True(user.CheckPassword("abc"));
		}

		[Fact]
		public void CheckPassword_doesnt_upgrade_after_a_wrong_password()
		{
			string legacyHash = Encryption.StringToMD5Hash("secret");
			var user = new User { Password = legacyHash };

			Assert.False(user.CheckPassword("wrong"));
			Assert.Equal(legacyHash, user.Password);
		}

		[Fact]
		public void GeneratePassword_sets_the_hash_of_a_new_password()
		{
			var user = new User();

			string password = user.GeneratePassword();

			Assert.Equal(8, password.Length);
			Assert.True(user.CheckPassword(password));
		}

		[Fact]
		public void Permissions_come_from_the_roles()
		{
			var user = Build.User(Build.Editor());

			Assert.True(user.HasPermission(AccessLevel.Editor));
			Assert.True(user.HasPermission(AccessLevel.Authenticated));
			Assert.False(user.HasPermission(AccessLevel.Administrator));
		}

		[Fact]
		public void IsInRole_compares_id_and_name()
		{
			var user = Build.User(Build.Editor());

			Assert.True(user.IsInRole(Build.Editor()));
			Assert.True(user.IsInRole("Editor"));
			Assert.False(user.IsInRole(Build.Role(2, "Other name", AccessLevel.Editor)));
			Assert.False(user.IsInRole("Administrator"));
		}

		[Fact]
		public void CanView_and_CanEdit_follow_the_permissions_of_the_roles()
		{
			var editor = Build.Editor();
			var anonymous = Build.Anonymous();
			var node = Build.Node(1, "Home");
			var section = Build.Section(1, node, "main").Allow(editor, view: true, edit: true).Allow(anonymous, view: true);
			var editorUser = Build.User(editor);
			var otherUser = Build.User(Build.Role(3, "Authenticated user", AccessLevel.Authenticated));

			Assert.True(editorUser.CanView(section));
			Assert.True(editorUser.CanEdit(section));
			Assert.False(otherUser.CanView(section));
			Assert.False(otherUser.CanEdit(section));
		}
	}
}
