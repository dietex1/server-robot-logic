namespace PSI_BOUDA
{

    class IdOutOfBoundsEx : Exception
    {
        public IdOutOfBoundsEx(string msg) : base(msg) { }

        public static readonly string KEY_ID_OUT_OF_BOUNDS = "Key id is not in <0;{0}> range";
    }

    class WorkEndEx : Exception
    {
        public static readonly string INVALID_END_OF_WORK = "Invalid end of work";

        public WorkEndEx(string msg) : base(msg) { }

        public WorkEndEx() : base(INVALID_END_OF_WORK) { }
    }

    class InvalidResponseEx : Exception
    {
        public InvalidResponseEx(string msg) : base(msg) { }

        public static readonly string INV_KEY_ID = "Invalid key ID";

        public static readonly string INV_COORDINATES = "Invalid Cordinates";

        public static readonly string MSG_END_WRONG_ST = "Wrong flag after rec incoming message";
    }

    public class Authenticator
    {

        public Authenticator(Client client)
        {
            networkWrapper = new ChargerWrapper(client.netWrapper);
            statusWrapper = new StatusWrapper(networkWrapper);
        }
        private readonly string KEY_REQUEST = "107 KEY REQUEST\a\b";

        private readonly string CONFIRMATION = "{0}\a\b";

        private readonly string LOGOUT = "106 LOGOUT\a\b";

        private readonly NetworkWrapper networkWrapper;

        private readonly StatusWrapper statusWrapper;

        private readonly Tuple<short, short>[] keys = {
            new Tuple<short, short>(23019, 32037),
            new Tuple<short, short>(32037, 29295),
            new Tuple<short, short>(18789, 13603),
            new Tuple<short, short>(16443, 29533),
            new Tuple<short, short>(18189, 21952)
        };

        private string getClientUsername()
        {
            networkWrapper.Rec(20, out string response);
            return response;
        }

        private ushort Hash(string str)
        {
            ushort hash = 0;
            foreach (char c in str)
            {
                hash += c;
            }
            hash *= 1000;

            return hash;
        }

        public void logout()
        {
            networkWrapper.Send(LOGOUT);
        }

        public void Go()
        {
            try
            {
                string user = getClientUsername();
                networkWrapper.Send(KEY_REQUEST);

                int clientKey = getClientKeyId();
                if (clientKey < 0 || clientKey > keys.Length - 1)
                {
                    throw new IdOutOfBoundsEx(string.Format(
                        IdOutOfBoundsEx.KEY_ID_OUT_OF_BOUNDS, keys.Length - 1));
                }
                ushort usernameHash = Hash(user);
                ushort serverAccCode = addServerKeyToHash(clientKey, usernameHash);
                networkWrapper.Send(string.Format(CONFIRMATION, serverAccCode));

                ushort cliAccCode = getClientConfirmation();
                ushort validCliAccCode = addClientKeyToHash(clientKey, usernameHash);
                if (cliAccCode.Equals(validCliAccCode))
                {
                    statusWrapper.Ok();
                    return;
                }
                else throw new UnauthorizedAccessException();
            }
            catch (UnauthorizedAccessException)
            {
                statusWrapper.LoginFailed();
            }
            catch (IdOutOfBoundsEx)
            {
                statusWrapper.KeyOutOfRangeError();
            }
            catch (InvalidResponseEx)
            {
                statusWrapper.SyntaxError();
            }

            throw new WorkEndEx();
        }

        private int getClientKeyId()
        {
            networkWrapper.Rec(5, out string response);
            if (response.Contains(' ')) throw new InvalidResponseEx(InvalidResponseEx.INV_KEY_ID);
            int keyId;
            try
            {
                keyId = int.Parse(response);
            }
            catch (FormatException)
            {
                throw new InvalidResponseEx(InvalidResponseEx.INV_KEY_ID);
            }
            return keyId;
        }

        private ushort addServerKeyToHash(int keyId, ushort hash)
        {
            return (ushort)(hash + keys[keyId].Item1);
        }

        private ushort addClientKeyToHash(int keyId, ushort hash)
        {
            return (ushort)(hash + keys[keyId].Item2);
        }

        private ushort getClientConfirmation()
        {
            networkWrapper.Rec(7, out string response);
            if (response.Contains(' ')) throw new InvalidResponseEx(InvalidResponseEx.INV_KEY_ID);

            return ushort.Parse(response);
        }




    }
}
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
using System.Text.RegularExpressions;

namespace PSI_BOUDA
{
    public class Mover
    {

        class Coordinates
        {
            public int y;
            public int x;

            public Coordinates(int x, int y)
            {
                this.y = y;
                this.x = x;
            }

            public bool Equals(Coordinates other)
            {
                return x == other.x && y == other.y;
            }
        }

        private static readonly string MOVE = "102 MOVE\a\b";

        private static readonly string TURN_RIGHT = "104 TURN RIGHT\a\b";

        class Direct
        {

            public const int DOWN = 2;
            public const int LEFT = 3;
            public const int NONE = 4;
            public const int UP = 0;
            public const int RIGHT = 1;


            public Direct(int state)
            {
                if (state < 0 || state > 4)
                {
                    throw new ArgumentException();
                }
                State = state;
            }

            public void Next()
            {
                Increment();
                if (State.Equals(NONE)) Increment();
            }

            public int State { get; set; }

            private void Decrement()
            {
                if (State.Equals(0))
                {
                    State = 4;
                    return;
                }
                State--;
            }

            private void Increment()
            {
                State = (State + 1) % 5;
            }

            public void Previous()
            {
                Decrement();
                if (State == NONE)
                {
                    Decrement();
                }
            }


            public bool Equals(Direct other)
            {
                return State.Equals(other.State);
            }

            public bool Equals(int state)
            {
                return State.Equals(state);
            }

        }

        public Mover(Client client)
        {
            networkWrapper = new ChargerWrapper(client.netWrapper);
            statusWrapper = new StatusWrapper(networkWrapper);
        }

        private readonly NetworkWrapper networkWrapper;

        private readonly StatusWrapper statusWrapper;

        private static readonly string TURN_LEFT = "103 TURN LEFT\a\b";

        private static readonly string OK_REGEX = @"^OK -?\d+ -?\d+$";

        private Coordinates coordinates;

        private Direct direction;

        public void Go()
        {
            try
            {
                Coordinates oldCoordinates;
                Move();
                oldCoordinates = coordinates;
                Move();
                GetDirection(oldCoordinates);

                if (hasReachedCentr())
                {
                    return;
                }

                if (direction.Equals(Direct.NONE))
                {
                    UrgentDirDetection();
                }

                switch (GetQuadrant())
                {
                    case 1:
                        while (!direction.Equals(Direct.DOWN)) turnRight();
                        break;
                    case 2:
                        while (!direction.Equals(Direct.DOWN)) turnRight();
                        break;
                    case 3:
                        while (!direction.Equals(Direct.UP)) turnRight();
                        break;
                    case 4:
                        while (!direction.Equals(Direct.UP)) turnRight();
                        break;
                    case -1:
                        FixxingDirection();
                        break;
                    default:
                        break;
                }


                Coordinates oldCoords2;
                while (!isAxis())
                {
                    oldCoords2 = coordinates;
                    Move();
                    if (coordinates.Equals(oldCoords2))
                    {
                        MoveThroughBar(false);
                        continue;
                    }
                }

                if (hasReachedCentr()) return;

                FixxingDirection();


                Coordinates oldCoords3;
                while (coordinates.x != 0 || coordinates.y != 0)
                {
                    oldCoords3 = coordinates;
                    Move();
                    if (coordinates.Equals(oldCoords3))
                    {
                        MoveThroughBar(true);
                        continue;
                    }
                }

                networkWrapper.Send("105 GET MESSAGE\a\b");
                networkWrapper.Rec(100, out string result);

                return;
            }
            catch (InvalidResponseEx)
            {
                statusWrapper.SyntaxError();
            }
            throw new WorkEndEx();
        }

        private void MoveThroughBar(bool wasOnAxis)
        {
            turnRight();
            Move();
            if (isAxis() && !wasOnAxis) return;
            turnLeft();
            Move();
            if (isAxis() && !wasOnAxis) return;

            Move();
            if (isAxis() && !wasOnAxis) return;
            turnLeft();
            Move();
            if (isAxis() && !wasOnAxis) return;
            turnRight();
        }

        private void FixxingDirection()
        {
            if (!coordinates.x.Equals(0) && !coordinates.y.Equals(0)) throw new ArgumentException();

            if (coordinates.x.Equals(0))
            {
                if (coordinates.y > 0)
                    while (!direction.Equals(Direct.DOWN)) turnRight();
                if (coordinates.y < 0)
                    while (!direction.Equals(Direct.UP)) turnRight();
            }
            if (coordinates.y.Equals(0))
            {
                if (coordinates.x > 0)
                    while (!direction.Equals(Direct.LEFT)) turnRight();
                if (coordinates.x < 0)
                    while (!direction.Equals(Direct.RIGHT)) turnRight();
            }
        }

        private void GetDirection(Coordinates oldCoordinates)
        {
            if (coordinates.x > oldCoordinates.x)
            {
                direction = new Direct(Direct.RIGHT);
                return;
            }
            if (coordinates.x < oldCoordinates.x)
            {
                direction = new Direct(Direct.LEFT);
                return;
            }
            if (coordinates.y > oldCoordinates.y)
            {
                direction = new Direct(Direct.UP);
                return;
            }
            if (coordinates.y < oldCoordinates.y)
            {
                direction = new Direct(Direct.DOWN);
                return;
            }
            direction = new Direct(Direct.NONE);
        }

        private void UrgentDirDetection()
        {
            Coordinates oldCoordinates = coordinates;
            Move();
            if (oldCoordinates.Equals(coordinates))
            {
                turnRight();
            }
            Move();
            GetDirection(oldCoordinates);
        }

        private int GetQuadrant()
        {
            if (coordinates.x > 0 && coordinates.y > 0)
            {
                return 1;
            }
            if (coordinates.x < 0 && coordinates.y > 0)
            {
                return 2;
            }
            if (coordinates.x < 0 && coordinates.y < 0)
            {
                return 3;
            }
            if (coordinates.x > 0 && coordinates.y < 0)
            {
                return 4;
            }
            return -1;
        }

        private bool hasReachedCentr() { return coordinates.x.Equals(0) && coordinates.y.Equals(0); }

        private bool isAxis() { return coordinates.x.Equals(0) || coordinates.y.Equals(0); }

        private void Move()
        {
            networkWrapper.Send(MOVE);

            networkWrapper.Rec(12, out string response);
            if (!Regex.IsMatch(response, OK_REGEX))
                throw new InvalidResponseEx(InvalidResponseEx.INV_COORDINATES);

            string[] responseArr = response.Split(' ');
            coordinates = new Coordinates(int.Parse(responseArr[1]), int.Parse(responseArr[2]));
        }

        private void turnRight()
        {
            networkWrapper.Send(TURN_RIGHT);

            networkWrapper.Rec(12, out string response);
            if (!Regex.IsMatch(response, OK_REGEX))
                throw new Exception();

            direction.Next();
        }

        private void turnLeft()
        {
            networkWrapper.Send(TURN_LEFT);

            networkWrapper.Rec(12, out string response);
            if (!Regex.IsMatch(response, OK_REGEX))
                throw new Exception();

            direction.Previous();
        }



    }
}

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
﻿using System.Net;
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
namespace PSI_BOUDA
{
    class StatusWrapper
    {
        private const string LOGIN_FAILED = "300 LOGIN FAILED\a\b";

        private const string SYNTAX_ERROR = "301 SYNTAX ERROR\a\b";

        private const string LOGIC_ERROR = "302 LOGIC ERROR\a\b";


        private const string OK = "200 OK\a\b";
        private const string KEY_OUT_OF_RANGE_ERROR = "303 KEY OUT OF RANGE\a\b";


        private readonly NetworkWrapper networkWrapper;

        public StatusWrapper(NetworkWrapper networkProvider)
        {
            networkWrapper = networkProvider;
        }

        public void LogicError()
        {
            networkWrapper.Send(LOGIC_ERROR);
        }

        public void SyntaxError()
        {
            networkWrapper.Send(SYNTAX_ERROR);
        }


        public void Ok()
        {
            networkWrapper.Send(OK);
        }

        public void KeyOutOfRangeError()
        {
            networkWrapper.Send(KEY_OUT_OF_RANGE_ERROR);
        }

        public void LoginFailed()
        {
            networkWrapper.Send(LOGIN_FAILED);
        }

    }

}
