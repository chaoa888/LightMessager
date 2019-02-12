using System;

namespace LightMessager.DAL
{
    /// <summary>
    /// 实体
    /// </summary>
    [Serializable]
	internal class MessageQueue
	{
		public MessageQueue()
		{}
        private long _id;
		private ulong _knuthhash;
		private string _msgcontent;
		private bool _canberemoved;
		private short _executecount;
		private DateTime _lastexecutetime;
		private DateTime _createdtime;

        /// <summary>
		/// 
		/// </summary>
		public long Id
        {
            set { _id = value; }
            get { return _id; }
        }

        /// <summary>
        /// 
        /// </summary>
        public ulong KnuthHash
		{
			set { _knuthhash = value; }
			get { return _knuthhash; }
		}

		/// <summary>
		/// 
		/// </summary>
		public string MsgContent
		{
			set { _msgcontent = value; }
			get { return _msgcontent; }
		}

		/// <summary>
		/// 
		/// </summary>
		public bool CanBeRemoved
		{
			set { _canberemoved = value; }
			get { return _canberemoved; }
		}

		/// <summary>
		/// 
		/// </summary>
		public short ExecuteCount
		{
			set { _executecount = value; }
			get { return _executecount; }
		}

		/// <summary>
		/// 
		/// </summary>
		public DateTime LastExecuteTime
		{
			set { _lastexecutetime = value; }
			get { return _lastexecutetime; }
		}

		/// <summary>
		/// 
		/// </summary>
		public DateTime CreatedTime
		{
			set { _createdtime = value; }
			get { return _createdtime; }
		}
	}
}
