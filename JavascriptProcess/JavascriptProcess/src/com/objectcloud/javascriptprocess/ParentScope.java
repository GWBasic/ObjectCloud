package com.objectcloud.javascriptprocess;

import java.io.OutputStreamWriter;

import org.mozilla.javascript.ClassShutter;
import org.mozilla.javascript.Context;
import org.mozilla.javascript.Function;
import org.mozilla.javascript.NativeFunction;
import org.mozilla.javascript.Scriptable;
import org.mozilla.javascript.ScriptableObject;

public class ParentScope {
   
	private final IOPump ioPump;
	private final OutputStreamWriter outputStreamWriter;
	private final CompiledJavascriptTracker compiledJavascriptTracker;
    private final ScriptableObject scope;
    private final NativeFunction getJsonStringifyFunction;
    private final NativeFunction getJsonParseFunction;
    final static String callFunctionInParentProcessName = "_____callParentFunction";
   
    // This is a separate class for memory reasons
    
    public ParentScope(IOPump ioPump, OutputStreamWriter outputStreamWriter, CompiledJavascriptTracker compiledJavascriptTracker) throws Exception {
      
    	this.ioPump = ioPump;
    	this.outputStreamWriter = outputStreamWriter;
    	this.compiledJavascriptTracker = compiledJavascriptTracker;
    	
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

            scope = context.initStandardObjects();

            // Load JSON methods
            Json2 myJson2 = new Json2();
            myJson2.call(context, scope, scope, null);
            getJsonStringifyFunction = new Json2stringify();
            getJsonParseFunction = new Json2parse();
           
            // This makes the parent scope sealed and immutable
            scope.sealObject();
        } finally {
            Context.exit();
        }
    }
   
    public ScopeWrapper createScopeWrapper(int scopeID) {

        final Context context = Context.enter();

        try {

	    	Scriptable childScope = context.newObject(scope);
	        childScope.setPrototype(scope);
	        childScope.setParentScope(null);
	
	        Function jsonStringifyFunction = (Function)getJsonStringifyFunction.call(context, childScope, childScope, null);
	        Function jsonParseFunction = (Function)getJsonParseFunction.call(context, childScope, childScope, null);
	
	    	return new ScopeWrapper(ioPump, outputStreamWriter, scopeID, childScope, jsonStringifyFunction, jsonParseFunction, compiledJavascriptTracker);   
        } finally {
            Context.exit();
        }
    }
}
