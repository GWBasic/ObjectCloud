#!/bin/bash

cp ../Server/DefaultFiles/API/json2.js .
java -cp ./js.jar org.mozilla.javascript.tools.jsc.Main -package com.objectcloud.javascriptprocess -o Json2 -d ./JavascriptProcess/Classes json2.js
rm json2.js

