using Slp.Common.Models;
using Slp.Common.Models.DbModels;
using NBitcoin;
using System;
using System.Threading.Tasks;

namespace Slp.Common.Interfaces
{
    public interface ISlpNotificationService
    {
        event Action<Block> OnNewBlock;
        event Action<Transaction> OnNewTransaction;
        Task RunAsync();

        void NotifySlpTransaction(SlpTransaction slpTransaction);
        void NotifySlpBlock(SlpBlock block);

        void NotifyHeartBeat(int progress);
    }
}
