using System.Net.Sockets;
using System.Text;

namespace PSI_BOUDA
{
    public class NetworkWrapper
    {

        protected readonly StreamReader sReader;

        private readonly TcpClient klient;

        protected readonly StreamWriter sWriter;

        private const int TIME_OUT = 1000;

        private readonly Encoding encoding = Encoding.ASCII;

        public NetworkWrapper(TcpClient client)
        {
            this.klient = client;
            this.klient.ReceiveTimeout = TIME_OUT;

            sReader = new StreamReader(this.klient.GetStream(), encoding);
            sWriter = new StreamWriter(this.klient.GetStream(), encoding);
            sWriter.AutoFlush = true;
        }

        public NetworkWrapper(NetworkWrapper other)
        {
            klient = other.klient;
            sReader = other.sReader;
            sWriter = other.sWriter;
            klient.ReceiveTimeout = other.klient.ReceiveTimeout;
            sWriter.AutoFlush = other.sWriter.AutoFlush;
        }

        public void Send(string msg)
        {
            sWriter.Write(msg);
        }

        protected void ResetRecTimeOut()
        {
            klient.ReceiveTimeout = TIME_OUT;
        }
        public virtual int Rec(int maxLength, out string result)
        {
            int state = StartRec(maxLength, out result);

            if (result == "RECHARGING" && state == 2)
            {
                return 1;
            }
            if (state == 2)
            {
                return 0;
            }
            if (result.Contains("REC"))
            {
                if (StartRec(12 - 3, out string possibleRecharging) == 2)
                {
                    result += possibleRecharging;
                    return 1;
                }

            }

            throw new InvalidResponseEx(InvalidResponseEx.MSG_END_WRONG_ST);
        }

        private int StartRec(int maxLen, out string res)
        {
            int flag = 0;
            char ch;
            res = "";

            for (int j = 0; j < maxLen; ++j)
            {
                if (flag == 2)
                {
                    break;
                }
                ch = (char)sReader.Read();
                switch (ch)
                {
                    case '\b':
                        if (flag.Equals(1))
                        {
                            flag = 2;
                        }
                        res += ch;
                        break;
                    case '\a':
                        if (flag.Equals(0))
                        {
                            flag = 1;
                        }
                        res += ch;
                        break;
                    default:
                        flag = 0;
                        res += ch;
                        break;
                }
            }
            if (flag.Equals(2))
            {
                res = res.Substring(0, res.Length - 2);
            }

            return flag;
        }

        protected void SetRecTimeOut(int timeout)
        {
            klient.ReceiveTimeout = timeout;
        }


        public void close()
        {
            klient.Close();
        }

    }
}
