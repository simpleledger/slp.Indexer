namespace Slp.Common.DataAccess
{
    public class DbSlpIdManager
    {
        public long currentSlpTransactionId;
        public long currentSlpTransactionInputId;
        public long currentSlpTransactionOutputId;

        public DbSlpIdManager(long slpTransactionId, long slpTransactionInputId, long slpTransactionOutputId)
        {
            currentSlpTransactionId = slpTransactionId;
            currentSlpTransactionInputId = slpTransactionInputId;
            currentSlpTransactionOutputId = slpTransactionOutputId;
        }
        public long GetNextSlpTransactionId()
        {
            currentSlpTransactionId += 1;
            return currentSlpTransactionId;
        }

        public long GetNextSlpTransactionInputId()
        {
            currentSlpTransactionInputId += 1;
            return currentSlpTransactionInputId;
        }

        public long GetNextSlpTransactionOutputId()
        {
            currentSlpTransactionOutputId += 1;
            return currentSlpTransactionOutputId;
        }
    }
}
