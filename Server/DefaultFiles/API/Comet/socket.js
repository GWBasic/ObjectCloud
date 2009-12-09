;(function() {

socket = {};

socket.readyState = {
    'initial': 0,
    'opening': 1,
    'open':    2,
    'closing': 3,
    'closed':  4
};

socket.settings = {
    hostname: 'localhost',
    port: 8000
};

var multiplexer = null;
var id = 0;
var frames = {
    'OPEN':  0,
    'CLOSE': 1,
    'DATA':  2
};

var Multiplexer = function(CometSession) {
//    var JSON = CometSession.prototype.JSON || JSON;
    if (multiplexer != null) {
        throw new Error("Multiplexer is a singleton");      
    }
    var parseFrames = function() {
        // parse frames grabs out a frame, then parses it as json, and fires
        // onpacket on the socket of ID indicated in the frame
        var frameBegin;
        while ((frameBegin = self.buffer.indexOf('[')) > -1) {
            var frameEnd = parseInt(self.buffer.slice(0, frameBegin)) + frameBegin;
            if (self.buffer.length < frameEnd)
                return;  // frame still incomplete
            var frame = JSON.parse(self.buffer.slice(frameBegin, frameEnd));
            self.buffer = self.buffer.slice(frameEnd); // shift packet out of buffer
            var socketId = frame[0];
            self.sockets[socketId].onpacket(frame.slice(1));
        };
    };
    var self = multiplexer = this;
    socket.TCPSocket.prototype.JSON = JSON; // XXX is this really necessary?
    self.buffer = "";
    self.sockets = {};
    self.csp = new CometSession();
    self.csp.connect("http://" + socket.settings.hostname + ":" + socket.settings.port, { encoding: 'plain' });// XXX: detect properly
    self.csp.onopen = function() {
        for (id in self.sockets)
            self.sockets[id].onopen();
    }
    self.csp.onclose = function(code) {
        multiplexer = null;
        for (id in self.sockets)
            self.sockets[id].onclose(code);
    }
    self.csp.onread = function(data) {
        self.buffer += data;
        parseFrames();
    }
    self.register = function(socket, onopen, onclose, onpacket) {
        self.sockets[socket.id] = {};
        self.sockets[socket.id].socket = socket;
        self.sockets[socket.id].onopen = onopen;
        self.sockets[socket.id].onclose = onclose;
        self.sockets[socket.id].onpacket = onpacket;
        if (self.csp && self.csp.readyState == csp.readyState.open)
            self.write([socket.id, frames.OPEN, socket.addr, socket.port]);
    }
    self.write = function(frame) {
        var output = JSON.stringify(frame); // XXX: won't work in opera...
        output = output.length + output;
        self.csp.write(output);
    }
}



socket.TCPSocket = function(CometSession) {    
    var self = this;
    if (!CometSession) {
        if (typeof(csp) != 'undefined' && csp.CometSession) {
            CometSession = csp.CometSession;
        }
        else if (window.csp && window.csp.CometSession) {
            CometSession = window.csp.CometSession;
        }
        else {
            throw new Error("Invalid CometSession implementation");
        }
    }
    self.id = ++id;
    self.readyState = socket.readyState.initial;
    self.addr = null;
    self.port = null;
    self.binary = null;
    self.onclose = function(code) {
        console.log("TCPSocket onclose:", code);
    }
    self.onopen = function() {
        console.log("TCPSocket onopen");
    }
    self.onread = function(data) {
        console.log("TCPSocket onread:", data);
    }
    self.open = function(addr, port, isBinary) {
        self.addr = addr;
        self.port = port;
        self.binary = !!isBinary;
        if (typeof(addr) != 'string' || addr.length == 0) {
            throw new Error('Invalid address: "' + addr + '"');
        }
        self.readyState = socket.readyState.opening;
        if (multiplexer == null)
            new Multiplexer(CometSession);
        multiplexer.register(self,
            function() { // onopen
                multiplexer.write([self.id, frames.OPEN, self.addr, self.port]);
            },
            function(code) { // onclose
                self.readyState = socket.readyState.closed;
                self.onclose(code);
            },
            function(frame) { // onpacket
                // console.log('GOT ' + ['OPEN', 'CLOSE', 'DATA'][frame[0]] + ' FRAME', uneval(frame[1]))
                var frameType = frame[0];
                switch(frameType) {
                    case frames.OPEN:
                        if (self.readyState == socket.readyState.opening) {
                            self.readyState = socket.readyState.open;
                            self.onopen();
                        }
                        break;
                    case frames.CLOSE:
                        self.readyState = socket.readyState.closed;
                        self.onclose(frame[1]);
                        break;
                    case frames.DATA:
                        // console.log('read', frame[1]);
                        self.onread(frame[1]);
                        break;
                }
            }
        );
    }
    self.send = function(data) {
        if (self.readyState != socket.readyState.open)
            throw new Error("TCPSocket: invalid readystate!");
        multiplexer.write([self.id, frames.DATA, data]);
//        console.log('send', data);
    }
};

// Try to auto detect the socket port and hostname
(function() {
    try {
        var scripts = document.getElementsByTagName('script');
        for (var i = 0, script; script = scripts[i]; ++i) {
            if (script.src.match('socket\.js$')) {
                var uri = csp.util.parseUri(script.src);
                socket.settings.hostname = uri.hostname || document.domain || 'localhost';
                socket.settings.port = uri.port || location.port || (document.domain && 80) || 8000;
                break;
            }
        }
    } catch(e) {
    }
})();



})();
