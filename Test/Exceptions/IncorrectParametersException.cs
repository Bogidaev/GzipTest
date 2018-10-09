using System;

namespace Test.Exceptions
{
    public class IncorrectParametersException : Exception
    {
        public IncorrectParametersException(string message) : base(message) { }
    }
}