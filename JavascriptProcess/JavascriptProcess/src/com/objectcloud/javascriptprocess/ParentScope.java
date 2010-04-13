package com.objectcloud.javascriptprocess;

import java.io.IOException;
import java.io.OutputStreamWriter;
import java.lang.reflect.Method;
import java.util.ArrayList;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;
import org.json.JSONString;
import org.mozilla.javascript.ClassShutter;
import org.mozilla.javascript.Context;
import org.mozilla.javascript.Function;
import org.mozilla.javascript.FunctionObject;
import org.mozilla.javascript.JavaScriptException;
import org.mozilla.javascript.Script;
import org.mozilla.javascript.Scriptable;
import org.mozilla.javascript.ScriptableObject;
import org.mozilla.javascript.Undefined;

public class ParentScope {
	
	IOPump ioPump;
	ScriptableObject scope;
	ArrayList<Script> compiledScripts = new ArrayList<Script>();
	Script getJsonStringifyFunction;
	Script getJsonParseFunction;
	Function jsonStringifyFunction;
	private final static Method callFunctionInParentProcessMethod = ScopeWrapper.getCallFunctionInParentProcessMethod();
	private final static String callFunctionInParentProcessName = "_____callParentFunction";
	OutputStreamWriter outputStreamWriter;

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

            // Load JSON methods
            context.evaluateString(scope, json2, "<cmd>", 1, null);
            getJsonStringifyFunction = context.compileString("JSON.stringify", "JSON.stringify", 0, null);
            getJsonParseFunction =  context.compileString("JSON.parse", "JSON.parse", 0, null);
            jsonStringifyFunction = (Function)getJsonStringifyFunction.exec(context, scope);
            
            // Load external function caller
            FunctionObject callFunctionInParentProcessMethodFunctionObject = new FunctionObject(
            	callFunctionInParentProcessName,
            	callFunctionInParentProcessMethod,
            	scope);
            scope.put(callFunctionInParentProcessName, scope, callFunctionInParentProcessMethodFunctionObject);

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
	
			JSONObject outData = new JSONObject();
			
			try {
				JSONArray scripts = data.getJSONArray("Scripts");
								
				for (int scriptCtr = 0; scriptCtr < scripts.length(); scriptCtr++)
					compiledScripts.add(context.compileString(scripts.getString(scriptCtr), new Integer(scriptCtr).toString(), 0, null));
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
		toReturn.jsonStringifyFunction = (Function)getJsonStringifyFunction.exec(context, childScope);
		toReturn.jsonParseFunction = (Function)getJsonParseFunction.exec(context, childScope);
		
		return toReturn;
	}

	public ArrayList<Script> getCompiledScripts() {
		return compiledScripts;
	}

	// json2.js, minimized
	private final static String json2 = "\"use strict\";if(!this.JSON){this.JSON={};}(function(){function f(n){return n<10?'0'+n:n;}if(typeof Date.prototype.toJSON!=='function'){Date.prototype.toJSON=function(key){return isFinite(this.valueOf())?this.getUTCFullYear()+'-'+f(this.getUTCMonth()+1)+'-'+f(this.getUTCDate())+'T'+f(this.getUTCHours())+':'+f(this.getUTCMinutes())+':'+f(this.getUTCSeconds())+'Z':null;};String.prototype.toJSON=Number.prototype.toJSON=Boolean.prototype.toJSON=function(key){return this.valueOf();};}var cx=/[\\u0000\\u00ad\\u0600-\\u0604\\u070f\\u17b4\\u17b5\\u200c-\\u200f\\u2028-\\u202f\\u2060-\\u206f\\ufeff\\ufff0-\\uffff]/g,escapable=/[\\\\\\\"\\x00-\\x1f\\x7f-\\x9f\\u00ad\\u0600-\\u0604\\u070f\\u17b4\\u17b5\\u200c-\\u200f\\u2028-\\u202f\\u2060-\\u206f\\ufeff\\ufff0-\\uffff]/g,gap,indent,meta={'\\b':'\\\\b','\\t':'\\\\t','\\n':'\\\\n','\\f':'\\\\f','\\r':'\\\\r','\"':'\\\\\"','\\\\':'\\\\\\\\'},rep;function quote(string){escapable.lastIndex=0;return escapable.test(string)?'\"'+string.replace(escapable,function(a){var c=meta[a];return typeof c==='string'?c:'\\\\u'+('0000'+a.charCodeAt(0).toString(16)).slice(-4);})+'\"':'\"'+string+'\"';}function str(key,holder){var i,k,v,length,mind=gap,partial,value=holder[key];if(value&&typeof value==='object'&&typeof value.toJSON==='function'){value=value.toJSON(key);}if(typeof rep==='function'){value=rep.call(holder,key,value);}switch(typeof value){case'string':return quote(value);case'number':return isFinite(value)?String(value):'null';case'boolean':case'null':return String(value);case'object':if(!value){return'null';}gap+=indent;partial=[];if(Object.prototype.toString.apply(value)==='[object Array]'){length=value.length;for(i=0;i<length;i+=1){partial[i]=str(i,value)||'null';}v=partial.length===0?'[]':gap?'[\\n'+gap+partial.join(',\\n'+gap)+'\\n'+mind+']':'['+partial.join(',')+']';gap=mind;return v;}if(rep&&typeof rep==='object'){length=rep.length;for(i=0;i<length;i+=1){k=rep[i];if(typeof k==='string'){v=str(k,value);if(v){partial.push(quote(k)+(gap?': ':':')+v);}}}}else{for(k in value){if(Object.hasOwnProperty.call(value,k)){v=str(k,value);if(v){partial.push(quote(k)+(gap?': ':':')+v);}}}}v=partial.length===0?'{}':gap?'{\\n'+gap+partial.join(',\\n'+gap)+'\\n'+mind+'}':'{'+partial.join(',')+'}';gap=mind;return v;}}if(typeof JSON.stringify!=='function'){JSON.stringify=function(value,replacer,space){var i;gap='';indent='';if(typeof space==='number'){for(i=0;i<space;i+=1){indent+=' ';}}else if(typeof space==='string'){indent=space;}rep=replacer;if(replacer&&typeof replacer!=='function'&&(typeof replacer!=='object'||typeof replacer.length!=='number')){throw new Error('JSON.stringify');}return str('',{'':value});};}if(typeof JSON.parse!=='function'){JSON.parse=function(text,reviver){var j;function walk(holder,key){var k,v,value=holder[key];if(value&&typeof value==='object'){for(k in value){if(Object.hasOwnProperty.call(value,k)){v=walk(value,k);if(v!==undefined){value[k]=v;}else{delete value[k];}}}}return reviver.call(holder,key,value);}cx.lastIndex=0;if(cx.test(text)){text=text.replace(cx,function(a){return'\\\\u'+('0000'+a.charCodeAt(0).toString(16)).slice(-4);});}if(/^[\\],:{}\\s]*$/.test(text.replace(/\\\\(?:[\"\\\\\\/bfnrt]|u[0-9a-fA-F]{4})/g,'@').replace(/\"[^\"\\\\\\n\\r]*\"|true|false|null|-?\\d+(?:\\.\\d*)?(?:[eE][+\\-]?\\d+)?/g,']').replace(/(?:^|:|,)(?:\\s*\\[)+/g,''))){j=eval('('+text+')');return typeof reviver==='function'?walk({'':j},''):j;}throw new SyntaxError('JSON.parse');};}}());";
}
