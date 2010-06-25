// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Common
{
    /// <summary>
    /// Bus of special ObjectCloud events.  Be careful when registering listeners on the bus
    /// </summary>
    public static class EventBus
    {
        /// <summary>
        /// Call this function to indicate that a fatal exception occured and that the server should reboot!
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        public static void OnFatalException(object sender, EventArgs<Exception> args)
        {
            if (System.Diagnostics.Debugger.IsAttached)
                System.Diagnostics.Debugger.Break();

            if (null != FatalException)
                FatalException(sender, args);
        }

        /// <summary>
        /// Event for when fatal exceptions occur.  These typicaly imply that the server is fubr and needs to be restarted or rebooted!
        /// </summary>
        public static event EventHandler<object, EventArgs<Exception>> FatalException;
    }
}
