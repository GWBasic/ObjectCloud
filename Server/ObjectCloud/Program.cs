// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml;

using SignalHandller;

using Spring.Context;
using Spring.Context.Support;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.Spring.Config;

namespace ObjectCloud
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                FileHandlerFactoryLocator fileHandlerFactoryLocator =
                    ContextLoader.GetFileHandlerFactoryLocatorForConfigurationFile("ObjectCloudConfig.xml");

                // If the hostname isn't specified, then use the current IP
                if (null == fileHandlerFactoryLocator.Hostname)
                {
                    string hostname = Dns.GetHostName();
                    IPHostEntry IPHost = Dns.GetHostEntry(hostname);

                    // When the hostname isn't specified, the current IP is defaulted to
                    // This is because the OpenID functionality needs stable hostnames in order to work.
                    // The hostname isn't used because they don't work from Windows to *nix
                    foreach (IPAddress address in IPHost.AddressList)
                        // For now, just grab the 1st IPv4 address...  I don't know how to handle IPv6
                        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            fileHandlerFactoryLocator.Hostname = address.ToString();
                            break;
                        }
                }

                if (0 == args.Length)
                    using (IWebServer webServer = fileHandlerFactoryLocator.WebServer)
                    {
                        fileHandlerFactoryLocator.FileSystemResolver.Started += FileSystemResolver_Started;

                        try
                        {
                            webServer.StartServer();
                        }
                        finally
                        {
                            fileHandlerFactoryLocator.FileSystemResolver.Started -= FileSystemResolver_Started;
                        }
                        
						object blockResult = Blocker.Block();
					
						Console.WriteLine("Recieved " + blockResult.ToString());

                        Thread forceExitThread = new Thread(delegate()
                            {
                                Thread.Sleep(3000);
								System.Diagnostics.Process.GetCurrentProcess().Kill();
                                	Environment.Exit(0);
                            });

                        forceExitThread.Name = "Forces the process to exit";
                        forceExitThread.IsBackground = false;

                        forceExitThread.Start();
                    }
                else
                    switch (args[0])
                    {
					    case ("GUI"):
							{
								System.Windows.Forms.Application.Run(new GUIForm(fileHandlerFactoryLocator));

                                break;
							}
                        case ("dump"):
                            {
                                //FileHandlerFactoryLocator fileHandlerFactoryLocator = (FileHandlerFactoryLocator)context["FileHandlerFactoryLocator"];

                                string objectCloudfileToDump = args[1];
                                string fileSystemDestination = args[2];

                                fileHandlerFactoryLocator.FileSystemResolver.Start();
                                IFileContainer fileContainer = fileHandlerFactoryLocator.FileSystemResolver.ResolveFile(objectCloudfileToDump);

                                using (TimedLock.Lock(fileContainer.FileHandler))
                                    fileContainer.FileHandler.Dump(fileSystemDestination, fileHandlerFactoryLocator.UserManagerHandler.Root.Id);

                                break;
                            }

                        case ("dumpsystem"):
                            {
                                string fileSystemDestination = args[1];

                                fileHandlerFactoryLocator.FileSystemResolver.Start();

                                IFileContainer fileContainer;

                                fileContainer = fileHandlerFactoryLocator.FileSystemResolver.ResolveFile("Shell");
                                fileContainer.FileHandler.Dump(fileSystemDestination + Path.DirectorySeparatorChar + "Shell", fileHandlerFactoryLocator.UserManagerHandler.Root.Id);

                                fileContainer = fileHandlerFactoryLocator.FileSystemResolver.ResolveFile("API");
                                fileContainer.FileHandler.Dump(fileSystemDestination + Path.DirectorySeparatorChar + "API", fileHandlerFactoryLocator.UserManagerHandler.Root.Id);

                                fileContainer = fileHandlerFactoryLocator.FileSystemResolver.ResolveFile("Templates");
                                fileContainer.FileHandler.Dump(fileSystemDestination + Path.DirectorySeparatorChar + "Templates", fileHandlerFactoryLocator.UserManagerHandler.Root.Id);

                                fileContainer = fileHandlerFactoryLocator.FileSystemResolver.ResolveFile("Tests");
                                fileContainer.FileHandler.Dump(fileSystemDestination + Path.DirectorySeparatorChar + "Tests", fileHandlerFactoryLocator.UserManagerHandler.Root.Id);

                                fileContainer = fileHandlerFactoryLocator.FileSystemResolver.ResolveFile("Pages");
                                fileContainer.FileHandler.Dump(fileSystemDestination + Path.DirectorySeparatorChar + "Pages", fileHandlerFactoryLocator.UserManagerHandler.Root.Id);

                                fileContainer = fileHandlerFactoryLocator.FileSystemResolver.ResolveFile("Docs");
                                fileContainer.FileHandler.Dump(fileSystemDestination + Path.DirectorySeparatorChar + "Docs", fileHandlerFactoryLocator.UserManagerHandler.Root.Id);

                                fileContainer = fileHandlerFactoryLocator.FileSystemResolver.ResolveFile("Classes");
                                fileContainer.FileHandler.Dump(fileSystemDestination + Path.DirectorySeparatorChar + "Classes", fileHandlerFactoryLocator.UserManagerHandler.Root.Id);

                                // This shouldn't be backed up
                                //fileContainer = fileHandlerFactoryLocator.FileSystemResolver.ResolveFile("index.page");
                                //fileContainer.FileHandler.Dump(fileSystemDestination + Path.DirectorySeparatorChar + "index.page", fileHandlerFactoryLocator.UserManagerHandler.Root.Id);

                                break;
                            }

                        case ("restore"):
                            {
                                string objectCloudfileToRestore = args[1];
                                string fileSystemSource = args[2];

                                string fileType = "directory";
                                if (args.Length > 3)
                                    fileType = args[3];

                                fileHandlerFactoryLocator.FileSystemResolver.Start();

                                if (!fileHandlerFactoryLocator.FileSystemResolver.IsFilePresent(objectCloudfileToRestore))
                                    fileHandlerFactoryLocator.FileSystemResolver.RootDirectoryHandler.CreateFile(
                                        objectCloudfileToRestore,
                                        fileType,
                                        fileHandlerFactoryLocator.UserFactory.RootUser.Id);

                                fileHandlerFactoryLocator.FileSystemResolver.ResolveFile(
                                    objectCloudfileToRestore).FileHandler.SyncFromLocalDisk(fileSystemSource, true);

                                break;
                            }

						case ("restoresystem"):
							{
						        // Delete the contents of every system directory so that it's completely resored the next time ObjectCloud is loaded
						
						        IFileSystemResolver fileSystemResolver = fileHandlerFactoryLocator.FileSystemResolver;
						        fileSystemResolver.Start();
						
								foreach (string dirNameToClean in new string[] {"Shell", "API", "Templates", "Tests", "Pages", "Docs", "Classes"})
								{
									IDirectoryHandler dirToClean = fileSystemResolver.ResolveFile(dirNameToClean).CastFileHandler<IDirectoryHandler>();
							
							        foreach (IFileContainer fileContainer in new List<IFileContainer>(dirToClean.Files))
										try
										{
											Console.WriteLine("Deleting: " + fileContainer.FullPath);
								
											dirToClean.DeleteFile(
									        	fileHandlerFactoryLocator.UserManagerHandler.Root,
									            fileContainer.Filename);
										}
										catch (Exception e)
										{
											Console.WriteLine("Excpetion when deleting " + fileContainer.FullPath + "\n" + e.Message);
										}
								}
							
								break;
		                    }
				}
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Super fatal error!");
                Console.Error.WriteLine(e.ToString());

                if (System.Diagnostics.Debugger.IsAttached)
                    System.Diagnostics.Debugger.Break();
            }
        }

        /// <summary>
        /// Loads a bunch of scopes on the threadpool  
        /// </summary>
        private static void FileSystemResolver_Started(IFileSystemResolver sender, EventArgs e)
        {
            IFileContainer classesFolder = sender.ResolveFile("/Classes");
            sender.FileHandlerFactoryLocator.ExecutionEnvironmentFactory.Start(classesFolder.CastFileHandler<IDirectoryHandler>().Files);
        }
    }
}
