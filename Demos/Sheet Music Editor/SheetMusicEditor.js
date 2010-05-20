function renderSheetMusic(sheetMusicString, renderedDestination)
{
   var renderedString = '<img src="/SheetMusicEditor/start.png" />';

   for (var i = 0; i < sheetMusicString.length; i++)
   {
      if (i > 0)
         if (0 == i % 4)
            renderedString += '<img src="/SheetMusicEditor/bar.png" />';

      renderedString += '<img src="/SheetMusicEditor/' + sheetMusicString.charAt(i) + '.png" />';
   }

   renderedString += '<img src="/SheetMusicEditor/end.png" />';

   renderedDestination.innerHTML = renderedString;
}