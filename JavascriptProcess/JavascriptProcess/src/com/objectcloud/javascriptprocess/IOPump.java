package com.objectcloud.javascriptprocess;

import java.io.FileWriter;
import java.io.IOException;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.io.OutputStreamWriter;
import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;

import org.json.JSONException;
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

		} catch (Throwable t) {
			
			System.err.println(serializeGeneralException(t));
			FileWriter outFile;

			try {
				outFile = new FileWriter("JavascriptProcessCrashReport.txt");
				outFile.write(serializeGeneralException(t));
				outFile.close();
			} catch (IOException e) { }
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

	public void start() throws Throwable {

		try {
			JSONTokener tokener = new JSONTokener(new InputStreamReader(inStream));
			final OutputStreamWriter outputStreamWriter = new OutputStreamWriter(outStream);

			final CompiledJavascriptTracker compiledJavascriptTracker = new CompiledJavascriptTracker(outputStreamWriter);

			// Create the parent scope
			final ParentScope parentScope = new ParentScope(this, outputStreamWriter, compiledJavascriptTracker);

			JSONObject inCommand = new JSONObject(tokener);

			while (inCommand.length() > 0) {

				final JSONObject inCommandFinal = inCommand;

				Runnable command = new Runnable() {

					@Override
					public void run() {

						try
						{
							// If there's a scopeID, then the command will be handled in a scope
							// Else, if there's no scopeID, then it has to do with compiled javascript
							if (inCommandFinal.has("ScopeID")) {

								// Get or create the scope wrapper
								int scopeID = inCommandFinal.getInt("ScopeID");


								ScopeWrapper scopeWrapper = null;

								synchronized(scopeWrappers) {
									if (scopeWrappers.containsKey(scopeID))
										scopeWrapper = scopeWrappers.get(scopeID);
								}

								if (null == scopeWrapper) {

									scopeWrapper = parentScope.createScopeWrapper(scopeID);

									synchronized(scopeWrappers) {
										scopeWrappers.put(scopeID, scopeWrapper);
									}
								}

								scopeWrapper.handle(inCommandFinal);
							} else 
								compiledJavascriptTracker.handle(inCommandFinal);

						} catch (Throwable t) {
							handleGeneralException(t, inCommandFinal, outputStreamWriter);
						}
					}
				};

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
	
	public static void handleGeneralException(Throwable t, JSONObject inCommand, OutputStreamWriter outputStreamWriter) {
		
		try {
			synchronized (outputStreamWriter) {
				outputStreamWriter.write(serializeGeneralException(t, inCommand));
				outputStreamWriter.write("\r\n");
				outputStreamWriter.flush();
			}
		} catch (IOException e) {
			
			try {
				FileWriter outFile = new FileWriter("JavascriptProcessCrashReport.txt");
				outFile.write(serializeGeneralException(t));
				outFile.close();
			} catch (IOException ie) { }

			System.exit(0);
		}
	}
	
	public static String serializeGeneralException(Throwable t) {
		return serializeGeneralException(t, null);
	}
	private static String serializeGeneralException(Throwable t, JSONObject inCommand) {
		
		JSONObject errorToSend = new JSONObject();
		try {
			errorToSend.put("Message", t.getMessage());
			
			if (null != inCommand) {
				errorToSend.put("Command", inCommand);
				errorToSend.put("ThreadID", inCommand.get("ThreadID"));
			}
			
			StringBuilder stackTraceBuilder = new StringBuilder();
			for (StackTraceElement ste : t.getStackTrace())
				stackTraceBuilder.append("\n" + ste.toString());
		
			errorToSend.put("StackTrace", stackTraceBuilder.toString());
		} catch (JSONException e) {

			try {
				FileWriter outFile = new FileWriter("JavascriptProcessCrashReport.txt");
				outFile.write(e.getMessage());

				for (StackTraceElement ste : e.getStackTrace())
					outFile.write("\n" + ste.toString());
				
				outFile.close();
			} catch (IOException ie) { }

			System.exit(1);
		}

		return errorToSend.toString();
	}

	public void DisposeScopeWrapper(int scopeID) {
		synchronized(scopeWrappers) {
			scopeWrappers.remove(scopeID);
		}
	}
}