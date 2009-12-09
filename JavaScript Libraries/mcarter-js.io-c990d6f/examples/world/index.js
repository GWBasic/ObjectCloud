jsio('import Class, bind, jsio.logging');
jsio('from jsio.util.browser import $');
jsio('from .world.constants import *');
jsio('from .world.client import *');

//jsio.logging.getLogger('world.client').setLevel(0);
//jsio.logging.getLogger('csp.transports.jsonp').setLevel(0);
//jsio.logging.getLogger('csp.client').setLevel(0);
//jsio.logging.getLogger('csp.transports').setLevel(0);
jsio.logging.setProduction(true);


function addToHistory(params, color) {
	if(!params || !params.msg) { return; }

	var history = $.id('history');
	var date = new Date(params.ts);postfix
	var h = date.getHours(), m = date.getMinutes();
	var postfix = h >= 12 ? 'pm' : 'am';
	if(m < 10) { m = '0' + m; }
	if(h > 12) { h -= 12; }
		
	if(!color) {
		color = client.players[params.username] && client.players[params.username].color || {r: 128, b: 128, g: 128};
	}

	history.insertBefore($.create({
		text: h + ':' + m + postfix + ' - ' 
			+ params.username + ': ' + params.msg
	}), history.firstChild).style.color = 'rgb(' + color.r + ',' + color.g + ',' + color.b + ')';
}

var historyToggled = true;
function toggleHistory() {
	historyToggled = !historyToggled;
	$.style('history', {opacity: historyToggled ? 1 : 0.1});
	(historyToggled ? $.removeClass : $.addClass)('history', 'history-hidden');
	$.style('board', {opacity: historyToggled ? 0.1 : 1});
	$.id('toggleHistoryBtn').innerHTML = historyToggled ? 'walk' : 'history';
}

function onConnect(presence, history) {
	// hide the history
	toggleHistory();
	
	// restore the history from server data
	for(var i = 0, line; line = history[i]; ++i) {
		addToHistory(line);
	}
	
	// show the game UI
	$.hide('joinBtn'); $.hide('joinInput');
	$.show('sayBtn', 'inline'); $.show('sayInput', 'inline');
	$.show('toggleHistoryBtn', 'inline');
	var sayInput = $.id('sayInput');
	sayInput.value = '';
	sayInput.focus();
	
	// connect some events
	$.onEvent('toggleHistoryBtn', 'click', toggleHistory);
	
	var say = function() {
		client.say(sayInput.value);
		sayInput.value = '';
		sayInput.focus();
	}
	$.onEvent(sayInput, 'keyup', function(e) { if(e.keyCode == 13) { say(); }});
	$.onEvent('sayBtn', 'click', say);

	$.onEvent('worldWrapper', 'mousedown', function(e) {
		var target = e.target || e.srcElement;
		while((target = target.parentNode) && target.id != 'console') {}
		if(!target) { $.stopEvent(e); }
	});
	
	$.onEvent('board', 'mousedown', function(e) {
		$.stopEvent(e);
		var offset = $.cursorPos(e, $.id('board'));
		client.move(offset.left - 22, offset.top - 22);
	});
}

exports.init = function() {
	$.style('worldWrapper', {
		width: kGameWidth + 'px',
		height: kGameHeight + 30 + 'px'
	});
	
	$.style('history', {
		width: kGameWidth - 20 + 'px',
		height: kGameHeight - 20 + 'px'
	});
	
	$.style(['board', 'background'], {
		width: kGameWidth + 'px',
		height: kGameHeight + 'px'
	});

	var url;
	
	$.hide('sayBtn'); $.hide('sayInput'); $.hide('toggleHistoryBtn');
	$.onEvent('joinInput', 'keyup', function(e) { if(e.keyCode == 13) { join(); }});
	$.onEvent('joinBtn', 'click', join);

	var queryParts = document.location.search.substr(1).split('&')
	var clientParams = {};
	for (var i=0, part; part = queryParts[i]; i++) { 
		var kvp = part.split('=');
		clientParams[decodeURIComponent(kvp[0])] = decodeURIComponent(kvp[1]); 
	}

	var joinInput = $.id('joinInput');
	var sayInput = $.id('sayInput');
	function join() {
		if(!joinInput.value.length) { return; }
		var uiPlayerFactory = function(params) {
			params.parent = $.id('board');
			params.history = $.id('history');
			return new WorldPlayer(params);
		}

		window.client = new WorldProtocol(uiPlayerFactory, joinInput.value, url || 'http://www.google.com/favicon.ico');
		client.subscribe('welcome', this, onConnect);
		client.subscribe('say', this, addToHistory);
		var domain = document.domain || "127.0.0.1";
		jsio.connect(client, clientParams.transport || 'csp', { url: "http://" + domain + ":5555" });
	}
}
