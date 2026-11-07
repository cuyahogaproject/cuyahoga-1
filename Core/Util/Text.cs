using System;
using System.Text.RegularExpressions;
using System.Web;

namespace Cuyahoga.Core.Util
{
	/// <summary>
	/// The Text class contains helper methods for text manipulation.
	/// </summary>
	public class Text
	{
		private Text()
		{
		}

		/// <summary>
		/// Truncate a given text to the given number of characters. 
		/// Also any embedded html is stripped.
		/// </summary>
		/// <param name="fullText"></param>
		/// <param name="numberOfCharacters"></param>
		/// <returns></returns>
		public static string TruncateText(string fullText, int numberOfCharacters)
		{
			string text;
			if (fullText.Length > numberOfCharacters)
			{
				int spacePos = fullText.IndexOf(" ", numberOfCharacters);
				if (spacePos > -1)
				{
					text = fullText.Substring(0, spacePos) + "...";
				}
				else
				{
					text = fullText;
				}
			}
			else
			{
				text = fullText;
			}
			Regex regexStripHTML = new Regex("<[^>]+>",RegexOptions.IgnoreCase|RegexOptions.Compiled);
			text = regexStripHTML.Replace(text, " ");
			return text;
		}

		/// <summary>
		/// Check if a URL is safe to use in a link or image that is created from user input. Only
		/// http, https, ftp and mailto URLs and relative URLs are allowed (no javascript:, data:, etc.).
		/// </summary>
		/// <param name="url">The URL (not HTML-encoded)</param>
		/// <returns></returns>
		public static bool IsSafeUrl(string url)
		{
			if (url == null)
			{
				return false;
			}
			// Browsers ignore whitespace and control characters in the scheme ("java\tscript:"), so do we.
			string normalizedUrl = Regex.Replace(url, @"[\s\x00-\x1f\x7f]", String.Empty);
			if (normalizedUrl.Length == 0)
			{
				return false;
			}
			int colonPos = normalizedUrl.IndexOf(':');
			if (colonPos == -1)
			{
				return true;
			}
			// A colon after the first /, ? or # isn't part of a scheme, so it's a relative URL.
			int firstDelimiterPos = normalizedUrl.IndexOfAny(new char[] { '/', '?', '#' });
			if (firstDelimiterPos > -1 && firstDelimiterPos < colonPos)
			{
				return true;
			}
			string scheme = normalizedUrl.Substring(0, colonPos).ToLowerInvariant();
			return scheme == "http" || scheme == "https" || scheme == "ftp" || scheme == "mailto";
		}

		/// <summary>
		/// Remove all html from the given text, including the contents of script and style elements.
		/// HTML entities are decoded, so the result is plain text that has to be encoded before
		/// writing it to a page.
		/// </summary>
		/// <param name="html"></param>
		/// <returns></returns>
		public static string StripHtml(string html)
		{
			if (html == null)
			{
				return null;
			}
			string text = Regex.Replace(html, @"<(script|style)\b.*?</\1\s*>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
			text = Regex.Replace(text, @"<!--.*?-->", " ", RegexOptions.Singleline);
			// Only tag-like constructs, so a plain "a < b" survives.
			text = Regex.Replace(text, @"<[a-zA-Z/!?][^>]*>?", " ");
			text = HttpUtility.HtmlDecode(text);
			return Regex.Replace(text, @"\s{2,}", " ").Trim();
		}

		/// <summary>
		/// Ensure that the given string has a trailing slash.
		/// </summary>
		/// <param name="stringThatNeedsTrailingSlash"></param>
		/// <returns></returns>
		public static string EnsureTrailingSlash(string stringThatNeedsTrailingSlash)
		{
			if (! stringThatNeedsTrailingSlash.EndsWith("/"))
			{
				return stringThatNeedsTrailingSlash + "/";
			}
			else
			{
				return stringThatNeedsTrailingSlash;
			}
		}
	}
}
