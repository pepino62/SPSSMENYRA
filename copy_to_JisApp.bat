@echo off
setlocal

REM -------------------------------------------------
REM aktuální adresáø projektu
REM -------------------------------------------------

for %%I in ("%CD%") do set PROJECT=%%~nxI

echo.
echo ==========================================
echo PROJECT : %PROJECT%
echo ==========================================
echo.

REM -------------------------------------------------
REM naètení verze
REM -------------------------------------------------

set /p VERSION=<version.txt

echo VERSION : %VERSION%
echo.

REM -------------------------------------------------
REM build
REM -------------------------------------------------

dotnet build -c Release

if errorlevel 1 (
echo.
echo BUILD FAILED
pause
exit /b 1
)

REM -------------------------------------------------
REM cílový adresáø
REM -------------------------------------------------

set TARGET=C:\JisApp\%PROJECT%\%PROJECT%_%VERSION%

echo.
echo Copying files to:
echo %TARGET%
echo.

REM -------------------------------------------------
REM vytvoøení adresáøe
REM -------------------------------------------------

if not exist "%TARGET%" mkdir "%TARGET%"

REM -------------------------------------------------
REM kopírování release
REM -------------------------------------------------

xcopy /E /Y /I ^
%PROJECT%\bin\Release\net48 ^
%TARGET%



echo.
echo ==========================================
echo RELEASE READY
echo ==========================================
echo.

echo %TARGET%

pause
