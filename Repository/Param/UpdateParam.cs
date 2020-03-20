using System;
using System.Collections.Generic;

namespace LightMessager.Repository
{
    public sealed class UpdateParam
    {
        public string MsgId;
        public List<string> MsgIds;
        public int OldState;
        public int NewState;
        public bool Increment_Republish;
        public bool Increment_Requeue;
        public string Remark;
        public DateTime ModifyTime;
    }
}
