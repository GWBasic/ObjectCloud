// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.Disk
{
    public class WrongPasswordException : SecurityException
    {
        public WrongPasswordException(string message)
            : base(message) { }

        public WrongPasswordException(string message, Exception inner)
            : base(message, inner) { }
    }
}
