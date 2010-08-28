// Scripts: /API/jquery.js, /API/jquery-ui.js

function audioviewer_enable(filename, mime)
{
   $(document).ready(function()
   {
      var playerDiv = $('[id=audioviewer_' + filename + ']');
      var playButton = $('<span>play</span>').button(
      {
         icons:
         {
            primary: 'ui-icon-triangle-1-e'
         }
      });

      playerDiv.append(playButton);

      playButton.click(function()
      {
         playerDiv.html(
'<object width="300" height="42">' +
      '<param name="src" value="' + filename + '?Method=ReadAll&amp;MimeOverride=' + mime + '" />' +
      '<param name="autoplay" value="true" />' +
      '<param name="controller" value="false" />' +
      '<embed src="' + filename + '?Method=ReadAll&amp;MimeOverride=' + mime + '" autostart="true" loop="false" width="300" height="42" controller="true"></embed>' +
   '</object>');
      });
   });
}

