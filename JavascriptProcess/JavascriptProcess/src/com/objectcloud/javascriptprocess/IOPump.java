package com.objectcloud.javascriptprocess;

import java.io.IOException;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.io.OutputStreamWriter;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import java.util.HashMap;
import java.util.Map;

import org.json.JSONObject;
import org.json.JSONTokener;

public class IOPump {

	/**
	 * @param args
	 */
	public static void main(String[] args) {
		IOPump me = new IOPump(System.in, System.out);
		
		try {
			me.start();

		} catch (Exception e) {
			
			StringBuilder errorBuilder = new StringBuilder(e.getMessage());
			
			for (StackTraceElement ste : e.getStackTrace()) {
				errorBuilder.append("\n");
				errorBuilder.append(ste.toString());
			}
			
			System.err.println(JSONObject.quote(errorBuilder.toString()));
		}
		
		System.exit(0);
	}
	
	public IOPump(InputStream inStream, OutputStream outStream) {
		this.inStream = inStream;
		this.outStream = outStream;
	}
	
	InputStream inStream;
	OutputStream outStream;
	
	final Map<Integer, ParentScope> parentScopes = new HashMap<Integer, ParentScope>();
	final Map<Integer, ScopeWrapper> scopeWrappers = new HashMap<Integer, ScopeWrapper>();
	private final Map<Object, Object> monitorObjectsByThreadID = new HashMap<Object, Object>();
	private final Map<Object, JSONObject> inCommandByThreadID = new HashMap<Object, JSONObject>();

	public void start() throws Exception {
		
		ExecutorService executorService = Executors.newCachedThreadPool();
		
		try {
			JSONTokener tokener = new JSONTokener(new InputStreamReader(inStream));
			final OutputStreamWriter outputStreamWriter = new OutputStreamWriter(outStream);
			
			// Create the parent scope
			JSONObject inCommand = new JSONObject(tokener);

			while (inCommand.length() > 0) {
			
				
				final Object threadID = inCommand.get("ThreadID");
				
				// If there's a thread waiting on the ThreadID, stuff the inCommand into a map, unblock the other thread, and return
				// The blocked thread will take over handling the command
				
				Object monitorObject = null;
				synchronized (monitorObjectsByThreadID) {
					if (monitorObjectsByThreadID.containsKey(threadID))
						monitorObject = monitorObjectsByThreadID.get(threadID);
				}
					
				if (null != monitorObject) {
					
					synchronized (inCommandByThreadID) {
						inCommandByThreadID.put(threadID, inCommand);
					}
					
					synchronized (monitorObject) {
						monitorObject.notifyAll();
					}
				} else {
					
					Runnable command;
					
					if (inCommand.has("ScopeID")) {
						// Get or create the scope wrapper
						final JSONObject inCommandFinal = inCommand;
	
						command = new Runnable() {
							
							@Override
							public void run() {
								
								ScopeWrapper scopeWrapper = null;
	
								try {
									int scopeID = inCommandFinal.getInt("ScopeID");
									
									boolean scopeExists;
									synchronized(scopeWrappers) {
										
										scopeExists = scopeWrappers.containsKey(scopeID);
	
										if (scopeExists)
											scopeWrapper = scopeWrappers.get(scopeID);
									}
									
									if (!scopeExists) {
										int parentScopeID = inCommandFinal.getInt("ParentScopeID");
										
										ParentScope parentScope;
										synchronized(parentScopes) {
											parentScope = parentScopes.get(parentScopeID);
										}
										
										scopeWrapper = parentScope.createScopeWrapper(scopeID);
										synchronized(scopeWrappers) {
											scopeWrappers.put(scopeID, scopeWrapper);
										}
									}
								} catch (Exception e) {
									StringBuilder toReturn = new StringBuilder();
									toReturn.append(e.getMessage());
									
									for (StackTraceElement ste : e.getStackTrace())
										toReturn.append("\n" + ste.toString());
									
									System.err.println(JSONObject.quote(toReturn.toString()));
									
									return;
								}
								
								scopeWrapper.handle(inCommandFinal, threadID);
							}
						};
					} else {
						final JSONObject inCommandFinal = inCommand;
						final IOPump me = this;
						
						command = new Runnable() {
							
							@Override
							public void run() {
								int parentScopeID;
								ParentScope parentScope = null;
								JSONObject outCommand = null;
	
								try {
									parentScopeID = inCommandFinal.getInt("ParentScopeID");
									
									if (inCommandFinal.has("Data")) {
										parentScope = new ParentScope(me, inCommandFinal.getJSONObject("Data"), outputStreamWriter);
	
										outCommand = new JSONObject();
										outCommand.put("ThreadID", inCommandFinal.get("ThreadID"));
										outCommand.put("ParentScopeID", parentScopeID);
										outCommand.put("Data", new JSONObject());
										outCommand.put("Command", "RespondCreateParentScope");
									}
	
								} catch (Exception e) {
									StringBuilder toReturn = new StringBuilder();
									toReturn.append(e.getMessage());
									
									for (StackTraceElement ste : e.getStackTrace())
										toReturn.append("\n" + ste.toString());
									
									System.err.println(JSONObject.quote(toReturn.toString()));
									
									return;
								}
								
								synchronized(parentScopes) {
									if (null != parentScope) {
										parentScopes.put(parentScopeID, parentScope);
									} else {
										parentScopes.remove(parentScopeID);
									}
								}
	
								if (null != outCommand)
									synchronized (outputStreamWriter) {
										try {
											outputStreamWriter.write(outCommand.toString() + "\r\n");
											outputStreamWriter.flush();
										} catch (IOException e) {
											e.printStackTrace();
										}
									}
							}
						};
					}
					
					executorService.execute(command);
				}
				
				// This is done last before the loop
				inCommand = new JSONObject(tokener);
			}
		}
		finally {
			executorService.shutdown();
			executorService.awaitTermination(Long.MAX_VALUE, TimeUnit.DAYS);
		}
	}
	
	public JSONObject block(Object threadID) throws InterruptedException {
		
		Object monitorObject = new Object();
		
		try {
			
			synchronized(monitorObject) {
				synchronized(monitorObjectsByThreadID) {
					monitorObjectsByThreadID.put(threadID, monitorObject);
				}
			
				monitorObject.wait();
			}
			
			// pull inCommand out of a map and then re-call handle, unless a response is returned
			JSONObject inCommand;
			
			synchronized (inCommandByThreadID) {
				inCommand = inCommandByThreadID.get(threadID);
				inCommandByThreadID.remove(threadID);
			}
			
			return inCommand;
			
		} finally {
			synchronized(monitorObjectsByThreadID) {
				monitorObjectsByThreadID.remove(threadID);
			}
		}
	}
	
	public void DisposeScopeWrapper(int scopeID) {
		synchronized(scopeWrappers) {
			scopeWrappers.remove(scopeID);
		}
	}
}