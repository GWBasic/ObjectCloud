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
	},
	function(objectCloudConnection)
	{
	},
	function(objectCloudError)
	{
		console.log("Error connecting to ObjectCloud:\n" + objectCloudError);
	});