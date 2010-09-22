/*
 * objectcloud.js
 *
 * Driver for consuming ObjectCloud objects in Node
 *
 * usage:

require("./objectcloud").connect(
	{
		numConnections: 1, // The number of concurrent requests to send to ObjectCloud
		username: 'username',
		password: 'password',
		port: 1080, // defaults to 80
		host: 'hostname' // NOTE:  This must be exact; ObjectCloud by default redirects to its
		// configured host; and this driver doesn't yet handle redirects
	},
	function(objectCloudConnection)
	{
		console.log("Successfully connected to ObjectCloud");
		
		objectCloudConnection.open(
			'/path to a file in ObjectCloud',
			function(wrapper)
			{
				// You can now use wrapper with the same API that's available to in-browser Javascript
			});
	});
 *
 * (C) 2010 Andrew Rondeau
 * Released under the SimPL 2.0 license, see http://opensource.org/licenses/simpl-2.0.html
 */

// Node doesn't put Content-Length into headers, but ObjectCloud requires it
Buffer = require('buffer').Buffer;

function alert(message)
{
	console.log(message);
}
 
function createObjectCloudConnection(getObjectCloudClient, objectcloudRequestMetadata, objectCloudSessionCookie)
{
	/*
	 * Emulates AJAX.  In the browser, ObjectCloud's CreateHttpRequest() function
	 * essentailly handles the IE 6 special case; but it also is used in various non-browser JS
	 * environments to emulate AJAX.  See /API/AJAX.js and /API/AJAX_serverside.js
	 */
	function CreateHttpRequest()
	{
		var toReturn =
		{
			headers: 
			{
				host: objectcloudRequestMetadata.host,
				'Cookie': objectCloudSessionCookie,
				'User-Agent': objectcloudRequestMetadata["User-Agent"]
			},

			open: function(webMethod, url, asyncronous)
			{
				if (!asyncronous)
					throw "Node.js doesn't support synchronous javascript";

				this.webMethod = webMethod;
				this.url = url;
			},

			setRequestHeader: function(name, value)
			{
				// header is currently ignored
				this.headers[name] = value;
			},

			send: function(payload)
			{
				//console.log('sending');

				if (null != payload)
					this.headers['Content-Length'] = Buffer.byteLength(payload, 'utf8');
				else if (this.webMethod == 'POST')
					console.log('WARNING!!!  POSTing NULL DATA');
				
				request = getObjectCloudClient().request(
					this.webMethod,
					this.url,
					this.headers);

				if (null != payload)
					request.write(payload, 'utf8');
	
				request.end();
				
				//console.log('sent');

				var me = this;

				request.on('response', function (response)
				{
					//console.log('response');
					
					me.responseText = '';
						
					response.on('data', function (chunk)
					{
						//console.log('data');
						me.responseText += chunk;
					});
		
					response.on('end', function ()
					{
						//console.log('end');
						me.readyState = 4;
			            me.status = response.statusCode;
						me.onreadystatechange();
					});
				});
			}
		}
		
		return toReturn;
	}

	var toReturn =
	{
		open: function(toOpen, openedCallback, errorCallback)
		{
			// Use some default callbacks
			if (!openedCallback)
				openedCallback = function(wrapper) { console.log(JSON.stringify(wrapper, null, '\t')); };
		
			if (!errorCallback)
				errorCallback = function(error) { console.log(error); };
		
			var request = getObjectCloudClient().request(
				'GET',
				toOpen + '?Method=GetJSW',
				{
					host: objectcloudRequestMetadata.host,
					'Cookie': objectCloudSessionCookie,
					'User-Agent': objectcloudRequestMetadata["User-Agent"]
				});
				
			request.end();
	
			request.on('response', function (response)
			{
				response.setEncoding('utf8');
		
				var result = '';
		
				response.on('data', function (chunk)
				{
					result += chunk;
				});
		
				response.on('end', function ()
				{
					if ('2' == (response.statusCode + '').substring(0,1))
					{
						var wrapper = eval('(' + result + ')');
						
						// Connect all the functions to CreateHttpRequest						
						for (var prop in wrapper)
							if (typeof wrapper[prop] == 'function')
							{
								wrapper[prop].prototype.CreateHttpRequest = CreateHttpRequest;
								wrapper[prop].prototype.alert = console.log;
							}
							
						openedCallback(wrapper);
					}
					else
						errorCallback(result);
				});
			});
		}
	};
	return toReturn;
}

exports.connect = function(args, connectedCallback, errorCallback)
{
	if (null == connectedCallback)
		throw "callback unspecified";
		
	if (null == errorCallback)
		errorCallback = function(objectCloudError)
		{
			console.log("Error connecting to ObjectCloud:\n" + objectCloudError);
		};

	if (!args.host)
		args.host = "localhost";
      
	if (!args.port)
		args.port = 80;
      
	if (!args.numConnections)
		args.numConnections = 1;
      
	if (!args.userAgent)
		args.userAgent = "Super happy fun node-based ObjectCloud client";
      
	if (!args.username)
		throw "args.username unspecified";
      
	if (!args.password)
		throw "args.password unspecified";

	var objectcloudRequestMetadata =
	{
		'User-Agent': args.userAgent
	};
	
	if (args.port == 80)
		objectcloudRequestMetadata.host = args.host;
	else
		objectcloudRequestMetadata.host = args.host + ':' + args.port;

	// Load dependancies
	var http = require('http');
	var querystring = require('querystring');

	// Create the ObjectCloud clients
	var objectCloudClients = [];
	for (var i = 0; i < args.numConnections; i++)
		objectCloudClients.push(http.createClient(args.port, args.host));

	function getObjectCloudClient()
	{
		var toReturn = objectCloudClients.shift();
		objectCloudClients.push(toReturn);
	
		return toReturn;
	}

	// The ObjectCloud session cookie
	var objectCloudSessionCookie = null;

	// Log into ObjectCloud
	var content = querystring.stringify(
	{
		username: args.username,
		password: args.password
	},
	'&', '=');
		
	var loginRequest = getObjectCloudClient().request(
		'POST',
		'/Users/UserDB?Method=Login',
		{
			'host': objectcloudRequestMetadata.host,
			'User-Agent': objectcloudRequestMetadata["User-Agent"],
			'Content-Length': content.length,
			'Content-Type': 'application/x-www-form-urlencoded'
		});

	loginRequest.write(content, 'utf8');
	loginRequest.end();

	loginRequest.on('response', function (response)
	{
		if ('2' == (response.statusCode + '').substring(0,1))
		{
			var cookies = response.headers["set-cookie"];
		
			if (!(cookies instanceof Array))
				cookies = [cookies];
			
			for (var i = 0; i < cookies.length; i++)
				if (0 == cookies[i].indexOf("SESSION="))
					objectCloudSessionCookie = cookies[i].split(';')[0].trim();

			if (null != objectCloudSessionCookie)
			{
				connectedCallback(createObjectCloudConnection(
					getObjectCloudClient,
					objectcloudRequestMetadata,
					objectCloudSessionCookie));
			}
			else
				errorCallback.log('SESSION cookie not found, cookies returned: \n' + JSON.stringify(response.headers["set-cookie"], null, '\t'));
		}
		else
		{
			response.setEncoding('utf8');
	
			var result = '';
	
			response.on('data', function (chunk)
			{
				result += chunk;
			});
	
			response.on('end', function ()
			{
				errorCallback(result);
			});
		}
	});
}