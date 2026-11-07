using System;
using System.Text.RegularExpressions;
using System.Web;

namespace Cuyahoga.Modules.Articles.Domain
{
	/// <summary>
	/// Summary description for Comment.
	/// </summary>
	public class Comment
	{
		private int _id;
		private string _commentText;
		private Article _article;
		private Cuyahoga.Core.Domain.User _user;
		private string _name;
		private string _website;
		private string _userIp;
		private DateTime _updateTimestamp;

		#region properties

		/// <summary>
		/// Property Id (int)
		/// </summary>
		public virtual int Id
		{
			get { return this._id; }
			set { this._id = value; }
		}

		/// <summary>
		/// Property Article (Article)
		/// </summary>
		public virtual Article Article
		{
			get { return this._article; }
			set { this._article = value; }
		}

		/// <summary>
		/// Property User (Cuyahoga.Core.User)
		/// </summary>
		public virtual Cuyahoga.Core.Domain.User User
		{
			get { return this._user; }
			set { this._user = value; }
		}

		/// <summary>
		/// Property Name (string)
		/// </summary>
		public virtual string Name
		{
			get { return this._name; }
			set { this._name = value; }
		}

		/// <summary>
		/// Property Website (string)
		/// </summary>
		public virtual string Website
		{
			get { return this._website; }
			set { this._website = value; }
		}

		/// <summary>
		/// Property CommentText (string). HTML-encoded text with &lt;br/&gt; for line breaks.
		/// Use CreateCommentText() to create it from user input.
		/// </summary>
		public virtual string CommentText
		{
			get { return this._commentText; }
			set { this._commentText = value; }
		}

		/// <summary>
		/// The comment text that is safe to write to a page. Comments are stored as HTML, but older
		/// comments may contain HTML that wasn't stripped properly, so everything except the line
		/// breaks is (re-)encoded.
		/// </summary>
		public virtual string CommentTextHtml
		{
			get
			{
				if (this._commentText == null)
				{
					return String.Empty;
				}
				string[] lines = Regex.Split(this._commentText, @"<br\s*/?>", RegexOptions.IgnoreCase);
				for (int i = 0; i < lines.Length; i++)
				{
					lines[i] = HttpUtility.HtmlEncode(HttpUtility.HtmlDecode(lines[i]));
				}
				return String.Join("<br/>", lines);
			}
		}

		/// <summary>
		/// Create the stored comment text from user input.
		/// </summary>
		/// <param name="text">The comment as entered by the user</param>
		/// <returns></returns>
		public static string CreateCommentText(string text)
		{
			string normalizedText = text.Replace("\r\n", "\n").Replace("\r", "\n");
			return HttpUtility.HtmlEncode(normalizedText).Replace("\n", "<br/>");
		}

		/// <summary>
		/// Property UserIp (string)
		/// </summary>
		public virtual string UserIp
		{
			get { return this._userIp; }
			set { this._userIp = value; }
		}

		/// <summary>
		/// Property UpdateTimestamp (DateTime)
		/// </summary>
		public virtual DateTime UpdateTimestamp
		{
			get { return this._updateTimestamp; }
			set { this._updateTimestamp = value; }
		}

		#endregion
		
		/// <summary>
		/// Default constructor.
		/// </summary>
		public Comment()
		{
			this._id = -1;
		}

	}
}
