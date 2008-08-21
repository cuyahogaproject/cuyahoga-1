using System;
using System.Collections;
using System.Configuration;
using System.Web;
using System.Web.Caching;

namespace Cuyahoga.Modules.Forum.Domain
{
	public class ForumUser
	{
		private int			_id;
		private DateTime	_dateCreated;
		private DateTime	_dateModified;
		private int			_userId;
		private string		_location;
		private string		_occupation;
		private string		_interest;
		private string		_homepage;
		private string		_msn;
		private string		_yahoomessenger;
		private string		_aimname;
		private string		_icqnumber;
		private string		_signature;
		private int			_gender;
		private int			_timezone;
		private string		_avartar;

		#region Properties
		public virtual int Id
		{
			get { return this._id; }
			set	{ this._id =  value; }
		}

		public virtual DateTime DateModified
		{
			get { return this._dateModified; }
			set { this._dateModified = value; }
		}

		public virtual DateTime DateCreated
		{
			get { return this._dateCreated; }
			set { this._dateCreated = value; }
		}

		public virtual int UserId
		{
			get { return this._userId; }
			set { this._userId = value; }
		}

		public virtual string Location
		{
			get { return this._location; }
			set { this._location = value; }
		}

		public virtual string Occupation
		{
			get { return this._occupation; }
			set { this._occupation = value; }
		}

		public virtual string Interest
		{
			get { return this._interest; }
			set { this._interest = value; }
		}

		public virtual string Homepage
		{
			get { return this._homepage; }
			set { this._homepage = value; }
		}

		public virtual string MSN
		{
			get { return this._msn; }
			set { this._msn = value; }
		}

		public virtual string YahooMessenger
		{
			get { return this._yahoomessenger; }
			set { this._yahoomessenger = value; }
		}

		public virtual string AIMName
		{
			get { return this._aimname; }
			set { this._aimname = value; }
		}

		public virtual string ICQNumber
		{
			get { return this._icqnumber; }
			set { this._icqnumber = value; }
		}

		public virtual string Signature
		{
			get { return this._signature; }
			set { this._signature = value; }
		}

		public virtual int Gender
		{
			get { return this._gender;}
			set { this._gender = value; }
		}

		public virtual int TimeZone
		{
			get { return this._timezone; }
			set { this._timezone = value; }
		}

		public virtual string Avartar
		{
			get { return this._avartar; }
			set { this._avartar = value; }
		}
	 		
		#endregion

		public ForumUser()
		{
			this._id				= -1;
			this._dateCreated		= DateTime.Now;
		}
	}
}
