// Scripts: /API/jquery.js

function setupOpenIDForm(identity, originalparameters)
{
   $(document).ready(function()
   {
      if (identity == originalparameters["openid.identity"])
         $('.password').hide();

      $('form.openIdPasswordForm').each(function()
      {
         var me = $(this);

         for (argname in originalparameters)
             if (argname != "Method")
             {
                var arg = $('<input type="hidden" />');
                arg.attr('name', argname);
                arg.val(originalparameters[argname]);

                me.append(arg);
             }
      });
   });
}