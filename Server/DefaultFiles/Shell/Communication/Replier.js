var rpy_object = null;
var rpy_oldHTML;
var rpy_editor = null;

function rply_addReply(inObject)
{
   rpy_object = inObject;

   var replier = $("Replier");

   rpy_oldHTML = replier.innerHTML;
   replier.innerHTML = "";
   replier.style.width="100%";

   rply_displayNicEdit();
}

function rply_displayNicEdit()
{
   rpy_editor = new nicEditor(
   {
      fullPanel: true,
      onSave : rpy_save,
      style: "contents"
   });

   rpy_editor.panelInstance(
      'Replier',
      {
         hasPanel: true,
         style: "contents"
      });
}

function rpy_save(content, id, instance)
{
   var replier = $("Replier");

   if (null != rpy_editor)
      rpy_editor.removeInstance('Replier');

   replier.innerHTML = "Saving...<div>" + content + "</div>";

   rpy_object.Replier_AddReply(
      {
         replyText: content
      },
      function()
      {
         replier.innerHTML = rpy_oldHTML;
      },
      function()
      {
         alert("Could not save the reply");
         replier.innerHTML = content;
         rply_displayNicEdit();
      });
}

function rpy_createReplyElement(reply)
{
   var replyHtml = '';

   replyHtml +=
      '<a href="' + reply.File.OwnerIdentity + '">' + reply.File.Owner + '</a>, at ';
   replyHtml += new Date(reply.File.Created) + ', says:';

   var toReturn = document.createElement('div');

   var headerSpan = document.createElement('span');
   headerSpan.innerHTML = replyHtml;
   toReturn.appendChild(headerSpan);

   var contentDiv = document.createElement('div');
   contentDiv.innerHTML = reply.View.Content;
   toReturn.appendChild(contentDiv);

   var linkDiv = document.createElement('span');
   linkDiv.innerHTML = '<a href="' + reply.File.FullPath + '">View / Reply</a>';
   toReturn.appendChild(linkDiv);

   toReturn.appendChild(document.createElement('hr'));

   return toReturn;
}