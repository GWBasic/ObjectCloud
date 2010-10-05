// Scripts: /API/jquery.js, /API/UserSuggest.js, /API/AJAX.js, /API/jquery.form.js

function setupStatusForm(statusForm)
{
   var nextStatusForm = statusForm.clone();

   statusForm.submit(function()
   {
      var me = $(this);

      // Remove the hidden user input
      $('.removebeforesubmit', me).remove();

      // Do some ajax-fu to submit the form
      POST(
         me.attr('action'),
         'application/x-www-form-urlencoded',
         me.formSerialize(),
         function()
         {
            $('.status_showduringsubmit').hide();
            $('.status_hideduringsubmit').show();
            alert('status posted');

            me.before(nextStatusForm);
            me.remove();
            setupStatusForm(nextStatusForm);
         },
         function()
         {
            alert('an error occurred when submitting your status');
            $('.status_showduringsubmit').hide();
            $('.status_hideduringsubmit').show();
         });

      //$('input[type=submit]', me).attr('disabled', 'disabled');
      $('.status_showduringsubmit').show();
      $('.status_hideduringsubmit').hide();

      return false;
   });

   $('.status_recipient', statusForm).each(function()
   {
      var statusRecipientSpan = $(this);
      var statusRecipientSpanClone = statusRecipientSpan.clone();
      statusRecipientSpan.hide();
      statusRecipientSpan.addClass('removebeforesubmit');

      var inputCtr = 0;

      function addInput()
      {
         var newRecipientSpan = statusRecipientSpanClone.clone();
         statusRecipientSpan.before(newRecipientSpan);

         $('input', newRecipientSpan).each(function()
         {
            var me = $(this);

            me.attr('name', me.attr('name') + inputCtr);

            me.bind('keydown.addInput', function()
            {
               me.unbind('keydown.addInput');
               addInput();
            });

            enableUserSuggest(me);
         });

         inputCtr++;
      }

      addInput();
   });
}

$(document).ready(function()
{
   $('.status_showduringsubmit').hide();

   $('form.status').each(function()
   {
      setupStatusForm($(this));
   });
});