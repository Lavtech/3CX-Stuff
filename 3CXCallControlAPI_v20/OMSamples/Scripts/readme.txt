Here are samples of scripts

all of them can be loaded using following command in OMSamplesCore (OMSamplesCore.csproj)
Run OMSamplesCore application built from source code
run OMSamplesCore
enter command 
scriptdev deployall folder=<relative path from the folder you are using to start OMSamplesCore or absolute path to the one of the subfolder specified here

Samples are deployed with DN property OMSAMPLES_SCRIPT=1

deploy is performed by enumeration of the all subfolders in the specified path and then
creates RoutePoints with DN.Number=<subfolder name>
recommended: <subfolder name> should be the token ^#?[%a-zA-Z0-9]+$ , in other words, the token which may start with # and contain alphanumeric characters or '%' character.
% is placeholder for '*'
So, it is possible to create RoutePoint where numbers will be like vertical GSM codes
#1
*2
#20**2

See .cs files in subfolders





