package com.objectcloud.javascriptprocess;

import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.io.OutputStreamWriter;
import java.util.HashMap;
import java.util.Map;

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

	public void start() throws Exception {
		
		ExecutorService threadPool = Executors.newCachedThreadPool();

		try {
			JSONTokener tokener = new JSONTokener(new InputStreamReader(inStream));
			OutputStreamWriter outputStreamWriter = new OutputStreamWriter(outStream);
			
			// Create the parent scope
			JSONObject inCommand = new JSONObject(tokener);
			ParentScope parentScope = new ParentScope(this, inCommand, outputStreamWriter);
			
			inCommand = new JSONObject(tokener);
			
			while (inCommand.length() > 0) {
			
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
				
				final JSONObject inCommandFinal = inCommand;
				
				Runnable command = new Runnable() {
	
					@Override
					public void run() {
						scopeWrapper.handle(inCommandFinal);
					}
					
				};
				
				threadPool.execute(command);
	
				// This is done last before the loop
				inCommand = new JSONObject(tokener);
			}
		}
		finally {
			threadPool.shutdown();
			threadPool.awaitTermination(Long.MAX_VALUE, TimeUnit.DAYS);
		}
	}
	
	public void DisposeScopeWrapper(int scopeID) {
		synchronized(scopeWrappers) {
			scopeWrappers.remove(scopeID);
		}
	}
}