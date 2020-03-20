using System;
using System.Collections.Generic;
using System.Text;
using LightMessager.Model;

namespace LightMessager.Repository
{
    internal sealed class DbRepository : IMessageRepository
    {
        public void Add(BaseMessage msg)
        {
            throw new NotImplementedException();
        }

        public void Add(List<BaseMessage> msgs)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public void Delete(DeleteParam parameter)
        {
            throw new NotImplementedException();
        }

        public int GetCount()
        {
            throw new NotImplementedException();
        }

        public BaseMessage GetModel(SelectParam parameter)
        {
            throw new NotImplementedException();
        }

        public List<BaseMessage> GetModels(SelectParam parameter)
        {
            throw new NotImplementedException();
        }

        public bool Update(UpdateParam parameter)
        {
            throw new NotImplementedException();
        }
    }
}
