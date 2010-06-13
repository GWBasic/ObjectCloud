package com.objectcloud.javascriptprocess;

import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.io.OutputStreamWriter;
import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;

import org.json.JSONObject;
import org.json.JSONTokener;

public class IOPump {

	/**
	 * @param args
	 */
	public static void main(String[] args) {
		// TODO Auto-generated method stub

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
	
	Map<Integer, ScopeWrapper> scopeWrappers = new HashMap<Integer, ScopeWrapper>();
	private final static ExecutorService executorService = Executors.newCachedThreadPool();

	public static ExecutorService getExecutorservice() {
		return executorService;
	}
	
	public void start() throws Exception {
		
		try {
			JSONTokener tokener = new JSONTokener(new InputStreamReader(inStream));
			OutputStreamWriter outputStreamWriter = new OutputStreamWriter(outStream);
			
			final CompiledJavascriptTracker compiledJavascriptTracker = new CompiledJavascriptTracker(outputStreamWriter);

			// Create the parent scope
			ParentScope parentScope = new ParentScope();
			
			JSONObject inCommand = new JSONObject(tokener);
			
			while (inCommand.length() > 0) {
			
				final JSONObject inCommandFinal = inCommand;
				Runnable command;
				
				// If there's a scopeID, then the command will be handled in a scope
				// Else, if there's no scopeID, then it has to do with compiled javascript
				if (inCommand.has("ScopeID")) {
					
					// Get or create the scope wrapper
					final ScopeWrapper scopeWrapper;
					int scopeID = inCommand.getInt("ScopeID");
					
					synchronized(scopeWrappers) {
						if (scopeWrappers.containsKey(scopeID))
							scopeWrapper = scopeWrappers.get(scopeID);
						else {
							scopeWrapper = parentScope.createScopeWrapper(scopeID);
							scopeWrappers.put(scopeID, scopeWrapper);
						}
					}
					
					command = new Runnable() {
		
						@Override
						public void run() {
							scopeWrapper.handle(inCommandFinal);
						}
						
					};
				
				} else {
					command = new Runnable() {
						
						@Override
						public void run() {
							compiledJavascriptTracker.handle(inCommandFinal);
						}
					};
				}
				
				executorService.execute(command);
				
				// This is done last before the loop
				inCommand = new JSONObject(tokener);
			}
		}
		finally {
			executorService.shutdown();
			executorService.awaitTermination(Long.MAX_VALUE, TimeUnit.DAYS);
		}
	}
	
	public void DisposeScopeWrapper(int scopeID) {
		synchronized(scopeWrappers) {
			scopeWrappers.remove(scopeID);
		}
	}
}