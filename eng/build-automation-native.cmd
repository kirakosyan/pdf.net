@echo off
call "%~1\VC\Auxiliary\Build\vcvarsall.bat" %~2 >nul
if errorlevel 1 exit /b %errorlevel%
cl /nologo /std:c++17 /EHsc /W4 /WX /MT "%~3" ole32.lib oleaut32.lib uuid.lib /Fe:"%~4" /Fo:"%~5"
exit /b %errorlevel%
