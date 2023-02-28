using System.Net.Sockets;

namespace PSI_BOUDA
{
    public class Client
    {
        public static void Proces(object cli)
        {
            var client = new Client((TcpClient)cli);
            client.Go();
        }
        public readonly NetworkWrapper netWrapper;

        public Client(TcpClient client)
        {
            netWrapper = new NetworkWrapper(client);
        }

        private void Go()
        {
            try
            {
                var auth = new Authenticator(this);
                auth.Go();
                new Mover(this).Go();
                auth.logout();
                netWrapper.close();
            }
            catch (Exception)
            {
                netWrapper.close();
            }
        }

    }
}
