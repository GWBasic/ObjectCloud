// Copyright 2009 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;

using Mono.Unix;
using Mono.Unix.Native;

namespace ObjectCloud.Platform
{
	/// <summary>
	/// Provides functions that block until the program recieves a terminating signal from unix
	/// </summary>
	public static class Blocker
	{
		/// <summary>
		/// Blocks until a signal is sent from Unix to terminate the process.  On non-unixes, waits for a keypress
		/// </summary>
		/// <returns>Either the UnixSignal that triggered the unblock, or the key pushed</returns>
		public static object Block()
		{
			return Block(delegate()
	      	{
				return System.Console.ReadKey();
			});
		}
		
		/// <summary>
		/// Blocks until a signal is sent from Unix to terminate the process.  On non-unixes, just calls the delegate
		/// </summary>
		/// <returns>Either the Signal that triggered the unblock, or the delegate's result</returns>
		public static object Block(BlockDelegate del)
		{
 	   		int p = (int) Environment.OSVersion.Platform;
                
			if ((p == 4) || (p == 6) || (p == 128)) 
				return DoBlock();
            else
				return del();
		}
		
		private static object DoBlock()
		{
			Signum[] quitSignals = new Signum[]
			{
				Signum.SIGABRT,
				//Signum.SIGBUS, Disabled because of mysterious MacOS behavior
				//Signum.SIGCHLD, Disabled because child processes stopping shouldn't kill the parent process
				Signum.SIGHUP,
				Signum.SIGILL,
				Signum.SIGINT,
				Signum.SIGQUIT,
				Signum.SIGTERM,
				Signum.SIGTSTP,
				Signum.SIGUSR1,
				Signum.SIGUSR2
			};
			
			List<UnixSignal> signals = new List<UnixSignal>();
			foreach (Signum quitSignal in quitSignals)
				signals.Add(new UnixSignal(quitSignal));
 
	        // Wait for a signal to be delivered
        		int index = UnixSignal.WaitAny(signals.ToArray(), -1);
			
			UnixSignal signal = signals[index];
			
			return signal.Signum;
		}
	}
	
	/// <summary>
	/// Delegate type that SignalHandler uses when it's not running on Unix
	/// </summary>
	public delegate object BlockDelegate();
}
