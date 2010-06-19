package com.objectcloud.javascriptprocess;

import java.io.IOException;
import java.io.OutputStreamWriter;
import java.util.ArrayList;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;
import org.json.JSONString;
import org.mozilla.javascript.Callable;
import org.mozilla.javascript.ClassShutter;
import org.mozilla.javascript.Context;
import org.mozilla.javascript.Function;
import org.mozilla.javascript.JavaScriptException;
import org.mozilla.javascript.NativeFunction;
import org.mozilla.javascript.Scriptable;
import org.mozilla.javascript.ScriptableObject;
import org.mozilla.javascript.Undefined;

public class ParentScope {
	
	private final IOPump ioPump;
	private final ScriptableObject scope;
	private final ArrayList<NativeFunction> compiledScripts = new ArrayList<NativeFunction>();
	private final NativeFunction getJsonStringifyFunction = new Json2stringify();
	private final NativeFunction getJsonParseFunction = new Json2parse();
	//private final NativeFunction getThrowFunction = new ThrowFunction();
	//private final Function throwFunction;
	private final Function jsonStringifyFunction;
	private final ArrayList<String> functions = new ArrayList<String>();
	private final OutputStreamWriter outputStreamWriter;

	public ParentScope(IOPump ioPump, JSONObject data, OutputStreamWriter outputStreamWriter) throws Exception {
		
		this.ioPump = ioPump;
		this.outputStreamWriter = outputStreamWriter;
		
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

            scope = context.initStandardObjects();
            
            // Load custom version of eval
            scope.put("eval", scope, new EvalCallable());

            // Load JSON methods
            Json2 json2 = new Json2();
            json2.call(context, scope, scope, null);
            jsonStringifyFunction = (Function)getJsonStringifyFunction.call(context, scope, scope, null);
            
            // Get easy way to throw
            //throwFunction = (Function)getThrowFunction.call(context, scope, scope, null);
            
        	/* // Uncomment to test stderr
            // Add function to test stderr
            Method testErrorMethod = ParentScope.class.getMethod("testError", String.class);
            FunctionObject testErrorMethodFunctionObject = new FunctionObject(
            	"testError",
            	testErrorMethod,
            	scope);
            scope.put("testError", scope, testErrorMethodFunctionObject);*/

            // Decode the functions for future placement into the child scope
            if (data.has("Functions"))
				for (Object function : data.getJSONArray("Functions"))
					functions.add(function.toString());
	
			JSONObject outData = new JSONObject();
			
			try {
				JSONArray scripts = data.getJSONArray("Scripts");
								
				CompiledJavascriptTracker cjt = CompiledJavascriptTracker.getInstance();
				
				for (int scriptCtr = 0; scriptCtr < scripts.length(); scriptCtr++) {
					String script = scripts.getString(scriptCtr);
					NativeFunction compiledScript = cjt.getGetOrCompileScript(script);
					compiledScripts.add(compiledScript);
				}
			} catch (JavaScriptException je) {
				returnResult(context, je.getValue(), outData, "Exception");
				throw je;
			} catch (Exception e) {
				returnResult(context, e.getMessage(), outData, "Exception");
				throw e;
			}
				
			outputStreamWriter.write("{}\r\n");
			outputStreamWriter.flush();
			
			// This makes the parent scope sealed and immutable
			scope.sealObject();
			
		} finally {
            Context.exit();
        }
	}
	
	private final static CompiledJavascriptTracker compiledJavascriptTracker = CompiledJavascriptTracker.getInstance();
	
	private class EvalCallable implements Callable {

		@Override
		public Object call(Context context, Scriptable scope, Scriptable thisObj, Object[] args) {
			
			String script = args[0].toString();
			
			if (script.length() < 15)
				return context.evaluateString(scope, args[0].toString(), "<cmd>", 0, null);
			else {
				NativeFunction nativeFunction;
				
				try {
					nativeFunction = compiledJavascriptTracker.getGetOrCompileScript(script);
				} catch (RuntimeException re) {
					throw re;
				} catch (Exception e) {
					throw new RuntimeException(e);
				}
				
				return nativeFunction.call(context, scope, thisObj, null);
			}
		}
		
	}
	
	/* // Uncomment to test stderr
	public static void testError(String errorString) {
		System.err.println(errorString);
	}*/

	private void returnResult(Context context, Object callResults, JSONObject outData, String resultsName) throws JSONException, IOException {

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
		
		outputStreamWriter.write(outData.toString());
		outputStreamWriter.write("\r\n");
		outputStreamWriter.flush();
	}
	
	public ScopeWrapper createScopeWrapper(int scopeID) {
		return new ScopeWrapper(ioPump, outputStreamWriter, scopeID, this);	
	}
	
	public class ScriptableAndResult {
		public Scriptable scope;
		public Function jsonStringifyFunction;
		public Function jsonParseFunction;
	}
	
	public ScriptableAndResult createScope(Context context) {

		Scriptable childScope = context.newObject(scope);
		childScope.setPrototype(scope);
		childScope.setParentScope(null);
		
		ScriptableAndResult toReturn = new ScriptableAndResult();
		toReturn.scope = childScope;
		toReturn.jsonStringifyFunction = (Function)getJsonStringifyFunction.call(context, childScope, childScope, null);
		toReturn.jsonParseFunction = (Function)getJsonParseFunction.call(context, childScope, childScope, null);
		
		return toReturn;
	}

	public ArrayList<NativeFunction> getCompiledScripts() {
		return compiledScripts;
	}

	public ArrayList<String> getFunctions() {
		return functions;
	}

	// Creates an empty scope
	public Scriptable createDummyScope(Context context) {
		Scriptable dummyScope = context.newObject(scope);
		dummyScope.setPrototype(scope);
		dummyScope.setParentScope(null);
		
		return dummyScope;
	}
}
