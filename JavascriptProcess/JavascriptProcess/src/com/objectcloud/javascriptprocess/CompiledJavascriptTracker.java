package com.objectcloud.javascriptprocess;

import java.util.HashMap;

import org.mozilla.javascript.CompilerEnvirons;
import org.mozilla.javascript.NativeFunction;
import org.mozilla.javascript.optimizer.ClassCompiler;

public class CompiledJavascriptTracker {
	
	private static final CompiledJavascriptTracker instance = new CompiledJavascriptTracker(); 
	public static CompiledJavascriptTracker getInstance() {
		return instance;
	}
	
	private final ClassCompiler classCompiler;
	private final MyClassLoader classLoader = new MyClassLoader();
	
	public CompiledJavascriptTracker() {
        CompilerEnvirons compilerEnvirons = new CompilerEnvirons();
        compilerEnvirons.setOptimizationLevel(1);
        classCompiler = new ClassCompiler(compilerEnvirons);
	}
	
    private class MyClassLoader extends ClassLoader {
    	
    	public MyClassLoader() { }
    	
    	
        public Class<?> loadClass(byte[] source) {
            return this.defineClass(null, source, 0, source.length);
        }
    }

	private NativeFunction compile(String script) throws Exception {
		
        Object[] classFiles;
        
        long hashCode = script.hashCode();
        hashCode = hashCode - Integer.MIN_VALUE;
        
   		classFiles = classCompiler.compileToClassFiles(script, "<cmd>", 0, "com.objectcloud.javascript.generated_" + new Long(hashCode).toString());
        
    	// Load all of the generated classes from Rhino's weirdo return format
    	Class<?> nativeFunctionClass = classLoader.loadClass((byte[])classFiles[1]);
        for (int ctr = 3; ctr < classFiles.length; ctr = ctr + 2) {
        	classLoader.loadClass((byte[])classFiles[3]);
        }
        
        return (NativeFunction)nativeFunctionClass.getConstructor().newInstance();
	}

	private final HashMap<String, NativeFunction> scripts = new HashMap<String, NativeFunction>(); 
	
	// Only lets one thread compile at a time
	private final Object CompileKey = new Object();
	
	public NativeFunction getGetOrCompileScript(String script) throws Exception {
		
		synchronized (scripts) {
			if (scripts.containsKey(script))
				return scripts.get(script);
		}
		
		synchronized (CompileKey) {

			synchronized (scripts) {
				if (scripts.containsKey(script))
					return scripts.get(script);
			}
			
			NativeFunction toReturn = compile(script);
		
			synchronized (scripts) {
				scripts.put(script, toReturn);
			}

			return toReturn;
		}
	}
}
