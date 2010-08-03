/*
 * objectcloud.js
 *
 * Driver for consuming ObjectCloud objects in Node
 *
 * (C) 2010 Andrew Rondeau
 * Released under the SimPL 2.0 license, see http://opensource.org/licenses/simpl-2.0.html
 */
 
exports.connect = function(args, connectedCallback, errorCallback)
{
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
				connectedCallback(
				{
					getObjectCloudClient : getObjectCloudClient,
					args: args,
					objectCloudSessionCookie : objectCloudSessionCookie
				});
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