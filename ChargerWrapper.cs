using System.Net.Sockets;

namespace PSI_BOUDA
{
    class LogicEx : Exception
    {
        public const string NO_FULL_POWER_MSG = "Didn't rec full power";

        public LogicEx(string msg) : base(msg) { }
    }

    class ChargerWrapper : NetworkWrapper
    {
        public ChargerWrapper(TcpClient client) : base(client) { }

        public ChargerWrapper(NetworkWrapper networkProvider) : base(networkProvider) { }
        private const string FULL_POWER_MSG = "FULL POWER";

        private const int TIMEOUT_RECH = 5000;

        private const int len = 12;

        private void SleepFP()
        {
            base.Rec(len, out string response);
            if (response != FULL_POWER_MSG)
            {
                throw new LogicEx(LogicEx.NO_FULL_POWER_MSG);
            }
        }

        public override int Rec(int maxLength, out string result)
        {
            if (base.Rec(maxLength, out result).Equals(0))
            {
                return 0;
            }
            SetRecTimeOut(TIMEOUT_RECH);
            try
            {
                SleepFP();
            }
            catch (LogicEx e)
            {
                new StatusWrapper(this).LogicError();
                throw e;
            }
            ResetRecTimeOut();

            base.Rec(maxLength, out result);
            return 0;
        }



    }
}
