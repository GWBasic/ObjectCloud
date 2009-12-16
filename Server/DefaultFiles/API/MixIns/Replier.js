Replier_AddReply.webCallable = "POST_application_x_www_form_urlencoded";
Replier_AddReply.minimumWebPermission = "Write";
Replier_AddReply.namedPermissions = "reply";
Replier_AddReply.webReturnConvention = "Status";
function Replier_AddReply(replyText)
{
   var roots = base.GetRelatedFiles(["root"], null, null, null, 1);

   callAsOwner(function()
   {
      elevate(function()
      {
         var parentDirectoryWrapper = getDefaultRelatedObjectDirectoryWrapper();

         var now = new Date();
         var replyFilename = fileMetadata.filename + "_reply" + now.getTime() + ".reply";

         var replyFile = parentDirectoryWrapper.CreateFile(
            replyFilename,
            "text",
            true);

         replyFile.WriteAll(sanitize(replyText));

         if (0 == roots.length)
            replyFile.AddRelatedFile(fileMetadata.filename, "root");
         else
            replyFile.AddRelatedFile(roots[0].FullPath, "root");

         replyFile.Chown(userMetadata.id);

         base.AddRelatedFile(replyFilename, "reply");
      });
   });
}

Replier_GetRepliesForDisplay.webCallable = "GET";
Replier_GetRepliesForDisplay.minimumWebPermission = "Read";
Replier_GetRepliesForDisplay.webReturnConvention = "JSON";
function Replier_GetRepliesForDisplay()
{
   var replies = base.GetRelatedFiles(["reply"], null, null, null, 200);

   var toReturn = [];

   for (var i = 0; i < replies.length; i++)
      toReturn.push(
      {
         File: replies[i],
         View: Shell_GET(replies[i].FullPath + "?Action=Preview")
      });

   return toReturn;
}