# EasySyncClient
A Dropbox® equivalent for Alfresco ECM

This sofware extend Alfresco ECM with offline sync capabilities similar to what Dropbox / SkyDrive / etc... do.

The software is made in C# and requires .NET Framework 4.5.2 to run on Windows.

It has been fully tested on Windows and being validated for MacOs.

The software is using :
  - dotCMIS library -> https://chemistry.apache.org/dotnet/dotcmis.html
  - DynamicLogViewer -> http://tringi.trimcore.cz/Dynamic_Log_Viewer
  - SQLite -> https://www.sqlite.org/
  - Mono (for the MacOs version) -> http://www.mono-project.com/

For more information : http://ks2.fr/produits/ks2-easysync-client-en/

#How does it work
1 - Donwload the latest release
2 - Run Easysyncclient.exe
3 - You will be prompted to enter your Alfresco's credential as well a your Alfresco's CMIS endpoint URL (this URL is http://YOUR_ALFRESO_SERVER_NAME_OR_IP/alfresco/cmisatom)
4 - Select the site you want to synchronize
5 - Select the local folder on your workstation where to synchonize
6 - That's it !
