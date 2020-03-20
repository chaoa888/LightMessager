using LightMessager.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace LightMessager.Repository
{
    internal sealed class InMemoryRepository : IMessageRepository
    {
        private readonly ConcurrentDictionary<long, BaseMessage> _repository;

        public InMemoryRepository()
        {
            _repository = new ConcurrentDictionary<long, BaseMessage>();
        }

        public void Add(BaseMessage msg)
        {
            throw new NotImplementedException();
        }

        public void Add(List<BaseMessage> msgs)
        {
            throw new NotImplementedException();
        }

        public int GetCount()
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

        public bool Update(UpdateParam parameter)
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
    }
}
