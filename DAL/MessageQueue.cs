using System;

namespace LightMessager.DAL
{
	/// <summary>
	/// LightMessager专用消息落地表实体
	/// </summary>
	[Serializable]
	public class MessageQueue
	{
		public MessageQueue()
		{}
		private int _id;
		private ulong _knuthhash;
		private string _msgcontent;
		private bool _canberemoved;
		private short _retrycount;
		private DateTime? _lastretrytime;
		private DateTime _createdtime;

		/// <summary>
		/// Id
		/// </summary>
		public int Id
		{
			set { _id = value; }
			get { return _id; }
		}

		/// <summary>
		/// KnuthHash
		/// </summary>
		public ulong KnuthHash
		{
			set { _knuthhash = value; }
			get { return _knuthhash; }
		}

		/// <summary>
		/// MsgContent
		/// </summary>
		public string MsgContent
		{
			set { _msgcontent = value; }
			get { return _msgcontent; }
		}

		/// <summary>
		/// CanBeRemoved
		/// </summary>
		public bool CanBeRemoved
		{
			set { _canberemoved = value; }
			get { return _canberemoved; }
		}

		/// <summary>
		/// ExecuteCount
		/// </summary>
		public short RetryCount
		{
			set { _retrycount = value; }
			get { return _retrycount; }
		}

		/// <summary>
		/// LastExecuteTime
		/// </summary>
		public DateTime? LastRetryTime
		{
			set { _lastretrytime = value; }
			get { return _lastretrytime; }
		}

		/// <summary>
		/// CreatedTime
		/// </summary>
		public DateTime CreatedTime
		{
			set { _createdtime = value; }
			get { return _createdtime; }
		}
	}
}
