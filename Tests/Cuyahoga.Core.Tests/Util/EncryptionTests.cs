using Cuyahoga.Core.Util;
using Xunit;

namespace Cuyahoga.Core.Tests.Util
{
	public class EncryptionTests
	{
		[Fact]
		public void Password_hash_is_salted_pbkdf2_that_verifies()
		{
			string hash = Encryption.CreatePasswordHash("secret");

			Assert.StartsWith("PBKDF2$100000$", hash);
			Assert.NotEqual(hash, Encryption.CreatePasswordHash("secret"));
			Assert.True(Encryption.VerifyPasswordHash("secret", hash));
			Assert.False(Encryption.VerifyPasswordHash("Secret", hash));
			Assert.False(Encryption.PasswordHashNeedsUpgrade(hash));
		}

		[Fact]
		public void Legacy_md5_hash_verifies_and_needs_an_upgrade()
		{
			string legacyHash = Encryption.StringToMD5Hash("secret");

			Assert.Equal("5ebe2294ecd0e0f08eab7690d2a6ee69", legacyHash);
			Assert.True(Encryption.VerifyPasswordHash("secret", legacyHash));
			Assert.True(Encryption.VerifyPasswordHash("secret", legacyHash.ToUpperInvariant()));
			Assert.False(Encryption.VerifyPasswordHash("other", legacyHash));
			Assert.True(Encryption.PasswordHashNeedsUpgrade(legacyHash));
		}

		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData("PBKDF2$not-a-number$c2FsdA==$aGFzaA==")]
		[InlineData("PBKDF2$1000$not base64$aGFzaA==")]
		[InlineData("PBKDF2$1000")]
		public void Malformed_hashes_dont_verify(string storedHash)
		{
			Assert.False(Encryption.VerifyPasswordHash("secret", storedHash));
		}

		[Fact]
		public void Hash_with_fewer_iterations_needs_an_upgrade()
		{
			Assert.True(Encryption.PasswordHashNeedsUpgrade("PBKDF2$1000$c2FsdA==$aGFzaA=="));
		}
	}
}
