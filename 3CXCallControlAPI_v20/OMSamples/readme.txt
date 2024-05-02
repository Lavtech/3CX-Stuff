Prerequisites:
Windows or Debian 12
Latest 3CX PhoneSystem v20
latest .netcore 8.0 SDK

build samples:

dotnet build OMSamplesCore.csproj

result of compilation will be available in
bin/Debug/net8.0

deploying call flow script samples
Run Debian 12
sudo bin/Debug/net8.0/OMSamplesCore
On Windows
bin/Debug/net8.0/OMSamplesCore

Copy the Scripts folder bin/Debug/net8.0/

Enter following commands:
>scriptdev deployall folder=Scripts/ExtensionStatus
>scriptdev deployall folder=Scripts/ForcePBXHours
>scriptdev deployall folder=Scripts/PersonalParkingWithAutoReturn
>scriptdev deployall folder=Scripts/QueueAgentStatus
>scriptdev deployall folder=Scripts/UserInputIVR

After deployment of scripts, you should see following Call Flow Applications in the Admin UI
#
#0
#1
#2
#3
#4
#30
#31
#60
#61
#62
#63
#64
8877

see brief readme.txt in corresponding folders.

../Help folder provides local html help
open
../Help/index.html and follow links to get information