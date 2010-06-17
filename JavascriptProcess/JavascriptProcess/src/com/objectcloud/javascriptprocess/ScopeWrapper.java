package com.objectcloud.javascriptprocess;

import java.io.IOException;
import java.io.OutputStreamWriter;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.Map;
import java.util.Random;
import java.util.Stack;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;
import org.json.JSONString;
import org.mozilla.javascript.Callable;
import org.mozilla.javascript.ClassShutter;
import org.mozilla.javascript.Context;
import org.mozilla.javascript.EcmaError;
import org.mozilla.javascript.Function;
import org.mozilla.javascript.JavaScriptException;
import org.mozilla.javascript.NativeFunction;
import org.mozilla.javascript.Scriptable;
import org.mozilla.javascript.Undefined;

public class ScopeWrapper {
	
	public ScopeWrapper(IOPump ioPump, OutputStreamWriter outputStreamWriter, int scopeID, ParentScope parentScope) {
	
		this.ioPump = ioPump;
		this.outputStreamWriter = outputStreamWriter;
		this.scopeID = new Integer(scopeID);
		this.parentScope = parentScope;
	}

	private final IOPump ioPump;
	private final OutputStreamWriter outputStreamWriter;
	Scriptable scope;
	private final Integer scopeID;
	Function jsonStringifyFunction;
	Function jsonParseFunction;
	private final Map<Object, Object> monitorObjectsByThreadID = new HashMap<Object, Object>();
	private final Map<Object, JSONObject> inCommandByThreadID = new HashMap<Object, JSONObject>();
	private final Map<Object, Function> callbacks = new HashMap<Object, Function>();
	private final Map<Object, Object> cachedObjects = new HashMap<Object, Object>();
	private final ParentScope parentScope;
	private static final CompiledJavascriptTracker compiledJavascriptTracker = CompiledJavascriptTracker.getInstance();
	
	static Random random = new Random();
	
	// All the current scope and thread ID mapped by their context
	static final ThreadLocal<Stack<Object>> threadIDStack = new ThreadLocal<Stack<Object>>() {
		@Override
		protected Stack<Object> initialValue() {
			return new Stack<Object>();
		}
	};
	
	public void handle(JSONObject inCommand) {
		
		try {
			
			Object threadID = inCommand.get("ThreadID");
			
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
				
				return;
			}
			
			String command = inCommand.getString("Command");
			JSONObject data = inCommand.getJSONObject("Data");
			
			final Context context = Context.enter();

			try {
				
				// Make sure that Javascript calls to Java can't escape
		        try {
		            context.setClassShutter(new ClassShutter() {
						@Override
						public boolean visibleToScripts(String arg0) {
							return false;
						}
		            });
		            
		        // For now these are being swallowed because they seem to occur if setting this twice
		        } catch (SecurityException se) {}
				
				threadIDStack.get().push(threadID);

				if (command.equals("CallFunctionInScope"))
					callFunctionInScope(context, threadID, data);
				
				else if (command.equals("CallCallback"))
					callCallback(context, threadID, data);
				
				else if (command.equals("CreateScope"))
					callCreateScope(context, threadID, data);
				
				else if (command.equals("DisposeScope"))
					ioPump.DisposeScopeWrapper(scopeID);
				
				// If this is a response, it means that somehow the parent process is faster then Java!
				// It's unlikely that this block will be called; it's just here in case of a potential weirdo
				// syncronization glitch
				else if (command.equals("RespondCallParentFunction")) {
					Thread.sleep(25);
					handle(inCommand);
				}

				else
					System.err.println(JSONObject.quote(command + " is unsupported"));
		
			} finally {
				threadIDStack.get().pop();
	            Context.exit();
	        }
		
		} catch (Exception e) {
			StringBuilder toReturn = new StringBuilder();
			toReturn.append(e.getMessage());
			
			for (StackTraceElement ste : e.getStackTrace())
				toReturn.append("\n" + ste.toString());
			
			System.err.println(JSONObject.quote(toReturn.toString()));
		}
	}

	
	private void callCreateScope(Context context, Object threadID, JSONObject data) throws Exception {
		
		ParentScope.ScriptableAndResult scriptableAndResult;
		JSONObject outData = new JSONObject();
		scriptableAndResult = parentScope.createScope(context);
		this.scope = scriptableAndResult.scope;
	    jsonStringifyFunction = scriptableAndResult.jsonStringifyFunction;
	    jsonParseFunction = scriptableAndResult.jsonParseFunction;

	    // Inject function callers
		for (String functionName : parentScope.getFunctions())
			scope.put(functionName, scope, new ParentProcessFunctionCaller(functionName));

	    
	    // Load properties
		for (String key : data.keysIterable()) {
			Object property = data.get(key);
			
			// If the property isn't a primitive, then convert it to something Rhino can handle by re-JSONing and unJSONing
			if ((property instanceof JSONArray) || (property instanceof JSONObject))
				property = jsonParseFunction.call(context, scope, scope, new Object[] {property.toString()});

			scope.put(key, scope, property);
		}

	    Object result = null;
		try {

			for (NativeFunction script : parentScope.getCompiledScripts())
				result = script.call(context, scope, scope, null);
			
		} catch (JavaScriptException je) {
			returnResult("RespondCreateScope", context, threadID, je.getValue(), outData, "Exception");
			return;
		} catch (Exception e) {
			returnResult("RespondCreateScope", context, threadID, e.getMessage(), outData, "Exception");
			return;
		}
		
	    
	    JSONObject functions = new JSONObject();
		outData.put("Functions", functions);
		
        for (Object id : scope.getIds()) {

        	String functionName = id.toString();

            Object javascriptMethodObject = scope.get(functionName, scope);

            // If the value is a Javascript function...
            if (Function.class.isInstance(javascriptMethodObject)) {
            	JSONObject function = new JSONObject();
            	functions.put(functionName, function);
            	
            	JSONObject properties = new JSONObject();
            	function.put("Properties", properties);
            	
                Function javascriptMethod = (Function)javascriptMethodObject;

                for (Object fId : javascriptMethod.getIds())
                	properties.put(fId.toString(), javascriptMethod.get(fId.toString(), scope));

                // Try to get the arguments
            	JSONArray arguments = new JSONArray();
            	function.put("Arguments", arguments);

            	//String unbrokenArgs = context.evaluateString(scope, functionName + ".toSource();", "<cmd>", 1, null).toString();
				Function toSourceFunction = (Function)javascriptMethod.getPrototype().get("toSource", scope);
				String unbrokenArgs = (String)toSourceFunction.call(context, javascriptMethod, javascriptMethod, new Object[] { javascriptMethod });

				unbrokenArgs = unbrokenArgs.substring(unbrokenArgs.indexOf('(') + 1);
            	unbrokenArgs = unbrokenArgs.substring(0, unbrokenArgs.indexOf(')'));

            	if (unbrokenArgs.length() > 0) {
                	
                	String[] args = unbrokenArgs.split(",");
                	for (String arg : args)
                		arguments.put(arg.trim());
                }
            }
        }

		
	    returnResult("RespondCreateScope", context, threadID, result, outData, "Result");
	}

	// Lets javascript call a function in the parent process
	private class ParentProcessFunctionCaller implements Callable {

		String functionName;

		public ParentProcessFunctionCaller(String functionName) {
			this.functionName = functionName;
		}

		@Override
		public Object call(Context context, Scriptable scope, Scriptable thisObj, Object[] args) {

			Object threadID = threadIDStack.get().peek();
			
			try {
				return callFunctionInParentProcess(context, args, functionName, threadID);
			} catch (JavaScriptException je) {
				throw je;
			} catch (Exception e) {
				Context.throwAsScriptRuntimeEx(e);
				throw new RuntimeException(e);
			}
		}
	}
	
	private void callFunctionInScope(Context context, Object threadID, JSONObject data) throws Exception {
		
		String functionName = data.getString("FunctionName");
		Function function = (Function)scope.get(functionName, scope);
		
		callFunction("RespondCallFunctionInScope", context, threadID, function, data.getJSONArray("Arguments"));
	}
	
	private void callCallback(Context context, Object threadID, JSONObject data) throws Exception {
		
		Object callbackID = data.get("CallbackId");
		Function function = callbacks.get(callbackID);
		
		callFunction("RespondCallCallback", context, threadID, function, data.getJSONArray("Arguments"));
	}

	private void callFunction(String command, Context context, Object threadID,
			Function function, JSONArray argumentsJSON)
				throws JSONException, IOException {

		ArrayList<Object> arguments = new ArrayList<Object>();
		for (Object o : argumentsJSON)
			arguments.add(o);
		
		for (int ctr = 0; ctr < arguments.size(); ctr++)
		{
			Object argument = arguments.get(ctr);
			
			/*Date start;
			Date complete;
			
			start = new Date();
			Context.javaToJS(context.evaluateString(scope, "(" + argument.toString() + ")", "<cmd>", 1, null), scope);
			complete = new Date();
			Logger.log("Time to decode via eval: " + new Long(complete.getTime() - start.getTime()).toString());
			
			start = new Date();
			jsonParseFunction.call(context, scope, scope, new Object[] { argument.toString()} );
			complete = new Date();
			Logger.log("Time to decode via JSON.parse: " + new Long(complete.getTime() - start.getTime()).toString());*/
			
			if ((argument instanceof JSONArray) || (argument instanceof JSONObject))
				arguments.set(
					ctr,
					jsonParseFunction.call(context, scope, scope, new Object[] { argument.toString()} ));
					//Context.javaToJS(context.evaluateString(scope, "(" + argument.toString() + ")", "<cmd>", 1, null), scope));
			else if (JSONObject.NULL == argument)
				arguments.set(
						ctr,
						null);
		}
		
		try {
			Object callResults = function.call(context, scope, scope, arguments.toArray());
			
			returnResult(command, context, threadID, callResults, "Result");
		} catch (JavaScriptException je) {
			returnResult("RespondCallFuncion", context, threadID, je.getValue(), "Exception");
			return;
		} catch (EcmaError ee) {
			returnResult(command, context, threadID, ee.getMessage(), "Exception");
		} catch (Exception e) {
			
			StringBuilder exceptionMessage = new StringBuilder(e.getMessage());
			for (StackTraceElement ste : e.getStackTrace())
				exceptionMessage.append("\n" + ste.toString());
			
			returnResult("RespondCallFunction", context, threadID, exceptionMessage.toString(), "Exception");
			return;
        }
	}
	
	private void returnResult(String command, Context context, Object threadID, Object callResults, String resultsName) throws JSONException, IOException {
		returnResult(command, context, threadID, callResults, new JSONObject(), resultsName);
	}
	
	private void returnResult(String command, final Context context, Object threadID, final Object callResults, JSONObject outData, String resultsName) throws JSONException, IOException {
		
		if (callResults != null)
			if (!(callResults instanceof Undefined)) {
				final Object serializedCallResults = jsonStringifyFunction.call(context, scope, scope, new Object[] { callResults });
				
				if (serializedCallResults instanceof String)
					outData.put(resultsName, new JSONString() {
						
						@Override
						public String toJSONString() {
							return (String)serializedCallResults;
						}
						
					});		
				}
		
		sendCommand(command, threadID, outData);
	}
	
	private void sendCommand(String command, Object threadID, JSONObject data) throws JSONException, IOException {
		
		JSONObject outCommand = new JSONObject();
		outCommand.put("Data", data);
		outCommand.put("ScopeID", scopeID);
		outCommand.put("ThreadID", threadID);
		outCommand.put("Command", command);

		synchronized (outputStreamWriter) {
			outputStreamWriter.write(outCommand.toString() + "\r\n");
			outputStreamWriter.flush();
		}
	}
	
	// Calls a function in the parent process
	public Object callFunctionInParentProcess(final Context context, Object[] args, String functionName, Object threadID) throws Exception {

		JSONArray argumentsForJSON = new JSONArray();
		
		// Extract callbacks so that the parent process can identify them and use them
		// The callbacks must be destroyed after this part of the call stack is complete.  For now, there is no way for
		// the parent process to hold on to them in the "heap"
		ArrayList<Object> callbackIDs = new ArrayList<Object>();
		
		try {
			
			for (int argumentIndex = 0; argumentIndex < args.length; argumentIndex++) {

				Object argument = args[argumentIndex];
				
				if (Function.class.isInstance(argument)) {
					
					Object callbackID;
					do {
						callbackID = random.nextInt();
					} while (callbacks.containsKey(callbackID));
					
					callbacks.put(callbackID, (Function)argument);
					callbackIDs.add(callbackID);
					
					JSONObject callbackIndicator = new JSONObject();
					callbackIndicator.put("Callback", true);
					callbackIndicator.put("CallbackID", callbackID);
					
					argumentsForJSON.put(argumentIndex, callbackIndicator);

				} else if (Scriptable.class.isInstance(argument))
					// use the wrapper that converts to a JSONString
					argumentsForJSON.put(argumentIndex, new JSONStringFromScriptable(context, (Scriptable)argument));
				
				else
					argumentsForJSON.put(argumentIndex, argument);
			}
			
			JSONObject data = new JSONObject();
			data.put("FunctionName", functionName);
			data.put("Arguments", argumentsForJSON);
			
			sendCommand("CallParentFunction", threadID, data);
			
			Object monitorObject = new Object();
			
			do
			{
				
				try {
					
					synchronized(monitorObjectsByThreadID) {
						monitorObjectsByThreadID.put(threadID, monitorObject);
					}
					
					synchronized(monitorObject) {
						monitorObject.wait();
					}
					
				} finally {
					synchronized(monitorObjectsByThreadID) {
						monitorObjectsByThreadID.remove(threadID);
					}
				}
				
				// pull inCommand out of a map and then re-call handle, unless a response is returned
				JSONObject inCommand = inCommandByThreadID.get(threadID);
				inCommandByThreadID.remove(threadID);
	
				// If the command is a response to the function call, return the data, else, handle the command
				if (inCommand.getString("Command").equals("RespondCallParentFunction")) {
					
					JSONObject dataFromParent = inCommand.getJSONObject("Data");
					
					if (dataFromParent.has("Exception")) {
						
						// First, try throwing the exception in Javascript
						context.evaluateString(scope, "throw " + dataFromParent.get("Exception").toString() + ";", "<cmd>", 1, null);
						//Object toThrow = jsonParseFunction.call(context, scope, scope, new Object[] { dataFromParent.get("Exception").toString() });
						//parentScope.getThrowFunction().call(context, scope, scope, new Object[] { toThrow });
						
						// if that didn't throw, then throw a meaner exception
						throw new RuntimeException(dataFromParent.get("Exception").toString());
					}
					else if (dataFromParent.has("Result")) {
						Object toReturn = dataFromParent.get("Result");
		
						// If the object is a JSONArray or JSONObject, then it can't be directly consumed in Rhino and must be
						// re-de-serialized in Rhino
						if (JSONArray.class.isInstance(toReturn) || JSONObject.class.isInstance(toReturn))				
							toReturn = context.evaluateString(scope, "(" + toReturn.toString() + ")", "<cmd>", 1, null);
						
						if (JSONObject.NULL == toReturn)
							toReturn = null;

						if (dataFromParent.has("CacheID"))
							cachedObjects.put(dataFromParent.get("CacheID"), toReturn);
						
						return toReturn;

					} else if (dataFromParent.has("Eval")) {
						//Object toReturn = context.evaluateString(scope, dataFromParent.getString("Eval"), "<cmd>", 1, null);
						NativeFunction toCall = compiledJavascriptTracker.getGetOrCompileScript(dataFromParent.getString("Eval"));
						Object toReturn = toCall.call(context, scope, scope, null);

						if (dataFromParent.has("CacheID"))
							cachedObjects.put(dataFromParent.get("CacheID"), toReturn);
						
						return toReturn;
						
					} else if (dataFromParent.has("CacheID"))
						return cachedObjects.get(dataFromParent.get("CacheID"));
					else
						return Undefined.instance;
				}
				
				handle(inCommand);
	
			} while (true);
		} finally {
			// clean up old callbacks
			// At some time there might be a way for the parent process to hold onto callbacks in the "heap"
			
			for (Object callbackID : callbackIDs)
				callbacks.remove(callbackID);
		}
	}
	
	private class JSONStringFromScriptable implements JSONString {
		
		public JSONStringFromScriptable(Context context, Scriptable scriptable) {
		
			this.context = context;
			this.scriptable = scriptable;
		}
		
		Context context;
		Scriptable scriptable;
		
		@Override
		public String toJSONString() {
			return jsonStringifyFunction.call(
				context,
				scope,
				scope,
				new Object[] { scriptable }).toString();
		}
		
	}
}
