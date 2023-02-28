using System.Net;
using System.Net.Sockets;
using PSI_BOUDA;

const int PORT = 3999;
var listener = new TcpListener(IPAddress.Any, PORT);
listener.Start();

while (true)
{
    var client = listener.AcceptTcpClient();

    var t = new Thread(new ParameterizedThreadStart(Client.Proces));
    t.Start(client);
}
