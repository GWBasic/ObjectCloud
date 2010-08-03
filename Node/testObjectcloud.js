/*
 * objectcloud.js
 *
 * Test for Driver for consuming ObjectCloud objects in Node
 *
 * (C) 2010 Andrew Rondeau
 * Released under the SimPL 2.0 license, see http://opensource.org/licenses/simpl-2.0.html
 *
 * Note:  In order for this test to work, you must type in the IP or host name that ObjectCloud is
 * running at.  The driver doesn't support HTTP redirect, so it will error out when ObjectCloud
 * redirects to the IP / hostname that its running at
 */
 
require("./objectcloud").connect(
	{
		username: 'root',
		password: 'root',
		port: 1080,
		host: '10.0.1.198'
	},
	function(objectCloudConnection)
	{
		console.log("Successfully connected to ObjectCloud");
		
		objectCloudConnection.open(
			'/Shell',
			function(wrapper)
			{
				console.log(JSON.stringify(wrapper, null, '\t'));
				
				wrapper.ListFiles(
					{},
					function(files)
					{
						console.log(JSON.stringify(files, null, '\t'));
						testWritingFile(objectCloudConnection);
					});
			});
	},
	function(objectCloudError)
	{
		console.log("Error connecting to ObjectCloud:\n" + objectCloudError);
	});
	
function testWritingFile(objectCloudConnection)
{
	console.log('opening my folder');
	
	objectCloudConnection.open(
		'/Users/[name]',
		function(myFolder)
		{
			console.log(JSON.stringify(myFolder, null, '\t'));
			console.log('creating a file');

			myFolder.CreateFile(
			{
				fileNameSuggestion: 'test file',
				extension: 'text',
				FileType: 'text'
			},
			function(myFile)
			{
				console.log(JSON.stringify(myFile, null, '\t'));
				console.log('created');
				
				myFile.WriteAll(
					'blah blah blah ' + new Date(),
					function()
					{
						console.log('written successfully');
						
						myFile.ReadAll(
							{},
							function(contents)
							{
								console.log("File correctly read\n" + contents);
							});
					});
			});
		});
}