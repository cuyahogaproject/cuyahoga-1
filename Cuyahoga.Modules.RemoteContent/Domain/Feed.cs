using System;
using System.Collections;

using Cuyahoga.Core.Domain;

namespace Cuyahoga.Modules.RemoteContent.Domain
{
	/// <summary>
	/// An rss feed to be consumed.
	/// </summary>
	public class Feed
	{
		private int _id;
		private string _url;
		private string _title;
		private DateTime _pubDate;
		private int _numberOfItems;
		private DateTime _updateTimestamp;
		private Section _section;
		private IList _feedItems;

		/// <summary>
		/// Property Id (int)
		/// </summary>
		public virtual int Id
		{
			get { return this._id; }
			set { this._id = value; }
		}

		/// <summary>
		/// Property Url (string)
		/// </summary>
		public virtual string Url
		{
			get { return this._url; }
			set { this._url = value; }
		}

		/// <summary>
		/// Property Title (string)
		/// </summary>
		public virtual string Title
		{
			get { return this._title; }
			set { this._title = value; }
		}

		/// <summary>
		/// Property PubDate (DateTime)
		/// </summary>
		public virtual DateTime PubDate
		{
			get { return this._pubDate; }
			set { this._pubDate = value; }
		}

		/// <summary>
		/// Property NumberOfItems (int)
		/// </summary>
		public virtual int NumberOfItems
		{
			get { return this._numberOfItems; }
			set { this._numberOfItems = value; }
		}

		/// <summary>
		/// Property UpdateTimestamp (DateTime)
		/// </summary>
		public virtual DateTime UpdateTimestamp
		{
			get { return this._updateTimestamp; }
			set { this._updateTimestamp = value; }
		}

		/// <summary>
		/// Property Section (Section)
		/// </summary>
		public virtual Section Section
		{
			get { return this._section; }
			set { this._section = value; }
		}

		/// <summary>
		/// Property Items (IList)
		/// </summary>
		public virtual IList FeedItems
		{
			get { return this._feedItems; }
			set { this._feedItems = value; }
		}

		/// <summary>
		/// Default constructor.
		/// </summary>
		public Feed()
		{
			this._id = -1;
			this._numberOfItems = 5;
		}

		/// <summary>
		/// Sort the items by date and truncate the number of feed items if it exceeds the 
		/// NumberOfItems property.
		/// </summary>
		public virtual void SortAndFilter()
		{
			FeedItem[] feedItemArray = new FeedItem[this._feedItems.Count];
			this._feedItems.CopyTo(feedItemArray, 0);
			Array.Sort(feedItemArray);
			// Little dirty: clear FeedItems and add the first NumberOfItems from the sorted array.
			this._feedItems.Clear();
			int i = 0;
			while (i < feedItemArray.Length && i < this._numberOfItems)
			{
				this._feedItems.Add(feedItemArray[i]);
				i++;
			}
		}
	}
}
