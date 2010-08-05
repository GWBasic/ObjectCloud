/*
 * Rebooter
 *
 * Reboots hung ObjectCloud instances
 */
 
// How long to wait until the script starts monitoring ObjectCloud, in miliseconds
var startDelay = 2500;

// How often to poll ObjectCloud, in miliseconds
var poll = 10000;

// ObjectCloud's host name.  This must be the exact host name, as the driver doesn't support
// ObjectCloud's redirect
var host = '192.168.5.166';

// ObjectCloud's port
var port = 1080;

// The username and password
var username = 'root';
var password = 'root';

// This function is called whenever this script determines that ObjectCloud is down and
// the server needs to reboot
function reboot()
{
	console.log('rebooting...');
	require('child_process').spawn('shutdown', ['-r', 'now']);
}

// Load the ObjectCloud driver
var objectcloud = require("./objectcloud");

function checkObjectCloud()
{
	console.log('checking ObjectCloud');
	
	objectcloud.connect(
    {
        numConnections: 1,
        username: username,
        password: password,
        port: port,
        host: host
    },
    function()
    {
    	console.log('Successfuly connected to ObjectCloud');
    },
    function()
    {
    	console.log('Error connecting to ObjectCloud');
    	reboot();
    });    
}

function start()
{
	console.log('starting ObjectCloud monitor, polling every ' + poll + ' milliseconds');
	setInterval(checkObjectCloud, poll);
	
	process.on('uncaughtException', function (err)
	{
		console.log('Caught exception: ' + err);
        reboot();
	});
}

console.log('waiting ' + startDelay + ' milliseconds for ObjectCloud to start');
setTimeout(start, startDelay);