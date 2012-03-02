// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Javascript;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Interfaces.WebServer
{
    /// <summary>
    /// All methods in this object are exposed to the web
    /// </summary>
    public interface IWebHandler : IWebHandlerPlugin
    {
        /// <summary>
        /// The implicit action to use if no action or method is specified.  This is usually View, but could be something else
        /// </summary>
        string ImplicitAction { get; }

        /// <summary>
        /// This should return a Javascript object that can perform all calls to all methods marked as WebCallable through AJAX.
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        IWebResults GetJSW(IWebConnection webConnection, string assignToVariable, string EncodeFor, bool bypassJavascript);

        /*// <summary>
        /// This should return a Javascript object that can perform all calls to all methods marked as WebCallable through server-side Javascript.
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        IWebResults GetServersideJavascriptWrapper(IWebConnection webConnection, string assignToVariable);*/

        /// <summary>
        /// Returns any server-side Javascript errors, if they can be determined.
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        IWebResults GetServersideJavascriptErrors(IWebConnection webConnection);

        /// <summary>
        /// Sets the user's permission for the given file.  Either the user or group ID or name are set
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        /// <param name="FilePermission">The permission, set to null to disable permissions to the file</param>
        /// <param name="Inherit">Set to true to allow permission inheritance.  For example, if this permission applies to a directory, it will be the default for files in the directory</param>
        /// <param name="UserOrGroup"></param>
        /// <param name="UserOrGroupId"></param>
        /// <param name="SendNotifications"></param>
        IWebResults SetPermission(IWebConnection webConnection,
            string UserOrGroupId,
            string UserOrGroup,
            string[] UserOrGroups,
            string[] UserOrGroupIds,
            string FilePermission,
            bool? Inherit,
            bool? SendNotifications,
            string[] namedPermissions);

        /// <summary>
        /// Returns the currently logged un user's permission for this file.  If the user doesn't have an assigned permission, a 0-length string is returned.
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        IWebResults GetPermission(IWebConnection webConnection);

        /// <summary>
        /// Returns the currently logged un user's permission for this file as a Javascript object that can be queried.
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        IWebResults GetPermissionAsJSON(IWebConnection webConnection);

        /// <summary>
		/// Returns all assigned permissions to the object
		/// </summary>
		/// <param name="webConnection">
		/// A <see cref="IWebConnection"/>
		/// </param>
		/// <returns>
		/// A <see cref="IWebResults"/>
		/// </returns>
        IWebResults GetPermissions(IWebConnection webConnection);
		
		/// <summary>
		/// Performs any needed cleanup and optimization operations needed on the file 
		/// </summary>
		/// <param name="webConnection">
		/// A <see cref="IWebConnection"/>
		/// </param>
		/// <returns>
		/// A <see cref="IWebResults"/>
		/// </returns>
		IWebResults Vacuum(IWebConnection webConnection);

        /// <summary>
        /// Endpoint used when in a Transport (layer 1) Comet communications loop with a browser
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        IWebResults PostComet(IWebConnection webConnection);

        /// <summary>
        /// Creates a new comet transport for Multiplexed (Layer 2) or Reliable (Layer 3) Comet Protocol.  Transport (layer 1) requires explicit support in the web handler, or the web handler must inherit from WebHandler
        /// </summary>
        /// <param name="session"></param>
        /// <param name="transportId"></param>
        /// <returns></returns>
        ICometTransport ConstructCometTransport(ISession session, IDictionary<string, string> getArguments, long transportId);
        
        /// <summary>
        /// Gets an existing comet transport
        /// </summary>
        /// <param name="session"></param>
        /// <param name="transportId"></param>
        /// <returns></returns>
        ICometTransport GetCometTransport(ISession session, long transportId);
        IChannelEventWebAdaptor Bus { get; }

        /// <summary>
        /// Returns all of the users connected to the bus
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        IWebResults GetConnectedUsers(IWebConnection webConnection);

        /// <summary>
        /// Posts to the bus as a user with read permissions
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="incoming"></param>
        /// <returns></returns>
        IWebResults PostBusAsRead(IWebConnection webConnection, string incoming);

        /// <summary>
        /// Posts to the bus as a user with write permissions
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="incoming"></param>
        /// <returns></returns>
        IWebResults PostBusAsWrite(IWebConnection webConnection, string incoming);

        /// <summary>
        /// Posts to the bus as a user with administer permissions
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="incoming"></param>
        /// <returns></returns>
        IWebResults PostBusAsAdminister(IWebConnection webConnection, string incoming);

        /// <summary>
        /// Sets a file as being related to this file
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="filename"></param>
        /// <param name="relationship"></param>
        /// <returns></returns>
        IWebResults AddRelatedFile(
		    IWebConnection webConnection,
		    string filename,
		    string relationship,
		    bool? inheritPermission,
		    string chownRelatedFileTo);
        
        /// <summary>
        /// Deletes a related file
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="filename"></param>
        /// <param name="relationship"></param>
        /// <returns></returns>
        IWebResults DeleteRelatedFile(IWebConnection webConnection, string filename, string relationship);

        /// <summary>
        /// Returns all files that are related to this file
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="relationships"></param>
        /// <param name="extensions"></param>
        /// <param name="newest"></param>
        /// <param name="maxToReturn"></param>
        /// <returns></returns>
        IWebResults GetRelatedFiles(
            IWebConnection webConnection,
            string relationships,
            string extensions,
            DateTime? newest,
            DateTime? oldest,
            uint? maxToReturn);

        /// <summary>
        /// Gets or creates the Javascript execution environment.  Returns null if there is no execution environment.
        /// </summary>
        IExecutionEnvironment GetOrCreateExecutionEnvironment();

        /// <summary>
        /// Creates an execution environment if no other thread is creating one
        /// </summary>
        void CreateExecutionEnvironmentIfNoOtherThreadCreating();
		
		/*// <summary>
		/// True if the execution environment is ready to be used, false if there will be a delay while it is allocated.  Also returns true if there isn't an execution environment 
		/// </summary>
		bool IsExecutionEnvironmentReady { get; }*/
		
		/// <summary>
		/// Resets the execution environment 
		/// </summary>
		void ResetExecutionEnvironment();
		
		/// <summary>
		/// Returns the named permissions that apply to this file 
		/// </summary>
		/// <param name="webConnection">
		/// A <see cref="IWebConnection"/>
		/// </param>
		/// <returns>
		/// A <see cref="IWebResults"/>
		/// </returns>
		IWebResults GetAssignableNamedPermissions(IWebConnection webConnection);

        /// <summary>
        /// Returns the currently logged in user's permission for this file as a Javascript object that can be queried, the file name, extension, full path, create date, and modification date.
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        IWebResults GetInfoAndPermission(IWebConnection webConnection);
    }
}
