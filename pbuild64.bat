:: $Id: pbuild64.bat 7750 2020-02-03 13:46:57Z onuchin $
:: Author: Valeriy Onuchin   19.04.2014
::

call defines.bat

%msbuild% /nologo  .\gep.sln /t:Build /m   /p:Platform=x64


:end

del /s /q *.pdb

pause

