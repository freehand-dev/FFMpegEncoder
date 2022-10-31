# FFMpegEncoder
> FFMpegEncoder - windows service for wrapper ffmpeg

[![.NET](https://github.com/freehand-dev/FFMpegEncoder/actions/workflows/dotnet.yml/badge.svg)](https://github.com/freehand-dev/FFMpegEncoder/actions/workflows/dotnet.yml)

## Compile and install
Once you have installed all the dependencies, get the code:
 ```powershell
git clone https://github.com/freehand-dev/FFMpegEncoder.git
cd FFMpegEncoder
```

Then just use:
```powershell
New-Item -Path "%ProgramData%\FreeHand\FFMpegEncoder\bin\" -ItemType "directory"
dotnet restore
dotnet build
dotnet publish --runtime win-x64 --output %ProgramData%\FreeHand\FFMpegEncoder\bin\ -p:PublishSingleFile=true -p:PublishTrimmed=true -p:PublishReadyToRun=true .\src\FFMpegEncoder
Expand-Archive  .\src\ffmpeg-bin\ffmpeg-decklink.zip %ProgramData%\FreeHand\FFMpegEncoder\bin\ffmpeg-bin\
```

Install as Windows Service
 ```powershell
sc create FreeHandFFMpegEncoderSvc_Default 
binPath= "%ProgramData%\FreeHand\FFMpegEncoder\bin\FFMpegEncoder.exe --azure-config azure-app-config-endpoint --local-config C:/ProgramData/FreeHand/FFMpegEncoder/default.json"
DisplayName= "FreeHand FFMpegEncoder (Default)" 
start= auto
description= "FreeHand FFMpegEncoder (Default)"
```

## Configure and start
To start the service, you can use the `FFMpegEncoder` executable as the application or `sc start FreeHandFFMpegEncoderSvc_Default` as a Windows service. For configuration you can edit a configuration file:

	notepad.exe %ProgramData%\FreeHand\FFMpegEncoder\default.json




