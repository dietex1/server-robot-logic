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
