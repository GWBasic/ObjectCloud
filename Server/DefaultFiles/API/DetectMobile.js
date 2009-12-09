var isMobile = false;

var uagent = navigator.userAgent.toLowerCase();
if ((uagent.search("iphone") > -1) || (uagent.search("ipod") > -1) || (uagent.search("series60") > -1) || (uagent.search("symbian") > -1) || (uagent.search("android") > -1) || (uagent.search("windows ce") > -1) || (uagent.search("palm") > -1))
{
   isMobile = true;
}