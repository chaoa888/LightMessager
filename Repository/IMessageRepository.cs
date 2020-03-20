using LightMessager.Model;
using System.Collections.Generic;

namespace LightMessager.Repository
{
    // 说明：
    // 这里有点小纠结的，设想中一个in-memory repo，一个db repo。
    // in-memory是天生支持linq表达式的，update，get这些操作会很好写；
    // 但是到了db上面就需要一个中间翻译，这块网上有思路但是都比较重并且
    // 想要降低开销可能还得需要自己做很多适配修改。所以最终还是选择了较
    // 笨的办法，主要就是简单易写，后面可以考虑使用predicate to sql的方式。
    //
    // links：
    // https://stackoverflow.com/questions/7731905/how-to-convert-an-expression-tree-to-a-partial-sql-query
    // https://github.com/microorm-dotnet/MicroOrm.Dapper.Repositories
    public interface IMessageRepository
    {
        // 增
        void Add(BaseMessage msg);
        void Add(List<BaseMessage> msgs);
        int GetCount();
        // 删
        void Clear();
        void Delete(DeleteParam parameter);
        // 改
        bool Update(UpdateParam parameter);
        // 查
        BaseMessage GetModel(SelectParam parameter);
        List<BaseMessage> GetModels(SelectParam parameter);
    }
}
