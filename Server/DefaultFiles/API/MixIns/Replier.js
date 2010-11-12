Replier_AddReply.webCallable = "POST_application_x_www_form_urlencoded";
Replier_AddReply.minimumWebPermission = "Write";
Replier_AddReply.namedPermissions = "reply";
Replier_AddReply.webReturnConvention = "JSON";
Replier_AddReply.parser_inheritPermission = "bool";
Replier_AddReply.parser_additionalRecipients = "JSON";
function Replier_AddReply(replyText, inheritPermission, additionalRecipients)
{
   var userMetadata = getConnectionMetadata();

   var toReturn;

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

         if (additionalRecipients.length > 0)
            replyFile.SetPermission_Sync(
               {
                  UserOrGroups: additionalRecipients,
                  FilePermission: 'Read'
               });

         toReturn = base.AddRelatedFile_Sync(
            {
               filename: replyFile.Filename,
               relationship: "reply",
               inheritPermission: inheritPermission,
               chownRelatedFileTo: userMetadata.identity
            });
      });
   });

   return toReturn;
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