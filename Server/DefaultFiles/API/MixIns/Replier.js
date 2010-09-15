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

   var userMetadata = getConnectionMetadata();

   callAsOwner(function()
   {
      elevate(function()
      {
         var parentDirectoryWrapper = getDefaultRelatedObjectDirectoryWrapper();

         var replyFile = parentDirectoryWrapper.CreateFile_Sync(
            {
               extension: 'reply',
               fileNameSuggestion: fileMetadata.filename
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

         base.AddRelatedFile_Sync(
               {
                  filename: replyFile.Filename,
                  relationship: "reply",
                  inheritPermission: true
               });

         replyFile.Chown_Sync(
            {
               newOwnerId: userMetadata.id
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