using System.Threading.Tasks;

namespace Slp.Indexer.Services
{
    public interface IIndexerService
    {
        Task SyncWithNetworkAsync();
    }
}
