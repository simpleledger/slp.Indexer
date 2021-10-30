using System;
using System.Collections.Generic;
using System.Text;

namespace Slp.Common.Utility
{
    public static class SD
    {
        public static readonly byte[] SlpLokadIdHex = { 0x53, 0x4c, 0x50, 0x00 }; //by protocol S L P 0
        public const int MessageSize = 1000;
        public const int HashHexSize = 64;
        public const int HashSize = 32;
        public const int AddressSize = 128;
        public const int WifSize = 128;
        public const int InvalidReasonLength = 128;
        public const string UnparsedAddress = "Unparsed address";
        public const string AnnotationLargeInteger = "decimal (38, 0)";
        public const int MaxOpReturnSize = 223;

        public const byte OP_RETURN = 0x6A;
        public const decimal SLPTransactionBchDustAmmount546 = 546;
        public const int SlpMaxAllowedOutputs19 = 19;

        public const int HeartBeatInMiliseconds = 1000;
        public const int TimeConsumingQueryTimeoutSeconds = 120; // Application settings defaut values - also serves to replace magic strings in code
        // From which block slp block fetching should start 
        public const int StartFromBlock = 0;

        public const string BchNodeType = "Mainnet";
        public const string BchNodeUser = "bitcoin";
        public const string BchNodePassword = "password";
        public const string BchNodeUrl = "http://localhost:8332";
        public const string ZmqSubscribeAddress = "tcp://localhost:28332";
        public const string ZmqPublishAddress = "tcp://localhost:28339";
        public const int RPCWorkerCount = 16; //meaning 16 simultaneus rpc worker will prefetch blocks
        public const int RpcBlockPrefetchLimit = 500; //meaning maximum of 500 block will be prefetched using different workers
        public const string NotificationWebhookUrl = "";
        public const int DbCommitBatchSize = 100; //commit to database every 100 transactions

        public enum DatabaseBackendType { POSTGRESQL, MSSQL};
        public const DatabaseBackendType DatabaseBackend = DatabaseBackendType.POSTGRESQL;
    }
}
