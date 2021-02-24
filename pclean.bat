:: $Id: pclean.bat 6961 2019-03-12 16:36:31Z onuchin $
:: Author: Valeriy Onuchin   17.02.2016
::
:: Clean output folders

for /D /R %%i in (bin*) do (rd /s /q "%%i")
for /D /R %%i in (obj*) do (rd /s /q "%%i")
for /D /R %%i in (redist*) do (rd /s /q "%%i")
for /D /R %%i in (debug*) do (rd /s /q "%%i")
for /D /R %%i in (release*) do (rd /s /q "%%i")
for /D /R %%i in (precompiledweb*) do (rd /s /q "%%i")
for /D /R %%i in (x64*) do (rd /s /q "%%i")
::for /D /R %%i in (_ReSharper*) do (rd /s /q "%%i")



:: Clean generated files

attrib /s -r -h -s *.suo
attrib /s -r -h -s Thumbs.db

del /s /q Redist\*.dll
del /s /q _Re*
del /s /q *.aps
del /s /q *_h.h
del /s /q *.ncb
del /s /q *.sln.cache
::del /s /q *.user
::del /s /q *.DotSettings
del /s /q Thumbs.db
del /s /q *.suo
del /s /q *.pch 
del /s /q *.opt
del /s /q *.plg
del /s /q *.bsc
del /s /q *.bak
del /s /q *.pdb
del *.exp /s /q
del *.ilk /s /q
del *.idb /s /q
del *.aps /s /q
::del *.o /s /q




pause
