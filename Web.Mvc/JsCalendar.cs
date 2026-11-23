using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;

namespace Cuyahoga.Web.Mvc
{
	/// <summary>
	/// A date (and time) field with the popup calendar of ~/Support/JsCalendar, with the markup of the Calendar server
	/// control of the WebForms site (Cuyahoga.ServerControls.Calendar): a text box, a calendar image that opens the
	/// popup, and the scripts. The date is in the format of the current culture (short date and time).
	/// </summary>
	public static class JsCalendar
	{
		private static readonly string[] Languages =
		{
			"af", "br", "ca", "da", "de", "du", "el", "en", "es", "fi", "fr", "hr", "hu", "it", "jp", "ko", "lt", "nl", "no",
			"pl", "pt", "ro", "ru", "sl", "si", "sk", "sp", "sv", "tr", "zh"
		};

		/// <summary>
		/// The stylesheet and scripts of the calendar, for the head of the page (once per page).
		/// </summary>
		public static IHtmlContent Head(HttpRequest request)
		{
			string supportDir = GetSupportDir(request);
			string language = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
			if (Array.IndexOf(Languages, language) < 0)
			{
				language = "en";
			}
			var html = new HtmlContentBuilder();
			html.AppendHtml("<style type=\"text/css\">@import url(").Append(supportDir + "calendar-system.css").AppendHtml(");</style>\n");
			foreach (string script in new[] { "calendar.js", "calendar-setup.js", $"lang/calendar-{language}.js" })
			{
				html.AppendHtml("<script language=\"JavaScript\" src=\"").Append(supportDir + script).AppendHtml("\"></script>\n");
			}
			return html;
		}

		/// <summary>
		/// The date field.
		/// </summary>
		/// <param name="request">The request.</param>
		/// <param name="id">The id of the control (the text box is {id}_dateTextBox, the image {id}_trigger).</param>
		/// <param name="name">The name of the text box in the form.</param>
		/// <param name="text">The text of the text box: a date from <see cref="Format"/>, or what was posted.</param>
		/// <param name="displayTime">With the time.</param>
		public static IHtmlContent Field(HttpRequest request, string id, string name, string text, bool displayTime)
		{
			string textBoxId = id + "_dateTextBox";
			string format = ConvertDateFormat(CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern);
			if (displayTime)
			{
				format += " " + ConvertTimeFormat(CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern);
			}
			var html = new HtmlContentBuilder();
			html.AppendHtml("<span id=\"").Append(id).AppendHtml("\">");
			html.AppendHtml("<input name=\"").Append(name).AppendHtml("\" type=\"text\"");
			if (!String.IsNullOrEmpty(text))
			{
				html.AppendHtml(" value=\"").Append(text).AppendHtml("\"");
			}
			html.AppendHtml(" id=\"").Append(textBoxId).AppendHtml("\" />");
			html.AppendHtml("<img id=\"").Append(id + "_trigger").AppendHtml("\" align=\"top\" hspace=\"4\" src=\"")
				.Append(GetSupportDir(request) + "cal.gif").AppendHtml("\" style=\"border-width:0px;\" />");
			html.AppendHtml("</span>");
			html.AppendHtml("<script type=\"text/javascript\">Calendar.setup( { inputField : \"")
				.AppendHtml(JavaScriptEncoder.Default.Encode(textBoxId))
				.AppendHtml("\", ifFormat : \"").AppendHtml(JavaScriptEncoder.Default.Encode(format))
				.AppendHtml("\", button : \"").AppendHtml(JavaScriptEncoder.Default.Encode(id))
				.AppendHtml("\", showsTime : ").AppendHtml(displayTime ? "true" : "false").AppendHtml(" } ); </script>");
			return html;
		}

		/// <summary>
		/// The text of a date in the field.
		/// </summary>
		public static string Format(DateTime value, bool displayTime)
		{
			return displayTime
				? value.ToShortDateString() + " " + value.ToShortTimeString()
				: value.ToShortDateString();
		}

		/// <summary>
		/// The date of the posted text, or null when it is empty or not a date.
		/// </summary>
		public static DateTime? Parse(string text)
		{
			return !String.IsNullOrWhiteSpace(text) && DateTime.TryParse(text, CultureInfo.CurrentCulture,
				DateTimeStyles.None, out DateTime value) ? value : null;
		}

		private static string GetSupportDir(HttpRequest request)
		{
			return PublicUrls.GetApplicationPath(request) + "Support/JsCalendar/";
		}

		private static string ConvertDateFormat(string shortDateFormat)
		{
			string format = ReplaceFormatCharacter(shortDateFormat, "y", "%Y");
			format = ReplaceFormatCharacter(format, "M", "%m");
			return ReplaceFormatCharacter(format, "d", "%d");
		}

		private static string ConvertTimeFormat(string shortTimeFormat)
		{
			string format = ReplaceFormatCharacter(shortTimeFormat, "H", "%H");
			format = ReplaceFormatCharacter(format, "m", "%M");
			format = ReplaceFormatCharacter(format, "h", "%I");
			return format.Replace("tt", "%p");
		}

		/// <summary>
		/// Replaces 1 to 4 times the character with the format of the calendar.
		/// </summary>
		private static string ReplaceFormatCharacter(string format, string from, string to)
		{
			return Regex.Replace(format, from + "{1,4}", to);
		}
	}
}
