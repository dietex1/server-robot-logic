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
