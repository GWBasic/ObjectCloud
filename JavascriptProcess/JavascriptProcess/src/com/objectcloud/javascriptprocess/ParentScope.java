package com.objectcloud.javascriptprocess;

import java.io.IOException;
import java.io.OutputStreamWriter;
import java.lang.reflect.Method;
import java.util.ArrayList;
import java.util.Stack;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;
import org.json.JSONString;
import org.mozilla.javascript.ClassShutter;
import org.mozilla.javascript.CompilerEnvirons;
import org.mozilla.javascript.Context;
import org.mozilla.javascript.Function;
import org.mozilla.javascript.FunctionObject;
import org.mozilla.javascript.NativeFunction;
import org.mozilla.javascript.Script;
import org.mozilla.javascript.Scriptable;
import org.mozilla.javascript.ScriptableObject;
import org.mozilla.javascript.Undefined;
import org.mozilla.javascript.optimizer.ClassCompiler;

public class ParentScope {
   
	private final IOPump ioPump;
    private final ScriptableObject scope;
    private final ArrayList<NativeFunction> compiledScripts = new ArrayList<NativeFunction>();
    private final Script getJsonStringifyFunction;
    private final Script getJsonParseFunction;
    private final Function jsonStringifyFunction;
    private final static Method callFunctionInParentProcessMethod = ScopeWrapper.getCallFunctionInParentProcessMethod();
    private final static String callFunctionInParentProcessName = "_____callParentFunction";
    private final OutputStreamWriter outputStreamWriter;

    private int numScriptsThatCanBePreloaded = 0;
    public int getNumScriptsThatCanBePreloaded() {
		return numScriptsThatCanBePreloaded;
	}

	private final Stack<ScriptableAndResult> preLoadedScopes = new Stack<ScriptableAndResult>();
    
    // a pre-loaded scope and its result, if appropriate
    public class ScriptableAndResult {
    	Scriptable scope;
    	Object result;
        public Function jsonStringifyFunction;
        public Function jsonParseFunction;
    }
   
    // This is a separate class for memory reasons
    private class JavascriptCompiler extends ClassLoader {
    	
    	public JavascriptCompiler(Context context) {
            CompilerEnvirons compilerEnvirons = new CompilerEnvirons();
            compilerEnvirons.setOptimizationLevel(1);
            classCompiler = new ClassCompiler(compilerEnvirons);
    		this.context = context;
    	}
    	
    	ClassCompiler classCompiler;
    	Context context;
    	
    	Integer classCtr = 0;
       
        public Class<?> loadClass(byte[] source) {
            return this.defineClass(null, source, 0, source.length);
        }
       
	    private NativeFunction Compile(String toCompile) throws Exception {
	
	        Object[] classFiles;
	        try
	        {
	        	classFiles = classCompiler.compileToClassFiles(toCompile, "<cmd>", 0, "com.objectcloud.javascript.generated_" + classCtr.toString());
		    } catch (Exception e) {
		        returnResult(context, e.getMessage(), new JSONObject(), "Exception");
		        throw e;
		    } finally {
		    	classCtr++;
		    }
	
		    // Load all of the compiled classes, with a special case for the first class as it's the one that's instantiated
	        Class<?> compiledClass = loadClass((byte[])classFiles[1]);
	        for (int ctr = 3; ctr < classFiles.length; ctr = ctr + 2)
	            loadClass((byte[])classFiles[ctr]);
	       
	        return (NativeFunction)compiledClass.getConstructor().newInstance();
	    }
    }
    
    public ParentScope(IOPump ioPump, JSONObject data, OutputStreamWriter outputStreamWriter) throws Exception {
       
        JSONArray scripts = data.getJSONArray("Scripts");
        
        Logger.setFilename("log_" + new Integer(scripts.getString(scripts.length() - 1).hashCode()).toString() + ".log");
                       
        this.ioPump = ioPump;
        this.outputStreamWriter = outputStreamWriter;
       
        final Context context = Context.enter();

        try {
            // Make sure that Javascript calls to Java can't escape
            try {
                context.setClassShutter(new ClassShutter() {
                    @Override
                    public boolean visibleToScripts(String className) {
                        return className.startsWith("org.mozilla.javascript.");
                    }
                });
               
            // For now these are being swallowed because they seem to occur if setting this twice
            } catch (SecurityException se) {}

            //long start = System.nanoTime();

            scope = context.initStandardObjects();

            JavascriptCompiler javascriptCompiler = new JavascriptCompiler(context);
           
            // Compile the functions accessors
            NativeFunction compiledFunctions = null;
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
                
                compiledFunctions = javascriptCompiler.Compile(functionsBuilder.toString());
            }
           
            //Logger.log("context.initStandardObjects() took " + (new Long(System.nanoTime() - start)).toString());
            //start = System.nanoTime();

            // Load JSON methods
            Json2 myJson2 = new Json2();
            myJson2.call(context, scope, scope, null);
            getJsonStringifyFunction = context.compileString("JSON.stringify", "JSON.stringify", 0, null);
            getJsonParseFunction =  context.compileString("JSON.parse", "JSON.parse", 0, null);
            jsonStringifyFunction = (Function)getJsonStringifyFunction.exec(context, scope);
           
            // Load external function caller
            FunctionObject callFunctionInParentProcessMethodFunctionObject = new FunctionObject(
                callFunctionInParentProcessName,
                callFunctionInParentProcessMethod,
                scope);
            scope.put(callFunctionInParentProcessName, scope, callFunctionInParentProcessMethodFunctionObject);
            
            // Add function wrappers
            if (null != compiledFunctions)
            	compiledFunctions.call(context, scope, scope, null);

            for (int scriptCtr = 0; scriptCtr < scripts.length(); scriptCtr++)
            	compiledScripts.add(javascriptCompiler.Compile(scripts.getString(scriptCtr)));

            //Logger.log("Setting up default functions in scope took " + (new Long(System.nanoTime() - start)).toString());
           
            /* // Uncomment to test stderr
            // Add function to test stderr
            Method testErrorMethod = ParentScope.class.getMethod("testError", String.class);
            FunctionObject testErrorMethodFunctionObject = new FunctionObject(
                "testError",
                testErrorMethod,
                scope);
            scope.put("testError", scope, testErrorMethodFunctionObject);*/


            // This makes the parent scope sealed and immutable
            scope.sealObject();

            // Determine how many scopes can be preloaded
            Scriptable childScope = context.newObject(scope);
            childScope.setPrototype(scope);
            childScope.setParentScope(null);

            // Load external function replacement
            FunctionObject throwParentProcessCalledMethodFunctionObject = new FunctionObject(
                callFunctionInParentProcessName,
                ParentScope.class.getMethod("throwParentProcessCalled", new Class[] { Context.class, Object[].class, Function.class, boolean.class }),
                childScope);

            childScope.put(callFunctionInParentProcessName, childScope, throwParentProcessCalledMethodFunctionObject);

            // Determine how many scripts can be called before an error occurs
            try
            {
            	Object result = null;
            	
	            for (NativeFunction compiledScript : compiledScripts) {
	            	result = compiledScript.call(context, childScope, childScope, null);
	            	numScriptsThatCanBePreloaded++;
	            }
	            
	            // If all of the scripts can be called successfully, then hold onto the constructed scope
	            ScriptableAndResult preLoadedScope = new ScriptableAndResult();
	            preLoadedScope.result = result;
	            preLoadedScope.scope = childScope;
	            preLoadedScope.jsonStringifyFunction = (Function)getJsonStringifyFunction.exec(context, childScope);
	            preLoadedScope.jsonParseFunction = (Function)getJsonParseFunction.exec(context, childScope);

	            preLoadedScopes.push(preLoadedScope);
	            
            } catch (Exception e) {
            	Logger.log("Exception preventing pre-loading of type: " + e.getClass().getName() + "\n: Message" + e.getMessage() + "\nScript:\n\n" + scripts.getString(numScriptsThatCanBePreloaded));
            }
            
            Logger.log(String.format("Total number of scripts: %d, number pre-runnable: %d", compiledScripts.size(), numScriptsThatCanBePreloaded));
            
            while (preLoadedScopes.size() < 25)
            	preLoadScope(context);
            
            outputStreamWriter.write("{}\r\n");
            outputStreamWriter.flush();

        } finally {
            Context.exit();
        }
    }
   
    /* // Uncomment to test stderr
    public static void testError(String errorString) {
        System.err.println(errorString);
    }*/
    
    private static class ParentProcessCalled extends Exception {

		/**
		 * 
		 */
		private static final long serialVersionUID = 9088002533382279624L;
    	
    }
    
    
    public static Object throwParentProcessCalled(Context context, Object[] args, Function ctorObj, boolean inNewExpr) throws Exception {
    	throw new ParentProcessCalled();
    }

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
   
    public ScriptableAndResult createScope(Context context) {
    
    	ScriptableAndResult toReturn = null;
    	synchronized (preLoadedScopes) {
    		if (!preLoadedScopes.empty())
    			toReturn = preLoadedScopes.pop();
    	}
    	
    	// If no pre-loaded scope was found, create it, else, schedule creating a replacement in the threadpool
    	if (null == toReturn)
    		toReturn = createAndPreLoadScope(context);
    	else {
			Runnable command = new Runnable() {
				
				@Override
				public void run() {
					
					Context subContext = Context.enter();
					
					try {
						preLoadScope(subContext);
					} finally {
						Context.exit();
					}
				}
				
			};
			
			IOPump.getExecutorservice().execute(command);
    	}    		
    	
        return toReturn;
    }
    
    public void preLoadScope(Context context) {

    	ScriptableAndResult pls = createAndPreLoadScope(context);
    	
    	synchronized (preLoadedScopes) {
    		preLoadedScopes.push(pls);
    	}
    }
    
    public ScriptableAndResult createAndPreLoadScope(Context context) {
        Scriptable childScope = context.newObject(scope);
        childScope.setPrototype(scope);
        childScope.setParentScope(null);

    	Object result = null;

    	for (int ctr = 0; ctr < numScriptsThatCanBePreloaded; ctr++)
        	result = compiledScripts.get(ctr).call(context, childScope, childScope, null);

    	ScriptableAndResult toReturn = new ScriptableAndResult();
    	toReturn.result = result;
    	toReturn.scope = childScope;
        toReturn.jsonStringifyFunction = (Function)getJsonStringifyFunction.exec(context, scope);
        toReturn.jsonParseFunction = (Function)getJsonParseFunction.exec(context, scope);

    	return toReturn;
    }

    public ArrayList<NativeFunction> getCompiledScripts() {
        return compiledScripts;
    }
}
