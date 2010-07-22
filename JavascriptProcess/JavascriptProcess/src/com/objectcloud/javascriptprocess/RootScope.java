package com.objectcloud.javascriptprocess;

import org.mozilla.javascript.Callable;
import org.mozilla.javascript.ClassShutter;
import org.mozilla.javascript.Context;
import org.mozilla.javascript.NativeFunction;
import org.mozilla.javascript.Scriptable;
import org.mozilla.javascript.ScriptableObject;

public class RootScope {
	private static final ScriptableObject scope;

	static {
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
            getScope().put("eval", getScope(), new EvalCallable());

            // Load JSON methods
            Json2 json2 = new Json2();
            json2.call(context, getScope(), getScope(), null);
			
			// This makes the parent scope sealed and immutable
			getScope().sealObject();
			
		} finally {
            Context.exit();
        }
	}

	private static class EvalCallable implements Callable {

		private final static CompiledJavascriptTracker compiledJavascriptTracker = CompiledJavascriptTracker.getInstance();
		
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

	public static ScriptableObject getScope() {
		return scope;
	}
}
