// Scripts: /API/jquery.js, /API/json2.js, /API/Comet/CometProtocol.js

// Give each editor instance its own GUID, this way it can ignore its own messages on the bus
var editorInstanceId = 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
    var r = Math.random()*16|0, v = c == 'x' ? r : (r&0x3|0x8);
    return v.toString(16);
});

//alert(editorInstanceId);

$(document).ready(function()
{
   MathJax.Hub.Queue(function()
   {
      var renderedArea = MathJax.Hub.getAllJax('renderedArea')[0];
      //alert(renderedArea);

      var editArea = $('#editArea');

      editArea.keyup(function()
      {
         var me = $(this);
         //alert(me.val());

         var val = me.val();

         MathJax.Hub.Queue(['Text', renderedArea, val]);

         var saved =
         {
            editorInstanceId: editorInstanceId,
            timestamp: new Date().getTime(),
            contents: val
         };

         //alert(JSON.stringify(saved));

         object_target.WriteAll(JSON.stringify(saved), function() {});
      });

      CP_QualityReliable.connect(
         object_target.FullPath + '?ChannelEndpoint=ChangingEvent',
      {
         handleIncomingData: function(data)
         {
            data = JSON.parse(data);
            if (data.editorInstanceId != editorInstanceId)
            {
//alert(data.editorInstanceId);
               editArea.val(data.contents);
               MathJax.Hub.Queue(['Text', renderedArea, data.contents]);
            }
         }
      });
   });
});