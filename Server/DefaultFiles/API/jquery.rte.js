// Scripts:  /API/jquery.js

/*
 * Lightweight RTE - jQuery Plugin, version 1.2
 * Copyright (c) 2009 Andrey Gayvoronsky - http://www.gayvoronsky.com
 */
jQuery.fn.rte = function(options, editors) {
	if(!editors || editors.constructor != Array)
		editors = new Array();
		
	$(this).each(function(i) {
		var id = (this.id) ? this.id : editors.length;
		editors[id] = new lwRTE (this, options || {});
	});
	
	return editors;
}

var lwRTE_resizer = function(textarea) {
	this.drag = false;
	this.rte_zone = $(textarea).parents('.rte-zone');
}

lwRTE_resizer.mousedown = function(resizer, e) {
	resizer.drag = true;
	resizer.event = (typeof(e) == "undefined") ? window.event : e;
	resizer.rte_obj = $(".rte-resizer", resizer.rte_zone).prev().eq(0);
	$('body', document).css('cursor', 'se-resize');
	return false;
}

lwRTE_resizer.mouseup = function(resizer, e) {
	resizer.drag = false;
	$('body', document).css('cursor', 'auto');
	return false;
}

lwRTE_resizer.mousemove = function(resizer, e) {
	if(resizer.drag) {
		e = (typeof(e) == "undefined") ? window.event : e;
		var w = Math.max(1, resizer.rte_zone.width() + e.screenX - resizer.event.screenX);
		var h = Math.max(1, resizer.rte_obj.height() + e.screenY - resizer.event.screenY);
		resizer.rte_zone.width(w);
		resizer.rte_obj.height(h);
		resizer.event = e;
	}
	return false;
}

var lwRTE = function (textarea, options) {
	this.css		= [];
	this.css_class	= options.frame_class || '';
	this.base_url	= options.base_url || '';
	this.width		= options.width || $(textarea).width() || '100%';
	this.height		= options.height || $(textarea).height() || 350;
	this.iframe		= null;
	this.iframe_doc	= null;
	this.textarea	= null;
	this.event		= null;
	this.range		= null;
	this.toolbars	= {rte: '', html : ''};
	this.controls	= {rte: {disable: {hint: 'Source editor'}}, html: {enable: {hint: 'Visual editor'}}};

	$.extend(this.controls.rte, options.controls_rte || {});
	$.extend(this.controls.html, options.controls_html || {});
	$.extend(this.css, options.css || {});

	if(document.designMode || document.contentEditable) {
		$(textarea).wrap($('<div></div>').addClass('rte-zone').width(this.width));		
		$('<div class="rte-resizer"><a href="#"></a></div>').insertAfter(textarea);

		var resizer = new lwRTE_resizer(textarea);
		
		$(".rte-resizer a", $(textarea).parents('.rte-zone')).mousedown(function(e) {
			$(document).mousemove(function(e) {
				return lwRTE_resizer.mousemove(resizer, e);
			});

			$(document).mouseup(function(e) {
				return lwRTE_resizer.mouseup(resizer, e)
			});

			return lwRTE_resizer.mousedown(resizer, e);
		});

		this.textarea	= textarea;
		this.enable_design_mode();
	}
}

lwRTE.prototype.editor_cmd = function(command, args) {
	this.iframe.contentWindow.focus();
	try {
		this.iframe_doc.execCommand(command, false, args);
	} catch(e) {
	}
	this.iframe.contentWindow.focus();
}

lwRTE.prototype.get_toolbar = function() {
	var editor = (this.iframe) ? $(this.iframe) : $(this.textarea);
	return (editor.prev().hasClass('rte-toolbar')) ? editor.prev() : null;
}

lwRTE.prototype.activate_toolbar = function(editor, tb) {
	var old_tb = this.get_toolbar();

	if(old_tb)
		old_tb.remove();

	$(editor).before($(tb).clone(true));
}
	
lwRTE.prototype.enable_design_mode = function() {
	var self = this;

	// need to be created this way
	self.iframe	= document.createElement("iframe");
	self.iframe.frameBorder = 0;
	self.iframe.frameMargin = 0;
	self.iframe.framePadding = 0;
	self.iframe.width = '100%';
	self.iframe.height = self.height || '100%';
	self.iframe.src	= "javascript:void(0);";

	if($(self.textarea).attr('class'))
		self.iframe.className = $(self.textarea).attr('class');

	if($(self.textarea).attr('id'))
		self.iframe.id = $(self.textarea).attr('id');

	if($(self.textarea).attr('name'))
		self.iframe.title = $(self.textarea).attr('name');

	var content	= $(self.textarea).val();

	$(self.textarea).hide().after(self.iframe).remove();
	self.textarea	= null;
	
	var css = '';
	
	for(var i in self.css)
		css += "<link type='text/css' rel='stylesheet' href='" + self.css[i] + "' />";

	var base = (self.base_url) ? "<base href='" + self.base_url + "' />" : '';
	var style = (self.css_class) ? "class='" + self.css_class + "'" : '';

	// Mozilla need this to display caret
	/*if($.trim(content) == '')
		content	= '<br>';*/

	var doc = "<html><head>" + base + css + "</head><body " + style + " style='padding:5px'>" + content + "</body></html>";

	self.iframe_doc	= self.iframe.contentWindow.document;

	try {
		self.iframe_doc.designMode = 'on';
	} catch ( e ) {
		// Will fail on Gecko if the editor is placed in an hidden container element
		// The design mode will be set ones the editor is focused
		$(self.iframe_doc).focus(function() { self.iframe_doc.designMode(); } );
	}

	self.iframe_doc.open();
	self.iframe_doc.write(doc);
	self.iframe_doc.close();

	if(!self.toolbars.rte)
		self.toolbars.rte	= self.create_toolbar(self.controls.rte);

	self.activate_toolbar(self.iframe, self.toolbars.rte);

	$(self.iframe).parents('form').submit( 
		function() { self.disable_design_mode(true); }
	);

	$(self.iframe_doc).mouseup(function(event) { 
		if(self.iframe_doc.selection)
			self.range = self.iframe_doc.selection.createRange();  //store to restore later(IE fix)

		self.set_selected_controls( (event.target) ? event.target : event.srcElement, self.controls.rte); 
	});

	$(self.iframe_doc).blur(function(event){ 
		if(self.iframe_doc.selection) 
			self.range = self.iframe_doc.selection.createRange(); // same fix for IE as above
	});

	$(self.iframe_doc).keyup(function(event) { self.set_selected_controls( self.get_selected_element(), self.controls.rte); });

	// Mozilla CSS styling off
	if(!$.browser.msie)
		self.editor_cmd('styleWithCSS', false);
}
    
lwRTE.prototype.disable_design_mode = function(submit) {
	var self = this;

	self.textarea = (submit) ? $('<input type="hidden" />').get(0) : $('<textarea>