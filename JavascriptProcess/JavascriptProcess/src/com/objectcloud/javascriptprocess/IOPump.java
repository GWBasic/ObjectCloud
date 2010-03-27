package com.objectcloud.javascriptprocess;

import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.io.OutputStreamWriter;
import java.util.HashMap;
import java.util.Map;

import org.json.JSONException;
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
			System.err.println(e.getMessage());
			e.printStackTrace();
		}
	}
	
	public IOPump(InputStream inStream, OutputStream outStream) {
		this.inStream = inStream;
		this.outStream = outStream;
	}
	
	InputStream inStream;
	OutputStream outStream;
	
	Map<Integer, ScopeWrapper> scopeWrappers = new HashMap<Integer, ScopeWrapper>();

	public void start() throws JSONException {
		JSONTokener tokener = new JSONTokener(new InputStreamReader(inStream));
		OutputStreamWriter outputStreamWriter = new OutputStreamWriter(outStream);
		
		JSONObject inCommand = new JSONObject(tokener);
		
		while (inCommand.length() > 0) {
		
			// Get or create the scope wrapper
			final ScopeWrapper scopeWrapper;
			int scopeID = inCommand.getInt("ScopeID");
			
			synchronized(scopeWrappers) {
				if (scopeWrappers.containsKey(scopeID))
					scopeWrapper = scopeWrappers.get(scopeID);
				else {
					scopeWrapper = new ScopeWrapper(this, outputStreamWriter, scopeID);
					scopeWrappers.put(scopeID, scopeWrapper);
				}
			}
			
			final JSONObject inCommandFinal = inCommand;
			
			Thread thread = new Thread(new Runnable() {

				@Override
				public void run() {
					scopeWrapper.handle(inCommandFinal);
				}
				
			});
			
			thread.start();

			// This is done last before the loop
			inCommand = new JSONObject(tokener);
		}
	}
	
	public void DisposeScopeWrapper(int scopeID) {
		synchronized(scopeWrappers) {
			scopeWrappers.remove(scopeID);
		}
	}
}