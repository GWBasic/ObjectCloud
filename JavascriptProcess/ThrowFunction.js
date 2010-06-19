try
{
   toThrow = JSON.parse(toThrow);
}
catch (exception) {}

throw toThrow;