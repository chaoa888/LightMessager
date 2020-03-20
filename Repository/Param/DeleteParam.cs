using System.Collections.Generic;

namespace LightMessager.Repository
{
    public sealed class DeleteParam
    {
        public string MsgId;
        public List<string> MsgIds;
        public int? State;
    }
}
