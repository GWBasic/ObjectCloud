package com.objectcloud.javascriptprocess;

import java.io.OutputStreamWriter;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.Iterator;
import java.util.Random;

import org.json.JSONArray;
import org.json.JSONObject;
import org.mozilla.javascript.CompilerEnvirons;
import org.mozilla.javascript.EvaluatorException;
import org.mozilla.javascript.NativeFunction;
import org.mozilla.javascript.optimizer.ClassCompiler;

public class CompiledJavascriptTracker {
	
	private final OutputStreamWriter outputStreamWriter;
	private final ClassCompiler classCompiler;
	private final MyClassLoader classLoader = new MyClassLoader();
	
	public CompiledJavascriptTracker(OutputStreamWriter outputStreamWriter) {
        CompilerEnvirons compilerEnvirons = new CompilerEnvirons();
        compilerEnvirons.setOptimizationLevel(1);
        classCompiler = new ClassCompiler(compilerEnvirons);

        this.outputStreamWriter = outputStreamWriter;
	}
	
	public void handle(JSONObject inCommand) {
		try {
			String command = inCommand.getString("Command");
			JSONObject data = inCommand.getJSONObject("Data");
			
			JSONObject toReturn = new JSONObject();

			try {
				if (command.equals("LoadCompiled")) {
					toReturn.put("Command", "RespondLoadCompiled");
					data = loadCompiled(data);
				}
				
				else if (command.equals("Compile")) {
					toReturn.put("Command", "RespondCompiled");
					data = compile(data);
				}
				
				else {
					System.err.println(JSONObject.quote(command + " is unsupported"));
					return;
				}
			} catch (Exception e) {
				data = new JSONObject();
				data.put("Exception", e.getMessage());
			}
			
			toReturn.put("Data", data);
			toReturn.put("ThreadID", inCommand.get("ThreadID"));
			
			// Send the result back to the parent process
			synchronized (outputStreamWriter) {
				outputStreamWriter.write(toReturn.toString() + "\r\n");
				outputStreamWriter.flush();
			}

		} catch (Exception e) {
			StringBuilder toReturn = new StringBuilder();
			toReturn.append(e.getMessage());
			
			for (StackTraceElement ste : e.getStackTrace())
				toReturn.append("\n" + ste.toString());
			
			System.err.println(JSONObject.quote(toReturn.toString()));
		}
	}
	
    private class MyClassLoader extends ClassLoader {
    	
    	public MyClassLoader() { }
    	
    	
        public Class<?> loadClass(byte[] source) {
            return this.defineClass(null, source, 0, source.length);
        }
    }

    Random random = new Random();
    
    private void loadNativeFunctionFromClasses(int scriptId, Iterable<byte[]> compiledClasses) throws Exception {
    	
    	Iterator<byte[]> iterator = compiledClasses.iterator();
    	
    	// Load all of the classes, holding on to the first one
    	Class<?> compiledClass = classLoader.loadClass(iterator.next());
    	while (iterator.hasNext())
    		classLoader.loadClass(iterator.next());
    	
    	NativeFunction script = (NativeFunction)compiledClass.getConstructor().newInstance();
    	
    	synchronized (scripts) {
    		scripts.put(scriptId, script);
    	}
    }
	
	private JSONObject compile(JSONObject data) throws Exception {
		
		String script = data.getString("Script");
		int scriptId = data.getInt("ScriptID");
		
		String uniqueName = new Long(Math.abs(random.nextLong())).toString() + new Long(Math.abs(random.nextLong())).toString(); 
		
        Object[] classFiles;
        
        try {
            classFiles = classCompiler.compileToClassFiles(script, "<cmd>", 0, "com.objectcloud.javascript.generated_" + uniqueName);
        } catch (EvaluatorException ee) {
        	JSONObject toReturn = new JSONObject();
        	toReturn.put("Exception", ee.getMessage() + "\nline: " + (new Integer(ee.lineNumber())).toString() + "\ncolumn: " + (new Integer(ee.columnNumber())).toString());
        	return toReturn;
        }
        
        // Convert Rhino's weirdo return format into a structure usable by the class loader and returnable
        ArrayList<byte[]> compiledClasses = new ArrayList<byte[]>();
        JSONArray compiledClassesBase64 = new JSONArray();
    	
        for (int ctr = 1; ctr < classFiles.length; ctr = ctr + 2) {
        	compiledClasses.add((byte[])classFiles[ctr]);
        	compiledClassesBase64.put(Base64.encodeBytes((byte[])classFiles[ctr]));
        }
        
        loadNativeFunctionFromClasses(scriptId, compiledClasses);
        
        JSONObject toReturn = new JSONObject();
        toReturn.put("CompiledScript", compiledClassesBase64);
       
        return toReturn;
	}

	private JSONObject loadCompiled(JSONObject data) throws Exception {
		
		int scriptId = data.getInt("ScriptID");
        JSONArray compiledClassesBase64 = data.getJSONArray("CompiledScript");

        ArrayList<byte[]> compiledClasses = new ArrayList<byte[]>();
        for (int ctr = 0; ctr < compiledClassesBase64.length(); ctr++)
        	compiledClasses.add(Base64.decode(compiledClassesBase64.getString(ctr)));
        
        loadNativeFunctionFromClasses(scriptId, compiledClasses);

        return new JSONObject();
	}
	
	private final HashMap<Integer, NativeFunction> scripts = new HashMap<Integer, NativeFunction>(); 
	
	public class ScriptNotFound extends Exception {

		/**
		 * 
		 */
		private static final long serialVersionUID = 7812710462256075900L;

		public ScriptNotFound(String message) {
			super(message);
		}
	}
	
	public NativeFunction getScript(int scriptID) throws ScriptNotFound {
		synchronized (scripts) {
			if (scripts.containsKey(scriptID))
				return scripts.get(new Integer(scriptID));
		}

		throw new ScriptNotFound("There is no script with ID " + new Integer(scriptID).toString());
	}
}
