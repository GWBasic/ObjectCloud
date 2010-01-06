Replier_AddReply.webCallable = "POST_application_x_www_form_urlencoded";
Replier_AddReply.minimumWebPermission = "Write";
Replier_AddReply.namedPermissions = "reply";
Replier_AddReply.webReturnConvention = "Status";
function Replier_AddReply(replyText)
{
   var roots = base.GetRelatedFiles_Sync(
      {
         relationships: ["root"],
         maxToReturn: 1
      });

   callAsOwner(function()
   {
      elevate(function()
      {
         var parentDirectoryWrapper = getDefaultRelatedObjectDirectoryWrapper();

         var now = new Date();
         var replyFilename = fileMetadata.filename + "_reply" + now.getTime() + ".reply";

         var replyFile = parentDirectoryWrapper.CreateFile_Sync(
            {
               FileName: replyFilename,
               FileType: "text",
               ErrorIfExists: true
           });

         replyFile.WriteAll_Sync(sanitize(replyText));

         if (0 == roots.length)
            replyFile.AddRelatedFile_Sync(
               {
                  filename: fileMetadata.filename,
                  relationship: "root"
               });
         else
            replyFile.AddRelatedFile_Sync(
               {
                  filename: roots[0].FullPath,
                  relationship: "root"
               });

         replyFile.Chown_Sync(
            {
               newOwnerId: userMetadata.id
            });

         base.AddRelatedFile_Sync(
               {
                  filename: replyFilename,
                  relationship: "reply"
               });
      });
   });
}

Replier_GetRepliesForDisplay.webCallable = "GET";
Replier_GetRepliesForDisplay.minimumWebPermission = "Read";
Replier_GetRepliesForDisplay.webReturnConvention = "JSON";
function Replier_GetRepliesForDisplay()
{
   var replies = base.GetRelatedFiles_Sync(
      {
         relationships: ["reply"],
         maxToReturn: 200
      });

   var toReturn = [];

   for (var i = 0; i < replies.length; i++)
      toReturn.push(
      {
         File: replies[i],
         View: Shell_GET(replies[i].FullPath + "?Action=Preview")
      });

   return toReturn;
}