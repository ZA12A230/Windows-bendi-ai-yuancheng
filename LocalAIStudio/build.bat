@echo off
echo Building Local AI Studio...

dotnet restore
dotnet build -c Release

echo.
echo Publishing single file executable...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=true

echo.
echo Build complete!
echo Output: bin\Release\net8.0-windows\win-x64\publish\LocalAIStudio.exe

pause
