using System;
using System.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace Cuyahoga.Core.Util
{
	/// <summary>
	/// Helper class that contains static methods for data encryption.
	/// </summary>
	public class Encryption
	{
		private const string PasswordHashPrefix = "PBKDF2";
		// The iteration count is stored with every hash. Raising it upgrades existing hashes at the next login.
		private const int PasswordHashIterations = 100000;
		private const int PasswordSaltSize = 16;
		private const int PasswordHashSize = 32;

		private Encryption()
		{
		}

		/// <summary>
		/// Calculates the MD5 of a given string.
		/// </summary>
		/// <param name="inputString"></param>
		/// <returns>The (hexadecimal) string representatation of the MD5 hash.</returns>
		/// <remarks>
		/// Only used to verify legacy password hashes. Don't use for new password hashes.
		/// </remarks>
		public static string StringToMD5Hash(string inputString)
		{
			MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
			byte[] encryptedBytes = md5.ComputeHash(Encoding.ASCII.GetBytes(inputString));
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < encryptedBytes.Length; i++)
			{
				sb.AppendFormat("{0:x2}", encryptedBytes[i]);
			}
			return sb.ToString();
		}

		/// <summary>
		/// Creates a salted PBKDF2 (HMAC-SHA256) hash of the given password.
		/// </summary>
		/// <param name="password">The password in clear text</param>
		/// <returns>The hash in the format PBKDF2$iterations$salt$hash (salt and hash Base64 encoded).</returns>
		public static string CreatePasswordHash(string password)
		{
			byte[] salt = new byte[PasswordSaltSize];
			using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
			{
				rng.GetBytes(salt);
			}
			byte[] hash = Pbkdf2(password, salt, PasswordHashIterations, PasswordHashSize);
			return String.Format("{0}${1}${2}${3}", PasswordHashPrefix, PasswordHashIterations
				, Convert.ToBase64String(salt), Convert.ToBase64String(hash));
		}

		/// <summary>
		/// Verifies a password against a stored hash. Supports PBKDF2 hashes and legacy (unsalted MD5) hashes.
		/// </summary>
		/// <param name="password">The password in clear text</param>
		/// <param name="storedHash">The stored password hash</param>
		/// <returns></returns>
		public static bool VerifyPasswordHash(string password, string storedHash)
		{
			if (password == null || String.IsNullOrEmpty(storedHash))
			{
				return false;
			}
			if (storedHash.StartsWith(PasswordHashPrefix + "$"))
			{
				string[] parts = storedHash.Split('$');
				int iterations;
				if (parts.Length != 4 || ! Int32.TryParse(parts[1], out iterations) || iterations <= 0)
				{
					return false;
				}
				byte[] salt;
				byte[] expectedHash;
				try
				{
					salt = Convert.FromBase64String(parts[2]);
					expectedHash = Convert.FromBase64String(parts[3]);
				}
				catch (FormatException)
				{
					return false;
				}
				byte[] actualHash = Pbkdf2(password, salt, iterations, expectedHash.Length);
				return FixedTimeEquals(actualHash, expectedHash);
			}
			// Legacy hash
			byte[] legacyActual = Encoding.ASCII.GetBytes(StringToMD5Hash(password));
			byte[] legacyExpected = Encoding.ASCII.GetBytes(storedHash.ToLowerInvariant());
			return FixedTimeEquals(legacyActual, legacyExpected);
		}

		/// <summary>
		/// Indicates if the stored hash was created with an older or weaker algorithm and should be
		/// replaced with a new hash (after a successful verification).
		/// </summary>
		/// <param name="storedHash"></param>
		/// <returns></returns>
		public static bool PasswordHashNeedsUpgrade(string storedHash)
		{
			if (String.IsNullOrEmpty(storedHash) || ! storedHash.StartsWith(PasswordHashPrefix + "$"))
			{
				return true;
			}
			string[] parts = storedHash.Split('$');
			int iterations;
			return parts.Length != 4 || ! Int32.TryParse(parts[1], out iterations) || iterations < PasswordHashIterations;
		}

		private static byte[] Pbkdf2(string password, byte[] salt, int iterations, int length)
		{
			// PBKDF2 with HMAC-SHA256. The Rfc2898DeriveBytes constructor that takes the hash algorithm is missing in
			// netstandard2.0; KeyDerivation gives the same result on every target.
			return KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA256, iterations, length);
		}

		private static bool FixedTimeEquals(byte[] a, byte[] b)
		{
			if (a.Length != b.Length)
			{
				return false;
			}
			int diff = 0;
			for (int i = 0; i < a.Length; i++)
			{
				diff |= a[i] ^ b[i];
			}
			return diff == 0;
		}
	}
}
