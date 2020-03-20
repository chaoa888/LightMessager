using System.Collections.Generic;

namespace LightMessager.Repository
{
    public sealed class SelectParam
    {
        public string MsgId;
        public List<string> MsgIds;
        public int? State;
    }
}
