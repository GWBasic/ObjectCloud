#!/bin/bash

cp ../Server/DefaultFiles/API/json2.js .
echo JSON.stringify > JSONstringify.js
echo JSON.parse > JSONparse.js
java -cp ./js.jar org.mozilla.javascript.tools.jsc.Main -package com.objectcloud.javascriptprocess -o Json2 -d ./JavascriptProcess/Classes json2.js
java -cp ./js.jar org.mozilla.javascript.tools.jsc.Main -package com.objectcloud.javascriptprocess -o Json2stringify -d ./JavascriptProcess/Classes JSONstringify.js
java -cp ./js.jar org.mozilla.javascript.tools.jsc.Main -package com.objectcloud.javascriptprocess -o Json2parse -d ./JavascriptProcess/Classes JSONparse.js
rm json2.js
rm JSONstringify.js
rm JSONparse.js
