package com.objectcloud.javascriptprocess;

import java.io.IOException;
import java.io.OutputStreamWriter;
import java.lang.reflect.Method;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.Map;
import java.util.Random;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;
import org.json.JSONString;
import org.mozilla.javascript.ClassShutter;
import org.mozilla.javascript.Context;
import org.mozilla.javascript.Function;
import org.mozilla.javascript.FunctionObject;
import org.mozilla.javascript.Scriptable;
import org.mozilla.javascript.ScriptableObject;
import org.mozilla.javascript.Undefined;

public class ScopeWrapper {
	
	public ScopeWrapper(IOPump ioPump, OutputStreamWriter outputStreamWriter, int scopeID) {
		
		this.ioPump = ioPump;
		this.outputStreamWriter = outputStreamWriter;
		this.scopeID = new Integer(scopeID);
		
		Context context = Context.enter();

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

            scope = context.initStandardObjects();

            // Load JSON methods
            context.evaluateString(scope, json2, "<cmd>", 1, null);
            jsonStringifyFunction = (Function)context.evaluateString(scope, "JSON.stringify", "<cmd>", 1, null);
            
            // Load external function caller
            FunctionObject callFunctionInParentProcessMethodFunctionObject = new FunctionObject(
            	callFunctionInParentProcessName,
            	callFunctionInParentProcessMethod,
            	scope);
            scope.put(callFunctionInParentProcessName, scope, callFunctionInParentProcessMethodFunctionObject);

		} finally {
            Context.exit();
        }
	}

	IOPump ioPump;
	OutputStreamWriter outputStreamWriter;
	ScriptableObject scope;
	Function jsonStringifyFunction;
	Integer scopeID;
	private final static Method callFunctionInParentProcessMethod = getCallFunctionInParentProcessMethod();
	private final static String callFunctionInParentProcessName = "_____callParentFunction";
	Map<Object, Object> monitorObjectsByThreadID = new HashMap<Object, Object>();
	Map<Object, JSONObject> inCommandByThreadID = new HashMap<Object, JSONObject>();
	Map<Object, Function> callbacks = new HashMap<Object, Function>();
	
	static Random random = new Random();
	
	// Pairing of ThreadID and ScopeWrapper
	private class ScopeWrapperAndThreadID {
		
		public ScopeWrapperAndThreadID(Object threadID, ScopeWrapper scopeWrapper) {
			this.threadID = threadID;
			this.scopeWrapper = scopeWrapper;
		}
		
		public Object threadID;
		public ScopeWrapper scopeWrapper;
	}
	
	// All of the scopes and thread IDs mapped by their context
	static final Map<Context, ScopeWrapperAndThreadID> scopeWrapperAndThreadIDsByContext =
		new HashMap<Context, ScopeWrapperAndThreadID>();
	
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
				
				scopeWrapperAndThreadIDsByContext.put(
					context,
					new ScopeWrapperAndThreadID(threadID, this));

				if (command.equals("CallFunctionInScope"))
					callFunctionInScope(context, threadID, data);
				
				else if (command.equals("EvalScope"))
					evalScope(context, threadID, data);
				
				else if (command.equals("CallCallback"))
					callCallback(context, threadID, data);
				
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
					System.err.println(command + " is unsupported");
		
			} finally {
				scopeWrapperAndThreadIDsByContext.remove(context);
	            Context.exit();
	        }
		
		} catch (Exception e) {
			System.err.println(e.getLocalizedMessage());
		}
	}

	private void evalScope(Context context, Object threadID, JSONObject data) throws Exception {
		
		if (data.has("Functions")) {
			JSONArray functions = data.getJSONArray("Functions");
			StringBuilder functionsBuilder = new StringBuilder();
			
			for (Object function : functions) {
				
				functionsBuilder.append("function ");
				functionsBuilder.append(function.toString());
				functionsBuilder.append("() { var args = []; for (var i = 0; i < arguments.length; i++) args[i] = arguments[i]; return ");
				functionsBuilder.append(callFunctionInParentProcessName);
				functionsBuilder.append("(\"");
				functionsBuilder.append(function.toString());
				functionsBuilder.append("\", args);} ");
			}
			
			context.evaluateString(scope, functionsBuilder.toString(), "<cmd>", 1, null);
		}

		Object callResults = context.evaluateString(scope, data.getString("Script"), "<cmd>", 1, null);
		
		JSONObject outData = new JSONObject();
		
		if (data.has("ReturnFunctions"))
			if (data.getBoolean("ReturnFunctions")) {
				
				JSONObject functions = new JSONObject();
				outData.put("Functions", functions);
				
                for (Object id : scope.getIds()) {

                	String method = id.toString();

                    Object javascriptMethodObject = scope.get(method, scope);

                    // If the value is a Javascript function...
                    if (Function.class.isInstance(javascriptMethodObject)) {
                    	JSONObject function = new JSONObject();
                    	functions.put(method, function);
                    	
                        Function javascriptMethod = (Function)javascriptMethodObject;

                        for (Object fId : javascriptMethod.getIds())
                        	function.put(fId.toString(), javascriptMethod.get(fId.toString(), scope));
                    }
                }
			}
		
		returnResult("RespondEvalScope", context, threadID, callResults, outData);
	}
	
	private void callFunctionInScope(Context context, Object threadID, JSONObject data) throws Exception {
		
		String functionName = data.getString("FunctionName");
		Function function = (Function)scope.get(functionName, scope);
		
		ArrayList<Object> arguments = new ArrayList<Object>();
		for (Object o : data.getJSONArray("Arguments"))
			arguments.add(o);
		
		final Object callResults = function.call(context, scope, scope, arguments.toArray());
		returnResult("RespondCallFunctionInScope", context, threadID, callResults);
	}
	
	private void callCallback(Context context, Object threadID, JSONObject data) throws Exception {
		
		Object callbackID = data.get("CallbackId");
		Function function = callbacks.get(callbackID);
		
		ArrayList<Object> arguments = new ArrayList<Object>();
		for (Object o : data.getJSONArray("Arguments"))
			arguments.add(o);
		
		final Object callResults = function.call(context, scope, scope, arguments.toArray());
		returnResult("RespondCallCallback", context, threadID, callResults);
	}
	
	private void returnResult(String command, Context context, Object threadID, Object callResults) throws JSONException, IOException {
		returnResult(command, context, threadID, callResults, new JSONObject());
	}
	
	private void returnResult(String command, final Context context, Object threadID, final Object callResults, JSONObject outData) throws JSONException, IOException {
		
		if (!Undefined.class.isInstance(callResults))
			outData.put("Result", new JSONString() {
	
				@Override
				public String toJSONString() {
					return jsonStringifyFunction.call(context, scope, scope, new Object[] { callResults }).toString();
				}
				
			});
		
		sendCommand(command, threadID, outData);
	}
	
	private void sendCommand(String command, Object threadID, JSONObject data) throws JSONException, IOException {
		
		JSONObject outCommand = new JSONObject();
		outCommand.put("Data", data);
		outCommand.put("ScopeID", scopeID);
		outCommand.put("ThreadID", threadID);
		outCommand.put("Command", command);

		outputStreamWriter.write(outCommand.toString());
		outputStreamWriter.flush();
	}
	
	private static Method getCallFunctionInParentProcessMethod() {
		try {
			return ScopeWrapper.class.getMethod("callFunctionInParentProcess", new Class[] { Context.class, Object[].class, Function.class, boolean.class });
		} catch (Exception e) {
			throw new RuntimeException(e);
		}
	}
	
	// Calls a function in the parent process
	public static Object callFunctionInParentProcess(final Context context, Object[] args, Function ctorObj, boolean inNewExpr) throws Exception {
		
		ScopeWrapperAndThreadID scopeWrapperAndThreadID = scopeWrapperAndThreadIDsByContext.get(context);
		return scopeWrapperAndThreadID.scopeWrapper.callFunctionInParentProcessInstance(context, args, scopeWrapperAndThreadID.threadID);
	}
	
	// Calls a function in the parent process
	public Object callFunctionInParentProcessInstance(final Context context, Object[] args, Object threadID) throws Exception {
		ScriptableObject arguments = (ScriptableObject)args[1];
		JSONArray argumentsForJSON = new JSONArray();
		
		// Extract callbacks so that the parent process can identify them and use them
		// The callbacks must be destroyed after this part of the call stack is complete.  For now, there is no way for
		// the parent process to hold on to them in the "heap"
		ArrayList<Object> callbackIDs = new ArrayList<Object>();
		
		try {
			
			for (Object argumentIndexObj : arguments.getIds()) {
				
				int argumentIndex = (Integer)argumentIndexObj;
				Object argument = arguments.get(argumentIndex, scope);
				
				if (Function.class.isInstance(argument)) {
					Object callbackID = random.nextInt();
					
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
			data.put("FunctionName", args[0]);
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
					
					Object toReturn = inCommand.getJSONObject("Data").get("Result");
	
					// If the object is a JSONArray or JSONObject, then it can't be directly consumed in Rhino and must be
					// re-de-serialized in Rhino
					if (JSONArray.class.isInstance(toReturn) || JSONObject.class.isInstance(toReturn))				
						return context.evaluateString(scope, "(" + toReturn.toString() + ")", "<cmd>", 1, null);
					
					return toReturn;
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
	
	// json2.js, minimized
	private final static String json2 = "\"use strict\";if(!this.JSON){this.JSON={};}(function(){function f(n){return n<10?'0'+n:n;}if(typeof Date.prototype.toJSON!=='function'){Date.prototype.toJSON=function(key){return isFinite(this.valueOf())?this.getUTCFullYear()+'-'+f(this.getUTCMonth()+1)+'-'+f(this.getUTCDate())+'T'+f(this.getUTCHours())+':'+f(this.getUTCMinutes())+':'+f(this.getUTCSeconds())+'Z':null;};String.prototype.toJSON=Number.prototype.toJSON=Boolean.prototype.toJSON=function(key){return this.valueOf();};}var cx=/[\\u0000\\u00ad\\u0600-\\u0604\\u070f\\u17b4\\u17b5\\u200c-\\u200f\\u2028-\\u202f\\u2060-\\u206f\\ufeff\\ufff0-\\uffff]/g,escapable=/[\\\\\\\"\\x00-\\x1f\\x7f-\\x9f\\u00ad\\u0600-\\u0604\\u070f\\u17b4\\u17b5\\u200c-\\u200f\\u2028-\\u202f\\u2060-\\u206f\\ufeff\\ufff0-\\uffff]/g,gap,indent,meta={'\\b':'\\\\b','\\t':'\\\\t','\\n':'\\\\n','\\f':'\\\\f','\\r':'\\\\r','\"':'\\\\\"','\\\\':'\\\\\\\\'},rep;function quote(string){escapable.lastIndex=0;return escapable.test(string)?'\"'+string.replace(escapable,function(a){var c=meta[a];return typeof c==='string'?c:'\\\\u'+('0000'+a.charCodeAt(0).toString(16)).slice(-4);})+'\"':'\"'+string+'\"';}function str(key,holder){var i,k,v,length,mind=gap,partial,value=holder[key];if(value&&typeof value==='object'&&typeof value.toJSON==='function'){value=value.toJSON(key);}if(typeof rep==='function'){value=rep.call(holder,key,value);}switch(typeof value){case'string':return quote(value);case'number':return isFinite(value)?String(value):'null';case'boolean':case'null':return String(value);case'object':if(!value){return'null';}gap+=indent;partial=[];if(Object.prototype.toString.apply(value)==='[object Array]'){length=value.length;for(i=0;i<length;i+=1){partial[i]=str(i,value)||'null';}v=partial.length===0?'[]':gap?'[\\n'+gap+partial.join(',\\n'+gap)+'\\n'+mind+']':'['+partial.join(',')+']';gap=mind;return v;}if(rep&&typeof rep==='object'){length=rep.length;for(i=0;i<length;i+=1){k=rep[i];if(typeof k==='string'){v=str(k,value);if(v){partial.push(quote(k)+(gap?': ':':')+v);}}}}else{for(k in value){if(Object.hasOwnProperty.call(value,k)){v=str(k,value);if(v){partial.push(quote(k)+(gap?': ':':')+v);}}}}v=partial.length===0?'{}':gap?'{\\n'+gap+partial.join(',\\n'+gap)+'\\n'+mind+'}':'{'+partial.join(',')+'}';gap=mind;return v;}}if(typeof JSON.stringify!=='function'){JSON.stringify=function(value,replacer,space){var i;gap='';indent='';if(typeof space==='number'){for(i=0;i<space;i+=1){indent+=' ';}}else if(typeof space==='string'){indent=space;}rep=replacer;if(replacer&&typeof replacer!=='function'&&(typeof replacer!=='object'||typeof replacer.length!=='number')){throw new Error('JSON.stringify');}return str('',{'':value});};}if(typeof JSON.parse!=='function'){JSON.parse=function(text,reviver){var j;function walk(holder,key){var k,v,value=holder[key];if(value&&typeof value==='object'){for(k in value){if(Object.hasOwnProperty.call(value,k)){v=walk(value,k);if(v!==undefined){value[k]=v;}else{delete value[k];}}}}return reviver.call(holder,key,value);}cx.lastIndex=0;if(cx.test(text)){text=text.replace(cx,function(a){return'\\\\u'+('0000'+a.charCodeAt(0).toString(16)).slice(-4);});}if(/^[\\],:{}\\s]*$/.test(text.replace(/\\\\(?:[\"\\\\\\/bfnrt]|u[0-9a-fA-F]{4})/g,'@').replace(/\"[^\"\\\\\\n\\r]*\"|true|false|null|-?\\d+(?:\\.\\d*)?(?:[eE][+\\-]?\\d+)?/g,']').replace(/(?:^|:|,)(?:\\s*\\[)+/g,''))){j=eval('('+text+')');return typeof reviver==='function'?walk({'':j},''):j;}throw new SyntaxError('JSON.parse');};}}());";
}
